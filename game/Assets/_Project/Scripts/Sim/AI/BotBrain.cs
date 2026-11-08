using Unity.Mathematics;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Context;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;

namespace Shieldbreak.Sim.AI
{
    /// <summary>
    /// Melee bot on the combat grammar (CLAUDE.md 3.5): it sees what a player would see (the enemy's windup), reacts
    /// after a seeded delay, may fail, and uses only the same Try* rules a player uses. Levels differ in reaction
    /// time, error rates and allowed techniques, never in stats. Runs inside the CombatJob, so it is deterministic.
    /// In a formation the context rules (FormationCombatRules) decide whom it may fight and how far it may leave its slot.
    /// A player's hero runs here too while the player is in the command view (HeroUnattended): bound to its post behind
    /// the own line like a soldier to its slot, the block held while an enemy is near.
    /// Riders charge pass after pass with the couched lance; a soldier who sees one coming at him braces his spear,
    /// raises his shield or steps aside, after the same reaction time as against a sword.
    /// </summary>
    internal static class BotBrain
    {
        const int ReacquireInterval = 30;
        /// <summary>A target is kept a little further than it would be picked, so it does not flicker at the edge (m).</summary>
        const float KeepMargin = 0.5f;

        /// <summary>A soldier looks for riders charging at him this often (ticks), staggered by id.</summary>
        const int RiderScanInterval = 6;
        /// <summary>A rider coming at a soldier passes within this distance (m) of him: closer, he has to answer it.</summary>
        const float RiderPathWidth = 2.5f;
        /// <summary>A rider riding at a soldier this fast (m/s) is watched: one that comes at a walk may still gallop the last metres.</summary>
        const float RiderWatchMinSpeed = 1f;

        public static void Think(int i, ref CombatData c, ref CombatSystem.CombatJob job)
        {
            var self = job.Agent(i);
            if (self.IsMounted && !self.IsRouting && !self.IsUnattended)
            {
                ThinkRider(i, ref c, ref job);
                return;
            }
            bool rider = !self.IsRouting && !self.IsMounted && WatchRiders(i, ref c, ref job);
            ThinkOnFoot(i, ref c, ref job, rider);
            if (rider) AnswerRider(i, ref c, ref job);
        }

