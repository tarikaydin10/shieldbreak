using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Shieldbreak.Sim.AI;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Context;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Combat.Resolvers;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Grid;

namespace Shieldbreak.Sim.Combat
{
    /// <summary>
    /// The combat grammar (CLAUDE.md 3.5, Schicht A): Windup → Release → Recovery, feint and morph in the windup,
    /// input buffer, one-button block in a front arc with stamina cost, perfect block with riposte stun, guard break,
    /// dodge step, sprint, archetype specials (shield bash, two-hand leaping overhead with a lunge), weapon switching,
    /// SwingTrace hits with energy-based damage, 1vX attack tokens, the couched lance (ContactTrace) and horses that run
    /// men down unless a braced spear or a shield wall stops them. Players (via InputQueue commands) and bots (BotBrain) call the same Try* rules. Runs single-threaded
    /// in agent id order, so hits resolve identically on every peer. Bots in a formation follow its context rules
    /// (FormationCombatRules); the formation arrays are only read here.
    /// </summary>
    public static class CombatSystem
    {
        public static void Step(int tick, NativeArray<AgentData> agents, NativeArray<CombatData> combat, int count,
            in SpatialHash spatial, CombatSetup setup, NativeList<CombatEvent> events, NativeArray<int> attackerScratch, NativeArray<CombatData> seenScratch, NativeArray<ProjectileData> projectiles,
            NativeArray<FormationData> formations, NativeArray<FormationMember> members, in FormationSettings formationSettings, NativeArray<float> heights, NativeArray<byte> ground)
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
                Heights = heights,
                Ground = ground,
                Weapons = setup.Weapons,
                Attacks = setup.Attacks,
                Keys = setup.Keys,
                Bots = setup.BotProfiles,
                Tuning = setup.Tuning,
                Events = events,
                AttackersOn = attackerScratch,
                Seen = seenScratch,
                Projectiles = projectiles,
                Formations = formations,
                Members = members,
                FormationSettings = formationSettings,
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

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        internal struct CombatJob : IJob
        {
            public int Tick;
            public int Count;
            public NativeArray<AgentData> Agents;
            public NativeArray<CombatData> Combat;
            [ReadOnly] public NativeArray<int> CellStart;
            [ReadOnly] public NativeArray<int> Entries;
            public GridLayout Layout;
            [ReadOnly] public NativeArray<float> Heights;
            [ReadOnly] public NativeArray<byte> Ground;
            [ReadOnly] public NativeArray<WeaponData> Weapons;
            [ReadOnly] public NativeArray<AttackData> Attacks;
            [ReadOnly] public NativeArray<WeaponPathKey> Keys;
            [ReadOnly] public NativeArray<BotProfile> Bots;
            public CombatTuning Tuning;
            public NativeList<CombatEvent> Events;
            /// <summary>Bots currently swinging at each agent (attack tokens).</summary>
            public NativeArray<int> AttackersOn;
            /// <summary>Everybody's combat state at the start of the tick: what the bots see of each other.</summary>
            public NativeArray<CombatData> Seen;
            /// <summary>Arrows in flight; empty in worlds without any.</summary>
            public NativeArray<ProjectileData> Projectiles;
            /// <summary>Read by the bots; arrows note in it that a formation is under fire.</summary>
            public NativeArray<FormationData> Formations;
            [ReadOnly] public NativeArray<FormationMember> Members;
            public FormationSettings FormationSettings;

            AttackData AttackOf(in CombatData c) => Attacks[Weapons[c.WeaponId].AttackIndex(c.Direction)];

            public void Execute()
            {
                NativeArray<CombatData>.Copy(Combat, 0, Seen, 0, Count);

                // Attack tokens: count bots already swinging at each target
                for (int i = 0; i < Count; i++) AttackersOn[i] = 0;
                for (int i = 0; i < Count; i++)
                {
                    var c = Combat[i];
                    // Tokens are for melee swings: shooters need none
                    if ((c.State == CombatState.Windup || c.State == CombatState.Release) && c.BotProfile > 0 && c.BotTarget >= 0 && c.BotTarget < Count && !Weapons[c.WeaponId].IsRanged)
                        AttackersOn[c.BotTarget]++;
                }

                // Whoever acts first in a tick strikes first. Over thousands of ticks that decides battles (measured: 11 against
                // 6 soldiers left of the same force), so the direction alternates and no agent id has the first move for
                // good. Bots read each other from Seen, so what they see does not depend on the direction either.
                bool ascending = (Tick & 1) == 0;
                for (int turn = 0; turn < Count; turn++)
                {
                    int i = ascending ? turn : Count - 1 - turn;
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
                Charges();
                FlyArrows();
            }

            /// <summary>Height of the ground under a position (Sim/Grid/Terrain).</summary>
            public float GroundAt(float2 position) => Terrain.HeightAt(Heights, Layout, position);

            /// <summary>Arrows leave the bow at this height and go for this height on the target: the chest (m).</summary>
            const float ShootHeight = 1.5f, AimHeight = 1.25f;

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
                if (c.State != CombatState.Release) return;
                if (attack.Resolver == AttackResolver.SwingTrace) Trace(i, ref c, attack, lunge);
                else if (attack.Resolver == AttackResolver.ContactTrace) Couch(i, ref c, attack);
                else if (c.HitCount == 0)
                {
                    // Once per shot, at the start of the release
                    c.HitCount = 1;
                    Shoot(i, ref c);
                }
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
                    // A soldier aims his swing at his enemy, so the trace ignores the ground: the blade would pass over a
                    // man lower down and strike him only with the slow end of the swing. From above he has his weight behind it
                    float struckGround = GroundAt(struck.Position);
                    float above = math.clamp((GroundAt(me.Position) - struckGround) * Tuning.HeightEnergyPerMeter, -Tuning.HeightEnergyLimit, Tuning.HeightEnergyLimit);
                    float effective = energy * (1f + above) * SwingTrace.Penetration(motion, bladeWorld, weapon.EdgeFactor, weapon.PointFactor);
                    float3 point = math.lerp(p0, p1, bestT);
                    point.y += struckGround;

                    RecordHit(ref c, bestTarget);
                    bool stopped = Resolve(i, ref c, bestTarget, bestZone, effective, weapon, point);
                    if (stopped) return;
                    energyKeep *= Tuning.MultiHitEnergyKeep;
                }
            }

