using NUnit.Framework;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>Formation behaviors (hold, advance, charge, follow, retreat) as FormationOrders, without combat.</summary>
    public class FormationBehaviorTests
    {
        const ushort PlayerA = 0, PlayerB = 1;
        static readonly float2 East = new float2(1f, 0f), West = new float2(-1f, 0f);

        static SimWorld World(int maxAgents, int size = 96)
        {
            var config = TestScenarios.Config(1, size, maxAgents);
            config.MaxFormations = 8;
            config.Formations = TestScenarios.Formations;
            return new SimWorld(config);
        }

        static void Run(SimWorld world, int ticks)
        {
            for (int t = 0; t < ticks; t++) world.Step();
        }

        static int Line(SimWorld world, int team, float2 front, float2 facing, int count = 20, int files = 10) =>
            world.SpawnFormation(team, team, FormationType.Line, files, count, front, facing, TestScenarios.AgentRadius);

        static bool Standing(SimWorld world, int f)
        {
            var d = world.Formations[f];
            return d.PathIndex >= d.PathCount && math.lengthsq(d.Velocity) < 1e-6f;
        }

        [Test]
        public void Advance_MarchesToTheNearestEnemy_AndStopsInContact()
        {
            using var world = World(60);
            int a = Line(world, 0, new float2(20f, 48f), East);
            Line(world, 1, new float2(80f, 30f), West);   // further away
            int b = Line(world, 1, new float2(60f, 48f), West);
            world.SubmitFormationOrder(PlayerB, FormationOrder.SetStance(b, FormationStance.HoldGround));   // b waits, it does not come to meet a
            world.SubmitFormationOrder(PlayerA, FormationOrder.Advance(a));
            Run(world, 3000);

            var f = world.Formations[a];
            var s = TestScenarios.Formations;
            Assert.That(f.Behavior, Is.EqualTo(FormationBehavior.Advance));
            Assert.That(Standing(world, a), "stopped in contact");
            // The front stops a contact gap short of the enemy's front rank, facing it
            float gap = world.Formations[b].Position.x - f.Position.x;
            Assert.That(gap, Is.EqualTo(s.AdvanceContactGap).Within(s.RetargetDistance + 0.1f));
            Assert.That(math.abs(f.Position.y - 48f), Is.LessThan(1f));
            Assert.That(math.dot(f.Facing, East), Is.GreaterThan(0.99f));
        }

        [Test]
        public void Advance_AgainstAChosenFormation_SkipsTheNearerOne_ThenFallsBackToTheNearest()
        {
            using var world = World(80);
            int a = Line(world, 0, new float2(20f, 48f), East);
            int far = Line(world, 1, new float2(20f, 90f), new float2(0f, -1f));   // 42 m north
            Line(world, 1, new float2(60f, 48f), West);                             // 40 m east, nearer
            int own = Line(world, 0, new float2(80f, 10f), East);
            world.SubmitFormationOrder(PlayerA, FormationOrder.Against(a, FormationBehavior.Advance, far));
            Run(world, world.Config.InputDelayTicks + 1);
            Assert.That(world.Formations[a].TargetFormation, Is.EqualTo(far));
            Run(world, 600);
            float2 heading = math.normalize(world.Formations[a].Destination - new float2(20f, 48f));
            Assert.That(heading.y, Is.GreaterThan(0.9f), "marches north on the chosen formation, not east on the nearer one");

            // Charging without a target of its own keeps the chosen one
            world.SubmitFormationOrder(PlayerA, FormationOrder.Charge(a));
            Run(world, world.Config.InputDelayTicks + 1);
            Assert.That(world.Formations[a].TargetFormation, Is.EqualTo(far), "advance on X, then charge: charge X");

            // An own formation is no target: the order falls back to the nearest enemy
            world.SubmitFormationOrder(PlayerA, FormationOrder.Against(a, FormationBehavior.Charge, own));
            Run(world, world.Config.InputDelayTicks + 1);
            Assert.That(world.Formations[a].TargetFormation, Is.EqualTo(-1));
            Assert.That(world.Formations[a].Behavior, Is.EqualTo(FormationBehavior.Charge));
            // A plain move clears the target
            world.SubmitFormationOrder(PlayerA, FormationOrder.Against(a, FormationBehavior.Advance, far));
            world.SubmitFormationOrder(PlayerA, FormationOrder.MoveTo(a, new float2(20f, 40f)));
            Run(world, world.Config.InputDelayTicks + 1);
            Assert.That(world.Formations[a].TargetFormation, Is.EqualTo(-1));
            Assert.That(world.Formations[a].Behavior, Is.EqualTo(FormationBehavior.Hold));
        }

        [Test]
        public void Advance_FollowsTheEnemy_WhenItMoves()
        {
            using var world = World(40);
            int a = Line(world, 0, new float2(20f, 30f), East);
            int b = Line(world, 1, new float2(50f, 30f), West);
            world.SubmitFormationOrder(PlayerA, FormationOrder.Advance(a));
            Run(world, 600);
            world.SubmitFormationOrder(PlayerB, FormationOrder.MoveTo(b, new float2(50f, 70f), West));
            Run(world, 4200);

            // B marched north across A's front: A follows and ends in contact with B's flank, facing B's centre
            var f = world.Formations[a];
            Assert.That(Standing(world, a));
            float2 centre = float2.zero;
            float nearest = float.MaxValue;
            int n = 0;
            for (int i = 0; i < world.AgentCount; i++)
            {
                if (world.FormationMembers[i].Formation != b) continue;
                centre += world.Agents[i].Position;
                nearest = math.min(nearest, math.distance(world.Agents[i].Position, f.Position));
                n++;
            }
            centre /= n;
            Assert.That(nearest, Is.EqualTo(TestScenarios.Formations.AdvanceContactGap).Within(TestScenarios.Formations.RetargetDistance + 0.5f));
            Assert.That(math.dot(f.Facing, math.normalize(centre - f.Position)), Is.GreaterThan(0.95f), "turned to face the enemy");
        }

        [Test]
        public void Charge_GoesRightIntoTheEnemy()
        {
            using var world = World(40);
            int a = Line(world, 0, new float2(20f, 48f), East);
            int b = Line(world, 1, new float2(50f, 48f), West);
            world.SubmitFormationOrder(PlayerA, FormationOrder.Charge(a));
            Run(world, 2400);

            var f = world.Formations[a];
            Assert.That(f.Behavior, Is.EqualTo(FormationBehavior.Charge));
            Assert.That(world.Formations[b].Position.x - f.Position.x, Is.LessThan(TestScenarios.Formations.RetargetDistance + 0.1f), "no contact gap");
        }

        [Test]
        public void Advance_WithoutEnemies_Holds()
        {
            using var world = World(20);
            int a = Line(world, 0, new float2(20f, 48f), East);
            world.SubmitFormationOrder(PlayerA, FormationOrder.Advance(a));
            Run(world, 30);
            Assert.That(world.Formations[a].Behavior, Is.EqualTo(FormationBehavior.Hold));
            Assert.That(Standing(world, a));
        }

        [Test]
        public void Follow_TrailsTheLeader_AndStopsWhenItComesBack()
        {
            using var world = World(21);
            int a = Line(world, 0, new float2(20f, 48f), East);
            int leader = world.SpawnAgent(0, new float2(24f, 48f), TestScenarios.AgentRadius, East);
            world.SubmitFormationOrder(PlayerA, FormationOrder.Follow(a, leader));
            world.Submit(PlayerA, InputCommandType.SetTargetRange, new int4(leader, 1, 0, 0), new float4(60f, 60f, 0f, 0f));
            Run(world, 3000);

            var s = TestScenarios.Formations;
            var f = world.Formations[a];
            Assert.That(f.Behavior, Is.EqualTo(FormationBehavior.Follow));
            float2 leaderPos = world.Agents[leader].Position;
            Assert.That(math.distance(f.Position, leaderPos), Is.EqualTo(s.FollowDistance).Within(s.RetargetDistance + 0.5f), "trails at the follow distance");
            Assert.That(math.dot(f.Facing, math.normalize(leaderPos - f.Position)), Is.GreaterThan(0.95f), "faces the leader");

            // The leader walks back into the formation: it stops instead of marching through
            world.Submit(PlayerA, InputCommandType.SetTargetRange, new int4(leader, 1, 0, 0), new float4(f.Position + f.Facing * 2f, 0f, 0f));
            float2 before = f.Position;
            Run(world, 600);
            Assert.That(math.distance(world.Formations[a].Position, before), Is.LessThan(s.RetargetDistance + 1f));
        }

        [Test]
        public void Follow_AnEnemyOrNobody_IsIgnoredOrHolds()
        {
            using var world = World(21);
            int a = Line(world, 0, new float2(20f, 48f), East);
            int enemy = world.SpawnAgent(1, new float2(40f, 48f), TestScenarios.AgentRadius, West);
            Assert.That(world.ApplyFormationOrder(FormationOrder.Follow(a, -1), PlayerA), Is.False);
            Assert.That(world.ApplyFormationOrder(FormationOrder.Follow(a, 1000), PlayerA), Is.False);
            Assert.That(world.ApplyFormationOrder(FormationOrder.SetBehavior(a, (FormationBehavior)99), PlayerA), Is.False);
            Assert.That(world.Formations[a].Behavior, Is.EqualTo(FormationBehavior.Hold));

            world.SubmitFormationOrder(PlayerA, FormationOrder.Follow(a, enemy));
            Run(world, 30);
            Assert.That(world.Formations[a].Behavior, Is.EqualTo(FormationBehavior.Hold), "never follows an enemy");
            Assert.That(world.Formations[a].Position, Is.EqualTo(new float2(20f, 48f)));
        }

        [Test]
        public void Retreat_FallsBackFromTheEnemy_ThenHoldsFacingIt()
        {
            using var world = World(40);
            int a = Line(world, 0, new float2(48f, 48f), East);
            Line(world, 1, new float2(60f, 48f), West);
            world.SubmitFormationOrder(PlayerA, FormationOrder.Retreat(a));
            Run(world, 60);
            Assert.That(math.dot(world.Formations[a].Facing, West), Is.GreaterThan(0.99f), "turns its back on a long retreat");
            Run(world, 2400);

            var f = world.Formations[a];
            var s = TestScenarios.Formations;
            Assert.That(f.Behavior, Is.EqualTo(FormationBehavior.Hold));
            Assert.That(Standing(world, a));
            Assert.That(f.Position.x, Is.EqualTo(48f - s.RetreatDistance).Within(1.5f));
            Assert.That(math.dot(f.Facing, East), Is.GreaterThan(0.99f), "faces the enemy again");
        }

        [Test]
        public void RetreatTo_GoesToTheGivenPoint()
        {
            using var world = World(40);
            int a = Line(world, 0, new float2(48f, 48f), East);
            int b = Line(world, 1, new float2(60f, 48f), West);
            world.SubmitFormationOrder(PlayerB, FormationOrder.SetStance(b, FormationStance.HoldGround));   // it does not give chase
            world.SubmitFormationOrder(PlayerA, FormationOrder.RetreatTo(a, new float2(48f, 20f), East));
            Run(world, 60);
            Assert.That(world.Formations[a].Behavior, Is.EqualTo(FormationBehavior.Retreat));
            Run(world, 2400);
            var f = world.Formations[a];
            Assert.That(f.Behavior, Is.EqualTo(FormationBehavior.Hold));
            Assert.That(math.distance(f.Position, new float2(48f, 20f)), Is.LessThan(0.01f));
        }

        [Test]
        public void HoldOrder_StopsAMarch_AndMoveOrder_EndsABehavior()
        {
            using var world = World(40);
            int a = Line(world, 0, new float2(20f, 48f), East);
            Line(world, 1, new float2(70f, 48f), West);
            world.SubmitFormationOrder(PlayerA, FormationOrder.Advance(a));
            Run(world, 600);
            Assert.That(world.Formations[a].Behavior, Is.EqualTo(FormationBehavior.Advance));
            world.SubmitFormationOrder(PlayerA, FormationOrder.Hold(a));
            Run(world, 7);   // input delay
            float2 stop = world.Formations[a].Position;
            Run(world, 300);
            Assert.That(world.Formations[a].Behavior, Is.EqualTo(FormationBehavior.Hold));
            Assert.That(math.distance(world.Formations[a].Position, stop), Is.LessThan(0.05f));

            world.SubmitFormationOrder(PlayerA, FormationOrder.Charge(a));
            Run(world, 60);
            world.SubmitFormationOrder(PlayerA, FormationOrder.MoveTo(a, new float2(20f, 20f)));
            Run(world, 60);
            Assert.That(world.Formations[a].Behavior, Is.EqualTo(FormationBehavior.Hold));
            Assert.That(world.Formations[a].Destination, Is.EqualTo(new float2(20f, 20f)));
        }

        [Test]
        public void SameBehaviorOrders_SameHash()
        {
            ulong Battle(bool retreat)
            {
                using var world = World(60);
                int a = Line(world, 0, new float2(20f, 48f), East);
                int b = Line(world, 1, new float2(70f, 48f), West, 30, 10);
                int leader = world.SpawnAgent(1, new float2(80f, 48f), TestScenarios.AgentRadius, West);
                for (int t = 0; t < 2400; t++)
                {
                    if (t == 5) world.SubmitFormationOrder(PlayerA, FormationOrder.Advance(a));
                    if (t == 10) world.SubmitFormationOrder(PlayerB, FormationOrder.Follow(b, leader));
                    if (t == 20) world.Submit(PlayerB, InputCommandType.SetTargetRange, new int4(leader, 1, 0, 0), new float4(80f, 80f, 0f, 0f));
                    if (t == 900) world.SubmitFormationOrder(PlayerA, retreat ? FormationOrder.Retreat(a) : FormationOrder.Charge(a));
                    world.Step();
                }
                return world.ComputeHash();
            }
            Assert.That(Battle(true), Is.EqualTo(Battle(true)));
            Assert.That(Battle(true), Is.Not.EqualTo(Battle(false)));
        }

        // ---- Stances (standing orders) ----

        static void SetStance(SimWorld world, ushort player, int formation, FormationStance stance) =>
            world.SubmitFormationOrder(player, FormationOrder.SetStance(formation, stance));

        [Test]
        public void Stance_React_GoesForAnEnemyThatComesNear_HoldGroundStays()
        {
            using var world = World(90);
            int react = Line(world, 0, new float2(20f, 60f), East);
            int stay = Line(world, 0, new float2(20f, 30f), East);
            int near = Line(world, 1, new float2(70f, 60f), West);
            int other = Line(world, 1, new float2(70f, 30f), West);
            Assert.That(world.Formations[react].Stance, Is.EqualTo(FormationStance.React), "the default stance");
            SetStance(world, PlayerA, stay, FormationStance.HoldGround);
            SetStance(world, PlayerB, near, FormationStance.HoldGround);
            SetStance(world, PlayerB, other, FormationStance.HoldGround);
            // Both enemies stop 7 m in front of the own lines
            world.SubmitFormationOrder(PlayerB, FormationOrder.MoveTo(near, new float2(27f, 60f), West));
            world.SubmitFormationOrder(PlayerB, FormationOrder.MoveTo(other, new float2(27f, 30f), West));
            Run(world, 3000);

            var r = world.Formations[react];
            Assert.That(r.Behavior, Is.EqualTo(FormationBehavior.Advance), "React advances on its own");
            Assert.That(r.TargetFormation, Is.EqualTo(near), "on the enemy that came near");
            Assert.That(r.Reacting, Is.EqualTo((int)FormationStance.React));
            Assert.That(r.Position.x, Is.GreaterThan(22f), "and closes in");
            var s = world.Formations[stay];
            Assert.That(s.Behavior, Is.EqualTo(FormationBehavior.Hold), "Hold Ground does not move");
            Assert.That(s.Position, Is.EqualTo(new float2(20f, 30f)));
        }

        [Test]
        public void Stance_React_WaitsUntilArrived_ThenAttacksTheEnemyFromBehind()
        {
            using var world = World(60);
            int a = Line(world, 0, new float2(52f, 72f), new float2(0f, -1f));
            int b = Line(world, 1, new float2(50f, 40f), West);
            SetStance(world, PlayerB, b, FormationStance.HoldGround);
            // From the north past the enemy's flank to 8 m behind it, facing its back; the march passes it closer than 10 m
            world.SubmitFormationOrder(PlayerA, FormationOrder.MoveTo(a, new float2(59f, 40f), West));
            bool reactedOnTheWay = false;
            int arrivedAt = -1;
            for (int t = 0; t < 4800 && arrivedAt < 0; t++)
            {
                world.Step();
                var f = world.Formations[a];
                bool marching = f.NeedsPath != 0 || f.PathIndex < f.PathCount || math.distancesq(f.Destination, f.Position) >= 0.25f;
                if (marching && f.Behavior != FormationBehavior.Hold) reactedOnTheWay = true;
                if (!marching && t > 60) arrivedAt = t;
            }
            Assert.That(arrivedAt, Is.GreaterThan(0), "the march arrives");
            Assert.That(reactedOnTheWay, Is.False, "a march is not hijacked by enemies it passes");
            Run(world, 600);
            var after = world.Formations[a];
            Assert.That(after.Behavior, Is.EqualTo(FormationBehavior.Advance));
            Assert.That(after.TargetFormation, Is.EqualTo(b));
            Assert.That(after.Position.x, Is.LessThan(58f), "it closes in on the enemy's back");
            Assert.That(math.dot(after.Facing, West), Is.GreaterThan(0.9f), "facing the enemy's back");
        }

        [Test]
        public void Stance_AnOrderEndsTheReaction_AndRadialHoldMeansHoldGround()
        {
            using var world = World(60);
            int a = Line(world, 0, new float2(20f, 48f), East);
            int b = Line(world, 1, new float2(70f, 48f), West);
            SetStance(world, PlayerB, b, FormationStance.HoldGround);
            world.SubmitFormationOrder(PlayerB, FormationOrder.MoveTo(b, new float2(28f, 48f), West));
            Run(world, 3000);
            Assert.That(world.Formations[a].Reacting, Is.EqualTo((int)FormationStance.React), "reacted to the enemy");

            Assert.That(QuickOrders.TryBuild(QuickCommand.Hold, a, -1, out var hold));
            world.SubmitFormationOrder(PlayerA, hold);
            Run(world, world.Config.InputDelayTicks + 1);
            float2 stoppedAt = world.Formations[a].Position;
            Run(world, 600);
            var f = world.Formations[a];
            Assert.That(f.Behavior, Is.EqualTo(FormationBehavior.Hold));
            Assert.That(f.Stance, Is.EqualTo(FormationStance.HoldGround), "Hold from the radial menu also holds the ground");
            Assert.That(f.Reacting, Is.EqualTo(0));
            Assert.That(math.distance(f.Position, stoppedAt), Is.LessThan(0.5f), "and it stays, though the enemy is near");

            // Stance orders from another player, or out of range, are ignored
            SetStance(world, PlayerB, a, FormationStance.Aggressive);
            world.SubmitFormationOrder(PlayerA, FormationOrder.SetStance(a, (FormationStance)7));
            Run(world, world.Config.InputDelayTicks + 1);
            Assert.That(world.Formations[a].Stance, Is.EqualTo(FormationStance.HoldGround));
        }

        // ---- Threats ----

        static readonly float2 North = new float2(0f, 1f), South = new float2(0f, -1f);

        /// <summary>A line facing east at (30, 48) that holds its ground, so only the threat reaction can move it.</summary>
        static int Holder(SimWorld world)
        {
            int a = Line(world, 0, new float2(30f, 48f), East);
            SetStance(world, PlayerA, a, FormationStance.HoldGround);
            return a;
        }

        static int Enemy(SimWorld world, float2 front, float2 facing, int count = 20, int files = 10)
        {
            int e = Line(world, 1, front, facing, count, files);
            SetStance(world, PlayerB, e, FormationStance.HoldGround);
            return e;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Threat_HoldingFormation_TurnsToItsFlank_WhereItStands(bool left)
        {
            using var world = World(60);
            int a = Holder(world);
            float side = left ? 1f : -1f;
            int b = Enemy(world, new float2(29f, 48f + 32f * side), left ? South : North);
            world.SubmitFormationOrder(PlayerB, FormationOrder.MoveTo(b, new float2(29f, 48f + 11f * side), left ? South : North));
            Run(world, 2400);

            var f = world.Formations[a];
            Assert.That(f.ThreatSide, Is.EqualTo(left ? FormationThreatSide.Left : FormationThreatSide.Right));
            Assert.That(f.Behavior, Is.EqualTo(FormationBehavior.Hold), "it turns, it does not leave");
            Assert.That(f.Stance, Is.EqualTo(FormationStance.HoldGround));
            float2 toEnemy = math.normalize(world.Formations[b].Position - f.Position);
            Assert.That(math.dot(f.Facing, toEnemy), Is.GreaterThan(0.8f), "the front points at the enemy");
            Assert.That(math.distance(f.Position, new float2(30f, 48f)), Is.LessThan(7f), "where it stands");
        }

        [Test]
        public void Threat_FromBehind_TheRearRankBecomesTheFront()
        {
            using var world = World(60);
            int a = Holder(world);
            int b = Enemy(world, new float2(8f, 48f), East);
            world.SubmitFormationOrder(PlayerB, FormationOrder.MoveTo(b, new float2(22f, 48f), East));
            Run(world, 2400);

            var f = world.Formations[a];
            Assert.That(f.ThreatSide, Is.EqualTo(FormationThreatSide.Rear));
            Assert.That(math.dot(f.Facing, West), Is.GreaterThan(0.9f));
            Assert.That(f.Position.x, Is.LessThan(30f), "the anchor moved to the former rear rank");
            Assert.That(Standing(world, a), "and it stands");
        }

        [Test]
        public void Threat_AFormationEngagedInFront_KeepsItsFront()
        {
            using var world = World(60);
            int a = Holder(world);
            Enemy(world, new float2(36f, 48f), West);                               // 20 in front
            Enemy(world, new float2(29f, 59f), South, count: 5, files: 5);          // 5 on the flank
            Run(world, 600);
            var f = world.Formations[a];
            Assert.That(f.ThreatSide, Is.EqualTo(FormationThreatSide.None), "more enemies in front than on the flank");
            Assert.That(math.dot(f.Facing, East), Is.GreaterThan(0.99f));
        }

        [Test]
        public void Threat_TooFewEnemies_OrACircle_DoNotTurn()
        {
            using var world = World(60);
            int a = Holder(world);
            Enemy(world, new float2(29f, 59f), South, count: 2, files: 2);
            int ring = world.SpawnFormation(0, 0, FormationType.Circle, 10, 16, new float2(70f, 48f), East, TestScenarios.AgentRadius);
            SetStance(world, PlayerA, ring, FormationStance.HoldGround);
            Enemy(world, new float2(70f, 56f), South, count: 6, files: 6);
            Run(world, 600);
            Assert.That(world.Formations[a].ThreatSide, Is.EqualTo(FormationThreatSide.None), "two soldiers do not turn a line");
            Assert.That(world.Formations[ring].ThreatSide, Is.EqualTo(FormationThreatSide.None), "a circle has no flank");
        }

        [Test]
        public void Threat_AnOrder_WinsForTheCooldown()
        {
            using var world = World(60);
            int a = Holder(world);
            Enemy(world, new float2(29f, 59f), South);
            Run(world, 600);
            Assert.That(math.dot(world.Formations[a].Facing, North), Is.GreaterThan(0.8f), "turned to the flank");

            // The player insists on the east: the formation obeys, and looks again only after the cooldown
            world.SubmitFormationOrder(PlayerA, FormationOrder.MoveTo(a, world.Formations[a].Position, East));
            int cooldown = TestScenarios.Formations.ThreatCooldownTicks;
            Run(world, cooldown - 30);
            Assert.That(math.dot(world.Formations[a].Facing, East), Is.GreaterThan(0.9f), "the order comes first");
            Run(world, 600);
            Assert.That(math.dot(world.Formations[a].Facing, North), Is.GreaterThan(0.8f), "then the threat counts again");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Threat_AReaction_TakesOnTheThreat_AnOrderedAttackKeepsItsTarget(bool ordered)
        {
            using var world = World(90);
            int a = Line(world, 0, new float2(30f, 48f), East);
            int b = Enemy(world, new float2(39f, 48f), West, count: 10, files: 10);     // 10 in front
            int c = Enemy(world, new float2(6f, 48f), East, count: 30, files: 10);      // 30 come from behind
            if (ordered) world.SubmitFormationOrder(PlayerA, FormationOrder.Against(a, FormationBehavior.Advance, b));
            Run(world, 300);
            Assert.That(world.Formations[a].TargetFormation, Is.EqualTo(b));
            Assert.That(world.Formations[a].Reacting, Is.EqualTo(ordered ? 0 : (int)FormationStance.React));

            world.SubmitFormationOrder(PlayerB, FormationOrder.MoveTo(c, new float2(31f, 48f), East));
            Run(world, 1800);
            var f = world.Formations[a];
            Assert.That(f.TargetFormation, Is.EqualTo(ordered ? b : c), ordered ? "the player's target stays" : "the bigger threat from behind becomes the target");
            Assert.That(f.ThreatSide, Is.EqualTo(ordered ? FormationThreatSide.None : FormationThreatSide.Rear));
        }
    }
}
