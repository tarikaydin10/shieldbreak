using System.Collections.Generic;
using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Combat.Resolvers;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shieldbreak.Tests
{
    /// <summary>Shooting: arrows as sim structs, shields against them, shooters on the grammar, screening and taking cover.</summary>
    public class RangedTests
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

        static CompanyScenarioDefinition.Unit Archers(int team, Vector2 front, Vector2 faces, int count = 8, FormationBehavior start = FormationBehavior.Hold) => new CompanyScenarioDefinition.Unit
        {
            name = "A", team = team, formationType = FormationType.Loose, count = count, files = count, weapon = Bow, sideWeapon = Sword, botLevel = L3, front = front, faceTowards = faces, startBehavior = start,
        };

        static CompanyScenarioDefinition.Unit Soldiers(int team, Vector2 front, Vector2 faces, int count = 12, FormationBehavior start = FormationBehavior.Hold) => new CompanyScenarioDefinition.Unit
        {
            name = "S", team = team, formationType = FormationType.Line, count = count, files = 6, weapon = Sword, botLevel = L3, front = front, faceTowards = faces, startBehavior = start,
        };

        static void Run(SimWorld world, int ticks, List<CombatEvent> log = null)
        {
            for (int t = 0; t < ticks; t++)
            {
                world.Step();
                if (log != null) foreach (var e in world.LastEvents) log.Add(e);
            }
        }

        static int Count(List<CombatEvent> log, int type) => log.FindAll(e => e.Type == type).Count;

        // ---- Ballistics ----

        [Test]
        public void Launch_ArrivesAtTheTarget_ByItsOwnFall()
        {
            var from = new float3(10f, 1.5f, 10f);
            var to = new float3(40f, 1.25f, 30f);
            var arrow = new ProjectileData { Position = from, Velocity = Projectile.Launch(from, to, 45f, 9.81f), Active = 1 };
            float best = float.MaxValue;
            for (int t = 0; t < 200; t++)
            {
                Projectile.Step(ref arrow, 9.81f, SimConstants.DT, out _);
                best = math.min(best, math.distance(arrow.Position, to));
            }
            Assert.That(best, Is.LessThan(0.6f), "within one step of the aim point");
        }

        [Test]
        public void Launch_GoesHigherTheFurtherItFlies()
        {
            var from = new float3(0f, 1.5f, 0f);
            float Up(float distance) => Projectile.Launch(from, new float3(distance, 1.25f, 0f), 45f, 9.81f).y;
            Assert.That(Up(40f), Is.GreaterThan(Up(10f)));
        }

        // ---- Shooting ----

        [Test]
        public void Archers_ShootWithoutMoving_AndWound()
        {
            scenario.units = new[]
            {
                Archers(0, new Vector2(48f, 30f), new Vector2(48f, 60f)),
                Soldiers(1, new Vector2(48f, 60f), new Vector2(48f, 30f)),
            };
            using var world = World();
            var start = world.Formations[0].Position;
            var log = new List<CombatEvent>();
            Run(world, 900, log);
            Assert.That(Count(log, CombatEventType.Shot), Is.GreaterThan(5), "they shoot");
            Assert.That(log.Exists(e => e.Type == CombatEventType.Hit && e.Damage > 0f), "and the arrows strike");
            Assert.That(world.Formations[0].Position, Is.EqualTo(start), "without leaving their place");
            float damage = log.Find(e => e.Type == CombatEventType.Hit && e.Damage > 0f && e.Zone == BodyZone.Torso).Damage;
            Assert.That(damage, Is.InRange(10f, 50f), "an arrow wounds, one alone does not kill");
            TestContext.WriteLine($"{Count(log, CombatEventType.Shot)} shots, {Count(log, CombatEventType.Hit)} hits in 15 s, torso hit {damage:0}");
        }

        [Test]
        public void Arrows_FallOutOfRange_AndNeverStrikeTheOwnSide()
        {
            scenario.units = new[]
            {
                Archers(0, new Vector2(48f, 10f), new Vector2(48f, 90f)),
                Soldiers(0, new Vector2(48f, 16f), new Vector2(48f, 90f)),     // right in the line of fire
                Soldiers(1, new Vector2(48f, 45f), new Vector2(48f, 90f), start: FormationBehavior.Hold),   // facing away, in range
            };
            using var world = World();
            var log = new List<CombatEvent>();
            Run(world, 600, log);
            Assert.That(Count(log, CombatEventType.Shot), Is.GreaterThan(0));
            foreach (var e in log)
                if (e.Type == CombatEventType.Hit) Assert.That(world.Agents[e.Target].Team, Is.EqualTo(1), "no friendly fire");
        }

        [Test]
        public void Shield_StopsArrowsFromTheFront_NotFromBehind()
        {
            (int blocked, int hit) Volley(float2 facing)
            {
                scenario.units = new[] { Archers(1, new Vector2(48f, 60f), new Vector2(48f, 30f), count: 6) };
                scenario.heroPosition = new Vector2(48f, 30f);
                scenario.heroFacesTowards = new Vector2(48f + facing.x, 30f + facing.y);
                using var world = World();
                int hero = 0;
                var log = new List<CombatEvent>();
                world.Submit(0, InputCommandType.CombatBlock, new int4(hero, 1, 0, 0), default);
                Run(world, 600, log);
                return (Count(log, CombatEventType.Blocked), Count(log, CombatEventType.Hit));
            }
            var front = Volley(new float2(0f, 1f));
            var back = Volley(new float2(0f, -1f));
            Assert.That(front.blocked, Is.GreaterThan(0), "the shield stops arrows in its arc");
            Assert.That(front.hit, Is.LessThan(front.blocked), "and few get past it");
            Assert.That(back.blocked, Is.Zero, "from behind it does not");
            Assert.That(back.hit, Is.GreaterThan(0));
        }

        [Test]
        public void Bow_HasNoPerfectBlock()
        {
            scenario.units = new[] { Archers(1, new Vector2(48f, 60f), new Vector2(48f, 30f), count: 8) };
            scenario.heroPosition = new Vector2(48f, 30f);
            scenario.heroFacesTowards = new Vector2(48f, 60f);
            using var world = World();
            var log = new List<CombatEvent>();
            for (int t = 0; t < 900; t++)
            {
                // Press the block over and over: nobody can time an arrow
                if (t % 20 == 0) world.Submit(0, InputCommandType.CombatBlock, new int4(0, 1, 0, 0), default);
                if (t % 20 == 10) world.Submit(0, InputCommandType.CombatBlock, new int4(0, 0, 0, 0), default);
                Run(world, 1, log);
            }
            Assert.That(Count(log, CombatEventType.PerfectBlock), Is.Zero);
        }

        [Test]
        public void Shooter_DrawsTheSideArm_WhenTheEnemyIsClose_AndTakesUpTheBowAgain()
        {
            scenario.units = new[]
            {
                Archers(0, new Vector2(48f, 30f), new Vector2(48f, 60f), count: 1),
                Soldiers(1, new Vector2(48f, 33f), new Vector2(48f, 10f), count: 1),
            };
            using var world = World();
            Assert.That(world.Combat[1].WeaponId, Is.EqualTo(Bow));
            Run(world, 180);
            Assert.That(world.Combat[1].WeaponId, Is.EqualTo(Sword), "an enemy at arm's length");
        }

        [Test]
        public void Aim_HigherLevelsHitMore()
        {
            int Hits(int level)
            {
                scenario.units = new[]
                {
                    Archers(0, new Vector2(48f, 30f), new Vector2(48f, 66f), count: 6),
                    Soldiers(1, new Vector2(48f, 66f), new Vector2(48f, 30f), count: 4),
                };
                scenario.units[0].botLevel = level;
                var log = new List<CombatEvent>();
                for (int seed = 1; seed <= 4; seed++)
                {
                    scenario.seed = seed;
                    using var world = World();
                    Run(world, 900, log);
                }
                // A shield that stops the arrow was found by it too
                return Count(log, CombatEventType.Hit) + Count(log, CombatEventType.Blocked);
            }
            int best = Hits(5), worst = Hits(1);
            TestContext.WriteLine($"arrows on target in 15 s, 4 x 6 shooters: L5 {best}, L1 {worst}");
            Assert.That(best, Is.GreaterThan(worst));
        }

        [Test]
        public void SameSeed_SameArrows_SameHash()
        {
            ulong Battle()
            {
                scenario.units = new[]
                {
                    Archers(0, new Vector2(48f, 30f), new Vector2(48f, 60f)),
                    Soldiers(1, new Vector2(48f, 60f), new Vector2(48f, 30f), start: FormationBehavior.Advance),
                };
                using var world = World();
                Run(world, 1800);
                return world.ComputeHash();
            }
            Assert.That(Battle(), Is.EqualTo(Battle()));
        }

        // ---- Formations ----

        [Test]
        public void Screen_MovesUpIntoRange_AndFallsBackBehindTheInfantry()
        {
            scenario.units = new[]
            {
                Archers(0, new Vector2(48f, 14f), new Vector2(48f, 90f), start: FormationBehavior.Screen),
                Soldiers(0, new Vector2(30f, 40f), new Vector2(48f, 90f)),
                Soldiers(1, new Vector2(48f, 66f), new Vector2(48f, 10f)),
            };
            using var world = World();
            Run(world, 1500);
            float y = world.Formations[0].Position.y;
            float gap = 66f - y;
            Assert.That(gap, Is.LessThan(world.FormationSettings.ScreenAdvanceDistance + 4f), "moved up into range");
            Assert.That(gap, Is.GreaterThan(world.FormationSettings.ScreenFallbackDistance), "but no further");

            // The enemy comes on: the shooters go behind their own infantry
            world.SubmitFormationOrder(PlayerB, FormationOrder.Advance(2));
            Run(world, 2400);
            var archers = world.Formations[0];
            var infantry = world.Formations[1];
            Assert.That(archers.Behavior, Is.EqualTo(FormationBehavior.Screen));
            float behind = math.dot(infantry.Position - archers.Position, infantry.Facing);
            Assert.That(behind, Is.GreaterThan(1f), "behind the own line");
        }

        /// <summary>Measured in the 100 v 100: shooters left at full range behind their own attack lost the battle for it.</summary>
        [Test]
        public void Screen_ShootersKeepUpWithTheirOwnAttack()
        {
            float Lag()
            {
                scenario.units = new[]
                {
                    Archers(0, new Vector2(48f, 26f), new Vector2(48f, 90f), start: FormationBehavior.Screen),
                    Soldiers(0, new Vector2(48f, 40f), new Vector2(48f, 90f), start: FormationBehavior.Advance),
                    Soldiers(1, new Vector2(48f, 84f), new Vector2(48f, 10f)),
                };
                using var world = World();
                Run(world, 3000);
                return world.Formations[1].Position.y - world.Formations[0].Position.y;
            }

            var definition = ScriptableObject.CreateInstance<FormationDefinition>();
            created.Add(definition);
            scenario.formation = definition;
            float follow = definition.screenFollowDistance;
            Assert.That(follow, Is.GreaterThan(0f));
            float keepingUp = Lag();
            definition.screenFollowDistance = 0f;
            float leftBehind = Lag();
            Assert.That(keepingUp, Is.LessThan(follow + 4f), "close behind the infantry");
            Assert.That(leftBehind, Is.GreaterThan(keepingUp + 5f), "without it they stay at full range");
        }

        [Test]
        public void UnderArrows_AHoldingFormation_TakesCover_AndGoesBackWhenTheyStop()
        {
            scenario.units = new[]
            {
                Soldiers(0, new Vector2(48f, 30f), new Vector2(48f, 10f)),     // looks away: the arrows come from behind
                Archers(1, new Vector2(48f, 62f), new Vector2(48f, 30f), count: 12),
            };
            using var world = World();
            world.SubmitFormationOrder(PlayerA, FormationOrder.SetStance(0, FormationStance.HoldGround));
            bool covered = false;
            for (int t = 0; t < 1800 && !covered; t++)
            {
                world.Step();
                covered = world.Formations[0].CoverType != 0;
            }
            Assert.That(covered, "it takes cover");
            Assert.That(world.Formations[0].Type, Is.EqualTo(FormationType.ShieldWall));
            Run(world, 300);
            Assert.That(world.Formations[0].Facing.y, Is.GreaterThan(0.7f), "turned to the archers");
            int raised = 0;
            for (int i = 0; i < world.AgentCount; i++)
                if (world.FormationMembers[i].Formation == 0 && world.Combat[i].BlockHeld == 1) raised++;
            Assert.That(raised, Is.GreaterThan(3), "shields up");

            // An order sends the archers away: the fire stops
            world.ApplyFormationOrder(FormationOrder.MoveTo(1, new float2(48f, 94f), new float2(0f, 1f)), PlayerB);
            Run(world, 1500);
            Assert.That(world.Formations[0].CoverType, Is.Zero);
            Assert.That(world.Formations[0].Type, Is.EqualTo(FormationType.Line), "back to its form");
        }

        [Test]
        public void Shooters_DoNotTakeCover_NorDoFormationsOnTheMarch()
        {
            scenario.units = new[]
            {
                Archers(0, new Vector2(30f, 30f), new Vector2(30f, 60f)),
                Soldiers(0, new Vector2(60f, 20f), new Vector2(60f, 90f)),
                Archers(1, new Vector2(45f, 62f), new Vector2(45f, 30f), count: 12),
            };
            using var world = World();
            world.SubmitFormationOrder(PlayerA, FormationOrder.MoveTo(1, new float2(60f, 56f), new float2(0f, 1f)));
            for (int t = 0; t < 900; t++)
            {
                world.Step();
                Assert.That(world.Formations[0].CoverType, Is.Zero, "shooters have no shields");
                Assert.That(world.Formations[1].CoverType, Is.Zero, "a march stays a march");
            }
        }
    }
}
