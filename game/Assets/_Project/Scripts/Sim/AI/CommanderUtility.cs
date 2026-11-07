using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Morale;
using Shieldbreak.Sim.Orders;

namespace Shieldbreak.Sim.AI
{
    /// <summary>What a commander's whole side does. Left and right as seen from the own side, looking at the enemy.</summary>
    public enum CommanderTactic
    {
        Hold = 0,
        Frontal = 1,
        FlankLeft = 2,
        FlankRight = 3,
        Retreat = 4,
    }

    /// <summary>
    /// Commander tuning (CLAUDE.md 3.7); filled from the CommanderDefinition ScriptableObject. Difficulty is how often
    /// it thinks, how much noise is in its judgement and which tactics it may use: never stats or resources (pillar 4).
    /// </summary>
    public struct CommanderSettings
    {
        public int DecisionIntervalTicks;
        /// <summary>A tactic stays at least this long, unless the commander retreats.</summary>
        public int MinTacticTicks;
        /// <summary>Bonus for the current tactic: a close rival does not change the plan.</summary>
        public float Hysteresis;
        /// <summary>Random addition per score and decision, in [0, ScoreNoise): a misjudging commander.</summary>
        public float ScoreNoise;
        /// <summary>1 = may flank.</summary>
        public int AllowFlank;

        public float HoldBase;
        /// <summary>Taken off per unit of strength advantage: the weaker side prefers to stand.</summary>
        public float HoldPerAdvantage;
        /// <summary>Taken off per tick spent holding: nobody stands opposite the enemy for ever.</summary>
        public float HoldImpatiencePerTick;
        public float FrontalBase;
        public float FrontalPerAdvantage;
        public float FrontalPerMorale;
        public float FlankBase;
        public float FlankPerAdvantage;
        /// <summary>Per unit of own front wider than the enemy's: a wide side has soldiers to spare.</summary>
        public float FlankPerWidth;
        /// <summary>Per unit of how empty that side of the enemy is.</summary>
        public float FlankPerOpenSide;
        /// <summary>Closer than this (m) to the enemy there is no room left to go round him.</summary>
        public float FlankMinDistance;
        /// <summary>The flanking formation stops this far (m) beyond the end of the enemy's line.</summary>
        public float FlankMargin;
        /// <summary>The flanking formation is sent on anew when its goal moved further than this (m).</summary>
        public float FlankRetargetDistance;
        /// <summary>Below this strength ratio the commander thinks of retreat.</summary>
        public float RetreatRatio;
        public float RetreatPerWeakness;
        /// <summary>Below this average morale the commander thinks of retreat.</summary>
        public float RetreatMorale;
        public float RetreatPerPanic;

        public void Validate()
        {
            if (DecisionIntervalTicks < 1 || MinTacticTicks < 0) throw new ArgumentException("Require DecisionIntervalTicks >= 1 and MinTacticTicks >= 0.");
            if (Hysteresis < 0f || ScoreNoise < 0f) throw new ArgumentException("Require Hysteresis >= 0 and ScoreNoise >= 0.");
            if (FlankMinDistance < 0f || FlankMargin < 0f || FlankRetargetDistance < 0f) throw new ArgumentException("Flank distances must be >= 0.");
            if (RetreatRatio < 0f || RetreatMorale < 0f) throw new ArgumentException("Require RetreatRatio >= 0 and RetreatMorale >= 0.");
            if (HoldImpatiencePerTick < 0f) throw new ArgumentException("HoldImpatiencePerTick must be >= 0.");
        }
    }

    /// <summary>
    /// What a commander sees: only what a player could read off the field. 76 bytes, no padding (hashed raw with the state).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CommanderFeatures
    {
        public int OwnFighters;
        public int EnemyFighters;
        public int OwnFormations;
        /// <summary>Own fighters per enemy fighter.</summary>
        public float Ratio;
        public float OwnMorale;
        public float EnemyMorale;
        /// <summary>Sum of the front widths per side (m).</summary>
        public float OwnFront;
        public float EnemyFront;
        /// <summary>Between the nearest formation anchors of both sides (m).</summary>
        public float Distance;
        /// <summary>Share of the enemy not on that side of his centre: 0.5 = even, near 1 = that side is empty.</summary>
        public float OpenLeft;
        public float OpenRight;
        /// <summary>How far the enemy reaches to that side of his centre (m).</summary>
        public float SpreadLeft;
        public float SpreadRight;
        public float2 OwnCentre;
        public float2 EnemyCentre;
        /// <summary>Unit vector from the own centre to the enemy's; its right is (y, -x).</summary>
        public float2 Axis;
    }

