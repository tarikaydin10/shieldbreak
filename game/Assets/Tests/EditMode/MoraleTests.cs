using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Grid;
using Shieldbreak.Sim.Morale;
using Shieldbreak.Sim.Orders;
using Unity.Collections;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>Panic field, morale and rout (CLAUDE.md 3.4, M2 task 5).</summary>
    public class MoraleTests
    {
        const ushort PlayerA = 0, PlayerB = 1;
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

        SimWorld World(Func<MoraleSettings, MoraleSettings> tweak = null, ulong seed = 1, int size = 64, int maxAgents = 64)
        {
            var config = TestScenarios.Config(seed, size, maxAgents);
            config.MaxFormations = 4;
            config.Formations = TestScenarios.Formations;
            config.Morale = tweak != null ? tweak(TestScenarios.Morale) : TestScenarios.Morale;
            return new SimWorld(config, setup);
        }

        static void Run(SimWorld world, int ticks, List<CombatEvent> log = null)
        {
            for (int t = 0; t < ticks; t++)
            {
                world.Step();
                if (log != null) foreach (var e in world.LastEvents) log.Add(e);
            }
        }

        static float Total(NativeArray<float> field)
        {
            double sum = 0;
            foreach (float v in field) sum += v;
            return (float)sum;
        }

        // ---- Field ----

        [Test]
        public void Diffusion_SpreadsEvenly_KeepsTheTotal_AndDecays()
        {
            var layout = new GridLayout(9, 9, float2.zero);
            var field = new NativeArray<float>(layout.CellCount, Allocator.Temp);
            var scratch = new NativeArray<float>(layout.CellCount, Allocator.Temp);
            try
            {
                int centre = layout.CellIndex(new int2(4, 4));
                field[centre] = 10f;
                for (int step = 0; step < 20; step++) MoraleDiffusion.Step(field, 0, scratch, layout, 1.5f, 0f, 0.05f);
                Assert.That(Total(field), Is.EqualTo(10f).Within(1e-4f), "no decay: nothing is lost, also not over the map edge");
                Assert.That(field[centre], Is.LessThan(10f));
                Assert.That(field[layout.CellIndex(new int2(3, 4))], Is.EqualTo(field[layout.CellIndex(new int2(5, 4))]), "symmetric");
                Assert.That(field[layout.CellIndex(new int2(4, 3))], Is.EqualTo(field[layout.CellIndex(new int2(4, 5))]));
                foreach (float v in field) Assert.That(v, Is.GreaterThanOrEqualTo(0f));

                // A corner keeps its panic too (no flux out of the map)
                for (int i = 0; i < field.Length; i++) field[i] = 0f;
                field[0] = 4f;
                for (int step = 0; step < 50; step++) MoraleDiffusion.Step(field, 0, scratch, layout, 1.5f, 0f, 0.05f);
                Assert.That(Total(field), Is.EqualTo(4f).Within(1e-4f));

                // Decay removes k·dt of the total per step
                float before = Total(field);
                MoraleDiffusion.Step(field, 0, scratch, layout, 1.5f, 0.3f, 0.05f);
                Assert.That(Total(field), Is.EqualTo(before * (1f - 0.3f * 0.05f)).Within(1e-4f));
            }
            finally
            {
                field.Dispose();
                scratch.Dispose();
            }
        }

        [Test]
        public void Settings_RejectAnUnstableField()
        {
            var s = TestScenarios.Morale;
            Assert.DoesNotThrow(() => s.Validate());
            s.Diffusion = 6f;   // 4·6·0.05 = 1.2 > 1
            Assert.Throws<ArgumentException>(() => s.Validate());
            s.Enabled = 0;
            Assert.DoesNotThrow(() => s.Validate(), "off: nothing to check");
        }

        // ---- Sources ----

        [Test]
        public void Death_FrightensItsSide_AndHeartensTheOther()
        {
            using var world = World(seed: 2);
            int killer = world.SpawnAgent(0, new float2(30f, 30f), 0.3f, new float2(0f, 1f));
            int victim = world.SpawnAgent(1, new float2(30f, 31.25f), 0.3f, new float2(0f, -1f));
            world.AssignPlayerAgent(PlayerA, killer);
            var input = new InputDriver(world);
            var log = new List<CombatEvent>();
            for (int k = 0; k < 6; k++) input.At(10 + k * 70, PlayerA, InputCommandType.CombatAttack, killer, AttackDirection.Overhead);
            int deathTick = -1;
            for (int t = 0; t < 600 && deathTick < 0; t++)
            {
                world.Step();
                foreach (var e in world.LastEvents) if (e.Type == CombatEventType.Death) deathTick = t;
            }
            Assert.That(deathTick, Is.GreaterThanOrEqualTo(0), "the player kills the standing soldier");
            float2 at = world.Agents[victim].Position;
            Assert.That(world.Grid.PanicAt(1, at), Is.GreaterThan(0f), "the dead soldier's side is frightened");
            Assert.That(world.Grid.PanicAt(0, at), Is.LessThan(0f), "the killer's side gains confidence");
        }

        [Test]
        public void HitsFromBehind_Frighten_HitsFromTheFront_DoNot()
        {
            float PanicAfterHit(bool fromBehind)
            {
                using var world = World(m => { m.DeathPanic = 0f; return m; });
                int a = world.SpawnAgent(0, new float2(30f, 30f), 0.3f, new float2(0f, 1f));
                int b = world.SpawnAgent(1, new float2(30f, 31.25f), 0.3f, new float2(0f, fromBehind ? 1f : -1f));
                world.AssignPlayerAgent(PlayerA, a);
                var input = new InputDriver(world);
                input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Thrust);
                var log = new List<CombatEvent>();
                input.RunUntil(80, log);
                Assert.That(log.Exists(e => e.Type == CombatEventType.Hit && e.Damage > 0f), "the thrust hits");
                return world.Grid.PanicAt(1, world.Agents[b].Position) + world.Grid.PanicAt(1, world.Agents[b].Position + new float2(0f, 1f));
            }
            Assert.That(PanicAfterHit(false), Is.EqualTo(0f));
            Assert.That(PanicAfterHit(true), Is.GreaterThan(0f));
        }

        // ---- Morale ----

        [Test]
        public void Morale_FallsUnderPanic_LessInAFormation_AndRecoversWhenCalm()
        {
            using var world = World(m => { m.Diffusion = 0f; m.Decay = 0f; m.RoutThreshold = 0f; return m; });
            world.SpawnFormation(0, PlayerA, FormationType.Line, 1, 1, new float2(20f, 30f), East, 0.3f, 0, L3);
            int loose = world.SpawnAgent(0, new float2(40f, 30f), 0.3f, East, 0, L3);
            Run(world, 3);   // the formation assigns its slot
            world.AddPanic(0, new float2(20f, 30f), 0.1f);
            world.AddPanic(0, new float2(40f, 30f), 0.1f);
            Run(world, 60);
            float inFormation = world.Morale[0].Morale, alone = world.Morale[loose].Morale;
            Assert.That(alone, Is.LessThan(1f));
            Assert.That(1f - inFormation, Is.EqualTo((1f - alone) * TestScenarios.Morale.FormationSteadiness).Within(0.01f), "the formation steadies");

            // Panic gone: morale comes back slowly
            world.AddPanic(0, new float2(40f, 30f), -0.1f);
            Run(world, 600);
            Assert.That(world.Morale[loose].Morale, Is.GreaterThan(alone + 0.15f));
        }

        [Test]
        public void Confidence_RaisesMorale()
        {
            using var world = World(m => { m.Diffusion = 0f; m.Decay = 0f; m.Recovery = 0f; return m; });
            int a = world.SpawnAgent(0, new float2(40f, 30f), 0.3f, East, 0, L3);
            world.AddPanic(0, new float2(40f, 30f), 0.2f);
            Run(world, 30);
            float shaken = world.Morale[a].Morale;
            world.AddPanic(0, new float2(40f, 30f), -0.4f);
            Run(world, 30);
            Assert.That(world.Morale[a].Morale, Is.GreaterThan(shaken));
        }

        [Test]
        public void Rout_LeavesTheFormation_StopsFighting_RunsAway_AndLeavesTheField()
        {
            using var world = World();
            int f = world.SpawnFormation(0, PlayerA, FormationType.Line, 5, 5, new float2(30f, 30f), East, 0.3f, 0, L3);
            int enemy = world.SpawnAgent(1, new float2(31.5f, 30f), 0.3f, West);   // stands still, never chases
            Run(world, 30);
            for (int i = 0; i < 5; i++) world.AddPanic(0, world.Agents[i].Position, 3f);
            var log = new List<CombatEvent>();
            Run(world, 60, log);
            for (int i = 0; i < 5; i++)
            {
                Assert.That(world.Agents[i].IsRouting, $"soldier {i} breaks under that much panic");
                Assert.That(world.FormationMembers[i].Formation, Is.EqualTo(-1), "and leaves its formation");
                Assert.That(world.Morale[i].RoutTick, Is.GreaterThanOrEqualTo(0));
                Assert.That(world.Combat[i].BotTarget, Is.EqualTo(-1), "picks no enemy any more");
                Assert.That(world.Combat[i].Sprinting, Is.EqualTo(1), "and runs");
            }
            Assert.That(world.Formations[f].MemberCount, Is.EqualTo(0));
            Assert.That(log.FindAll(e => e.Type == CombatEventType.Rout).Count, Is.EqualTo(5));

            float before = math.distance(world.Agents[0].Position, world.Agents[enemy].Position);
            log.Clear();
            Run(world, 120, log);
            Assert.That(log.Exists(e => e.Type == CombatEventType.AttackStart && e.Attacker < 5), Is.False, "fleeing soldiers do not attack");
            Assert.That(world.Combat[0].BlockHeld, Is.EqualTo(0));
            Assert.That(math.distance(world.Agents[0].Position, world.Agents[enemy].Position), Is.GreaterThan(before + 2f), "they run from the enemy");

            Run(world, 60 * 40, log);
            for (int i = 0; i < 5; i++)
            {
                var a = world.Agents[i];
                Assert.That(a.HasFled && a.IsDead, $"soldier {i} left the field at the map edge");
                Assert.That(world.Combat[i].State, Is.EqualTo(CombatState.Dead));
            }
            Assert.That(log.FindAll(e => e.Type == CombatEventType.Fled).Count, Is.EqualTo(5));
        }

        [Test]
        public void AnEnemyBreaking_HeartensTheOtherSide()
        {
            using var world = World(m => { m.Diffusion = 0f; m.Decay = 0f; return m; });
            int a = world.SpawnAgent(0, new float2(30f, 30f), 0.3f, East, 0, L3);
            world.SpawnAgent(1, new float2(36f, 30f), 0.3f, West);
            world.AddPanic(0, new float2(30f, 30f), 3f);
            var log = new List<CombatEvent>();
            Run(world, 30, log);
            var rout = log.Find(e => e.Type == CombatEventType.Rout && e.Target == a);
            Assert.That(rout.Type, Is.EqualTo(CombatEventType.Rout));
            Assert.That(world.Grid.PanicAt(1, rout.Point.xz), Is.EqualTo(-TestScenarios.Morale.KillCourage).Within(1e-6f));
        }

        [Test]
        public void Rout_IsContagious()
        {
            float NeighbourMorale(float contagion)
            {
                using var world = World(m => { m.RoutPanicPerSecond = contagion; m.Recovery = 0f; return m; });
                int runner = world.SpawnAgent(0, new float2(30f, 30f), 0.3f, East, 0, L3);
                world.SpawnAgent(1, new float2(36f, 30f), 0.3f, West);   // something to run from, never chases
                int neighbour = world.SpawnAgent(0, new float2(28f, 32f), 0.3f, East, 0, L3);
                world.AddPanic(0, world.Agents[runner].Position, 2f);
                Run(world, 30);
                Assert.That(world.Agents[runner].IsRouting);
                Run(world, 120);
                return world.Morale[neighbour].Morale;
            }
            Assert.That(NeighbourMorale(2f), Is.LessThan(NeighbourMorale(0f) - 0.02f));
        }

        [Test]
        public void HeroesAndSoldiersWithoutABrain_NeverBreak_AndTheHeroCalmsHisSide()
        {
            float PanicNear(bool leader)
            {
                using var world = World(m => { m.Diffusion = 0.5f; return m; });
                int hero = world.SpawnAgent(0, new float2(30f, 30f), 0.3f, East);
                int bot = world.SpawnAgent(0, new float2(31f, 30f), 0.3f, East, 0, L3);
                int plain = world.SpawnAgent(0, new float2(29f, 30f), 0.3f, East);
                if (leader) world.AssignPlayerAgent(PlayerA, hero);
                world.AddPanic(0, new float2(30f, 30f), 5f);
                Run(world, 120);
                Assert.That(world.Agents[hero].IsRouting || world.Agents[plain].IsRouting, Is.False, "heroes and plain agents never rout");
                Assert.That(world.Agents[bot].IsRouting, "a bot under that much panic does");
                return world.Grid.PanicAt(0, new float2(30f, 30f));
            }
            Assert.That(PanicNear(true), Is.LessThan(PanicNear(false) * 0.8f), "the hero is a sink in the field");
        }

        [Test]
        public void MoraleOff_NoPanic_NoRout()
        {
            using var world = World(m => { m.Enabled = 0; return m; });
            int a = world.SpawnAgent(0, new float2(30f, 30f), 0.3f, East, 0, L3);
            world.AddPanic(0, new float2(30f, 30f), 5f);
            Run(world, 120);
            Assert.That(world.HasMorale, Is.False);
            Assert.That(world.Agents[a].IsRouting, Is.False);
            Assert.That(world.Morale[a].Morale, Is.EqualTo(1f));
        }

        // ---- Battles ----

        [Test]
        public void TwentyAgainstTwenty_EndsInARout_AndIsDeterministic()
        {
            (int fightingA, int fightingB, int routedLoser, ulong hash) Battle(ulong seed)
            {
                using var world = World(seed: seed, maxAgents: 40);
                int a = world.SpawnFormation(0, PlayerA, FormationType.Line, 10, 20, new float2(17f, 32f), East, 0.3f, 0, L3);
                int b = world.SpawnFormation(1, PlayerB, FormationType.Line, 10, 20, new float2(47f, 32f), West, 0.3f, 0, L3);
                world.SubmitFormationOrder(PlayerA, FormationOrder.Advance(a));
                world.SubmitFormationOrder(PlayerB, FormationOrder.Advance(b));
                var fighting = int2.zero;
                var broke = int2.zero;
                for (int t = 0; t < 120 * 60; t++)
                {
                    world.Step();
                    if (t % 60 != 0) continue;
                    fighting = int2.zero;
                    broke = int2.zero;
                    for (int i = 0; i < world.AgentCount; i++)
                    {
                        var ag = world.Agents[i];
                        if (ag.IsRouting) broke[ag.Team]++;
                        else if (!ag.IsDead) fighting[ag.Team]++;
                    }
                    if (fighting.x == 0 || fighting.y == 0) break;
                }
                int loser = fighting.x == 0 ? 0 : 1;
                return (fighting.x, fighting.y, broke[loser], world.ComputeHash());
            }
            var first = Battle(1);
            Assert.That(first.fightingA == 0 || first.fightingB == 0, "one side has nobody left fighting within two minutes");
            Assert.That(math.max(first.fightingA, first.fightingB), Is.GreaterThan(5), "the winner still stands");
            Assert.That(first.routedLoser, Is.GreaterThan(0), "the loser broke instead of dying to the last man");
            Assert.That(Battle(1).hash, Is.EqualTo(first.hash));
            Assert.That(Battle(2).hash, Is.Not.EqualTo(first.hash));
        }
    }
}
