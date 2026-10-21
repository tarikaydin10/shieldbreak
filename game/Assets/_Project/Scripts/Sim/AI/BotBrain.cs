using Unity.Mathematics;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat;
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
    /// </summary>
    internal static class BotBrain
    {
        const int ReacquireInterval = 30;
        /// <summary>A target is kept a little further than it would be picked, so it does not flicker at the edge (m).</summary>
        const float KeepMargin = 0.5f;

        public static void Think(int i, ref CombatData c, ref CombatSystem.CombatJob job)
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
                // Without an enemy near, an unattended hero walks back with the shield down (blocking slows)
                if (c.BlockHeld == 1 && (p.HoldBlock == 0 || unattended)) CombatSystem.SetBlock(ref c, false, tick, job.Attack(c), rules, job.EventList, i);
                c.RngState = rng.State;
                return;
            }

            var enemy = job.Agent(c.BotTarget);
            var ec = job.CombatOf(c.BotTarget);
            float2 toEnemy = enemy.Position - me.Position;
            float dist = math.length(toEnemy);
            float2 dir = dist > 1e-4f ? toEnemy / dist : me.Facing;

            // Attack token (1vX): only a limited number of bots swing at the same target; the others hold back and threaten
            bool swinging = c.State == CombatState.Windup || c.State == CombatState.Release;
            bool tokenFree = rules.MaxAttackersPerTarget <= 0 || swinging || job.AttackersSwingingAt(c.BotTarget) < rules.MaxAttackersPerTarget;

            // Keep engage distance (further back while waiting for a token), face the enemy, sprint to close in
            // In formation the bot steps out of its slot at most the leash and does not sprint
            float keep = tokenFree ? p.EngageRange : p.EngageRange + rules.WaitingDistance;
            me.FaceAgent = c.BotTarget;
            me.Target = FormationCombatRules.Leashed(ctx, formationRules, enemy.Position - dir * keep);
            job.SetAgent(i, me);
            c.Sprinting = FormationCombatRules.AllowsSprint(ctx) && c.State == CombatState.Idle && c.BlockHeld == 0 && dist > p.EngageRange + 3f ? 1 : 0;

            // Threat: the enemy swings at us (faces us, close enough; a lunging attack reaches further)
            var enemyAttack = job.Attack(ec);
            bool threat = (ec.State == CombatState.Windup || ec.State == CombatState.Release) &&
                          math.dot(enemy.Facing, -dir) > 0.5f && dist < p.EngageRange + 1.2f + enemyAttack.LungeDistance;
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
                else if (c.BlockHeld == 1 && tick - c.BotThreatStartTick > p.ReactionMaxTicks + 20)
                {
                    CombatSystem.SetBlock(ref c, false, tick, job.Attack(c), rules, job.EventList, i);
                }
            }

            // Attack when in range and not under threat; openings (enemy recovering or stunned) are punished more often
            if (tick >= c.BotNextDecisionTick)
            {
                c.BotNextDecisionTick = tick + p.DecisionIntervalTicks;
                var weapon = job.Weapon(c);
                // Two-hand leap: from just outside the engage range the lunge closes the gap and adds to the blow
                bool leapBand = weapon.HasSpecialAttack && dist > p.EngageRange + 0.35f && dist <= p.EngageRange + 1.3f;
                if (c.State == CombatState.Idle && !threat && tokenFree && leapBand && rng.NextFloat() < p.SpecialChance)
                {
                    if (CombatSystem.TrySpecial(ref c, tick, weapon, rules, job.EventList, i)) job.ClaimAttackToken(c.BotTarget);
                }
                else if (c.State == CombatState.Idle && !threat && tokenFree && dist <= p.EngageRange + 0.35f)
                {
                    // A raised enemy block invites the special (shield bash breaks it, the leap presses it); otherwise
                    // attack, more often into an opening
                    if (ec.BlockHeld == 1 && dist <= rules.SpecialRange + 0.4f && rng.NextFloat() < p.SpecialChance)
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