            /// <summary>
            /// ContactTrace (CLAUDE.md 3.5): the couched lance is held still along the pose; the first enemy body on it
            /// is struck with the closing speed of rider and target along the lance, E = ½·m·v². A lance at rest pushes,
            /// it does not pierce. One strike, then the lance is raised again (recovery).
            /// </summary>
            void Couch(int i, ref CombatData c, in AttackData attack)
            {
                if (c.HitCount >= attack.MaxTargets) return;
                var me = Agents[i];
                var weapon = Weapons[c.WeaponId];
                WeaponPath.Evaluate(Keys, attack.FirstKey, attack.KeyCount, attack.Timing.PoseTime(c.StateTick), out float3 hand, out float3 blade);
                float3 hilt = SwingTrace.ToWorld(me.Position, me.Facing, hand);
                float3 tip = SwingTrace.ToWorld(me.Position, me.Facing, hand + blade * attack.BladeLength);
                float2 along = math.normalizesafe(tip.xz - hilt.xz, me.Facing);

                int best = -1, bestZone = -1;
                float bestS = 2f;
                int r = (int)math.ceil((attack.BladeLength + 1f) / SimConstants.CellSize);
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
                        if (j == i || j >= Count || AlreadyHit(c, j)) continue;
                        var other = Agents[j];
                        if (other.IsDead || other.Team == me.Team) continue;
                        if (SwingTrace.SweepAgent(hilt, tip, other.Position, out float s, out int zone) && (s < bestS || (s == bestS && j < best)))
                        {
                            bestS = s;
                            best = j;
                            bestZone = zone;
                        }
                    }
                }
                if (best < 0) return;