        static void ThinkOnFoot(int i, ref CombatData c, ref CombatSystem.CombatJob job, bool riderComing)
        {
            var p = job.Profile(c.BotProfile);
            var rng = new SimRandom { State = c.RngState };
            int tick = job.CurrentTick;
            var rules = job.Rules;
            var me = job.Agent(i);
            bool unattended = me.IsUnattended;
            var ctx = unattended ? FormationCombatRules.ForUnattendedHero(c.OrderTarget, c.OrderFacing, me.Facing) : job.FormationContext(i);
            bool holdBlock = p.HoldBlock == 1 || unattended;
            var formationRules = job.FormationRules;

            // Routing (Sim/Morale): no target, no block, no attack; run (sprint while stamina lasts). The morale
            // system sets where to.
            if (me.IsRouting)
            {
                c.BotTarget = -1;
                c.BotPlannedBlockTick = -1;
                if (c.BlockHeld == 1) CombatSystem.SetBlock(ref c, false, tick, job.Attack(c), rules, job.EventList, i);
                c.Sprinting = c.State == CombatState.Idle && c.Stamina > 0f ? 1 : 0;
                c.RngState = rng.State;
                return;
            }

            if (job.Weapon(c).IsRanged)
            {
                ThinkShooter(i, ref c, ref me, ctx, ref rng, ref job);
                c.RngState = rng.State;
                return;
            }

            // Target acquisition: periodic (staggered by id), when the current target is gone, or when the formation
            // no longer lets the bot fight it (out of the leash or the front arc, retreat)
            bool lost = c.BotTarget < 0 || c.BotTarget >= job.AgentCount || job.Agent(c.BotTarget).IsDead ||
                        !FormationCombatRules.Accepts(ctx, formationRules, me.Position, job.Agent(c.BotTarget).Position, p.EngageRange + KeepMargin);
            if (lost || (tick + i) % ReacquireInterval == 0)
                c.BotTarget = ctx.Disengaged == 1 ? -1 : FindTarget(i, me, p.AcquireRange, ctx, p.EngageRange, ref job);

            if (c.BotTarget < 0)
            {
                me.Target = c.OrderTarget;
                if (math.lengthsq(c.OrderFacing) > 0f)
                {
                    me.FaceAgent = AgentData.FaceFixed;
                    me.FaceDirection = c.OrderFacing;
                }
                else
                {
                    me.FaceAgent = -1;
                }
                job.SetAgent(i, me);
                // Under arrows the shields stay up; otherwise, without an enemy near, an unattended hero walks back with the shield down (blocking slows)
                if (ctx.TakingCover == 1)
                {
                    if (c.State == CombatState.Idle && c.BlockHeld == 0) CombatSystem.SetBlock(ref c, true, tick, job.Attack(c), rules, job.EventList, i);
                }
                else if (c.BlockHeld == 1 && (p.HoldBlock == 0 || unattended) && !riderComing) CombatSystem.SetBlock(ref c, false, tick, job.Attack(c), rules, job.EventList, i);
                DrawBow(i, ref c, ref job);
                c.RngState = rng.State;
                return;
            }

            var enemy = job.Agent(c.BotTarget);
            var ec = job.CombatOf(c.BotTarget);
            float2 toEnemy = enemy.Position - me.Position;
            float dist = math.length(toEnemy);
            float2 dir = dist > 1e-4f ? toEnemy / dist : me.Facing;

            // A shooter fighting with the side arm goes back to the bow once the enemy is out of reach of it
            if (dist > job.ShooterSwitchRange(c)) DrawBow(i, ref c, ref job);

            // Attack token (1vX): only a limited number of bots swing at the same target; the others hold back and threaten
            bool swinging = c.State == CombatState.Windup || c.State == CombatState.Release;
            bool tokenFree = rules.MaxAttackersPerTarget <= 0 || swinging || job.AttackersSwingingAt(c.BotTarget) < rules.MaxAttackersPerTarget;

            // Keep engage distance (further back while waiting for a token), face the enemy, sprint to close in
            // In formation the bot steps out of its slot at most the leash and does not sprint
            float engage = p.EngageRange + job.Weapon(c).EngageBonus;
            float keep = tokenFree ? engage : engage + rules.WaitingDistance;
            me.FaceAgent = c.BotTarget;
            me.Target = FormationCombatRules.Leashed(ctx, formationRules, enemy.Position - dir * keep);
            job.SetAgent(i, me);
            c.Sprinting = FormationCombatRules.AllowsSprint(ctx) && c.State == CombatState.Idle && c.BlockHeld == 0 && dist > engage + 3f ? 1 : 0;

            // Threat: the enemy swings at us (faces us, close enough; a lunging attack reaches further)
            var enemyAttack = job.Attack(ec);
            bool threat = (ec.State == CombatState.Windup || ec.State == CombatState.Release) &&
                          math.dot(enemy.Facing, -dir) > 0.5f && dist < p.EngageRange + 1.2f + job.Weapon(ec).EngageBonus + enemyAttack.LungeDistance;
            if (threat)
            {
                int start = tick - ec.StateTick;
                if (c.BotThreatStartTick != start)
                {
                    c.BotThreatStartTick = start;
                    c.BotReactAtTick = tick + rng.NextInt(p.ReactionMinTicks, p.ReactionMaxTicks + 1);
                }
                if (tick >= c.BotReactAtTick && c.BotDefenseDecidedFor != start && CombatSystem.CanAct(c))
                {
                    c.BotDefenseDecidedFor = start;
                    int contactTick = start + enemyAttack.Timing.WindupTicks + enemyAttack.Timing.ReleaseTicks / 2;
                    float blockChance = p.BlockChance, stepChance = p.StepChance;
                    FormationCombatRules.PreferBlock(ctx, ref blockChance, ref stepChance);
                    float roll = rng.NextFloat();
                    if (roll < p.PerfectBlockChance) c.BotPlannedBlockTick = math.max(tick, contactTick - rules.PerfectBlockTicks / 2);
                    else if (roll < p.PerfectBlockChance + blockChance) c.BotPlannedBlockTick = tick;
                    else
                    {
                        c.BotPlannedBlockTick = -1;
                        if (roll < p.PerfectBlockChance + blockChance + stepChance)
                            CombatSystem.TryStep(ref c, StepDirection.Back, tick, rules, job.EventList, i);
                    }
                }
                if (c.BotPlannedBlockTick >= 0 && tick >= c.BotPlannedBlockTick)
                {
                    // A block raised long ago is not perfect: lower and raise it again
                    if (c.BlockHeld == 1 && tick - c.BlockPressedTick > rules.PerfectBlockTicks)
                        CombatSystem.SetBlock(ref c, false, tick, job.Attack(c), rules, job.EventList, i);
                    CombatSystem.SetBlock(ref c, true, tick, job.Attack(c), rules, job.EventList, i);
                    c.BotPlannedBlockTick = -1;
                }
            }
            else
            {
                c.BotPlannedBlockTick = -1;
                if (holdBlock)
                {
                    if (c.State == CombatState.Idle && c.BlockHeld == 0) CombatSystem.SetBlock(ref c, true, tick, job.Attack(c), rules, job.EventList, i);
                }
                else if (c.BlockHeld == 1 && tick - c.BotThreatStartTick > p.ReactionMaxTicks + 20 && !riderComing)
                {
                    CombatSystem.SetBlock(ref c, false, tick, job.Attack(c), rules, job.EventList, i);
                }
            }

            // Attack when in range and not under threat; openings (enemy recovering or stunned) are punished more often
            if (tick >= c.BotNextDecisionTick && !riderComing)
            {
                c.BotNextDecisionTick = tick + p.DecisionIntervalTicks;
                var weapon = job.Weapon(c);
                // Two-hand leap: from just outside the engage range the lunge closes the gap and adds to the blow
                bool leapBand = weapon.Archetype == WeaponArchetype.TwoHand && dist > engage + 0.35f && dist <= engage + 1.3f;
                if (c.State == CombatState.Idle && !threat && tokenFree && leapBand && rng.NextFloat() < p.SpecialChance)
                {
                    if (CombatSystem.TrySpecial(ref c, tick, weapon, rules, job.EventList, i)) job.ClaimAttackToken(c.BotTarget);
                }
                else if (c.State == CombatState.Idle && !threat && tokenFree && dist <= engage + 0.35f)
                {
                    // A raised enemy block invites the special (shield bash breaks it, the leap presses it); otherwise
                    // attack, more often into an opening. A spear has no need of a raised block: its sweep clears the arc
                    bool specialFits = weapon.Archetype == WeaponArchetype.Polearm || (ec.BlockHeld == 1 && dist <= rules.SpecialRange + 0.4f);
                    if (specialFits && rng.NextFloat() < p.SpecialChance)
                    {
                        if (CombatSystem.TrySpecial(ref c, tick, weapon, rules, job.EventList, i) && c.State == CombatState.Windup) job.ClaimAttackToken(c.BotTarget);
                    }
                    else
                    {
                        bool opening = ec.State == CombatState.Recovery || ec.State == CombatState.Stunned;
                        if (rng.NextFloat() < (opening ? p.OpeningAttackChance : p.AttackChance))
                        {
                            CombatSystem.TryAttack(ref c, rng.NextInt(AttackDirection.Count), tick, job.Attack(c), rules, job.EventList, i);
                            if (c.State == CombatState.Windup) job.ClaimAttackToken(c.BotTarget);
                        }
                    }
                }
            }

            // Feint at 30 % of the own windup, then come again soon
            if (c.State == CombatState.Windup)
            {
                var own = job.Attack(c);
                if (c.StateTick == math.max(1, (int)(own.Timing.WindupTicks * 0.3f)) && rng.NextFloat() < p.FeintChance &&
                    CombatSystem.TryFeint(ref c, tick, own, rules, job.EventList, i))
                    c.BotNextDecisionTick = tick + 4;
            }

            c.RngState = rng.State;
        }

