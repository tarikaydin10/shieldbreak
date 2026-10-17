using Unity.Mathematics;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;

namespace Shieldbreak.Sim.AI
{
    /// <summary>
    /// Melee bot on the combat grammar (CLAUDE.md 3.5): it sees what a player would see (the enemy's windup), reacts
    /// after a seeded delay, may fail, and uses only the same Try* rules a player uses. Levels differ in reaction
    /// time, error rates and allowed techniques, never in stats. Runs inside the CombatJob, so it is deterministic.
    /// </summary>
    internal static class BotBrain
    {
        const int ReacquireInterval = 30;

        public static void Think(int i, ref CombatData c, ref CombatSystem.CombatJob job)
        {
            var p = job.Profile(c.BotProfile);
            var rng = new SimRandom { State = c.RngState };
            int tick = job.CurrentTick;
            var rules = job.Rules;
            var me = job.Agent(i);

            // Target acquisition: periodic (staggered by id) or when the current target is gone
            bool reacquire = c.BotTarget < 0 || c.BotTarget >= job.AgentCount || job.Agent(c.BotTarget).IsDead || (tick + i) % ReacquireInterval == 0;
            if (reacquire) c.BotTarget = FindTarget(i, me, p.AcquireRange, ref job);

            if (c.BotTarget < 0)
            {
                me.FaceAgent = -1;
                me.Target = c.OrderTarget;
                job.SetAgent(i, me);
                if (c.BlockHeld == 1 && p.HoldBlock == 0) CombatSystem.SetBlock(ref c, false, tick, job.Attack(c), rules, job.EventList, i);
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
            float keep = tokenFree ? p.EngageRange : p.EngageRange + rules.WaitingDistance;
            me.FaceAgent = c.BotTarget;
            me.Target = enemy.Position - dir * keep;
            job.SetAgent(i, me);
            c.Sprinting = c.State == CombatState.Idle && c.BlockHeld == 0 && dist > p.EngageRange + 3f ? 1 : 0;

            // Threat: the enemy swings at us (faces us, close enough)
            bool threat = (ec.State == CombatState.Windup || ec.State == CombatState.Release) &&
                          math.dot(enemy.Facing, -dir) > 0.5f && dist < p.EngageRange + 1.2f;
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
                    var enemyAttack = job.Attack(ec);
                    int contactTick = start + enemyAttack.Timing.WindupTicks + enemyAttack.Timing.ReleaseTicks / 2;
                    float roll = rng.NextFloat();
                    if (roll < p.PerfectBlockChance) c.BotPlannedBlockTick = math.max(tick, contactTick - rules.PerfectBlockTicks / 2);
                    else if (roll < p.PerfectBlockChance + p.BlockChance) c.BotPlannedBlockTick = tick;
                    else
                    {
                        c.BotPlannedBlockTick = -1;
                        if (roll < p.PerfectBlockChance + p.BlockChance + p.StepChance)
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
                if (p.HoldBlock == 1)
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
                if (c.State == CombatState.Idle && !threat && tokenFree && dist <= p.EngageRange + 0.35f)
                {
                    // A raised enemy block invites the shield bash; otherwise attack, more often into an opening
                    if (ec.BlockHeld == 1 && dist <= rules.SpecialRange + 0.4f && rng.NextFloat() < p.SpecialChance)
                        CombatSystem.TrySpecial(ref c, tick, rules, job.EventList, i);
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

        static int FindTarget(int i, in AgentData me, float range, ref CombatSystem.CombatJob job)
        {
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
