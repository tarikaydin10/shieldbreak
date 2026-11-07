using System.Collections.Generic;
using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.Sim.AI;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Grid;
using Shieldbreak.Sim.Orders;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using GridLayout = Shieldbreak.Sim.Grid.GridLayout;
using Object = UnityEngine.Object;
using Terrain = Shieldbreak.Sim.Grid.Terrain;

namespace Shieldbreak.Tests
{
    /// <summary>The height of the ground (M4 task 3): the field itself, walking, blows, arrows and the commander.</summary>
    public class HeightTests
    {
        const int Sword = 0, Bow = 2;
        const ushort PlayerA = 0, PlayerB = 1;
        const int L3 = 3;
        readonly List<Object> created = new List<Object>();
        CompanyScenarioDefinition scenario;
        AgentMovementDefinition movement;

        [SetUp]
        public void SetUp()
        {
            scenario = ScriptableObject.CreateInstance<CompanyScenarioDefinition>();
            var weapons = TestCombat.Weapons(created, out var tuning);
            var bow = ScriptableObject.CreateInstance<WeaponDefinition>();
            bow.ApplyBowDefaults();
            var shot = ScriptableObject.CreateInstance<AttackDefinition>();
            shot.ApplyDefaults(AttackDirection.Thrust);
            shot.windupMs = 900f;
            shot.releaseMs = 100f;
            shot.recoveryMs = 900f;
            shot.shootsProjectile = true;
            for (int d = 0; d < AttackDirection.Count; d++) bow.attacks[d] = shot;
            created.Add(bow);
            created.Add(shot);
            scenario.weapons = new[] { weapons[0], weapons[1], bow };
            scenario.combatTuning = tuning;
            scenario.heroPosition = new Vector2(4f, 4f);
            scenario.heroFacesTowards = new Vector2(4f, 40f);
            scenario.mapSize = 96;
            scenario.units = new CompanyScenarioDefinition.Unit[0];
            movement = ScriptableObject.CreateInstance<AgentMovementDefinition>();
            created.Add(scenario);
            created.Add(movement);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        SimWorld World() => scenario.CreateWorld(movement.ToSettings(), out _);

        static CompanyScenarioDefinition.Unit Archers(int team, Vector2 front, Vector2 faces) => new CompanyScenarioDefinition.Unit
        {
            name = "A", team = team, formationType = FormationType.Loose, count = 8, files = 8, weapon = Bow, sideWeapon = Sword, botLevel = L3, front = front, faceTowards = faces,
        };

        static CompanyScenarioDefinition.Unit Soldiers(int team, Vector2 front, Vector2 faces) => new CompanyScenarioDefinition.Unit
        {
            name = "S", team = team, formationType = FormationType.Line, count = 12, files = 6, weapon = Sword, botLevel = L3, front = front, faceTowards = faces,
        };

        static CompanyScenarioDefinition.Hill Hill(float x, float y, float radius, float height) => new CompanyScenarioDefinition.Hill { centre = new Vector2(x, y), radius = radius, height = height };

        static int Shots(SimWorld world, int ticks, out int hits)
        {
            int shots = 0;
            hits = 0;
            for (int t = 0; t < ticks; t++)
            {
                world.Step();
                foreach (var e in world.LastEvents)
                {
                    if (e.Type == CombatEventType.Shot) shots++;
                    if (e.Type == CombatEventType.Hit) hits++;
                }
            }
            return shots;
        }

        // ---- The field ----

        [Test]
        public void Hill_RisesSmoothly_FromAFlatFoot_ToAFlatTop()
        {
            using var grid = new WorldGrid(new GridLayout(40, 40, float2.zero), Allocator.Persistent);
            Assert.That(grid.HasRelief, Is.False);
            Assert.That(grid.HeightAt(new float2(20f, 20f)), Is.Zero, "flat until a hill is raised");
            grid.AddHill(new float2(20f, 20f), 10f, 5f);
            Assert.That(grid.HasRelief);
            Assert.That(grid.HeightAt(new float2(20f, 20f)), Is.EqualTo(5f).Within(1e-3f), "the top");
            Assert.That(grid.HeightAt(new float2(20f, 25f)), Is.EqualTo(2.5f).Within(0.1f), "half way down at half the radius");
            Assert.That(grid.HeightAt(new float2(35f, 20f)), Is.Zero, "beyond the foot");
            float2 slope = Terrain.SlopeAt(grid.Heights, grid.Layout, new float2(15f, 20f));
            Assert.That(slope.x, Is.GreaterThan(0.3f), "uphill towards the top");
            Assert.That(math.abs(slope.y), Is.LessThan(0.1f), "and nothing to speak of across the slope");
        }

        [Test]
        public void Height_IsContinuousBetweenCorners_AndGoesOnBeyondTheMap()
        {
            using var grid = new WorldGrid(new GridLayout(20, 20, new float2(-5f, 3f)), Allocator.Persistent);
            grid.AddHill(new float2(5f, 13f), 12f, 4f);
            float last = grid.HeightAt(new float2(-5f, 13f));
            for (float x = -5f; x < 15f; x += 0.05f)
            {
                float h = grid.HeightAt(new float2(x, 13.3f));
                Assert.That(math.abs(h - grid.HeightAt(new float2(x + 0.01f, 13.3f))), Is.LessThan(0.02f), $"no step at {x}");
                last = h;
            }
            Assert.That(grid.HeightAt(new float2(500f, 13f)), Is.EqualTo(grid.HeightAt(new float2(15f, 13f))), "the border height goes on");
            Assert.That(last, Is.GreaterThanOrEqualTo(0f));
        }

        // ---- Walking ----

        float MarchTime(params CompanyScenarioDefinition.Hill[] hills)
        {
            scenario.hills = hills;
            scenario.units = new[] { Soldiers(0, new Vector2(48f, 20f), new Vector2(48f, 90f)) };
            using var world = World();
            world.SubmitFormationOrder(PlayerA, FormationOrder.MoveTo(0, new float2(48f, 70f), new float2(0f, 1f)));
            while (world.Tick < 120 * 60 && math.distance(world.Formations[0].Position, new float2(48f, 70f)) > 0.5f) world.Step();
            return world.Tick / 60f;
        }

        [Test]
        public void Formation_MarchesSlowerUphill_AndFasterDownhill()
        {
            float flat = MarchTime();
            float up = MarchTime(Hill(48f, 76f, 34f, 8f));
            float down = MarchTime(Hill(48f, 14f, 34f, 8f));
            TestContext.WriteLine($"50 m: flat {flat:0.0} s, uphill {up:0.0} s, downhill {down:0.0} s");
            Assert.That(up, Is.GreaterThan(flat * 1.15f));
            Assert.That(down, Is.LessThan(flat * 0.95f));
        }

        [Test]
        public void Slope_FollowsTheWalkingDirection_AndStaysWithinItsLimits()
        {
            var s = movement.ToSettings();
            float2 slope = new float2(0f, 0.1f);
            Assert.That(s.SlopeFactor(slope, new float2(0f, 1f)), Is.EqualTo(1f - s.UphillSlowdown * 0.1f).Within(1e-4f));
            Assert.That(s.SlopeFactor(slope, new float2(0f, -1f)), Is.EqualTo(1f + s.DownhillBoost * 0.1f).Within(1e-4f));
            Assert.That(s.SlopeFactor(slope, new float2(1f, 0f)), Is.EqualTo(1f).Within(1e-4f), "along the slope nothing changes");
            Assert.That(s.SlopeFactor(new float2(0f, 5f), new float2(0f, 1f)), Is.EqualTo(s.SlopeSpeedMin));
            Assert.That(s.SlopeFactor(new float2(0f, 5f), new float2(0f, -1f)), Is.EqualTo(s.SlopeSpeedMax));
        }

        // ---- Blows ----

        float Blow(float hillX, float hillY)
        {
            using var setup = TestCombat.Setup();
            using var world = TestCombat.World(setup, 4);
            if (hillX > 0f) world.AddHill(new float2(hillX, hillY), 20f, 6f);
            int a = world.SpawnAgent(0, new float2(10f, 10f), 0.3f, new float2(0f, 1f));
            int b = world.SpawnAgent(1, new float2(10f, 11.25f), 0.3f, new float2(0f, -1f));
            world.AssignPlayerAgent(PlayerA, a);
            world.AssignPlayerAgent(PlayerB, b);
            var input = new InputDriver(world);
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            var log = new List<CombatEvent>();
            input.RunUntil(140, log);
            return log.Find(e => e.Type == CombatEventType.Hit && e.Target == b).Energy;
        }

        [Test]
        public void Blow_FromAboveStrikesHarder_FromBelowLess()
        {
            float flat = Blow(0f, 0f);
            float above = Blow(10f, 2f);
            float below = Blow(10f, 20f);
            TestContext.WriteLine($"overhead: flat {flat:0} J, from above {above:0} J, from below {below:0} J");
            Assert.That(flat, Is.GreaterThan(0f));
            Assert.That(above, Is.GreaterThan(flat));
            Assert.That(below, Is.GreaterThan(0f), "the blow still lands");
            Assert.That(below, Is.LessThan(above));
        }

        // ---- Arrows ----

        [Test]
        public void Arrows_CarryFurtherDownhill_AndFallShortUphill()
        {
            // 46 m: out of range on flat ground (40 m)
            scenario.units = new[]
            {
                Archers(0, new Vector2(48f, 20f), new Vector2(48f, 90f)),
                Soldiers(1, new Vector2(48f, 66f), new Vector2(48f, 10f)),
            };
            using (var flat = World()) Assert.That(Shots(flat, 600, out _), Is.Zero, "out of range on the flat");

            scenario.hills = new[] { Hill(48f, 20f, 24f, 8f) };
            using (var above = World()) Assert.That(Shots(above, 600, out _), Is.GreaterThan(0), "from 8 m up it reaches");

            // 36 m: in range on flat ground, out of range 8 m uphill
            scenario.units = new[]
            {
                Archers(0, new Vector2(48f, 30f), new Vector2(48f, 90f)),
                Soldiers(1, new Vector2(48f, 66f), new Vector2(48f, 10f)),
            };
            scenario.hills = new[] { Hill(48f, 72f, 24f, 8f) };
            using (var below = World()) Assert.That(Shots(below, 600, out _), Is.Zero, "uphill it falls short");
        }

        [Test]
        public void Arrows_StickInAHillBetweenShooterAndTarget()
        {
            scenario.units = new[]
            {
                Archers(0, new Vector2(48f, 30f), new Vector2(48f, 90f)),
                Soldiers(1, new Vector2(48f, 66f), new Vector2(48f, 10f)),
            };
            using (var open = World())
            {
                Shots(open, 900, out int hits);
                Assert.That(hits, Is.GreaterThan(0), "over open ground they strike");
            }

            scenario.hills = new[] { Hill(48f, 48f, 10f, 6f) };
            using var hidden = World();
            int shots = Shots(hidden, 900, out int blocked);
            Assert.That(shots, Is.GreaterThan(0), "they shoot");
            Assert.That(blocked, Is.Zero, "the hill takes every arrow");
        }

        // ---- The commander ----

        [Test]
        public void Commander_HoldsTheHeight_AndThinksTwiceBeforeAttackingUphill()
        {
            var definition = ScriptableObject.CreateInstance<CommanderDefinition>();
            created.Add(definition);
            var s = definition.ToSettings();
            var level = new CommanderFeatures { OwnFighters = 20, EnemyFighters = 20, OwnFormations = 2, Ratio = 1f, Balance = 1f, OwnMorale = 1f, EnemyMorale = 1f, Distance = 40f };
            var above = level;
            above.OwnGround = 6f;
            var below = level;
            below.EnemyGround = 6f;
            var flat = CommanderUtility.Score(level, s);
            var high = CommanderUtility.Score(above, s);
            var low = CommanderUtility.Score(below, s);
            Assert.That(high.Hold, Is.GreaterThan(flat.Hold), "up here it pays to stand");
            Assert.That(low.Hold, Is.LessThan(flat.Hold), "down there it does not");
            Assert.That(low.Frontal, Is.LessThan(flat.Frontal), "and neither does charging up");
            Assert.That(high.Frontal, Is.EqualTo(flat.Frontal), "going downhill costs nothing");
        }

        // ---- Determinism ----

        [Test]
        public void SameHills_SameHash_OtherHills_OtherHash()
        {
            ulong Battle(float height)
            {
                scenario.hills = new[] { Hill(48f, 48f, 30f, height) };
                scenario.units = new[]
                {
                    Soldiers(0, new Vector2(48f, 30f), new Vector2(48f, 70f)),
                    Soldiers(1, new Vector2(48f, 66f), new Vector2(48f, 30f)),
                };
                using var world = World();
                world.SubmitFormationOrder(PlayerA, FormationOrder.Charge(0));
                for (int t = 0; t < 1800; t++) world.Step();
                return world.ComputeHash();
            }

            Assert.That(Battle(6f), Is.EqualTo(Battle(6f)));
            Assert.That(Battle(6f), Is.Not.EqualTo(Battle(5f)));
        }
    }
}
