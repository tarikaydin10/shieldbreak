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
    /// dodge step, sprint, archetype specials (shield bash, two-hand leaping overhead with a lunge), weapon switching,
    /// SwingTrace hits with energy-based damage, 1vX attack tokens. Players (via InputQueue commands) and bots (BotBrain) call the same Try* rules. Runs single-threaded
    /// in agent id order, so hits resolve identically on every peer.
    /// </summary>
    public static class CombatSystem
    {
        public static void Step(int tick, NativeArray<AgentData> agents, NativeArray<CombatData> combat, int count,
            in SpatialHash spatial, CombatSetup setup, NativeList<CombatEvent> events, NativeArray<int> attackerScratch)
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
                AttackersOn = attackerScratch,
            }.Run();
        }

        // ---- Rules shared by player commands and bots ------------------------------------------------------------

        public static bool CanAct(in CombatData c) => c.State != CombatState.Dead && c.State != CombatState.Stunned;

        static bool IsBusy(in CombatData c) => c.State != CombatState.Idle;

        /// <summary>Start an attack, morph a young windup, or buffer the input while busy.</summary>
        public static void TryAttack(ref CombatData c, int direction, int tick, in AttackData current, in CombatTuning tuning, NativeList<CombatEvent> events, int self)
        {
            direction = math.clamp(direction, 0, AttackDirection.Count - 1);
            if (c.State == CombatState.Dead) return;
            if (c.State == CombatState.Idle && c.Stamina > 0f)
            {
                c.BlockHeld = 0;
                c.Sprinting = 0;
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
            if (c.State == CombatState.Windup && direction != c.Direction && c.Direction != AttackDirection.Special && c.StateTick <= (int)(tuning.MorphWindow * current.Timing.WindupTicks))
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
                c.Sprinting = 0;
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

        /// <summary>Dodge step in a direction relative to the facing; only from idle (a raised block is dropped).</summary>
        public static bool TryStep(ref CombatData c, int stepDirection, int tick, in CombatTuning tuning, NativeList<CombatEvent> events, int self)
        {
            if (IsBusy(c) || c.Stamina <= 0f) return false;
            c.BlockHeld = 0;
            c.Sprinting = 0;
            c.State = CombatState.Stepping;
            c.Direction = math.clamp(stepDirection, 0, 3);
            c.StateTick = 0;
            c.Stamina -= tuning.StepStaminaCost;
            c.LastStaminaUseTick = tick;
            events.Add(new CombatEvent { Type = CombatEventType.Step, Attacker = self, Target = -1, Direction = c.Direction });
            return true;
        }

        /// <summary>
        /// Archetype special (CLAUDE.md 3.5, Schicht B). One-hand + shield: shield bash (short windup, then a push in a
        /// narrow cone that breaks any raised block). Two-hand: leaping overhead, an attack from the special slot whose
        /// lunge adds the body's speed to the blade.
        /// </summary>
        public static bool TrySpecial(ref CombatData c, int tick, in WeaponData weapon, in CombatTuning tuning, NativeList<CombatEvent> events, int self)
        {
            if (IsBusy(c) || c.Stamina <= 0f) return false;
            c.BlockHeld = 0;
            c.Sprinting = 0;
            c.State = weapon.HasSpecialAttack ? CombatState.Windup : CombatState.Special;
            if (weapon.HasSpecialAttack) c.Direction = AttackDirection.Special;
            c.StateTick = 0;
            c.HitCount = 0;
            c.HitIds = new int4(-1);
            c.Buffered = BufferedAction.None;
            c.Stamina -= tuning.SpecialStaminaCost;
            c.LastStaminaUseTick = tick;
            events.Add(new CombatEvent { Type = CombatEventType.SpecialStart, Attacker = self, Target = -1, Direction = c.Direction });
            return true;
        }

        /// <summary>Draw the weapon in a loadout slot; only from idle, buffered while busy. Costs WeaponSwitchTicks.</summary>
        public static bool TrySwitch(ref CombatData c, int slot, int tick, in CombatTuning tuning, NativeList<CombatEvent> events, int self)
        {
            if (slot < 0 || slot > 3 || c.State == CombatState.Dead) return false;
            int weapon = c.Loadout[slot];
            if (weapon < 0 || weapon == c.WeaponId) return false;
            if (IsBusy(c))
            {
                c.Buffered = BufferedAction.Switch;
                c.BufferedDirection = slot;
                c.BufferedTick = tick;
                return false;
            }
            c.BlockHeld = 0;
            c.Sprinting = 0;
            c.PreviousWeaponId = c.WeaponId;
            c.WeaponId = weapon;
            c.ActiveSlot = slot;
            c.Direction = 0;
            c.State = CombatState.Switching;
            c.StateTick = 0;
            c.Buffered = BufferedAction.None;
            events.Add(new CombatEvent { Type = CombatEventType.WeaponSwitch, Attacker = self, Target = -1, Direction = slot });
            return true;
        }

        /// <summary>Jump: only from idle; the block drops, running continues, attacks and blocks are buffered until landing.</summary>
        public static bool TryJump(ref CombatData c, int tick, in CombatTuning tuning, NativeList<CombatEvent> events, int self)
        {
            if (IsBusy(c) || c.Stamina <= 0f) return false;
            c.BlockHeld = 0;
            c.State = CombatState.Jumping;
            c.StateTick = 0;
            c.Stamina -= tuning.JumpStaminaCost;
            c.LastStaminaUseTick = tick;
            events.Add(new CombatEvent { Type = CombatEventType.Jump, Attacker = self, Target = -1 });
            return true;
        }

        /// <summary>Airborne until the landing recovery starts: legs cannot be hit, nothing can be done.</summary>
        public static bool IsAirborne(in CombatData c, in CombatTuning tuning) => c.State == CombatState.Jumping && c.StateTick < tuning.JumpTicks;

        /// <summary>Local unit vector of a step direction (x = right, y = forward).</summary>
        public static float2 StepVector(int stepDirection) =>
            stepDirection == StepDirection.Back ? new float2(0f, -1f) :
            stepDirection == StepDirection.Left ? new float2(-1f, 0f) :
            stepDirection == StepDirection.Right ? new float2(1f, 0f) : new float2(0f, 1f);

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
            /// <summary>Bots currently swinging at each agent (attack tokens).</summary>
            public NativeArray<int> AttackersOn;

            AttackData AttackOf(in CombatData c) => Attacks[Weapons[c.WeaponId].AttackIndex(c.Direction)];

            public void Execute()
            {
                // Attack tokens: count bots already swinging at each target
                for (int i = 0; i < Count; i++) AttackersOn[i] = 0;
                for (int i = 0; i < Count; i++)
                {
                    var c = Combat[i];
                    if ((c.State == CombatState.Windup || c.State == CombatState.Release) && c.BotProfile > 0 && c.BotTarget >= 0 && c.BotTarget < Count)
                        AttackersOn[c.BotTarget]++;
                }

                for (int i = 0; i < Count; i++)
                {
                    var c = Combat[i];
                    ApplyKnockback(i, ref c);
                    if (c.State == CombatState.Dead)
                    {
                        Combat[i] = c;
                        continue;
                    }

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
                            else if (action == BufferedAction.Switch) TrySwitch(ref c, c.BufferedDirection, Tick, Tuning, Events, i);
                            else SetBlock(ref c, true, Tick, AttackOf(c), Tuning, Events, i);
                        }
                    }

                    if (c.State == CombatState.Windup || c.State == CombatState.Release || c.State == CombatState.Recovery)
                        AdvanceAttack(i, ref c);
                    else if (c.State == CombatState.Stepping)
                        AdvanceStep(i, ref c);
                    else if (c.State == CombatState.Special)
                        AdvanceSpecial(i, ref c);
                    else if (c.State == CombatState.Jumping)
                    {
                        c.StateTick++;
                        if (c.StateTick >= Tuning.JumpTicks + Tuning.JumpLandTicks) { c.State = CombatState.Idle; c.StateTick = 0; }
                    }
                    else if (c.State == CombatState.Switching)
                    {
                        c.StateTick++;
                        if (c.StateTick >= Tuning.WeaponSwitchTicks) { c.State = CombatState.Idle; c.StateTick = 0; }
                    }

                    // Sprint drains stamina; stamina regenerates when idle, not blocking, not sprinting
                    var a = Agents[i];
                    bool moving = math.lengthsq(a.Velocity) > 0.25f;
                    if (c.Sprinting == 1 && (c.State == CombatState.Idle || c.State == CombatState.Jumping) && moving)
                    {
                        c.Stamina -= Tuning.SprintStaminaPerSecond * SimConstants.DT;
                        c.LastStaminaUseTick = Tick;
                        if (c.Stamina <= 0f) { c.Stamina = 0f; c.Sprinting = 0; }
                    }
                    else if (c.State == CombatState.Idle && c.BlockHeld == 0 && Tick - c.LastStaminaUseTick >= Tuning.StaminaRegenDelayTicks)
                        c.Stamina = math.min(Tuning.StaminaMax, c.Stamina + Tuning.StaminaRegenPerSecond * SimConstants.DT);

                    Combat[i] = c;
                    UpdateMovement(i, c);
                }
            }

            /// <summary>Stagger step: the push moves the body directly (steering cannot cancel it) and fades out.</summary>
            void ApplyKnockback(int i, ref CombatData c)
            {
                if (math.lengthsq(c.Knockback) < 1e-4f)
                {
                    c.Knockback = float2.zero;
                    return;
                }
                var a = Agents[i];
                float2 max = Layout.Origin + new float2(Layout.Width, Layout.Height) * SimConstants.CellSize - 1e-3f;
                a.Position = math.clamp(a.Position + c.Knockback * SimConstants.DT, Layout.Origin, max);
                Agents[i] = a;
                c.Knockback *= Tuning.KnockbackDecay;
            }

            float2 Push(int from, int to, float speed)
            {
                float2 d = math.normalizesafe(Agents[to].Position - Agents[from].Position, Agents[from].Facing);
                return d * math.min(Tuning.KnockbackMax, speed);
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
                float3 lunge = attack.LungeDistance > 0f ? Lunge(i, attack, c.StateTick) : float3.zero;
                if (c.State == CombatState.Release) Trace(i, ref c, attack, lunge);
            }

            /// <summary>
            /// Moves the attacker forward by the share of the lunge that falls into this tick's pose span. It stops short
            /// of an enemy in front instead of running through it. Returns the world displacement (adds to hit energy).
            /// </summary>
            float3 Lunge(int i, in AttackData attack, int stateTick)
            {
                float p0 = attack.Timing.PoseTime(stateTick - 1), p1 = attack.Timing.PoseTime(stateTick);
                float share = math.max(0f, math.min(p1, attack.LungeEndPose) - math.max(p0, attack.LungeStartPose)) / (attack.LungeEndPose - attack.LungeStartPose);
                if (share <= 0f) return float3.zero;
                var me = Agents[i];
                float step = share * attack.LungeDistance;
                int blocker = NearestEnemyInCone(i, 2.5f, 0.5f, out float dist);
                if (blocker >= 0) step = math.min(step, math.max(0f, dist - me.Radius - Agents[blocker].Radius - Tuning.LungeStopGap));
                if (step <= 0f) return float3.zero;
                me.Position += me.Facing * step;
                float2 max = Layout.Origin + new float2(Layout.Width, Layout.Height) * SimConstants.CellSize - 1e-3f;
                me.Position = math.clamp(me.Position, Layout.Origin, max);
                Agents[i] = me;
                return new float3(me.Facing.x, 0f, me.Facing.y) * step;
            }

            /// <summary>Nearest living enemy within <paramref name="range"/> whose direction lies within acos(<paramref name="cosArc"/>) of the facing.</summary>
            int NearestEnemyInCone(int i, float range, float cosArc, out float distance)
            {
                var me = Agents[i];
                int best = -1;
                float bestSq = range * range;
                int r = (int)math.ceil(range / SimConstants.CellSize);
                int2 cell = math.clamp(Layout.WorldToCell(me.Position), int2.zero, new int2(Layout.Width - 1, Layout.Height - 1));
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int2 cc = cell + new int2(dx, dy);
                    if (!Layout.InBounds(cc)) continue;
                    int index = Layout.CellIndex(cc);
                    for (int e = CellStart[index]; e < CellStart[index + 1]; e++)
                    {
                        int j = Entries[e];
                        if (j == i || j >= Count) continue;
                        var other = Agents[j];
                        if (other.IsDead || other.Team == me.Team) continue;
                        float2 d = other.Position - me.Position;
                        float dsq = math.lengthsq(d);
                        if (dsq > bestSq || dsq < 1e-6f || (dsq == bestSq && j > best)) continue;
                        if (math.dot(d / math.sqrt(dsq), me.Facing) < cosArc) continue;
                        bestSq = dsq;
                        best = j;
                    }
                }
                distance = best >= 0 ? math.sqrt(bestSq) : 0f;
                return best;
            }

            void AdvanceStep(int i, ref CombatData c)
            {
                c.StateTick++;
                var a = Agents[i];
                float2 local = StepVector(c.Direction);
                float2 right = new float2(a.Facing.y, -a.Facing.x);
                float2 world = right * local.x + a.Facing * local.y;
                a.Position += world * (Tuning.StepDistance / Tuning.StepTicks);
                float2 max = Layout.Origin + new float2(Layout.Width, Layout.Height) * SimConstants.CellSize - 1e-3f;
                a.Position = math.clamp(a.Position, Layout.Origin, max);
                a.Velocity = float2.zero;
                Agents[i] = a;
                if (c.StateTick >= Tuning.StepTicks) { c.State = CombatState.Idle; c.StateTick = 0; }
            }

            void AdvanceSpecial(int i, ref CombatData c)
            {
                c.StateTick++;
                int activeEnd = Tuning.SpecialWindupTicks + Tuning.SpecialActiveTicks;
                if (c.StateTick > Tuning.SpecialWindupTicks && c.StateTick <= activeEnd && c.HitCount == 0)
                    Bash(i, ref c);
                if (c.StateTick >= activeEnd + Tuning.SpecialRecoveryTicks) { c.State = CombatState.Idle; c.StateTick = 0; }
            }

            /// <summary>Shield bash: nearest enemy in a narrow cone. A raised block breaks; a body is staggered and hurt.</summary>
            void Bash(int i, ref CombatData c)
            {
                var me = Agents[i];
                int best = NearestEnemyInCone(i, Tuning.SpecialRange + 0.3f, math.cos(math.radians(Tuning.SpecialArcDeg * 0.5f)), out _);
                if (best < 0) return;
                c.HitCount = 1;
                c.HitIds.x = best;

                var target = Combat[best];
                var targetAgent = Agents[best];
                float3 point = new float3(targetAgent.Position.x, 1.2f, targetAgent.Position.y);
                if (target.State == CombatState.Dead) return;
                bool guardFacesMe = math.dot(math.normalizesafe(me.Position - targetAgent.Position, targetAgent.Facing), targetAgent.Facing) >= math.cos(math.radians(Weapons[target.WeaponId].BlockArcDeg * 0.5f));
                if (target.BlockHeld == 1 && guardFacesMe)
                {
                    target.BlockHeld = 0;
                    target.Stamina = 0f;
                    target.LastStaminaUseTick = Tick;
                    target.State = CombatState.Stunned;
                    target.StateTick = 0;
                    target.StunTicks = Tuning.GuardBreakStunTicks;
                    target.Knockback += Push(i, best, Tuning.BashKnockback);
                    Events.Add(new CombatEvent { Type = CombatEventType.GuardBreak, Attacker = i, Target = best, Zone = BodyZone.Torso, Point = point });
                }
                else
                {
                    target.Health -= Tuning.SpecialDamage;
                    target.BlockHeld = 0;
                    target.Knockback += Push(i, best, Tuning.BashKnockback);
                    Events.Add(new CombatEvent { Type = CombatEventType.Hit, Attacker = i, Target = best, Zone = BodyZone.Torso, Damage = Tuning.SpecialDamage, Point = point });
                    if (target.Health <= 0f) Kill(i, best, ref target, ref targetAgent, BodyZone.Torso, point);
                    else
                    {
                        target.State = CombatState.Stunned;
                        target.StateTick = 0;
                        target.StunTicks = Tuning.SpecialStunTicks;
                    }
                }
                Combat[best] = target;
            }

            void Kill(int attacker, int j, ref CombatData target, ref AgentData targetAgent, int zone, float3 point)
            {
                target.Health = 0f;
                target.State = CombatState.Dead;
                Events.Add(new CombatEvent { Type = CombatEventType.Death, Attacker = attacker, Target = j, Zone = zone, Point = point });
                targetAgent.Flags |= AgentFlags.Dead;
                targetAgent.SpeedScale = 0f;
                Agents[j] = targetAgent;
            }

            /// <summary>Sweeps every blade socket from the previous to the current tick and resolves contacts.</summary>
            void Trace(int i, ref CombatData c, in AttackData attack, float3 lunge)
            {
                var me = Agents[i];
                var weapon = Weapons[c.WeaponId];
                WeaponPath.Evaluate(Keys, attack.FirstKey, attack.KeyCount, attack.Timing.PoseTime(c.StateTick - 1), out float3 h0, out float3 b0);
                WeaponPath.Evaluate(Keys, attack.FirstKey, attack.KeyCount, attack.Timing.PoseTime(c.StateTick), out float3 h1, out float3 b1);
                float energyKeep = 1f;
                // The body's own motion (walk, sprint, attack lunge) adds to the blade: a charge into the line hits harder
                float3 bodyStep = new float3(me.Velocity.x, 0f, me.Velocity.y) * SimConstants.DT + lunge;

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
                            bool airborne = IsAirborne(Combat[j], Tuning);
                            for (int k = 1; k <= attack.SocketCount; k++)
                            {
                                float along = attack.BladeLength * k / attack.SocketCount;
                                float3 from = SwingTrace.ToWorld(me.Position, me.Facing, h0 + b0 * along);
                                float3 to = SwingTrace.ToWorld(me.Position, me.Facing, h1 + b1 * along);
                                if (SwingTrace.SweepAgent(from, to, other.Position, out float t, out int zone) && !(airborne && zone == BodyZone.Legs) &&
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
                    float3 motion = p1 - p0 + bodyStep;
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
                            attacker.Knockback += Push(j, i, effective * Tuning.KnockbackPerJoule * Tuning.BlockKnockbackScale);
                            return true;
                        }
                        target.Stamina -= effective * Tuning.BlockStaminaPerJoule * weapon.BlockPressure;
                        target.LastStaminaUseTick = Tick;
                        target.Knockback += Push(i, j, effective * Tuning.KnockbackPerJoule * Tuning.BlockKnockbackScale);
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
                        var attack = Attacks[Weapons[attacker.WeaponId].AttackIndex(attacker.Direction)];
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
                    target.Knockback += Push(i, j, effective * Tuning.KnockbackPerJoule);
                    target.Health -= damage;
                    target.BlockHeld = 0;
                    target.Sprinting = 0;
                    if (target.Health <= 0f) Kill(i, j, ref target, ref targetAgent, zone, point);
                    else
                    {
                        // Hit stun interrupts whatever the target was doing, including its own attack or step
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
                else if (c.State == CombatState.Stepping) a.SpeedScale = 0f;
                else if (c.State == CombatState.Jumping) a.SpeedScale = c.StateTick < Tuning.JumpTicks ? (c.Sprinting == 1 ? Tuning.SprintSpeedScale : 1f) : Tuning.StunSpeedScale;
                else if (c.State == CombatState.Stunned) a.SpeedScale = Tuning.StunSpeedScale;
                else if (c.State != CombatState.Idle) a.SpeedScale = Tuning.AttackSpeedScale;
                else if (c.BlockHeld == 1) a.SpeedScale = Tuning.BlockSpeedScale;
                else if (c.Sprinting == 1) a.SpeedScale = Tuning.SprintSpeedScale;
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
            public WeaponData Weapon(in CombatData c) => Weapons[c.WeaponId];
            public BotProfile Profile(int p) => Bots[p];
            public int AgentCount => Count;
            public GridLayout Grid => Layout;
            public int CellBegin(int cellIndex) => CellStart[cellIndex];
            public int CellEnd(int cellIndex) => CellStart[cellIndex + 1];
            public int EntryAt(int e) => Entries[e];
            public NativeList<CombatEvent> EventList => Events;
            public int AttackersSwingingAt(int target) => target >= 0 && target < Count ? AttackersOn[target] : 0;
            public void ClaimAttackToken(int target) { if (target >= 0 && target < Count) AttackersOn[target]++; }
        }
    }
}