        /// <summary>
        /// A shooter on the same grammar: stays on its slot, faces its target, draws, looses, nocks. Levels differ in
        /// how often it shoots and how well it aims. An enemy too close makes it draw its side arm if it has one.
        /// </summary>
        static void ThinkShooter(int i, ref CombatData c, ref AgentData me, in FormationCombatContext ctx, ref SimRandom rng, ref CombatSystem.CombatJob job)
        {
            var p = job.Profile(c.BotProfile);
            var weapon = job.Weapon(c);
            int tick = job.CurrentTick;
            if (c.BlockHeld == 1) CombatSystem.SetBlock(ref c, false, tick, job.Attack(c), job.Rules, job.EventList, i);
            bool lost = c.BotTarget < 0 || c.BotTarget >= job.AgentCount || job.Agent(c.BotTarget).IsDead;
            if (ctx.Disengaged == 1) c.BotTarget = -1;
            else if ((lost && (tick + i) % 6 == 0) || (tick + i) % ReacquireInterval == 0) c.BotTarget = FindShot(me, weapon.MaxRange, weapon.RangePerMeterAbove, ctx, ref job);
            me.Target = c.OrderTarget;
            if (c.BotTarget < 0 || c.BotTarget >= job.AgentCount || job.Agent(c.BotTarget).IsDead)
            {
                c.BotTarget = -1;
                if (math.lengthsq(c.OrderFacing) > 0f)
                {
                    me.FaceAgent = AgentData.FaceFixed;
                    me.FaceDirection = c.OrderFacing;
                }
                else me.FaceAgent = -1;
                job.SetAgent(i, me);
                return;
            }
            me.FaceAgent = c.BotTarget;
            job.SetAgent(i, me);
            float dist = math.distance(job.Agent(c.BotTarget).Position, me.Position);
            if (dist < weapon.MinRange)
            {
                int side = job.MeleeSlot(c);
                if (side >= 0)
                {
                    CombatSystem.TrySwitch(ref c, side, tick, job.Rules, job.EventList, i);
                    return;
                }
            }
            if (tick < c.BotNextDecisionTick || c.State != CombatState.Idle) return;
            c.BotNextDecisionTick = tick + p.DecisionIntervalTicks;
            if (rng.NextFloat() < p.AttackChance) CombatSystem.TryAttack(ref c, AttackDirection.Thrust, tick, job.Attack(c), job.Rules, job.EventList, i);
        }

