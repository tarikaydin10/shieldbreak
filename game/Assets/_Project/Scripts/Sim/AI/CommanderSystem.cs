using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Grid;
using Shieldbreak.Sim.Morale;
using Shieldbreak.Sim.Orders;

namespace Shieldbreak.Sim.AI
{
    /// <summary>
    /// The commanders of a world (CLAUDE.md 3.1, 3.7): each scores its tactics every few seconds in a Burst job, then
    /// gives its formations orders. It runs inside the sim and applies the same FormationOrder a player sends, as the
    /// owner of its formations, so it can do nothing a player could not. Runs first in the tick, on last tick's state.
    /// </summary>
    public sealed class CommanderSystem : IDisposable
    {
        readonly CommanderSettings settings;
        readonly FormationSystem formations;
        readonly WorldGrid grid;
        readonly ulong seed;
        NativeArray<CommanderState> commanders;
        int count;

        public CommanderSystem(int capacity, in CommanderSettings settings, FormationSystem formations, WorldGrid grid, ulong seed)
        {
            this.settings = settings;
            this.formations = formations;
            this.grid = grid;
            this.seed = seed;
            commanders = new NativeArray<CommanderState>(capacity, Allocator.Persistent);
        }

        public NativeArray<CommanderState>.ReadOnly Commanders => commanders.GetSubArray(0, count).AsReadOnly();
        public CommanderSettings Settings => settings;

        /// <summary>Scenario setup: this side now has a commander, who owns the formations of <paramref name="owner"/>.</summary>
        public void Add(int owner, int team)
        {
            if (count >= commanders.Length) throw new InvalidOperationException($"Commander capacity {commanders.Length} reached.");
            settings.Validate();
            commanders[count++] = new CommanderState
            {
                Rng = new SimRandom(seed ^ (0xC2B2AE3D27D4EB4FUL * (ulong)(owner + 1))).State,
                Owner = owner,
                Team = team,
                Tactic = CommanderTactic.Frontal,
                AppliedTactic = -1,
                Flanker = -1,
            };
        }

        public void Step(int tick, NativeArray<AgentData> agents, NativeArray<MoraleData> morale, NativeArray<CombatData> combat, int agentCount)
        {
            if (count == 0) return;
            new DecideJob
            {
                Tick = tick,
                Count = count,
                AgentCount = agentCount,
                FormationCount = formations.Count,
                Settings = settings,
                FormationSettings = formations.Settings,
                Commanders = commanders,
                Agents = agents,
                Morale = morale,
                Combat = combat,
                Formations = formations.FormationBuffer,
                Heights = grid.Heights,
                Layout = grid.Layout,
            }.Schedule().Complete();

            for (int c = 0; c < count; c++)
            {
                var state = commanders[c];
                if (state.Execute == 0) continue;
                state.Execute = 0;
                Execute(ref state);
                commanders[c] = state;
            }
        }