    /// <summary>One score per tactic. 20 bytes, no padding.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CommanderScores
    {
        public float Hold;
        public float Frontal;
        public float FlankLeft;
        public float FlankRight;
        public float Retreat;

        public float this[CommanderTactic tactic]
        {
            get
            {
                switch (tactic)
                {
                    case CommanderTactic.Hold: return Hold;
                    case CommanderTactic.Frontal: return Frontal;
                    case CommanderTactic.FlankLeft: return FlankLeft;
                    case CommanderTactic.FlankRight: return FlankRight;
                    default: return Retreat;
                }
            }
            set
            {
                switch (tactic)
                {
                    case CommanderTactic.Hold: Hold = value; break;
                    case CommanderTactic.Frontal: Frontal = value; break;
                    case CommanderTactic.FlankLeft: FlankLeft = value; break;
                    case CommanderTactic.FlankRight: FlankRight = value; break;
                    default: Retreat = value; break;
                }
            }
        }
    }

    /// <summary>One commander. Sim state, hashed raw: 136 bytes, no padding.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CommanderState
    {
        public ulong Rng;
        /// <summary>Player id it gives orders as: it commands the formations this id owns.</summary>
        public int Owner;
        public int Team;
        public CommanderTactic Tactic;
        /// <summary>Tactic the orders were last given for, -1 = none yet.</summary>
        public int AppliedTactic;
        public int NextDecisionTick;
        /// <summary>Tick the current tactic was chosen.</summary>
        public int TacticTick;
        /// <summary>Formation sent round the enemy, -1 = none.</summary>
        public int Flanker;
        /// <summary>1 = decided this tick: the orders follow (set by the job, read by CommanderSystem).</summary>
        public int Execute;
        public CommanderFeatures Features;
        /// <summary>Scores of the last decision, with noise and hysteresis, as they were compared.</summary>
        public CommanderScores Scores;
    }

    /// <summary>
    /// Pure parts of the commander (CLAUDE.md 3.7): what it reads off the field and how it scores its tactics.
    /// Static and without state, so the Burst job and the tests call the same code.
    /// </summary>
    public static class CommanderUtility
    {
        public const int TacticCount = 5;

        /// <summary>
        /// Reads the field from one team's side. Everything here is visible to a player too: positions, formations
        /// and morale bars. With nobody left on one side the counts say so and the rest stays zero.
        /// </summary>
        public static CommanderFeatures Read(int team, int owner, NativeArray<AgentData> agents, NativeArray<MoraleData> morale, int count,
            NativeArray<FormationData> formations, int formationCount, in FormationSettings formationSettings)
        {
            var f = default(CommanderFeatures);
            float ownMorale = 0f, enemyMorale = 0f;
            for (int i = 0; i < count; i++)
            {
                var a = agents[i];
                if (a.IsDead || a.IsRouting) continue;
                if (a.Team == team)
                {
                    f.OwnFighters++;
                    f.OwnCentre += a.Position;
                    ownMorale += morale[i].Morale;
                }
                else
                {
                    f.EnemyFighters++;
                    f.EnemyCentre += a.Position;
                    enemyMorale += morale[i].Morale;
                }
            }
            if (f.OwnFighters == 0 || f.EnemyFighters == 0) return f;
            f.OwnCentre /= f.OwnFighters;
            f.EnemyCentre /= f.EnemyFighters;
            f.OwnMorale = ownMorale / f.OwnFighters;
            f.EnemyMorale = enemyMorale / f.EnemyFighters;
            f.Ratio = (float)f.OwnFighters / f.EnemyFighters;
            f.Axis = math.normalizesafe(f.EnemyCentre - f.OwnCentre, new float2(0f, 1f));

            float2 right = new float2(f.Axis.y, -f.Axis.x);
            int onLeft = 0, onRight = 0;
            for (int i = 0; i < count; i++)
            {
                var a = agents[i];
                if (a.Team == team || a.IsDead || a.IsRouting) continue;
                float lateral = math.dot(a.Position - f.EnemyCentre, right);
                if (lateral < 0f)
                {
                    onLeft++;
                    f.SpreadLeft = math.max(f.SpreadLeft, -lateral);
                }
                else
                {
                    onRight++;
                    f.SpreadRight = math.max(f.SpreadRight, lateral);
                }
            }
            f.OpenLeft = 1f - (float)onLeft / f.EnemyFighters;
            f.OpenRight = 1f - (float)onRight / f.EnemyFighters;

            float nearestSq = math.distancesq(f.OwnCentre, f.EnemyCentre);
            for (int a = 0; a < formationCount; a++)
            {
                var own = formations[a];
                if (own.MemberCount == 0) continue;
                float width = FormationLinePlanner.FrontWidth(own.Type, own.Files, own.MemberCount, formationSettings);
                if (own.Team != team)
                {
                    f.EnemyFront += width;
                    continue;
                }
                f.OwnFront += width;
                if (own.Owner == owner) f.OwnFormations++;
                for (int b = 0; b < formationCount; b++)
                {
                    var other = formations[b];
                    if (other.Team != team && other.MemberCount > 0) nearestSq = math.min(nearestSq, math.distancesq(own.Position, other.Position));
                }
            }
            f.Distance = math.sqrt(nearestSq);
            return f;
        }