        // ---- Cavalry ----

        /// <summary>
        /// A rider on the same grammar: it picks the nearest enemy anywhere on the field, aims behind him so the horse
        /// rides through, lowers the lance at a gallop within CouchRange, and after the pass rides on and turns for the
        /// next. A rider whose horse came to a stand at the enemy (refused, or stuck in the crowd) rides away to charge
        /// again. Bound to a holding formation it keeps its slot; only a charge order sends it in.
        /// </summary>
        static void ThinkRider(int i, ref CombatData c, ref CombatSystem.CombatJob job)
        {
            var p = job.Profile(c.BotProfile);
            var rng = new SimRandom { State = c.RngState };
            int tick = job.CurrentTick;
            var rules = job.Rules;
            var me = job.Agent(i);
            var ctx = job.FormationContext(i);
            c.Sprinting = 0;
            if (c.BlockHeld == 1) CombatSystem.SetBlock(ref c, false, tick, job.Attack(c), rules, job.EventList, i);

            if (ctx.Disengaged == 1 || ctx.Bound == 1)
            {
                c.BotTarget = -1;
                c.RidePhase &= ~1;
                c.RidePoint = me.Position;
                me.Target = c.OrderTarget;
                if (math.lengthsq(c.OrderFacing) > 0f)
                {
                    me.FaceAgent = AgentData.FaceFixed;
                    me.FaceDirection = c.OrderFacing;
                }
                else me.FaceAgent = -1;
                job.SetAgent(i, me);
                c.RngState = rng.State;
                return;
            }

            me.FaceAgent = -1;
            if ((c.RidePhase & 1) == 1)
            {
                // Riding out after a pass; at the turning point the next pass begins from here
                me.Target = c.RidePoint;
                job.SetAgent(i, me);
                if (math.distancesq(me.Position, c.RidePoint) < 36f)
                {
                    c.RidePhase += 1;
                    c.RidePoint = me.Position;
                    c.BotTarget = -1;
                }
                c.RngState = rng.State;
                return;
            }

            bool lost = c.BotTarget < 0 || c.BotTarget >= job.AgentCount || job.Agent(c.BotTarget).IsDead;
            if (lost || (tick + i) % ReacquireInterval == 0) c.BotTarget = FindRiderTarget(me, ref job);
            if (c.BotTarget < 0)
            {
                me.Target = c.OrderTarget;
                job.SetAgent(i, me);
                c.RngState = rng.State;
                return;
            }

            var enemy = job.Agent(c.BotTarget);
            float2 to = enemy.Position - me.Position;
            float dist = math.length(to);
            float2 dir = dist > 1e-4f ? to / dist : me.Facing;
            float speed = math.length(me.Velocity);
            me.Target = InsideMap(enemy.Position + dir * rules.RideThroughDistance, job.Grid, 2f);
            job.SetAgent(i, me);

            if (c.State == CombatState.Idle && dist <= rules.CouchRange && speed >= rules.CouchMinSpeed && tick >= c.BotNextDecisionTick)
            {
                c.BotNextDecisionTick = tick + p.DecisionIntervalTicks;
                if (rng.NextFloat() < p.OpeningAttackChance) CombatSystem.TryAttack(ref c, AttackDirection.Thrust, tick, job.Attack(c), rules, job.EventList, i);
            }

            // The pass is over once the target lies between where it began and the rider, the lance struck, or the
            // horse stands at the enemy
            bool passed = math.dot(enemy.Position - c.RidePoint, me.Position - enemy.Position) > 0f;
            bool struck = c.State == CombatState.Recovery && c.HitCount > 0;
            bool stalled = dist < 4f && speed < 2f;
            if (passed || struck || stalled)
            {
                float2 away = stalled ? math.normalizesafe(me.Position - enemy.Position, -me.Facing) : math.normalizesafe(me.Velocity, me.Facing);
                c.RidePoint = InsideMap(me.Position + away * rules.RideOutDistance, job.Grid, 6f);
                c.RidePhase |= 1;
            }
            c.RngState = rng.State;
        }

