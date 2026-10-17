using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Shieldbreak.Sim.AI;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Combat.Resolvers;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Grid;

namespace Shieldbreak.Sim.Combat
{
    /// <summary>
    /// The combat grammar (CLAUDE.md 3.5, Schicht A): Windup → Release → Recovery, feint and morph in the windup,
    /// input buffer, one-button block in a front arc with stamina cost, perfect block with riposte stun, guard break,
    /// SwingTrace hits with energy-based damage. Players (via InputQueue commands) and bots (BotBrain) call the same
    /// Try* rules. Runs single-threaded in agent id order, so hits resolve identically on every peer.
    /// </summary>
    public static class CombatSystem
    {
        public static void Step(int tick, NativeArray<AgentData> agents, NativeArray<CombatData> combat, int count,
            in SpatialHash spatial, CombatSetup setup, NativeList<CombatEvent> events)
        {
            if (count == 0 || !setup.Weapons.IsCreated || setup.Weapons.Length == 0) return;
            new CombatJob
            {
                Tick = tick,
                Count = count,
                Agents = agents,
                Combat = combat,
                CellStart = spatial.CellStart,
                Entries = spatial.Entries,
                Layout = spatial.Layout,
                Weapons = setup.Weapons,
                Attacks = setup.Attacks,
                Keys = setup.Keys,
                Bots = setup.BotProfiles,
                Tuning = setup.Tuning,
                Events = events,
            }.Run();
        }

        // ---- Rules shared by player commands and bots ------------------------------------------------------------

        public static bool CanAct(in CombatData c) => c.State != CombatState.Dead && c.State != CombatState.Stunned;

        /// <summary>Start an attack, morph a young windup, or buffer the input while busy.</summary>
        public static void TryAttack(ref CombatData c, int direction, int tick, in AttackData current, in CombatTuning tuning, NativeList<CombatEvent> events, int self)
        {
            direction = math.clamp(direction, 0, AttackDirection.Count - 1);
            if (c.State == CombatState.Dead) return;
            if (c.State == CombatState.Idle && c.Stamina > 0f)
            {
                c.BlockHeld = 0;
                c.State = CombatState.Windup;
                c.Direction = direction;
                c.StateTick = 0;
                c.Stamina -= tuning.AttackStaminaCost;
                c.LastStaminaUseTick = tick;
                c.HitIds = new int4(-1);
                c.HitCount = 0;
                c.Buffered = BufferedAction.None;
                events.Add(new CombatEvent { Type = CombatEventType.AttackStart, Attacker = self, Target = -1, Direction = direction });
                return;
            }
            if (c.State == CombatState.Windup && direction != c.Direction && c.StateTick <= (int)(tuning.MorphWindow * current.Timing.WindupTicks))
            {
                c.Direction = direction;
                return;
            }
            c.Buffered = BufferedAction.Attack;
            c.BufferedDirection = direction;
            c.BufferedTick = tick;
        }

        /// <summary>Cancel a young windup (feint).</summary>
        public static bool TryFeint(ref CombatData c, int tick, in AttackData current, in CombatTuning tuning, NativeList<CombatEvent> events, int self)
        {
            if (c.State != CombatState.Windup || c.StateTick > (int)(tuning.FeintWindow * current.Timing.WindupTicks)) return false;
            c.State = CombatState.Idle;
            c.StateTick = 0;
            c.Stamina -= tuning.FeintStaminaCost;
            c.LastStaminaUseTick = tick;
            events.Add(new CombatEvent { Type = CombatEventType.Feint, Attacker = self, Target = -1, Direction = c.Direction });
            return true;
        }