        /// <summary>
        /// Turns the tactic into orders, only where a formation is not already doing what the tactic asks: repeated
        /// orders would reset marches and cooldowns. A formation fighting on its own (stance) is left to it unless
        /// the tactic just changed.
        /// </summary>
        void Execute(ref CommanderState c)
        {
            bool changed = (int)c.Tactic != c.AppliedTactic;
            c.AppliedTactic = (int)c.Tactic;
            bool flanking = c.Tactic == CommanderTactic.FlankLeft || c.Tactic == CommanderTactic.FlankRight;
            if (!flanking) c.Flanker = -1;
            else if (changed || !Stands(c.Flanker)) c.Flanker = PickFlanker(c);

            var all = formations.Formations;
            for (int id = 0; id < all.Length; id++)
            {
                var f = all[id];
                if (f.Owner != c.Owner || f.MemberCount == 0) continue;
                if (f.Ranged == 1)
                {
                    // Shooters follow the retreat and otherwise screen, whatever the line does. With no line left there is
                    // nothing to screen: they go in with the side arm, or two sets of shooters would stand off for ever
                    if (c.Tactic == CommanderTactic.Retreat)
                    {
                        if (changed) Give(c, FormationOrder.Retreat(id));
                    }
                    else if (c.Features.OwnFormations == 0)
                    {
                        if (f.Behavior != FormationBehavior.Charge) Give(c, FormationOrder.Charge(id));
                    }
                    else if (f.Behavior != FormationBehavior.Screen) Give(c, FormationOrder.SetBehavior(id, FormationBehavior.Screen));
                    continue;
                }
                if (f.Mounted == 1)
                {
                    LeadRiders(c, f, changed);
                    continue;
                }
                if (id == c.Flanker) SendRound(c, f, changed);
                else if (c.Tactic == CommanderTactic.Hold) StandFast(c, f, changed);
                else if (c.Tactic == CommanderTactic.Retreat)
                {
                    if (changed) Give(c, FormationOrder.Retreat(id));
                }
                else if (f.Behavior != FormationBehavior.Advance && f.Behavior != FormationBehavior.Charge) Give(c, FormationOrder.Advance(id));
            }
        }

        /// <summary>
        /// Riders are the reserve: they stand until the lines are close, then charge the best target anyone can read off
        /// the field, shooters and men already fighting before spears and shield walls. They stay on it unless a clearly
        /// better one turns up. Only a retreat calls them back.
        /// </summary>
        void LeadRiders(in CommanderState c, in FormationData f, bool changed)
        {
            if (c.Tactic == CommanderTactic.Retreat)
            {
                if (changed) Give(c, FormationOrder.Retreat(f.Id));
                return;
            }
            if (f.Behavior != FormationBehavior.Charge && settings.RiderWaitDistance > 0f && c.Features.Distance > settings.RiderWaitDistance)
            {
                StandFast(c, f, changed);
                return;
            }
            int target = BestRiderTarget(f);
            if (target < 0)
            {
                if (f.Behavior != FormationBehavior.Charge) Give(c, FormationOrder.Charge(f.Id));
                return;
            }
            if (f.Behavior != FormationBehavior.Charge || f.TargetFormation != target) Give(c, FormationOrder.Against(f.Id, FormationBehavior.Charge, target));
        }

        int BestRiderTarget(in FormationData riders)
        {
            var all = formations.Formations;
            int best = -1;
            float bestScore = float.MinValue;
            for (int g = 0; g < all.Length; g++)
            {
                var enemy = all[g];
                if (enemy.Team == riders.Team || enemy.MemberCount == 0) continue;
                bool engaged = false;
                for (int o = 0; o < all.Length && !engaged; o++)
                {
                    var own = all[o];
                    engaged = own.Team == riders.Team && own.MemberCount > 0 && own.Ranged == 0 && own.Mounted == 0 &&
                              math.distancesq(own.Position, enemy.Position) < settings.ContactDistance * settings.ContactDistance;
                }
                float score = CommanderUtility.RiderTargetScore(enemy, math.distance(enemy.Position, riders.Position), engaged, settings);
                if (g == riders.TargetFormation) score += settings.RiderRetargetMargin;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = g;
                }
            }
            return best;
        }

        bool Stands(int formation) => formation >= 0 && formation < formations.Count && formations.Formations[formation].MemberCount > 0;

        /// <summary>The own formation furthest out on the flanking side; the lowest id on ties.</summary>
        int PickFlanker(in CommanderState c)
        {
            float2 axis = c.Features.Axis;
            float2 outwards = new float2(axis.y, -axis.x) * (c.Tactic == CommanderTactic.FlankRight ? 1f : -1f);
            int best = -1;
            float bestOut = float.MinValue;
            var all = formations.Formations;
            for (int id = 0; id < all.Length; id++)
            {
                var f = all[id];
                if (f.Owner != c.Owner || f.MemberCount == 0 || f.Ranged == 1 || f.Mounted == 1) continue;
                float reach = math.dot(f.Position - c.Features.OwnCentre, outwards);
                if (reach > bestOut)
                {
                    bestOut = reach;
                    best = id;
                }
            }
            return best;
        }

