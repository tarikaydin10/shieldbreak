using System.Collections.Generic;
using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Grid;
using Shieldbreak.Sim.Orders;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shieldbreak.Tests
{
    /// <summary>
    /// Maps as data and the deployment phase before a battle (M4 task 8): nothing moves or fights, the same FormationOrder
    /// places a formation at once inside its side's zone, and the battle begins when every player is ready.
    /// </summary>
    public class DeploymentTests
    {
        const ushort Own = 0, Enemy = 1;
        const int L3 = 3;
        static readonly float2 North = new float2(0f, 1f), South = new float2(0f, -1f), East = new float2(1f, 0f);
        static readonly float4 OwnZone = new float4(10f, 10f, 118f, 50f), EnemyZone = new float4(10f, 78f, 118f, 118f);

        CombatSetup setup;

        [SetUp]
        public void SetUp() => setup = TestCombat.Setup();

        [TearDown]
        public void TearDown()
        {
            setup?.Dispose();
            setup = null;
        }

        SimWorld World(out int hero, out int own, out int enemy)
        {
            var config = TestScenarios.Config(1, 128, 64);
            config.InputDelayTicks = 1;
            config.MaxFormations = 4;
            config.Formations = TestScenarios.Formations;
            var world = new SimWorld(config, setup);
            hero = world.SpawnAgent(0, new float2(64f, 20f), TestScenarios.AgentRadius, North, TestCombat.OneHand);
            world.AssignPlayerAgent(Own, hero);
            own = world.SpawnFormation(0, Own, FormationType.Line, 6, 12, new float2(64f, 40f), North, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
            enemy = world.SpawnFormation(1, Enemy, FormationType.Line, 6, 12, new float2(64f, 85f), South, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
            world.BeginDeployment(OwnZone, EnemyZone);
            return world;
        }

        static void Run(SimWorld world, int ticks)
        {
            for (int t = 0; t < ticks; t++) world.Step();
        }

        static void Ready(SimWorld world, ushort player) => world.Submit(player, InputCommandType.EndDeployment);

        [Test]
        public void WhileDeploying_NobodyMovesOrFights()
        {
            using var world = World(out _, out int own, out _);
            world.ApplyFormationOrder(FormationOrder.Charge(own), Own);
            Run(world, 2);
            var before = world.Agents[1].Position;
            Run(world, 300);
            Assert.That(world.IsDeploying);
            Assert.That(world.Agents[1].Position, Is.EqualTo(before));
            Assert.That(world.Agents[1].Velocity, Is.EqualTo(float2.zero));
        }

        [Test]
        public void AnOrder_PlacesTheFormationAtOnce_WithEverySoldierOnHisSlot()
        {
            using var world = World(out _, out int own, out _);
            world.SubmitFormationOrder(Own, FormationOrder.MoveTo(own, new float2(30f, 30f), East));
            Run(world, 3);
            var f = world.Formations[own];
            Assert.That(f.Position, Is.EqualTo(new float2(30f, 30f)));
            Assert.That(f.Facing.x, Is.EqualTo(1f).Within(1e-4f));
            for (int i = 0; i < world.AgentCount; i++)
            {
                var m = world.FormationMembers[i];
                if (m.Formation != own) continue;
                Assert.That(math.distance(world.Agents[i].Position, f.ToWorld(m.Local)), Is.LessThan(1e-3f), $"soldier {i}");
                Assert.That(world.Agents[i].Facing.x, Is.EqualTo(1f).Within(1e-3f));
            }
        }

        [Test]
        public void APlacement_StaysInsideTheZoneOfItsSide()
        {
            using var world = World(out _, out int own, out _);
            world.SubmitFormationOrder(Own, FormationOrder.MoveTo(own, new float2(64f, 100f), North));
            Run(world, 3);
            Assert.That(world.Formations[own].Position, Is.EqualTo(new float2(64f, 50f)), "pulled back to the edge of the own zone");
        }

        [Test]
        public void TheShape_ChangesAtOnce_TooAndTheOrderStands()
        {
            using var world = World(out _, out int own, out _);
            world.SubmitFormationOrder(Own, FormationOrder.Reshape(own, FormationType.ShieldWall));
            Run(world, 3);
            var f = world.Formations[own];
            Assert.That(f.Type, Is.EqualTo(FormationType.ShieldWall));
            Assert.That(f.Position, Is.EqualTo(new float2(64f, 40f)), "in its place");
        }

        [Test]
        public void TheEnemy_CannotBePlacedByThePlayer()
        {
            using var world = World(out _, out _, out int enemy);
            world.SubmitFormationOrder(Own, FormationOrder.MoveTo(enemy, new float2(64f, 110f), South));
            Run(world, 3);
            Assert.That(world.Formations[enemy].Position, Is.EqualTo(new float2(64f, 85f)));
        }

        [Test]
        public void WhileDeploying_TheHeroObeysNoCombatOrWalkCommand()
        {
            using var world = World(out int hero, out _, out _);
            world.Submit(Own, InputCommandType.CombatAttack, new int4(hero, 0, 0, 0));
            world.Submit(Own, InputCommandType.PlayerMove, new int4(hero, 0, 0, 0), new float4(1f, 0f, 0f, 0f));
            Run(world, 60);
            Assert.That(world.Combat[hero].State, Is.EqualTo(Shieldbreak.Sim.Combat.Grammar.CombatState.Idle));
            Assert.That(world.Agents[hero].Position, Is.EqualTo(new float2(64f, 20f)));
        }

        [Test]
        public void TheBattle_BeginsWhenThePlayerIsReady_AndOrdersCount()
        {
            using var world = World(out _, out int own, out _);
            world.SubmitFormationOrder(Own, FormationOrder.Charge(own));
            Run(world, 3);
            float y = world.Agents[1].Position.y;
            Ready(world, Own);
            Run(world, 120);
            Assert.That(world.IsDeploying, Is.False);
            Assert.That(world.Agents[1].Position.y, Is.GreaterThan(y + 1f), "the charge given in the deployment goes off");
        }

        [Test]
        public void WithTwoPlayers_TheBattleWaitsForBoth()
        {
            using var world = World(out _, out _, out _);
            // A second hero makes team 1 a human side
            int other = world.SpawnAgent(1, new float2(64f, 100f), TestScenarios.AgentRadius, South, TestCombat.OneHand);
            world.AssignPlayerAgent(Enemy, other);
            Ready(world, Own);
            Run(world, 10);
            Assert.That(world.IsDeploying, "one of two is not enough");
            Ready(world, Enemy);
            Run(world, 10);
            Assert.That(world.IsDeploying, Is.False);
        }

        [Test]
        public void TheHero_StartsBehindHisArmy()
        {
            using var world = World(out int hero, out int own, out _);
            world.SubmitFormationOrder(Own, FormationOrder.MoveTo(own, new float2(90f, 45f), North));
            Run(world, 3);
            Ready(world, Own);
            Run(world, 3);
            var pos = world.Agents[hero].Position;
            Assert.That(pos.x, Is.EqualTo(90f).Within(0.5f), "with the army");
            Assert.That(pos.y, Is.LessThan(45f - 1.3f - 1f), "behind its rear rank");
            Assert.That(world.Agents[hero].Facing.y, Is.GreaterThan(0.99f));
        }

        [Test]
        public void Deployment_IsDeterministic()
        {
            ulong Play()
            {
                using var world = World(out _, out int own, out _);
                world.SubmitFormationOrder(Own, FormationOrder.MoveTo(own, new float2(30f, 30f), East));
                Run(world, 10);
                world.SubmitFormationOrder(Own, FormationOrder.Charge(own));
                Ready(world, Own);
                Run(world, 900);
                return world.ComputeHash();
            }
            Assert.That(Play(), Is.EqualTo(Play()));
        }

        // ---- Maps as data ----

        readonly List<Object> created = new List<Object>();

        CompanyScenarioDefinition ScenarioOnMap(bool deployment)
        {
            var map = ScriptableObject.CreateInstance<MapDefinition>();
            map.size = 120;
            map.hills = new[] { new CompanyScenarioDefinition.Hill { centre = new Vector2(60f, 60f), radius = 20f, height = 5f } };
            map.areas = new[] { new CompanyScenarioDefinition.Area { kind = GroundKind.Forest, centre = new Vector2(20f, 60f), radius = 10f } };
            map.ownZone = new MapDefinition.Zone { min = new Vector2(10f, 10f), max = new Vector2(110f, 40f) };
            map.enemyZone = new MapDefinition.Zone { min = new Vector2(10f, 80f), max = new Vector2(110f, 110f) };
            var scenario = ScriptableObject.CreateInstance<CompanyScenarioDefinition>();
            scenario.map = map;
            scenario.deployment = deployment;
            scenario.heroPosition = new Vector2(60f, 20f);
            scenario.heroFacesTowards = new Vector2(60f, 90f);
            scenario.units = new[]
            {
                new CompanyScenarioDefinition.Unit { name = "A", team = 0, count = 6, files = 6, front = new Vector2(60f, 35f), faceTowards = new Vector2(60f, 90f) },
                new CompanyScenarioDefinition.Unit { name = "B", team = 1, count = 6, files = 6, front = new Vector2(60f, 85f), faceTowards = new Vector2(60f, 30f) },
            };
            scenario.weapons = TestCombat.Weapons(created, out var tuning);
            scenario.combatTuning = tuning;
            created.Add(map);
            created.Add(scenario);
            return scenario;
        }

        [TearDown]
        public void DestroyCreated()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        SimWorld MapWorld(CompanyScenarioDefinition scenario)
        {
            var movement = ScriptableObject.CreateInstance<AgentMovementDefinition>();
            created.Add(movement);
            return scenario.CreateWorld(movement.ToSettings(), out _);
        }

        [Test]
        public void AMap_BringsItsSizeHillsAndGround()
        {
            using var world = MapWorld(ScenarioOnMap(false));
            Assert.That(world.Config.GridWidth, Is.EqualTo(120));
            Assert.That(Shieldbreak.Sim.Grid.Terrain.HeightAt(world.Grid.Heights, world.Grid.Layout, new float2(60f, 60f)), Is.GreaterThan(4f));
            Assert.That(world.Grid.Ground[world.Grid.Layout.ClampedCellIndex(new float2(20f, 60f))], Is.EqualTo((byte)GroundKind.Forest));
            Assert.That(world.IsDeploying, Is.False, "without the phase the battle starts at once");
        }

        [Test]
        public void AScenarioWithDeployment_StartsInItsMapZones()
        {
            using var world = MapWorld(ScenarioOnMap(true));
            Assert.That(world.IsDeploying);
            Assert.That(world.DeployZone(0), Is.EqualTo(new float4(10f, 10f, 110f, 40f)));
            Assert.That(world.DeployZone(1), Is.EqualTo(new float4(10f, 80f, 110f, 110f)));
        }
    }
}