        /// <summary>
        /// The scores from what was read, before noise and hysteresis. A tactic that is not possible scores 0.
        /// <paramref name="ticksHolding"/>: how long the side has been holding, 0 under any other tactic.
        /// </summary>
        public static CommanderScores Score(in CommanderFeatures f, in CommanderSettings s, int ticksHolding = 0)
        {
            float advantage = math.clamp(f.Ratio - 1f, -1f, 1f);
            float moraleEdge = f.OwnMorale - f.EnemyMorale;
            var scores = default(CommanderScores);
            scores.Hold = s.HoldBase - s.HoldPerAdvantage * advantage - s.HoldImpatiencePerTick * ticksHolding;
            scores.Frontal = s.FrontalBase + s.FrontalPerAdvantage * advantage + s.FrontalPerMorale * moraleEdge;
            if (s.AllowFlank == 1 && f.OwnFormations >= 2 && f.Distance >= s.FlankMinDistance)
            {
                float widthEdge = f.EnemyFront > 0f ? math.clamp(f.OwnFront / f.EnemyFront - 1f, -1f, 1f) : 0f;
                float flank = s.FlankBase + s.FlankPerAdvantage * advantage + s.FlankPerWidth * widthEdge;
                scores.FlankLeft = flank + s.FlankPerOpenSide * (f.OpenLeft - 0.5f) * 2f;
                scores.FlankRight = flank + s.FlankPerOpenSide * (f.OpenRight - 0.5f) * 2f;
            }
            scores.Retreat = s.RetreatPerWeakness * math.max(0f, s.RetreatRatio - f.Ratio) + s.RetreatPerPanic * math.max(0f, s.RetreatMorale - f.OwnMorale);
            return scores;
        }

        /// <summary>
        /// One decision: adds noise and the bonus of the current tactic, picks the best (first in enum order on ties)
        /// and keeps the current one until its minimum time is over, unless the best is a retreat.
        /// </summary>
        public static CommanderTactic Decide(ref CommanderScores scores, CommanderTactic current, int ticksInTactic, in CommanderSettings s, ref SimRandom rng)
        {
            for (int t = 0; t < TacticCount; t++)
            {
                var tactic = (CommanderTactic)t;
                // Always draw, so the number of draws does not depend on the scores
                float noise = rng.NextFloat() * s.ScoreNoise;
                // A tactic that is not possible stays out of the race
                if (scores[tactic] > 0f) scores[tactic] += noise;
            }
            scores[current] += s.Hysteresis;
            var best = current;
            for (int t = 0; t < TacticCount; t++)
                if (scores[(CommanderTactic)t] > scores[best]) best = (CommanderTactic)t;
            if (best != CommanderTactic.Retreat && ticksInTactic < s.MinTacticTicks) return current;
            return best;
        }

        /// <summary>Where the formation sent round the enemy stands, and which way it then faces: at the end of his line, looking along it.</summary>
        public static float2 FlankPoint(in CommanderFeatures f, CommanderTactic side, in CommanderSettings s, out float2 facing)
        {
            float2 right = new float2(f.Axis.y, -f.Axis.x);
            bool toRight = side == CommanderTactic.FlankRight;
            float reach = (toRight ? f.SpreadRight : f.SpreadLeft) + s.FlankMargin;
            float2 outwards = toRight ? right : -right;
            facing = -outwards;
            return f.EnemyCentre + outwards * reach;
        }
    }
}