        /// <summary>Nearest living enemy on foot in a formation (riders charge the ranks, they do not hunt stragglers), else the nearest
        /// on foot, else the nearest rider; a plain loop, riders look across the whole field.</summary>
        static int FindRiderTarget(in AgentData me, ref CombatSystem.CombatJob job)
        {
            int best = -1, bestLoose = -1, bestRider = -1;
            float bestSq = float.MaxValue, bestLooseSq = float.MaxValue, bestRiderSq = float.MaxValue;
            for (int j = 0; j < job.AgentCount; j++)
            {
                var other = job.Agent(j);
                if (other.IsDead || other.Team == me.Team) continue;
                float dsq = math.distancesq(other.Position, me.Position);
                if (other.IsMounted)
                {
                    if (dsq < bestRiderSq) { bestRiderSq = dsq; bestRider = j; }
                }
                else if (!job.InFormation(j))
                {
                    if (dsq < bestLooseSq) { bestLooseSq = dsq; bestLoose = j; }
                }
                else if (dsq < bestSq) { bestSq = dsq; best = j; }
            }
            return best >= 0 ? best : bestLoose >= 0 ? bestLoose : bestRider;
        }

        static float2 InsideMap(float2 p, in Shieldbreak.Sim.Grid.GridLayout layout, float margin)
        {
            float2 max = layout.Origin + new float2(layout.Width, layout.Height) * SimConstants.CellSize;
            return math.clamp(p, layout.Origin + margin, math.max(layout.Origin + margin, max - margin));
        }

