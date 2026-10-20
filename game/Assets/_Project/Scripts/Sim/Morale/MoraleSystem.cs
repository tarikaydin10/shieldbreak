using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat.Context;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Grid;

namespace Shieldbreak.Sim.Morale
{
    /// <summary>
    /// Morale (CLAUDE.md 3.4, M2 task 5). One panic field per team on the WorldGrid: P += D·ΔP − k·P + sources − sinks.
    /// Sources: own soldiers dying, being hit from the side or behind, routing. Sinks: decay, enemies dying or
    /// breaking nearby, leaders (player heroes). The field is signed: below zero it is confidence and spreads the same
    /// way. Every bot loses morale in proportion to the panic where it stands (less while it holds its place in a
    /// formation), gains it where its side is confident, and recovers slowly where it is calm. At the threshold it routs: it leaves its formation, stops
    /// fighting, runs from the enemy and leaves the field at the map edge. Heroes never rout. Runs after combat as one
    /// Burst job in agent order, so every peer gets the same result.
    /// </summary>
    public sealed class MoraleSystem : IDisposable
    {
        readonly MoraleSettings settings;
        readonly WorldGrid grid;
        NativeArray<MoraleData> morale;
        NativeArray<float> scratch;

        public MoraleSystem(int maxAgents, in MoraleSettings settings, WorldGrid grid)
        {
            settings.Validate();
            this.settings = settings;
            this.grid = grid;
            morale = new NativeArray<MoraleData>(maxAgents, Allocator.Persistent);
            for (int i = 0; i < maxAgents; i++) morale[i] = MoraleData.Steady;
            scratch = new NativeArray<float>(settings.Enabled == 1 ? grid.Layout.CellCount : 0, Allocator.Persistent);
        }

        public bool Enabled => settings.Enabled == 1;
        public MoraleSettings Settings => settings;
        public NativeArray<MoraleData>.ReadOnly Agents(int count) => morale.GetSubArray(0, count).AsReadOnly();

        /// <summary>
        /// One tick, after combat: <paramref name="events"/> holds this tick's combat events; Rout and Fled events are
        /// appended. <paramref name="playerAgents"/> are the leaders (one per player, -1 = none).
        /// </summary>
        public void Step(int tick, NativeArray<AgentData> agents, NativeArray<CombatData> combat, int count, NativeList<CombatEvent> events,
            in SpatialHash spatial, NativeArray<int> playerAgents, NativeArray<FormationData> formations, NativeArray<FormationMember> members)
        {
            if (!Enabled || count == 0) return;
            new MoraleJob
            {
                Tick = tick,
                Count = count,
                Settings = settings,
                Layout = grid.Layout,
                Panic = grid.Panic,
                Scratch = scratch,
                Morale = morale,
                Agents = agents,
                Combat = combat,
                Events = events,
                CellStart = spatial.CellStart,
                Entries = spatial.Entries,
                PlayerAgents = playerAgents,
                Formations = formations,
                Members = members,
            }.Run();
        }

        public void AddToHash(ref SimHash h, int count) => h.Add(morale.GetSubArray(0, count));

