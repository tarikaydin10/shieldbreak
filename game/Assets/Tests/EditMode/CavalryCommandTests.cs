using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.Sim.AI;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Morale;
using Shieldbreak.Sim.Orders;
using Unity.Mathematics;
using UnityEngine;

namespace Shieldbreak.Tests
{
    /// <summary>
    /// Leading cavalry (M4 task 7): rider formations, the shield wall against cavalry, the commander's riders and the hero
    /// in the saddle.
    /// </summary>
    public class CavalryCommandTests
    {
        const ushort Own = 0, Enemy = 1;
        const int L3 = 3;
        static readonly float2 East = new float2(1f, 0f), West = new float2(-1f, 0f), North = new float2(0f, 1f);

        CombatSetup setup;

        [SetUp]
        public void SetUp() => setup = TestCombat.Setup();

        [TearDown]
        public void TearDown()
        {
            setup?.Dispose();
            setup = null;
        }

        SimWorld World(bool commander = false)
        {
            var config = TestScenarios.Config(1, 128, 128);
            config.InputDelayTicks = 1;
            config.MaxFormations = 6;
            config.Formations = TestScenarios.Formations;
            if (commander)
            {
                var definition = ScriptableObject.CreateInstance<CommanderDefinition>();
                definition.scoreNoise = 0f;
                config.Commander = definition.ToSettings();
                config.Morale = TestScenarios.Morale;
                Object.DestroyImmediate(definition);
            }
            return new SimWorld(config, setup);
        }

        static void Run(SimWorld world, int ticks)
        {
            for (int t = 0; t < ticks; t++) world.Step();
        }

        static int Riders(SimWorld world, int team, ushort owner, float2 at, float2 facing, int count = 8, int bot = L3) =>
            world.SpawnFormation(team, owner, FormationType.Line, count, count, at, facing, TestScenarios.AgentRadius, TestCombat.Lance, bot, -1, mounted: true);

        // ---- The formation ----