        /// <summary>
        /// A rider riding at the soldier: in front of him (nobody sees one behind his back), moving, and its path
        /// passes within RiderPathWidth of him. The one already seen is kept while it still comes; others are looked for
        /// every RiderScanInterval ticks. A new one starts the reaction time.
        /// </summary>
        static bool WatchRiders(int i, ref CombatData c, ref CombatSystem.CombatJob job)
        {
            var me = job.Agent(i);
            int tick = job.CurrentTick;
            int rider = c.BotRider >= 0 && Comes(me, c.BotRider, ref job) ? c.BotRider : -1;
            if (rider < 0 && (tick + i) % RiderScanInterval == 0)
            {
                float bestSq = float.MaxValue;
                for (int j = 0; j < job.AgentCount; j++)
                {
                    if (!job.Agent(j).IsMounted || !Comes(me, j, ref job)) continue;
                    float dsq = math.distancesq(job.Agent(j).Position, me.Position);
                    if (dsq < bestSq) { bestSq = dsq; rider = j; }
                }
            }
            if (rider != c.BotRider)
            {
                c.BotRider = rider;
                c.BotRiderPlan = BotRiderAnswer.Undecided;
                if (rider >= 0)
                {
                    var p = job.Profile(c.BotProfile);
                    var rng = new SimRandom { State = c.RngState };
                    c.BotReactAtTick = tick + rng.NextInt(p.ReactionMinTicks, p.ReactionMaxTicks + 1);
                    c.RngState = rng.State;
                }
            }
            return rider >= 0;
        }

        static bool Comes(in AgentData me, int id, ref CombatSystem.CombatJob job)
        {
            if (id < 0 || id >= job.AgentCount) return false;
            var r = job.Agent(id);
            if (!r.IsMounted || r.IsDead || r.IsRouting || r.Team == me.Team) return false;
            var rules = job.Rules;
            float2 d = me.Position - r.Position;
            float dist = math.length(d);
            float speed = math.length(r.Velocity);
            if (dist < 1e-3f || dist > rules.RiderWatchRange || speed < RiderWatchMinSpeed) return false;
            if (math.dot(-d / dist, me.Facing) < -0.2f) return false;
            float2 heading = r.Velocity / speed;
            return math.dot(d, heading) > 0f && math.abs(heading.x * d.y - heading.y * d.x) < RiderPathWidth;
        }

        /// <summary>
        /// Once its reaction time is up the soldier answers the rider once, with its own skill: a spear braces and a
        /// shield wall stands (the step share goes to the block, as against a sword); a shooter has nothing to block
        /// with and can only step aside. A step aside comes at the last moment, away from the horse's line.
        /// </summary>
        static void AnswerRider(int i, ref CombatData c, ref CombatSystem.CombatJob job)
        {
            var me = job.Agent(i);
            var r = job.Agent(c.BotRider);
            var rules = job.Rules;
            int tick = job.CurrentTick;
            me.FaceAgent = c.BotRider;
            job.SetAgent(i, me);
            if (tick < c.BotReactAtTick || !CombatSystem.CanAct(c)) return;
            if (c.BotRiderPlan == BotRiderAnswer.Undecided)
            {
                var p = job.Profile(c.BotProfile);
                var weapon = job.Weapon(c);
                float block = p.BlockChance + p.PerfectBlockChance, step = p.StepChance;
                if (weapon.IsRanged || weapon.Archetype == WeaponArchetype.Lance)
                {
                    step += block;
                    block = 0f;
                }
                else if (weapon.Archetype == WeaponArchetype.Polearm || job.FormationContext(i).ShieldWall == 1)
                {
                    block += step;
                    step = 0f;
                }
                var rng = new SimRandom { State = c.RngState };
                float roll = rng.NextFloat();
                c.RngState = rng.State;
                c.BotRiderPlan = roll < block ? BotRiderAnswer.Block : roll < block + step ? BotRiderAnswer.Step : BotRiderAnswer.None;
                // The unattended hero holds his block (CLAUDE.md 3.9, HeroUnattended)
                if (me.IsUnattended && block > 0f) c.BotRiderPlan = BotRiderAnswer.Block;
            }
            if (c.BotRiderPlan == BotRiderAnswer.Block)
            {
                if (c.BlockHeld == 0) CombatSystem.SetBlock(ref c, true, tick, job.Attack(c), rules, job.EventList, i);
                return;
            }
            if (c.BotRiderPlan != BotRiderAnswer.Step) return;
            float2 d = me.Position - r.Position;
            float speed = math.max(math.length(r.Velocity), 0.1f);
            float2 heading = r.Velocity / speed;
            float arrival = (math.dot(d, heading) - rules.TrampleReach - me.Radius - r.Radius) / speed;
            if (arrival > rules.StepTicks * SimConstants.DT) return;
            float side = heading.x * d.y - heading.y * d.x;
            float2 away = side >= 0f ? new float2(-heading.y, heading.x) : new float2(heading.y, -heading.x);
            float2 right = new float2(me.Facing.y, -me.Facing.x);
            if (CombatSystem.TryStep(ref c, math.dot(away, right) >= 0f ? StepDirection.Right : StepDirection.Left, tick, rules, job.EventList, i))
                c.BotRiderPlan = BotRiderAnswer.None;
        }