        public void Dispose()
        {
            if (morale.IsCreated) morale.Dispose();
            if (scratch.IsCreated) scratch.Dispose();
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        struct MoraleJob : IJob
        {
            /// <summary>A routing soldier steers to a point this far ahead on its flight line (m).</summary>
            const float FleeLead = 6f;
            const float PanicFloor = MoraleDiffusion.Floor;

            public int Tick;
            public int Count;
            public MoraleSettings Settings;
            public GridLayout Layout;
            public NativeArray<float> Panic;
            public NativeArray<float> Scratch;
            public NativeArray<MoraleData> Morale;
            public NativeArray<AgentData> Agents;
            public NativeArray<CombatData> Combat;
            public NativeList<CombatEvent> Events;
            [ReadOnly] public NativeArray<int> CellStart;
            [ReadOnly] public NativeArray<int> Entries;
            [ReadOnly] public NativeArray<int> PlayerAgents;
            [ReadOnly] public NativeArray<FormationData> Formations;
            [ReadOnly] public NativeArray<FormationMember> Members;

            public void Execute()
            {
                int combatEvents = Events.Length;
                AddEventSources(combatEvents);
                if (Tick % Settings.FieldIntervalTicks == 0)
                {
                    float dt = Settings.FieldDeltaTime;
                    EmitRoutPanic(dt);
                    CalmAroundLeaders(dt);
                    for (int team = 0; team < SimConstants.MaxTeams; team++) Diffuse(team, dt);
                    UpdateMorale(dt);
                }
                Flee(Tick % Settings.FieldIntervalTicks == 0);
            }

            int CellOf(float2 position) => Layout.ClampedCellIndex(position);

            void AddPanic(int team, int cell, float amount)
            {
                if ((uint)team >= SimConstants.MaxTeams) return;
                int index = team * Layout.CellCount + cell;
                float p = Panic[index] + amount;
                Panic[index] = math.abs(p) < PanicFloor ? 0f : p;
            }

            /// <summary>Deaths frighten the dead soldier's team and steady the killer's; hits from the side or behind frighten.</summary>
            void AddEventSources(int count)
            {
                for (int k = 0; k < count; k++)
                {
                    var e = Events[k];
                    if (e.Target < 0 || e.Target >= Count) continue;
                    var victim = Agents[e.Target];
                    int cell = CellOf(victim.Position);
                    if (e.Type == CombatEventType.Death)
                    {
                        AddPanic(victim.Team, cell, Settings.DeathPanic);
                        if (e.Attacker >= 0 && e.Attacker < Count && Agents[e.Attacker].Team != victim.Team)
                            AddPanic(Agents[e.Attacker].Team, cell, -Settings.KillCourage);
                    }
                    else if (e.Type == CombatEventType.Hit && e.Damage > 0f && e.Attacker >= 0 && e.Attacker < Count)
                    {
                        float2 toAttacker = math.normalizesafe(Agents[e.Attacker].Position - victim.Position, victim.Facing);
                        if (math.dot(toAttacker, victim.Facing) < Settings.FlankArcCos) AddPanic(victim.Team, cell, Settings.FlankHitPanic);
                    }
                }
            }

            /// <summary>Rout is contagious: every fleeing soldier frightens those around it.</summary>
            void EmitRoutPanic(float dt)
            {
                float amount = Settings.RoutPanicPerSecond * dt;
                if (amount <= 0f) return;
                for (int i = 0; i < Count; i++)
                {
                    var a = Agents[i];
                    if (a.IsRouting && !a.IsDead) AddPanic(a.Team, CellOf(a.Position), amount);
                }
            }

            /// <summary>Leaders (player heroes) are sinks: around them their team's panic (not its confidence) fades faster.</summary>
            void CalmAroundLeaders(float dt)
            {
                float keep = 1f - Settings.LeaderCalm * dt;
                if (Settings.LeaderRadius <= 0f || keep >= 1f) return;
                int r = (int)math.ceil(Settings.LeaderRadius / SimConstants.CellSize);
                float r2 = Settings.LeaderRadius * Settings.LeaderRadius;
                for (int p = 0; p < PlayerAgents.Length; p++)
                {
                    int id = PlayerAgents[p];
                    if (id < 0 || id >= Count) continue;
                    var leader = Agents[id];
                    if (leader.IsDead || (uint)leader.Team >= SimConstants.MaxTeams) continue;
                    int2 centre = Layout.WorldToCell(leader.Position);
                    int offset = leader.Team * Layout.CellCount;
                    for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int2 cell = centre + new int2(dx, dy);
                        if (!Layout.InBounds(cell)) continue;
                        float2 cellCentre = Layout.Origin + ((float2)cell + 0.5f) * SimConstants.CellSize;
                        if (math.distancesq(cellCentre, leader.Position) > r2) continue;
                        int index = offset + Layout.CellIndex(cell);
                        float v = Panic[index];
                        if (v <= 0f) continue;
                        v *= keep;
                        Panic[index] = v < PanicFloor ? 0f : v;
                    }
                }
            }

            void Diffuse(int team, float dt) =>
                MoraleDiffusion.Step(Panic, team * Layout.CellCount, Scratch, Layout, Settings.Diffusion, Settings.Decay, dt);

            bool IsLeader(int i)
            {
                for (int p = 0; p < PlayerAgents.Length; p++)
                    if (PlayerAgents[p] == i) return true;
                return false;
            }

            /// <summary>
            /// Bots lose morale under panic (less while holding their place in a formation), gain it from their side's
            /// confidence, recover slowly when calm and rout at the threshold.
            /// </summary>
            void UpdateMorale(float dt)
            {
                for (int i = 0; i < Count; i++)
                {
                    var a = Agents[i];
                    if (a.IsDead || a.IsRouting || (uint)a.Team >= SimConstants.MaxTeams) continue;
                    if (Combat[i].BotProfile <= 0 || IsLeader(i)) continue;
                    var m = Morale[i];
                    float panic = Panic[a.Team * Layout.CellCount + CellOf(a.Position)];
                    var member = Members[i];
                    bool steady = member.Formation >= 0 && member.Formation < Formations.Length &&
                                  FormationCombatRules.For(member, Formations[member.Formation]).Bound == 1;
                    m.Morale -= Settings.PanicDrain * panic * (steady && panic > 0f ? Settings.FormationSteadiness : 1f) * dt;
                    if (panic < Settings.CalmPanic) m.Morale += Settings.Recovery * dt;
                    m.Morale = math.clamp(m.Morale, 0f, 1f);
                    if (m.Morale <= Settings.RoutThreshold)
                    {
                        // Seeing an enemy break steadies the other side like a kill
                        for (int team = 0; team < SimConstants.MaxTeams; team++)
                            if (team != a.Team) AddPanic(team, CellOf(a.Position), -Settings.KillCourage);
                        a.Flags |= AgentFlags.Routing;
                        a.FaceAgent = -1;
                        m.RoutTick = Tick;
                        m.FleeDirection = math.normalizesafe(AwayFromEnemies(i, a), -a.Facing);
                        Agents[i] = a;
                        var c = Combat[i];
                        c.BlockHeld = 0;
                        c.BotTarget = -1;
                        Combat[i] = c;
                        Events.Add(new CombatEvent { Type = CombatEventType.Rout, Attacker = -1, Target = i, Point = new float3(a.Position.x, 1f, a.Position.y) });
                    }
                    Morale[i] = m;
                }
            }

            /// <summary>Sum of pushes away from the enemies within FleeSenseRange, nearer ones weigh more.</summary>
            float2 AwayFromEnemies(int i, in AgentData me)
            {
                float range = Settings.FleeSenseRange;
                int r = (int)math.ceil(range / SimConstants.CellSize);
                int2 centre = math.clamp(Layout.WorldToCell(me.Position), int2.zero, new int2(Layout.Width - 1, Layout.Height - 1));
                float2 away = float2.zero;
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int2 cell = centre + new int2(dx, dy);
                    if (!Layout.InBounds(cell)) continue;
                    int index = Layout.CellIndex(cell);
                    for (int e = CellStart[index]; e < CellStart[index + 1]; e++)
                    {
                        int j = Entries[e];
                        if (j == i || j >= Count) continue;
                        var other = Agents[j];
                        if (other.IsDead || other.Team == me.Team) continue;
                        float2 d = me.Position - other.Position;
                        float dsq = math.lengthsq(d);
                        if (dsq > range * range) continue;
                        away += d / math.max(dsq, 0.25f);
                    }
                }
                return away;
            }