                var struck = Agents[best];
                float closing = math.dot(me.Velocity - struck.Velocity, along);
                if (closing < Tuning.CouchMinSpeed) return;
                float3 motion = new float3(along.x, 0f, along.y) * closing;
                float energy = 0.5f * weapon.Mass * closing * closing;
                float struckGround = GroundAt(struck.Position);
                float above = math.clamp((GroundAt(me.Position) - struckGround) * Tuning.HeightEnergyPerMeter, -Tuning.HeightEnergyLimit, Tuning.HeightEnergyLimit);
                float effective = energy * (1f + above) * SwingTrace.Penetration(motion, SwingTrace.DirectionToWorld(me.Facing, blade), weapon.EdgeFactor, weapon.PointFactor);
                float3 point = math.lerp(hilt, tip, bestS);
                point.y += struckGround;
                RecordHit(ref c, best);
                if (Resolve(i, ref c, best, bestZone, effective, weapon, point) || c.State != CombatState.Release) return;
                c.State = CombatState.Recovery;
                c.StateTick = math.max(c.StateTick, attack.Timing.WindupTicks + attack.Timing.ReleaseTicks);
            }

            /// <summary>
            /// Horses (CLAUDE.md M4): a horse that reaches a man on foot at speed runs him down with its weight, E = ½·m·v²
            /// of the closing speed, and loses some of its own speed with every man. A spear braced against it (the block
            /// of a polearm) turns that energy against the rider; a shield wall makes the horse refuse. Runs after every
            /// agent acted, in id order.
            /// </summary>
            void Charges()
            {
                for (int i = 0; i < Count; i++)
                {
                    var rider = Agents[i];
                    if (!rider.IsMounted || rider.IsDead || math.lengthsq(rider.Velocity) < Tuning.TrampleMinSpeed * Tuning.TrampleMinSpeed) continue;
                    int r = (int)math.ceil((rider.Radius + Tuning.TrampleReach + 0.5f) / SimConstants.CellSize);
                    int2 cell = math.clamp(Layout.WorldToCell(rider.Position), int2.zero, new int2(Layout.Width - 1, Layout.Height - 1));
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
                            var me = Agents[i];
                            if (me.IsDead) continue;
                            var other = Agents[j];
                            if (other.IsDead || other.IsMounted || other.Team == me.Team || Tick - Combat[j].LastTrampledTick < Tuning.TrampleCooldownTicks) continue;
                            float2 d = other.Position - me.Position;
                            float dist = math.length(d);
                            if (dist < 1e-4f || dist > me.Radius + other.Radius + Tuning.TrampleReach) continue;
                            float2 dir = d / dist;
                            if (math.dot(dir, math.normalizesafe(me.Velocity, me.Facing)) < 0.5f) continue;
                            float closing = math.dot(me.Velocity - other.Velocity, dir);
                            if (closing >= Tuning.TrampleMinSpeed) Impact(i, j, dir, closing);
                        }
                    }
                }
            }

            void Impact(int i, int j, float2 dir, float closing)
            {
                var me = Agents[i];
                var rider = Combat[i];
                var targetAgent = Agents[j];
                var target = Combat[j];
                var arm = Weapons[target.WeaponId];
                float energy = 0.5f * Tuning.HorseMass * closing * closing;
                float blow = energy * Tuning.TrampleEnergyShare;
                float3 point = new float3(targetAgent.Position.x, GroundAt(targetAgent.Position) + 1.2f, targetAgent.Position.y);
                target.LastTrampledTick = Tick;
                bool guard = target.BlockHeld == 1 && target.State == CombatState.Idle &&
                             math.dot(-dir, targetAgent.Facing) >= math.cos(math.radians(arm.BlockArcDeg * 0.5f));

                if (guard && arm.Archetype == WeaponArchetype.Polearm)
                {
                    // Brace: the horse runs onto the spear, the rider takes the point with the horse's weight behind it
                    float effective = energy * Tuning.BraceEnergyShare * arm.PointFactor;
                    float3 riderPoint = new float3(me.Position.x, GroundAt(me.Position) + 1.2f, me.Position.y);
                    Events.Add(new CombatEvent { Type = CombatEventType.Brace, Attacker = j, Target = i, Zone = BodyZone.Torso, Energy = effective, Point = riderPoint });
                    me.Velocity = float2.zero;
                    Agents[i] = me;
                    target.Knockback += dir * math.min(Tuning.KnockbackMax, blow * Tuning.KnockbackPerJoule * Tuning.BlockKnockbackScale);
                    Combat[j] = target;
                    ApplyHit(j, i, ref rider, ref me, BodyZone.Torso, effective, riderPoint, new CombatEvent { Attacker = j, Target = i, Zone = BodyZone.Torso, Energy = effective, Point = riderPoint });
                    Agents[i] = me;
                    Combat[i] = rider;
                    return;
                }

                var ev = new CombatEvent { Attacker = i, Target = j, Zone = BodyZone.Torso, Energy = blow, Point = point };
                if (guard && FormationContext(j).ShieldWall == 1)
                {
                    // The horse will not run into a wall of shields: it stops, the rider is shaken, the wall takes the shock
                    me.Velocity *= Tuning.ShieldWallSpeedKeep;
                    if (Tuning.RefuseStunTicks > 0 && rider.State != CombatState.Dead)
                    {
                        rider.BlockHeld = 0;
                        rider.State = CombatState.Stunned;
                        rider.StateTick = 0;
                        rider.StunTicks = Tuning.RefuseStunTicks;
                    }
                    Events.Add(new CombatEvent { Type = CombatEventType.Refuse, Attacker = i, Target = j, Zone = BodyZone.Torso, Energy = blow, Point = point });
                    Guard(ref target, blow, arm, dir, ref ev);
                }
                else
                {
                    Events.Add(new CombatEvent { Type = CombatEventType.Trample, Attacker = i, Target = j, Zone = BodyZone.Torso, Energy = blow, Point = point });
                    if (guard) Guard(ref target, blow, arm, dir, ref ev);
                    else ApplyHit(i, j, ref target, ref targetAgent, BodyZone.Torso, blow, point, ev);
                    me.Velocity *= Tuning.TrampleSpeedKeep;
                }
                Combat[j] = target;
                Combat[i] = rider;
                Agents[i] = me;
            }

            /// <summary>A raised shield or weapon takes the shock of a horse for stamina, like a blocked blow; empty, the guard breaks.</summary>
            void Guard(ref CombatData target, float blow, in WeaponData arm, float2 dir, ref CombatEvent ev)
            {
                target.Stamina -= blow * Tuning.BlockStaminaPerJoule * arm.BlockCost;
                target.LastStaminaUseTick = Tick;
                target.Knockback += dir * math.min(Tuning.KnockbackMax, blow * Tuning.KnockbackPerJoule * Tuning.BlockKnockbackScale);
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

            /// <summary>
            /// Looses an arrow at the bot's target, led by how far it walks in the flight time. The aim misses by the
            /// bot's error: that is the skill of a shooter, never the arrow's damage. Without a target the arrow goes straight ahead.
            /// </summary>
            void Shoot(int i, ref CombatData c)
            {
                int slot = -1;
                for (int p = 0; p < Projectiles.Length && slot < 0; p++)
                    if (Projectiles[p].Active == 0) slot = p;
                if (slot < 0) return;
                var me = Agents[i];
                var weapon = Weapons[c.WeaponId];
                var from = new float3(me.Position.x, GroundAt(me.Position) + ShootHeight, me.Position.y) + new float3(me.Facing.x, 0f, me.Facing.y) * 0.4f;
                int target = c.BotTarget >= 0 && c.BotTarget < Count && !Agents[c.BotTarget].IsDead ? c.BotTarget : -1;
                float2 aim = me.Position + me.Facing * weapon.MaxRange * 0.5f;
                if (target >= 0)
                {
                    var enemy = Agents[target];
                    float distance = math.distance(enemy.Position, me.Position);
                    aim = enemy.Position + enemy.Velocity * (distance / weapon.ProjectileSpeed);
                    float error = c.BotProfile > 0 && c.BotProfile < Bots.Length ? Bots[c.BotProfile].AimErrorPerMeter * distance : 0f;
                    var rng = new SimRandom { State = c.RngState };
                    aim += new float2(rng.NextFloat(-error, error), rng.NextFloat(-error, error));
                    c.RngState = rng.State;
                }
                Projectiles[slot] = new ProjectileData
                {
                    Position = from,
                    Velocity = Projectile.Launch(from, new float3(aim.x, GroundAt(aim) + AimHeight, aim.y), weapon.ProjectileSpeed, weapon.ProjectileGravity),
                    Owner = i,
                    Team = me.Team,
                    WeaponId = c.WeaponId,
                    Active = 1,
                };
                Events.Add(new CombatEvent { Type = CombatEventType.Shot, Attacker = i, Target = target, Point = from });
            }

            /// <summary>One step for every arrow: it strikes the first enemy on its way, or sticks in the ground. No friendly fire.</summary>
            void FlyArrows()
            {
                for (int p = 0; p < Projectiles.Length; p++)
                {
                    var arrow = Projectiles[p];
                    if (arrow.Active == 0) continue;
                    Projectile.Step(ref arrow, Weapons[arrow.WeaponId].ProjectileGravity, SimConstants.DT, out float3 from);
                    int struck = -1, zone = -1;
                    float bestT = 2f;
                    int2 cell = Layout.WorldToCell(arrow.Position.xz);
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int2 cc = cell + new int2(dx, dy);
                        if (!Layout.InBounds(cc)) continue;
                        int index = Layout.CellIndex(cc);
                        for (int e = CellStart[index]; e < CellStart[index + 1]; e++)
                        {
                            int j = Entries[e];
                            if (j >= Count) continue;
                            var other = Agents[j];
                            if (other.IsDead || other.Team == arrow.Team) continue;
                            if (SwingTrace.SweepAgent(from, arrow.Position, other.Position, out float t, out int z, GroundAt(other.Position)) && !(IsAirborne(Combat[j], Tuning) && z == BodyZone.Legs) &&
                                (t < bestT || (t == bestT && j < struck)))
                            {
                                bestT = t;
                                struck = j;
                                zone = z;
                            }
                        }
                    }
                    if (struck >= 0)
                    {
                        ResolveArrow(arrow, struck, zone, math.lerp(from, arrow.Position, bestT));
                        arrow.Active = 0;
                    }
                    else if (!Layout.InBounds(cell) || arrow.Position.y <= GroundAt(arrow.Position.xz) || StopsInTrees(arrow, from, p)) arrow.Active = 0;
                    Projectiles[p] = arrow;
                }
            }

            /// <summary>
            /// Below the crowns every metre of forest may stop an arrow. The dice come from the arrow, its slot and the
            /// tick, so nobody's random numbers shift with how many arrows fly.
            /// </summary>
            bool StopsInTrees(in ProjectileData arrow, float3 from, int slot)
            {
                if (Ground[Layout.ClampedCellIndex(arrow.Position.xz)] != (byte)GroundKind.Forest) return false;
                if (arrow.Position.y > GroundAt(arrow.Position.xz) + Tuning.ForestCrownHeight) return false;
                var rng = new SimRandom((ulong)(uint)Tick * 0x9E3779B97F4A7C15UL + (ulong)(uint)slot * 0xC2B2AE3D27D4EB4FUL + (ulong)(uint)arrow.Owner + 1UL);
                return rng.NextFloat() < Tuning.ForestArrowStopPerMeter * math.distance(from, arrow.Position);
            }

            /// <summary>
            /// A shield blocks an arrow in its front arc for stamina, like a swing, but a perfect block does not exist:
            /// nobody can read an arrow (docs/combat/papier-test-bogen-lanze.md). The energy comes from the flight.
            /// </summary>
            void ResolveArrow(in ProjectileData arrow, int j, int zone, float3 point)
            {
                var weapon = Weapons[arrow.WeaponId];
                var target = Combat[j];
                var targetAgent = Agents[j];
                float effective = 0.5f * weapon.Mass * math.lengthsq(arrow.Velocity) * weapon.PointFactor;
                var ev = new CombatEvent { Attacker = arrow.Owner, Target = j, Zone = zone, Energy = effective, Point = point };
                float2 toArcher = math.normalizesafe(-arrow.Velocity.xz, targetAgent.Facing);
                NoteFire(j, toArcher);
                float arc = Weapons[target.WeaponId].BlockArcDeg;
                if (target.BlockHeld == 1 && target.State == CombatState.Idle && math.dot(toArcher, targetAgent.Facing) >= math.cos(math.radians(arc * 0.5f)))
                {
                    target.Stamina -= effective * Tuning.BlockStaminaPerJoule * weapon.BlockPressure * Weapons[target.WeaponId].BlockCost;
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
                    return;
                }
                ApplyHit(arrow.Owner, j, ref target, ref targetAgent, zone, effective, point, ev);
                Combat[j] = target;
            }

            /// <summary>The formation of <paramref name="j"/> is under fire from that direction (FormationSystem decides what it does about it).</summary>
            void NoteFire(int j, float2 toArcher)
            {
                int id = Members[j].Formation;
                if (id < 0 || id >= Formations.Length) return;
                var f = Formations[id];
                f.UnderFireTick = Tick;
                f.ArrowHits++;
                f.FireDirection += toArcher;
                Formations[id] = f;
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
                        target.Stamina -= effective * Tuning.BlockStaminaPerJoule * weapon.BlockPressure * Weapons[target.WeaponId].BlockCost;
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

                ApplyHit(i, j, ref target, ref targetAgent, zone, effective, point, ev);
                Combat[j] = target;
                // Overhead and thrust stop in the first body; side cuts may carry on (MaxTargets)
                return false;
            }

            /// <summary>Armour is a threshold per zone (CLAUDE.md 3.5); what gets through wounds, stuns or kills.</summary>
            void ApplyHit(int i, int j, ref CombatData target, ref AgentData targetAgent, int zone, float effective, float3 point, CombatEvent ev)
            {
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
            }

            void UpdateMovement(int i, in CombatData c)
            {
                var a = Agents[i];
                if (c.State == CombatState.Dead)
                {
                    a.SpeedScale = 0f;
                    a.Flags |= AgentFlags.Dead;
                }
                // The horse runs on whatever the rider's arm does; only a shaken rider (refused, struck) holds it in
                else if (a.IsMounted) a.SpeedScale = c.State == CombatState.Stunned ? Tuning.StunSpeedScale : 1f;
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
            /// <summary>How an agent stood at the start of the tick (for bots: all see the same moment).</summary>
            public CombatData CombatOf(int i) => Seen[i];

            /// <summary>Loadout slot of the bow, -1 = none or already drawn.</summary>
            public int BowSlot(in CombatData c)
            {
                if (Weapons[c.WeaponId].IsRanged) return -1;
                for (int s = 0; s < 4; s++)
                    if (c.Loadout[s] >= 0 && Weapons[c.Loadout[s]].IsRanged) return s;
                return -1;
            }

            /// <summary>Loadout slot of a melee weapon, -1 = none.</summary>
            public int MeleeSlot(in CombatData c)
            {
                for (int s = 0; s < 4; s++)
                    if (c.Loadout[s] >= 0 && !Weapons[c.Loadout[s]].IsRanged) return s;
                return -1;
            }

            /// <summary>From this distance a shooter with its side arm goes back to the bow: past the bow's MinRange, with a margin, so it does not flicker.</summary>
            public float ShooterSwitchRange(in CombatData c)
            {
                int bow = BowSlot(c);
                return bow >= 0 ? Weapons[c.Loadout[bow]].MinRange + 2f : float.MaxValue;
            }
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
            public FormationSettings FormationRules => FormationSettings;

            /// <summary>What the agent's formation asks of it in a fight; default (free) outside formations.</summary>
            public FormationCombatContext FormationContext(int i)
            {
                if (i >= Members.Length) return default;
                var m = Members[i];
                if (m.Formation < 0 || m.Formation >= Formations.Length) return default;
                var ctx = FormationCombatRules.For(m, Formations[m.Formation]);
                return Ground[Layout.ClampedCellIndex(Agents[i].Position)] == (byte)GroundKind.Forest ? FormationCombatRules.InForest(ctx) : ctx;
            }
            public bool InFormation(int i) => i < Members.Length && Members[i].Formation >= 0;
            public void ClaimAttackToken(int target) { if (target >= 0 && target < Count) AttackersOn[target]++; }
        }
    }
}
