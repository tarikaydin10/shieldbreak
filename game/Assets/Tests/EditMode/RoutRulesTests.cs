using NUnit.Framework;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Morale;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>
    /// When soldiers run, modelled on Bannerlord: casualties near a soldier frighten him up to a limit, a shield wall
    /// takes it better, and panic spreads as a chain reaction through those around him, not across the field.
    /// </summary>
    public class RoutRulesTests
    {
        const ushort PlayerA = 0;
        const int L3 = 3;
        static readonly float2 East = new float2(1f, 0f), West = new float2(-1f, 0f);

        CombatSetup setup;

        [SetUp]
        public void SetUp() => setup = TestCombat.Setup();

        [TearDown]
        public void TearDown()
        {
            setup?.Dispose();
            setup = null;
        }

        SimWorld World(System.Func<MoraleSettings, MoraleSettings> tweak = null)
        {
            var config = TestScenarios.Config(1, 96, 64);
            config.MaxFormations = 4;
            config.Formations = TestScenarios.Formations;
            config.Morale = tweak != null ? tweak(TestScenarios.Morale) : TestScenarios.Morale;
            return new SimWorld(config, setup);
        }

        static void Run(SimWorld world, int ticks)
        {
            for (int t = 0; t < ticks; t++) world.Step();
        }

        static int[] Soldiers(SimWorld world, int team, int count, float x, float y, float2 facing, int level = L3)
        {
            var ids = new int[count];
            for (int i = 0; i < count; i++) ids[i] = world.SpawnAgent(team, new float2(x, y + i * 1.1f), 0.3f, facing, 0, level);
            return ids;
        }

        static int Routing(SimWorld world, int[] soldiers)
        {
            int n = 0;
            foreach (int s in soldiers) if (world.Agents[s].IsRouting) n++;
            return n;
        }

        float MoraleAfter(float panic)
        {
            using var world = World(m => { m.Diffusion = 0f; m.Decay = 0f; m.RoutThreshold = 0f; m.PanicCap = 0.1f; return m; });
            int soldier = world.SpawnAgent(0, new float2(30f, 30f), 0.3f, East, 0, L3);
            world.AddPanic(0, new float2(30f, 30f), panic);
            Run(world, 12);
            return world.Morale[soldier].Morale;
        }

        [Test]
        public void Fright_HasALimit_TenDeadAreNotTenTimesAsBad()
        {
            float cap = MoraleAfter(0.1f);
            Assert.That(cap, Is.LessThan(1f), "panic does frighten");
            Assert.That(MoraleAfter(3f), Is.EqualTo(cap).Within(1e-4f), "more of it does not frighten more");
        }

        float LossInFormation(FormationType type)
        {
            using var world = World(m => { m.Diffusion = 0f; m.Decay = 0f; m.RoutThreshold = 0f; return m; });
            world.SpawnFormation(0, PlayerA, type, 1, 1, new float2(30f, 30f), East, 0.3f, 0, L3);
            Run(world, 3);
            world.AddPanic(0, new float2(30f, 30f), 0.1f);
            Run(world, 60);
            return 1f - world.Morale[0].Morale;
        }

        [Test]
        public void AShieldWall_TakesFrightBetterThanALine()
        {
            float line = LossInFormation(FormationType.Line);
            float wall = LossInFormation(FormationType.ShieldWall);
            Assert.That(line, Is.GreaterThan(0f));
            Assert.That(wall, Is.LessThan(line * 0.75f));
        }

        // ---- The state of the battle (Tarik: losing half while the enemy loses nothing shakes a side) ----

        [Test]
        public void LosingHalf_WhileTheEnemyLosesNothing_ShakesTheSide()
        {
            var s = TestScenarios.Morale;
            Assert.That(MoraleRules.LossGapDrain(0.55f, 0f, s), Is.GreaterThan(0f), "half gone, the enemy whole");
            Assert.That(MoraleRules.LossGapDrain(0.55f, 0.5f, s), Is.Zero, "both bled the same: they fight on");
            Assert.That(MoraleRules.LossGapDrain(0.3f, 0f, s), Is.LessThan(MoraleRules.LossGapDrain(0.55f, 0f, s)), "the worse the gap, the worse");
            Assert.That(MoraleRules.LossGapDrain(0f, 0.55f, s), Is.Zero, "winning frightens nobody");
        }

        [Test]
        public void BelowTheThreshold_ASoldierBreaksByChance_SoonerTheLowerHeSinks()
        {
            var s = TestScenarios.Morale;
            Assert.That(MoraleRules.BreakChance(s.RoutThreshold + 0.01f, s, 1f), Is.Zero, "above it, never");
            float justBelow = MoraleRules.BreakChance(s.RoutThreshold - 0.01f, s, 1f);
            float broken = MoraleRules.BreakChance(0f, s, 1f);
            Assert.That(justBelow, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(broken, Is.GreaterThan(justBelow));
        }

        [Test]
        public void TerrifiedSoldiers_BreakOneByOne_NotAllInTheSameMoment()
        {
            using var world = World();
            var group = Soldiers(world, 0, 8, 40f, 30f, East);
            Soldiers(world, 1, 12, 41.5f, 29f, West, 0);
            foreach (int s in group) world.AddPanic(0, world.Agents[s].Position, 3f);
            int first = -1, last = -1;
            for (int t = 0; t < 900; t++)
            {
                world.Step();
                foreach (var e in world.LastEvents)
                {
                    if (e.Type != Shieldbreak.Sim.Combat.Grammar.CombatEventType.Rout) continue;
                    if (first < 0) first = world.Tick;
                    last = world.Tick;
                }
            }
            Assert.That(Routing(world, group), Is.EqualTo(group.Length), "in the end all of them run");
            Assert.That(last - first, Is.GreaterThan(30), "but one after the other");
        }

        [Test]
        public void ACollapseStaysWhereItHappens()
        {
            using var world = World();
            var broken = Soldiers(world, 0, 3, 20f, 30f, East);
            Soldiers(world, 1, 12, 21.5f, 26f, West, 0);
            var line = Soldiers(world, 0, 8, 55f, 30f, East);
            Soldiers(world, 1, 8, 56.5f, 30f, West, 0);
            foreach (int s in broken) world.AddPanic(0, world.Agents[s].Position, 3f);
            Run(world, 300);
            Assert.That(Routing(world, broken), Is.EqualTo(3));
            Assert.That(Routing(world, line), Is.Zero, "the line 35 m away fights on");
        }

        [Test]
        public void AChainReaction_CanTakeAWholeFormationWithIt()
        {
            using var world = World(m => { m.RoutPanicPerSecond = 3f; return m; });
            var line = Soldiers(world, 0, 8, 40f, 30f, East);
            Soldiers(world, 1, 8, 41.5f, 30f, West, 0);
            world.AddPanic(0, world.Agents[line[3]].Position, 6f);
            Run(world, 600);
            Assert.That(Routing(world, line), Is.EqualTo(line.Length), "one breaks, and the others follow");
        }
    }
}