            /// <summary>
            /// Routing soldiers run along their flight line (turning away from enemies that come close) and leave the
            /// field at the map edge. Their brain (BotBrain) no longer fights; this sets where they run.
            /// </summary>
            void Flee(bool look)
            {
                float2 min = Layout.Origin, max = Layout.Origin + new float2(Layout.Width, Layout.Height) * SimConstants.CellSize;
                for (int i = 0; i < Count; i++)
                {
                    var a = Agents[i];
                    if (!a.IsRouting || a.IsDead) continue;
                    var m = Morale[i];
                    if (look)
                    {
                        float2 away = AwayFromEnemies(i, a);
                        if (math.lengthsq(away) > 1e-8f) m.FleeDirection = math.normalizesafe(m.FleeDirection + math.normalize(away), m.FleeDirection);
                        Morale[i] = m;
                    }
                    float2 edge = math.min(a.Position - min, max - a.Position);
                    if (math.cmin(edge) <= Settings.EdgeMargin)
                    {
                        a.Flags |= AgentFlags.Dead | AgentFlags.Fled;
                        a.SpeedScale = 0f;
                        a.Velocity = float2.zero;
                        Agents[i] = a;
                        var c = Combat[i];
                        c.State = CombatState.Dead;
                        c.BlockHeld = 0;
                        c.Sprinting = 0;
                        Combat[i] = c;
                        Events.Add(new CombatEvent { Type = CombatEventType.Fled, Attacker = -1, Target = i, Point = new float3(a.Position.x, 1f, a.Position.y) });
                        continue;
                    }
                    a.Target = math.clamp(a.Position + m.FleeDirection * FleeLead, min, max);
                    a.FaceAgent = -1;
                    Agents[i] = a;
                }
            }
        }
    }
}