        /// <summary>Raise or lower the block. Raising in a young windup feints into the block; while busy it is buffered.</summary>
        public static void SetBlock(ref CombatData c, bool down, int tick, in AttackData current, in CombatTuning tuning, NativeList<CombatEvent> events, int self)
        {
            if (!down)
            {
                c.BlockHeld = 0;
                if (c.Buffered == BufferedAction.Block) c.Buffered = BufferedAction.None;
                return;
            }
            if (c.BlockHeld == 1) return;
            if (c.State == CombatState.Windup) TryFeint(ref c, tick, current, tuning, events, self);
            if (c.State == CombatState.Idle)
            {
                c.BlockHeld = 1;
                c.BlockPressedTick = tick;
                c.Buffered = BufferedAction.None;
                return;
            }
            if (c.State != CombatState.Dead)
            {
                c.Buffered = BufferedAction.Block;
                c.BufferedTick = tick;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct CombatJob : IJob
        {
            public int Tick;
            public int Count;
            public NativeArray<AgentData> Agents;
            public NativeArray<CombatData> Combat;
            [ReadOnly] public NativeArray<int> CellStart;
            [ReadOnly] public NativeArray<int> Entries;
            public GridLayout Layout;
            [ReadOnly] public NativeArray<WeaponData> Weapons;
            [ReadOnly] public NativeArray<AttackData> Attacks;
            [ReadOnly] public NativeArray<WeaponPathKey> Keys;
            [ReadOnly] public NativeArray<BotProfile> Bots;
            public CombatTuning Tuning;
            public NativeList<CombatEvent> Events;

            AttackData AttackOf(in CombatData c) => Attacks[Weapons[c.WeaponId].FirstAttack + c.Direction];

            public void Execute()
            {
                for (int i = 0; i < Count; i++)
                {
                    var c = Combat[i];
                    if (c.State == CombatState.Dead) continue;

                    if (c.State == CombatState.Stunned)
                    {
                        c.StateTick++;
                        if (c.StateTick >= c.StunTicks) { c.State = CombatState.Idle; c.StateTick = 0; }
                    }

                    if (c.BotProfile > 0 && c.BotProfile < Bots.Length) BotBrain.Think(i, ref c, ref this);

                    // Input buffer: replay a recent action as soon as the agent is free
                    if (c.Buffered != BufferedAction.None)
                    {
                        if (Tick - c.BufferedTick > Tuning.InputBufferTicks) c.Buffered = BufferedAction.None;
                        else if (c.State == CombatState.Idle)
                        {
                            int action = c.Buffered;
                            c.Buffered = BufferedAction.None;
                            if (action == BufferedAction.Attack) TryAttack(ref c, c.BufferedDirection, Tick, AttackOf(c), Tuning, Events, i);
                            else SetBlock(ref c, true, Tick, AttackOf(c), Tuning, Events, i);
                        }
                    }

                    if (c.State == CombatState.Windup || c.State == CombatState.Release || c.State == CombatState.Recovery)
                        AdvanceAttack(i, ref c);

                    // Stamina regenerates when the agent is idle, not blocking and has not spent stamina for a while
                    if (c.State == CombatState.Idle && c.BlockHeld == 0 && Tick - c.LastStaminaUseTick >= Tuning.StaminaRegenDelayTicks)
                        c.Stamina = math.min(Tuning.StaminaMax, c.Stamina + Tuning.StaminaRegenPerSecond * SimConstants.DT);

                    Combat[i] = c;
                    UpdateMovement(i, c);
                }
            }

            void AdvanceAttack(int i, ref CombatData c)
            {
                var attack = AttackOf(c);
                c.StateTick++;
                var phase = attack.Timing.PhaseAt(c.StateTick);
                if (phase == AttackPhase.Done)
                {
                    c.State = CombatState.Idle;
                    c.StateTick = 0;
                    return;
                }
                c.State = phase == AttackPhase.Windup ? CombatState.Windup : phase == AttackPhase.Release ? CombatState.Release : CombatState.Recovery;
                if (c.State == CombatState.Release) Trace(i, ref c, attack);
            }

            /// <summary>Sweeps every blade socket from the previous to the current tick and resolves contacts.</summary>
            void Trace(int i, ref CombatData c, in AttackData attack)
            {
                var me = Agents[i];
                var weapon = Weapons[c.WeaponId];
                WeaponPath.Evaluate(Keys, attack.FirstKey, attack.KeyCount, attack.Timing.PoseTime(c.StateTick - 1), out float3 h0, out float3 b0);
                WeaponPath.Evaluate(Keys, attack.FirstKey, attack.KeyCount, attack.Timing.PoseTime(c.StateTick), out float3 h1, out float3 b1);
                float energyKeep = 1f;

                // Candidates within reach: 5x5 cells around the attacker (blade reach up to ~1.6 m)
                int2 cell = math.clamp(Layout.WorldToCell(me.Position), int2.zero, new int2(Layout.Width - 1, Layout.Height - 1));
                while (c.HitCount < attack.MaxTargets)
                {
                    int bestTarget = -1, bestZone = -1, bestSocket = -1;
                    float bestT = 2f;
                    for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int2 cc = cell + new int2(dx, dy);
                        if (!Layout.InBounds(cc)) continue;
                        int index = Layout.CellIndex(cc);
                        for (int e = CellStart[index]; e < CellStart[index + 1]; e++)
                        {
                            int j = Entries[e];
                            if (j == i || j >= Count || AlreadyHit(c, j)) continue;
                            var other = Agents[j];
                            if (other.IsDead || other.Team == me.Team) continue;
                            for (int k = 1; k <= attack.SocketCount; k++)
                            {
                                float along = attack.BladeLength * k / attack.SocketCount;
                                float3 from = SwingTrace.ToWorld(me.Position, me.Facing, h0 + b0 * along);
                                float3 to = SwingTrace.ToWorld(me.Position, me.Facing, h1 + b1 * along);
                                if (SwingTrace.SweepAgent(from, to, other.Position, out float t, out int zone) &&
                                    (t < bestT || (t == bestT && j < bestTarget)))
                                {
                                    bestT = t;
                                    bestTarget = j;
                                    bestZone = zone;
                                    bestSocket = k;
                                }
                            }
                        }
                    }
                    if (bestTarget < 0) return;

                    // The blade strikes with its fastest part that reaches this target in this tick, not with whichever
                    // socket touched first (often the slow part near the grip)
                    var struck = Agents[bestTarget];
                    float bestSpeedSq = -1f;
                    for (int k = 1; k <= attack.SocketCount; k++)
                    {
                        float along = attack.BladeLength * k / attack.SocketCount;
                        float3 from = SwingTrace.ToWorld(me.Position, me.Facing, h0 + b0 * along);
                        float3 to = SwingTrace.ToWorld(me.Position, me.Facing, h1 + b1 * along);
                        if (!SwingTrace.SweepAgent(from, to, struck.Position, out _, out _)) continue;
                        float sq = math.distancesq(from, to);
                        if (sq > bestSpeedSq)
                        {
                            bestSpeedSq = sq;
                            bestSocket = k;
                        }
                    }

                    float alongBest = attack.BladeLength * bestSocket / attack.SocketCount;
                    float3 p0 = SwingTrace.ToWorld(me.Position, me.Facing, h0 + b0 * alongBest);
                    float3 p1 = SwingTrace.ToWorld(me.Position, me.Facing, h1 + b1 * alongBest);
                    float3 motion = p1 - p0;
                    float speed = math.length(motion) / SimConstants.DT;
                    float3 bladeWorld = SwingTrace.DirectionToWorld(me.Facing, b1);
                    float energy = 0.5f * weapon.Mass * speed * speed * energyKeep;
                    float effective = energy * SwingTrace.Penetration(motion, bladeWorld, weapon.EdgeFactor, weapon.PointFactor);
                    float3 point = math.lerp(p0, p1, bestT);

                    RecordHit(ref c, bestTarget);
                    bool stopped = Resolve(i, ref c, bestTarget, bestZone, effective, weapon, point);
                    if (stopped) return;
                    energyKeep *= Tuning.MultiHitEnergyKeep;
                }
            }

            static bool AlreadyHit(in CombatData c, int j) => c.HitIds.x == j || c.HitIds.y == j || c.HitIds.z == j || c.HitIds.w == j;

            static void RecordHit(ref CombatData c, int j)
            {
                switch (c.HitCount)
                {
                    case 0: c.HitIds.x = j; break;
                    case 1: c.HitIds.y = j; break;
                    case 2: c.HitIds.z = j; break;
                    default: c.HitIds.w = j; break;
                }
                c.HitCount++;
            }

            /// <summary>Applies block, perfect block or damage. Returns true if the swing stops here.</summary>
            bool Resolve(int i, ref CombatData attacker, int j, int zone, float effective, in WeaponData weapon, float3 point)
            {
                var target = Combat[j];
                var targetAgent = Agents[j];
                var me = Agents[i];
                var ev = new CombatEvent { Attacker = i, Target = j, Zone = zone, Direction = attacker.Direction, Energy = effective, Point = point };

                // Block: held, defender able to act, attacker inside the defender's front arc
                if (target.BlockHeld == 1 && target.State == CombatState.Idle)
                {
                    float arc = Weapons[target.WeaponId].BlockArcDeg;
                    float2 toAttacker = math.normalizesafe(me.Position - targetAgent.Position, targetAgent.Facing);
                    if (math.dot(toAttacker, targetAgent.Facing) >= math.cos(math.radians(arc * 0.5f)))
                    {
                        if (Tick - target.BlockPressedTick <= Tuning.PerfectBlockTicks)
                        {
                            ev.Type = CombatEventType.PerfectBlock;
                            Events.Add(ev);
                            attacker.State = CombatState.Stunned;
                            attacker.StateTick = 0;
                            attacker.StunTicks = Tuning.PerfectBlockStunTicks;
                            return true;
                        }
                        target.Stamina -= effective * Tuning.BlockStaminaPerJoule * weapon.BlockPressure;
                        target.LastStaminaUseTick = Tick;
                        if (target.Stamina <= 0f)
                        {
                            target.Stamina = 0f;
                            target.BlockHeld = 0;
                            target.State = CombatState.Stunned;
                            target.StateTick = 0;
                            target.StunTicks = Tuning.GuardBreakStunTicks;
                            ev.Type = CombatEventType.GuardBreak;
                        }
                        else ev.Type = CombatEventType.Blocked;
                        Events.Add(ev);
                        Combat[j] = target;
                        // A blocked swing bounces into recovery
                        var attack = Attacks[Weapons[attacker.WeaponId].FirstAttack + attacker.Direction];
                        attacker.State = CombatState.Recovery;
                        attacker.StateTick = math.max(attacker.StateTick, attack.Timing.WindupTicks + attack.Timing.ReleaseTicks);
                        return true;
                    }
                }

                float threshold = zone == BodyZone.Head ? Tuning.ZoneThreshold.z : zone == BodyZone.Torso ? Tuning.ZoneThreshold.y : Tuning.ZoneThreshold.x;
                float multiplier = zone == BodyZone.Head ? Tuning.ZoneMultiplier.z : zone == BodyZone.Torso ? Tuning.ZoneMultiplier.y : Tuning.ZoneMultiplier.x;
                float damage = math.max(0f, effective - threshold) * Tuning.DamagePerJoule * multiplier;
                ev.Type = CombatEventType.Hit;
                ev.Damage = damage;
                Events.Add(ev);

                if (damage > 0f)
                {
                    target.Health -= damage;
                    target.BlockHeld = 0;
                    if (target.Health <= 0f)
                    {
                        target.Health = 0f;
                        target.State = CombatState.Dead;
                        Events.Add(new CombatEvent { Type = CombatEventType.Death, Attacker = i, Target = j, Zone = zone, Point = point });
                        targetAgent.Flags |= AgentFlags.Dead;
                        targetAgent.SpeedScale = 0f;
                        Agents[j] = targetAgent;
                    }
                    else
                    {
                        // Hit stun interrupts whatever the target was doing, including its own attack
                        target.State = CombatState.Stunned;
                        target.StateTick = 0;
                        target.StunTicks = Tuning.HitStunTicks;
                    }
                }
                Combat[j] = target;
                // Overhead and thrust stop in the first body; side cuts may carry on (MaxTargets)
                return false;
            }

            void UpdateMovement(int i, in CombatData c)
            {
                var a = Agents[i];
                if (c.State == CombatState.Dead)
                {
                    a.SpeedScale = 0f;
                    a.Flags |= AgentFlags.Dead;
                }
                else if (c.State == CombatState.Stunned) a.SpeedScale = Tuning.StunSpeedScale;
                else if (c.State != CombatState.Idle) a.SpeedScale = Tuning.AttackSpeedScale;
                else if (c.BlockHeld == 1) a.SpeedScale = Tuning.BlockSpeedScale;
                else a.SpeedScale = 1f;
                Agents[i] = a;
            }

            // ---- Accessors for the bot brain (same rules as player commands) ----

            public int CurrentTick => Tick;
            public CombatTuning Rules => Tuning;
            public AgentData Agent(int i) => Agents[i];
            public CombatData CombatOf(int i) => Combat[i];
            public void SetAgent(int i, AgentData a) => Agents[i] = a;
            public AttackData Attack(in CombatData c) => AttackOf(c);
            public BotProfile Profile(int p) => Bots[p];
            public int AgentCount => Count;
            public GridLayout Grid => Layout;
            public int CellBegin(int cellIndex) => CellStart[cellIndex];
            public int CellEnd(int cellIndex) => CellStart[cellIndex + 1];
            public int EntryAt(int e) => Entries[e];
            public NativeList<CombatEvent> EventList => Events;
        }

    }
}
