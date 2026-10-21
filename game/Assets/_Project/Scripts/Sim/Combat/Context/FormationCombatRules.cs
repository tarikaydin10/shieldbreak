using Unity.Mathematics;
using Shieldbreak.Sim.Formations;

namespace Shieldbreak.Sim.Combat.Context
{
    /// <summary>What a soldier's formation asks of it in a fight, from the formation's behavior and type.</summary>
    public struct FormationCombatContext
    {
        /// <summary>1 = keeps its slot (hold, advance, follow): leash and front arc apply. 0 = fights freely (no formation, charge).</summary>
        public int Bound;
        /// <summary>1 = does not pick fights at all (retreat).</summary>
        public int Disengaged;
        /// <summary>1 = shield wall: answers a seen attack with the shield, never with a dodge step out of the wall.</summary>
        public int ShieldWall;
        /// <summary>World position of the soldier's slot.</summary>
        public float2 Slot;
        /// <summary>Direction the slot faces (the formation's front; outwards for a circle).</summary>
        public float2 Front;
    }

    /// <summary>
    /// Combat in formation (CLAUDE.md 3.5, Schicht D): context rules on top of the grammar, read by the bot brain.
    /// Soldiers of a holding, advancing or following formation keep their slot and step out of it at most EngageLeash
    /// to fight enemies in front of the formation, or anyone who comes closer than SelfDefenseRange; a shield wall
    /// blocks instead of dodging; a charge releases the soldiers; a retreat does not fight. Nothing here changes a rule
    /// of the grammar or a stat: bots react with the same reaction time and error rate as they would alone.
    /// </summary>
    public static class FormationCombatRules
    {
        public static FormationCombatContext For(in FormationMember member, in FormationData formation)
        {
            var ctx = default(FormationCombatContext);
            if (member.Formation < 0 || member.Slot < 0 || formation.Behavior == FormationBehavior.Charge) return ctx;
            if (formation.Behavior == FormationBehavior.Retreat)
            {
                ctx.Disengaged = 1;
                return ctx;
            }
            ctx.Bound = 1;
            ctx.ShieldWall = formation.Type == FormationType.ShieldWall ? 1 : 0;
            ctx.Slot = formation.ToWorld(member.Local);
            ctx.Front = FormationData.Rotate(member.LocalFacing, formation.Facing);
            return ctx;
        }

        /// <summary>
        /// An unattended hero (command view, CLAUDE.md 3.9) is bound like a soldier holding a slot: its post behind
        /// the own line, facing the line's front. It fights only what comes within its leash or too close.
        /// </summary>
        public static FormationCombatContext ForUnattendedHero(float2 post, float2 front, float2 fallbackFront) =>
            new FormationCombatContext { Bound = 1, Slot = post, Front = math.normalizesafe(front, fallbackFront) };

        /// <summary>
        /// May the soldier at <paramref name="self"/> pick (or keep) the enemy at <paramref name="enemy"/>?
        /// <paramref name="reach"/> is how far it fights from where it stands (its engage range, plus a margin to keep a target).
        /// </summary>
        public static bool Accepts(in FormationCombatContext ctx, in FormationSettings settings, float2 self, float2 enemy, float reach)
        {
            if (ctx.Disengaged == 1) return false;
            if (ctx.Bound == 0) return true;
            if (math.distancesq(enemy, self) <= settings.SelfDefenseRange * settings.SelfDefenseRange) return true;
            float2 fromSlot = enemy - ctx.Slot;
            float limit = settings.EngageLeash + reach;
            if (math.lengthsq(fromSlot) > limit * limit) return false;
            return math.dot(math.normalizesafe(fromSlot, ctx.Front), ctx.Front) >= settings.FrontArcCos;
        }

        /// <summary>Where a bound soldier may stand to fight: <paramref name="wanted"/>, pulled back to at most EngageLeash from its slot.</summary>
        public static float2 Leashed(in FormationCombatContext ctx, in FormationSettings settings, float2 wanted)
        {
            if (ctx.Bound == 0) return wanted;
            float2 offset = wanted - ctx.Slot;
            float distance = math.length(offset);
            return distance <= settings.EngageLeash ? wanted : ctx.Slot + offset * (settings.EngageLeash / distance);
        }

        /// <summary>Sprinting breaks the line: only free fighters sprint.</summary>
        public static bool AllowsSprint(in FormationCombatContext ctx) => ctx.Bound == 0;

        /// <summary>
        /// Shield wall: a seen attack is blocked where the bot would otherwise dodge with a step out of the wall
        /// (the step share of its defence goes to the block). The share it fails to answer stays its own skill.
        /// A permanently raised shield was tried and rejected: the shield bash breaks any raised block (DECISIONS.md).
        /// </summary>
        public static void PreferBlock(in FormationCombatContext ctx, ref float blockChance, ref float stepChance)
        {
            if (ctx.ShieldWall == 0) return;
            blockChance += stepChance;
            stepChance = 0f;
        }
    }
}