        /// <summary>A shooter fighting with its side arm takes up the bow again.</summary>
        static void DrawBow(int i, ref CombatData c, ref CombatSystem.CombatJob job)
        {
            int bow = job.BowSlot(c);
            if (bow >= 0 && c.State == CombatState.Idle) CombatSystem.TrySwitch(ref c, bow, job.CurrentTick, job.Rules, job.EventList, i);
        }

        /// <summary>Nearest living enemy in range and in the front arc. A plain loop: a bow reaches over too many cells for the grid.</summary>
        static int FindShot(in AgentData me, float range, float rangePerMeter, in FormationCombatContext ctx, ref CombatSystem.CombatJob job)
        {
            var formationRules = job.FormationRules;
            int best = -1;
            float bestSq = float.MaxValue;
            float ground = job.GroundAt(me.Position);
            for (int j = 0; j < job.AgentCount; j++)
            {
                var other = job.Agent(j);
                if (other.IsDead || other.Team == me.Team) continue;
                float dsq = math.distancesq(other.Position, me.Position);
                // An arrow carries further downhill and falls short uphill
                float reach = math.max(0f, range + rangePerMeter * (ground - job.GroundAt(other.Position)));
                if (dsq >= bestSq || dsq >= reach * reach || !FormationCombatRules.Accepts(ctx, formationRules, me.Position, other.Position, reach)) continue;
                bestSq = dsq;
                best = j;
            }
            return best;
        }

        /// <summary>Nearest living enemy within <paramref name="range"/> that the formation context lets the bot fight.</summary>
        static int FindTarget(int i, in AgentData me, float range, in FormationCombatContext ctx, float reach, ref CombatSystem.CombatJob job)
        {
            var formationRules = job.FormationRules;
            var layout = job.Grid;
            int r = (int)math.ceil(range / SimConstants.CellSize);
            int2 cell = math.clamp(layout.WorldToCell(me.Position), int2.zero, new int2(layout.Width - 1, layout.Height - 1));
            int best = -1;
            float bestSq = range * range;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int2 cc = cell + new int2(dx, dy);
                if (!layout.InBounds(cc)) continue;
                int index = layout.CellIndex(cc);
                for (int e = job.CellBegin(index); e < job.CellEnd(index); e++)
                {
                    int j = job.EntryAt(e);
                    if (j == i || j >= job.AgentCount) continue;
                    var other = job.Agent(j);
                    if (other.IsDead || other.Team == me.Team) continue;
                    float dsq = math.distancesq(other.Position, me.Position);
                    if (dsq > bestSq || (dsq == bestSq && j > best)) continue;
                    if (!FormationCombatRules.Accepts(ctx, formationRules, me.Position, other.Position, reach)) continue;
                    if (dsq < bestSq || (dsq == bestSq && j < best))
                    {
                        bestSq = dsq;
                        best = j;
                    }
                }
            }
            return best;
        }
    }
}