        /// <summary>
        /// Marches to the end of the enemy's line, Aggressive: on arrival the stance takes it into his flank. The goal
        /// follows the enemy while the formation is on its way; once it fights, it is left alone.
        /// </summary>
        void SendRound(in CommanderState c, in FormationData f, bool changed)
        {
            float2 goal = CommanderUtility.FlankPoint(c.Features, c.Tactic, settings, out float2 facing);
            bool moved = math.distance(f.Destination, goal) > settings.FlankRetargetDistance;
            if (!changed && (f.Behavior != FormationBehavior.Hold || !moved)) return;
            var order = FormationOrder.MoveTo(f.Id, goal, facing);
            order.Flags |= FormationOrderFlags.Stance;
            order.Stance = FormationStance.Aggressive;
            Give(c, order);
        }

        /// <summary>Stops and stands, ready to meet whoever comes near (React).</summary>
        void StandFast(in CommanderState c, in FormationData f, bool changed)
        {
            if (!changed && (f.Behavior == FormationBehavior.Hold || f.Reacting != 0)) return;
            var order = FormationOrder.Hold(f.Id);
            order.Flags |= FormationOrderFlags.Stance;
            order.Stance = FormationStance.React;
            Give(c, order);
        }

        void Give(in CommanderState c, in FormationOrder order) => formations.Apply(order, c.Owner);

        public void AddToHash(ref SimHash h) => h.Add(commanders.GetSubArray(0, count));

        public void Dispose()
        {
            if (commanders.IsCreated) commanders.Dispose();
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        struct DecideJob : IJob
        {
            public int Tick, Count, AgentCount, FormationCount;
            public CommanderSettings Settings;
            public FormationSettings FormationSettings;
            public NativeArray<CommanderState> Commanders;
            [ReadOnly] public NativeArray<AgentData> Agents;
            [ReadOnly] public NativeArray<MoraleData> Morale;
            [ReadOnly] public NativeArray<CombatData> Combat;
            [ReadOnly] public NativeArray<FormationData> Formations;
            [ReadOnly] public NativeArray<float> Heights;
            public GridLayout Layout;

            public void Execute()
            {
                for (int c = 0; c < Count; c++)
                {
                    var state = Commanders[c];
                    if (Tick < state.NextDecisionTick) continue;
                    state.NextDecisionTick = Tick + Settings.DecisionIntervalTicks;
                    state.Features = CommanderUtility.Read(state.Team, state.Owner, Agents, Morale, Combat, AgentCount, Formations, FormationCount, FormationSettings, Settings, Heights, Layout);
                    if (state.Features.OwnFighters > 0 && state.Features.EnemyFighters > 0)
                    {
                        var rng = new SimRandom { State = state.Rng };
                        bool attacking = state.Tactic != CommanderTactic.Hold && state.Tactic != CommanderTactic.Retreat;
                        if (attacking && state.Features.Distance < Settings.ContactDistance) state.LastFightTick = Tick;
                        state.Scores = CommanderUtility.Score(state.Features, Settings, Tick - state.LastFightTick, state.Tactic);
                        // The first decision is free: there is no plan yet to stick to
                        var rules = Settings;
                        if (state.AppliedTactic < 0) rules.Hysteresis = rules.MinTacticTicks = 0;
                        var chosen = CommanderUtility.Decide(ref state.Scores, state.Tactic, Tick - state.TacticTick, rules, ref rng);
                        state.Rng = rng.State;
                        if (chosen != state.Tactic)
                        {
                            state.Tactic = chosen;
                            state.TacticTick = Tick;
                            state.TacticChanges++;
                        }
                        state.Execute = 1;
                    }
                    Commanders[c] = state;
                }
            }
        }
    }
}