        [Test]
        public void Riders_StandFurtherApart_AndMarchFasterThanMen()
        {
            using var world = World();
            int riders = Riders(world, 0, Own, new float2(20f, 60f), East);
            int men = world.SpawnFormation(0, Own, FormationType.Line, 8, 8, new float2(20f, 30f), East, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
            Run(world, 2);
            float closest = float.MaxValue;
            for (int a = 0; a < 8; a++)
                for (int b = a + 1; b < 8; b++)
                    closest = math.min(closest, math.distance(world.Agents[a].Position, world.Agents[b].Position));
            Assert.That(closest, Is.GreaterThan(2f), "a horse needs room");
            world.ApplyFormationOrder(FormationOrder.MoveTo(riders, new float2(70f, 60f), East), Own);
            world.ApplyFormationOrder(FormationOrder.MoveTo(men, new float2(70f, 30f), East), Own);
            Run(world, 20 * 60);
            Assert.That(world.Formations[riders].Position.x, Is.EqualTo(70f).Within(0.5f), "50 m in 20 s");
            Assert.That(world.Formations[men].Position.x, Is.LessThan(50f), "men take longer");
            for (int i = 0; i < 8; i++) Assert.That(world.Agents[i].Position.x, Is.EqualTo(70f).Within(2.5f), $"rider {i} arrived with the formation");
        }

        [Test]
        public void RidersAdvancing_BreakIntoACharge_NearTheEnemy()
        {
            using var world = World();
            int riders = Riders(world, 0, Own, new float2(10f, 60f), East);
            world.SpawnFormation(1, Enemy, FormationType.Line, 8, 8, new float2(100f, 60f), West, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
            world.ApplyFormationOrder(FormationOrder.Advance(riders), Own);
            Run(world, 120);
            Assert.That(world.Formations[riders].Behavior, Is.EqualTo(FormationBehavior.Advance), "far away they trot");
            for (int t = 0; t < 30 * 60 && world.Formations[riders].Behavior == FormationBehavior.Advance; t++) world.Step();
            Assert.That(world.Formations[riders].Behavior, Is.EqualTo(FormationBehavior.Charge));
        }

        [Test]
        public void Riders_GoForTheFormationTheyAreSentAgainst()
        {
            using var world = World();
            int riders = Riders(world, 0, Own, new float2(10f, 60f), East);
            world.SpawnFormation(1, Enemy, FormationType.Line, 8, 8, new float2(50f, 60f), West, TestScenarios.AgentRadius, TestCombat.OneHand, 1);
            int first = world.AgentCount;
            int far = world.SpawnFormation(1, Enemy, FormationType.Line, 8, 8, new float2(100f, 60f), West, TestScenarios.AgentRadius, TestCombat.OneHand, 1);
            world.ApplyFormationOrder(FormationOrder.Against(riders, FormationBehavior.Charge, far), Own);
            Run(world, 60);
            Assert.That(world.Combat[0].BotTarget, Is.InRange(first, first + 7), "not the nearer line");
        }

        // ---- Shield wall against cavalry ----

        /// <summary>Four riders without a brain at x = 95, sent at the point (x, 60).</summary>
        static int[] Horsemen(SimWorld world, float y = 60f, float x = 95f)
        {
            var ids = new int[4];
            for (int i = 0; i < 4; i++)
            {
                ids[i] = world.SpawnAgent(1, new float2(x, y + i * 3f), TestScenarios.AgentRadius, West);
                world.SetMounted(ids[i]);
            }
            return ids;
        }

        static void Send(SimWorld world, int[] horsemen, float2 to)
        {
            foreach (int h in horsemen)
                world.Submit(Own, InputCommandType.SetTargetRange, new int4(h, 1, 0, 0), new float4(to, 0f, 0f));
        }

        [Test]
        public void AHoldingLine_ClosesIntoAShieldWall_BeforeTheHorsesArrive_AndOpensAgain()
        {
            using var world = World();
            int line = world.SpawnFormation(0, Own, FormationType.Line, 8, 8, new float2(30f, 64f), East, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
            var horsemen = Horsemen(world);
            Send(world, horsemen, new float2(5f, 64f));
            for (int t = 0; t < 20 * 60 && world.Agents[horsemen[0]].Position.x > 42f; t++) world.Step();
            Assert.That(world.Formations[line].Type, Is.EqualTo(FormationType.ShieldWall), "the wall stands when they are still 12 m away");
            Send(world, horsemen, new float2(120f, 64f));
            Run(world, 10 * 60);
            Assert.That(world.Formations[line].Type, Is.EqualTo(FormationType.Line), "the riders are gone, the line opens again");
        }

        [Test]
        public void HorsesRidingPast_AreNoReason_ForAShieldWall()
        {
            using var world = World();
            int line = world.SpawnFormation(0, Own, FormationType.Line, 8, 8, new float2(30f, 40f), East, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
            var horsemen = Horsemen(world, 66f, 95f);
            Send(world, horsemen, new float2(5f, 110f));
            Run(world, 12 * 60);
            Assert.That(world.Formations[line].Type, Is.EqualTo(FormationType.Line));
        }

        [Test]
        public void Spears_TurnToRidersFromTheFlank_AndShootersIgnoreThem()
        {
            using var world = World();
            int spears = world.SpawnFormation(0, Own, FormationType.Line, 8, 8, new float2(40f, 40f), East, TestScenarios.AgentRadius, TestCombat.Polearm, L3);
            var horsemen = Horsemen(world, 90f, 40f);
            Send(world, horsemen, new float2(40f, 5f));
            for (int t = 0; t < 20 * 60 && world.Agents[horsemen[0]].Position.y > 60f; t++) world.Step();
            Assert.That(world.Formations[spears].Type, Is.EqualTo(FormationType.Line), "no shields, no wall");
            Assert.That(math.dot(world.Formations[spears].TargetFacing, North), Is.GreaterThan(0.8f), "the spears turn to the riders");
        }

        // ---- The commander ----

        [Test]
        public void CommanderRiders_WaitForTheLines()
        {
            using var world = World(commander: true);
            int riders = Riders(world, 0, Own, new float2(10f, 60f), East);
            world.SpawnFormation(0, Own, FormationType.Line, 10, 20, new float2(20f, 60f), East, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
            world.SpawnFormation(1, Enemy, FormationType.Line, 10, 20, new float2(100f, 60f), West, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
            world.ApplyFormationOrder(FormationOrder.Advance(riders), Own);
            world.AddCommander(Own, 0);
            Run(world, 60);
            Assert.That(world.Formations[riders].Behavior, Is.EqualTo(FormationBehavior.Hold), "80 m apart: the riders stay in reserve");
        }

        [Test]
        public void CommanderRiders_GoRoundSpears_ForTheLineBesideThem()
        {
            using var world = World(commander: true);
            int riders = Riders(world, 0, Own, new float2(10f, 60f), East);
            world.SpawnFormation(0, Own, FormationType.Line, 10, 30, new float2(55f, 60f), East, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
            world.SpawnFormation(1, Enemy, FormationType.Line, 8, 8, new float2(75f, 40f), West, TestScenarios.AgentRadius, TestCombat.Polearm, 1);
            int line = world.SpawnFormation(1, Enemy, FormationType.Line, 8, 8, new float2(75f, 80f), West, TestScenarios.AgentRadius, TestCombat.OneHand, 1);
            world.AddCommander(Own, 0);
            Run(world, 30);
            Assert.That(world.Commanders[0].Tactic, Is.Not.EqualTo(CommanderTactic.Retreat), "the stronger side has no reason to run");
            Assert.That(world.Formations[riders].Behavior, Is.EqualTo(FormationBehavior.Charge));
            Assert.That(world.Formations[riders].TargetFormation, Is.EqualTo(line));
        }

        [Test]
        public void RiderScores_ReadWhatIsOnTheField()
        {
            var definition = ScriptableObject.CreateInstance<CommanderDefinition>();
            var s = definition.ToSettings();
            Object.DestroyImmediate(definition);
            float Score(FormationData f, bool engaged = false) => CommanderUtility.RiderTargetScore(f, 40f, engaged, s);
            var line = new FormationData { Type = FormationType.Line };
            Assert.That(Score(new FormationData { Type = FormationType.Loose, Ranged = 1 }), Is.GreaterThan(Score(line)), "shooters first");
            Assert.That(Score(new FormationData { Type = FormationType.Line, Spears = 1 }), Is.LessThan(Score(line)), "not into spears");
            Assert.That(Score(new FormationData { Type = FormationType.ShieldWall }), Is.LessThan(Score(line)), "not into a wall");
            Assert.That(Score(line, engaged: true), Is.GreaterThan(Score(line)), "men already fighting cannot turn");
        }

        // ---- The hero in the saddle ----

        int Hero(SimWorld world, float x = 10f)
        {
            int hero = world.SpawnAgent(0, new float2(x, 60f), TestScenarios.AgentRadius, East, TestCombat.Lance);
            world.SetLoadout(hero, new int4(TestCombat.Lance, TestCombat.OneHand, -1, -1));
            world.SetMounted(hero);
            world.AssignPlayerAgent(Own, hero);
            return hero;
        }

        static void Ride(SimWorld world, int hero, float2 way, float2 look = default, int sprint = 0) =>
            world.Submit(Own, InputCommandType.PlayerMove, new int4(hero, sprint, 0, 0), new float4(way, look));

        [Test]
        public void TheHorse_GoesWhereItIsRidden_NotWhereTheRiderLooks()
        {
            using var world = World();
            int hero = Hero(world);
            Ride(world, hero, float2.zero, North);
            Run(world, 60);
            Assert.That(world.Agents[hero].Facing.x, Is.EqualTo(1f).Within(0.01f), "looking around does not turn it");
            Ride(world, hero, East);
            Run(world, 240);
            Ride(world, hero, North);
            Run(world, 300);
            Assert.That(world.Agents[hero].Velocity.y, Is.GreaterThan(7f), "ridden north it gallops north");
        }

        [Test]
        public void LettingGo_BrakesTheHorse_WithoutTurningIt()
        {
            using var world = World();
            int hero = Hero(world);
            Ride(world, hero, East);
            Run(world, 240);
            float x = world.Agents[hero].Position.x;
            Ride(world, hero, float2.zero);
            Run(world, 180);
            Assert.That(math.length(world.Agents[hero].Velocity), Is.LessThan(0.5f), "it stops");
            Assert.That(world.Agents[hero].Facing.x, Is.GreaterThan(0.99f), "and does not turn round");
            Assert.That(world.Agents[hero].Position.x, Is.GreaterThan(x));
        }

        [Test]
        public void InTheSaddle_ThereIsNoStep_NoJump_NoSprintDrain_AndNoBlockWithALance()
        {
            using var world = World();
            int hero = Hero(world);
            Ride(world, hero, East, default, sprint: 1);
            Run(world, 120);
            Assert.That(world.Combat[hero].Stamina, Is.EqualTo(setup.Tuning.StaminaMax), "the horse runs, not the rider");
            world.Submit(Own, InputCommandType.CombatStep, new int4(hero, StepDirection.Left, 0, 0));
            world.Submit(Own, InputCommandType.CombatJump, new int4(hero, 0, 0, 0));
            world.Submit(Own, InputCommandType.CombatBlock, new int4(hero, 1, 0, 0));
            world.Submit(Own, InputCommandType.CombatSpecial, new int4(hero, 0, 0, 0));
            Run(world, 4);
            Assert.That(world.Combat[hero].State, Is.EqualTo(CombatState.Idle));
            Assert.That(world.Combat[hero].BlockHeld, Is.Zero);
        }

        [Test]
        public void TheHero_CouchesTheLance_AndKillsAtAGallop()
        {
            using var world = World();
            int hero = Hero(world);
            int man = world.SpawnAgent(1, new float2(70f, 60f), TestScenarios.AgentRadius, West, TestCombat.OneHand);
            Ride(world, hero, East);
            for (int t = 0; t < 10 * 60 && world.Agents[hero].Position.x < 58f; t++) world.Step();
            world.Submit(Own, InputCommandType.CombatAttack, new int4(hero, AttackDirection.Thrust, 0, 0));
            Run(world, 150);
            Assert.That(world.Agents[man].IsDead);
        }

        [Test]
        public void TheUnattendedHero_PutsTheLanceAway_AndDrawsHisSword()
        {
            using var world = World();
            int hero = Hero(world);
            world.SpawnFormation(0, Own, FormationType.Line, 8, 8, new float2(30f, 60f), East, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
            world.Submit(Own, InputCommandType.HeroUnattended, new int4(hero, 1, 0, 0));
            Run(world, 90);
            Assert.That(setup.Weapons[world.Combat[hero].WeaponId].Archetype, Is.EqualTo(WeaponArchetype.OneHandShield));
        }

        [Test]
        public void LedCavalry_IsDeterministic()
        {
            ulong Play()
            {
                using var world = World(commander: true);
                Riders(world, 0, Own, new float2(10f, 60f), East);
                world.SpawnFormation(0, Own, FormationType.Line, 10, 20, new float2(40f, 60f), East, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
                world.SpawnFormation(1, Enemy, FormationType.Line, 10, 20, new float2(70f, 40f), West, TestScenarios.AgentRadius, TestCombat.OneHand, L3);
                world.SpawnFormation(1, Enemy, FormationType.Line, 10, 10, new float2(70f, 80f), West, TestScenarios.AgentRadius, TestCombat.Polearm, L3);
                world.AddCommander(Own, 0);
                Run(world, 1800);
                return world.ComputeHash();
            }
            Assert.That(Play(), Is.EqualTo(Play()));
        }
    }
}
