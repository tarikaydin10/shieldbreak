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
            Line(world, 1, new float2(60f, 48f), West);
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
    }
}
