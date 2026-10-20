using System.Collections.Generic;
using NUnit.Framework;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Context;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>Fighting in formation (CLAUDE.md 3.5 Schicht D): leash, front arc, shield wall, charge, retreat, 20 against 20.</summary>
    public class FormationCombatTests
    {
        const ushort PlayerA = 0, PlayerB = 1;
        /// <summary>Bot levels; level 1 holds its block and rarely attacks.</summary>
        const int L1 = 1, L3 = 3, L5 = 5;
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

        SimWorld World(ulong seed = 1, int size = 64)
        {
            var config = TestScenarios.Config(seed, size, 64);
            config.MaxFormations = 4;
            config.Formations = TestScenarios.Formations;
            return new SimWorld(config, setup);
        }

        static int Company(SimWorld world, int team, FormationType type, int files, float2 front, float2 facing, int level, int count = 20) =>
            world.SpawnFormation(team, team, type, files, count, front, facing, TestScenarios.AgentRadius, TestCombat.OneHand, level);

        static int Alive(SimWorld world, int team)
        {
            int n = 0;
            for (int i = 0; i < world.AgentCount; i++)
                if (!world.Agents[i].IsDead && world.Agents[i].Team == team) n++;
            return n;
        }

        /// <summary>Runs until one side is gone or <paramref name="maxTicks"/>; returns survivors of team 0 and 1.</summary>
        static int2 Fight(SimWorld world, int maxTicks, List<CombatEvent> log = null)
        {
            for (int t = 0; t < maxTicks; t++)
            {
                world.Step();
                if (log != null) foreach (var e in world.LastEvents) log.Add(e);
                if (t % 60 == 0 && (Alive(world, 0) == 0 || Alive(world, 1) == 0)) break;
            }
            return new int2(Alive(world, 0), Alive(world, 1));
        }

        static float SlotError(SimWorld world, int agent)
        {
            var m = world.FormationMembers[agent];
            return m.Formation < 0 || m.Slot < 0 ? 0f : math.distance(world.Agents[agent].Position, world.Formations[m.Formation].ToWorld(m.Local));
        }

        // ---- Rules ----

        [Test]
        public void Rules_LeashFrontArcAndSelfDefence()
        {
            var s = TestScenarios.Formations;
            var f = new FormationData { Position = new float2(10f, 10f), Facing = East, Type = FormationType.Line, Behavior = FormationBehavior.Hold };
            var m = new FormationMember { Formation = 0, Slot = 0, Local = new float2(0f, -1.3f), LocalFacing = new float2(0f, 1f) };
            var ctx = FormationCombatRules.For(m, f);
            Assert.That(ctx.Bound, Is.EqualTo(1));
            Assert.That(ctx.Slot, Is.EqualTo(new float2(8.7f, 10f)));
            float reach = 1.25f;
            float2 me = ctx.Slot;

            Assert.That(FormationCombatRules.Accepts(ctx, s, me, me + East * (s.EngageLeash + reach - 0.1f), reach), "in front, within leash and reach");
            Assert.That(FormationCombatRules.Accepts(ctx, s, me, me + East * (s.EngageLeash + reach + 0.1f), reach), Is.False, "too far from the slot");
            Assert.That(FormationCombatRules.Accepts(ctx, s, me, me + new float2(0f, 2.5f), reach), Is.False, "beside the line, not close");
            Assert.That(FormationCombatRules.Accepts(ctx, s, me, me + West * (s.SelfDefenseRange - 0.1f), reach), "behind but close: self-defence");
            Assert.That(FormationCombatRules.Leashed(ctx, s, me + East * 5f), Is.EqualTo(me + East * s.EngageLeash));

            f.Behavior = FormationBehavior.Charge;
            ctx = FormationCombatRules.For(m, f);
            Assert.That(ctx.Bound, Is.EqualTo(0));
            Assert.That(FormationCombatRules.Accepts(ctx, s, me, me + new float2(0f, 6f), reach), "charging soldiers fight anyone");
            Assert.That(FormationCombatRules.Leashed(ctx, s, me + East * 5f), Is.EqualTo(me + East * 5f));

            f.Behavior = FormationBehavior.Retreat;
            ctx = FormationCombatRules.For(m, f);
            Assert.That(FormationCombatRules.Accepts(ctx, s, me, me + East * 0.5f, reach), Is.False, "a retreat does not fight");

            var loose = FormationCombatRules.For(FormationMember.None, f);
            Assert.That(loose.Bound, Is.EqualTo(0));
            Assert.That(loose.Disengaged, Is.EqualTo(0), "soldiers outside formations fight as in the duel");

            f.Behavior = FormationBehavior.Hold;
            f.Type = FormationType.ShieldWall;
            ctx = FormationCombatRules.For(m, f);
            float block = 0.6f, step = 0.1f;
            FormationCombatRules.PreferBlock(ctx, ref block, ref step);
            Assert.That(block, Is.EqualTo(0.7f).Within(1e-6f));
            Assert.That(step, Is.EqualTo(0f));
        }

        // ---- In the sim ----

        [Test]
        public void HoldingLine_KeepsItsSlots_ChargingLine_BreaksUp()
        {
            using var world = World();
            int a = Company(world, 0, FormationType.Line, 10, new float2(20f, 32f), East, L3);
            int b = Company(world, 1, FormationType.Line, 10, new float2(36f, 32f), West, L3);
            world.SubmitFormationOrder(PlayerB, FormationOrder.Charge(b));
            var log = new List<CombatEvent>();
            float limit = TestScenarios.Formations.EngageLeash + 1.5f;
            int[] samples = new int[2], stray = new int[2];
            for (int t = 0; t < 2400; t++)
            {
                world.Step();
                foreach (var e in world.LastEvents) log.Add(e);
                if (t < 600 || t % 10 != 0) continue;
                for (int i = 0; i < world.AgentCount; i++)
                {
                    int f = world.FormationMembers[i].Formation;
                    if (f < 0) continue;
                    int side = f == a ? 0 : 1;
                    samples[side]++;
                    if (SlotError(world, i) > limit) stray[side]++;
                }
            }
            Assert.That(log.Exists(e => e.Type == CombatEventType.Hit && e.Damage > 0f), "they fought");
            Assert.That(stray[0], Is.LessThan(samples[0] / 20), "the holding line stays within its leash (< 5 % of samples outside)");
            Assert.That(stray[1], Is.GreaterThan(samples[1] / 4), "the charging line leaves its slots");
        }

        [Test]
        public void Retreat_StopsFighting()
        {
            using var world = World();
            int a = Company(world, 0, FormationType.Line, 10, new float2(30f, 32f), East, L3);
            Company(world, 1, FormationType.Line, 10, new float2(31.5f, 32f), West, L1);
            Fight(world, 300);
            world.SubmitFormationOrder(PlayerA, FormationOrder.Retreat(a));
            var log = new List<CombatEvent>();
            // Input delay, then swings already started run out
            Fight(world, 60);
            Fight(world, 600, log);
            Assert.That(log.Exists(e => e.Type == CombatEventType.AttackStart && world.Agents[e.Attacker].Team == 0), Is.False, "no new attacks while retreating");
            for (int i = 0; i < world.AgentCount; i++)
                if (world.FormationMembers[i].Formation == a) Assert.That(world.Combat[i].BotTarget, Is.EqualTo(-1));
            Assert.That(world.Formations[a].Position.x, Is.LessThan(30f - 5f), "and walks away");
        }

        [Test]
        public void ShieldWall_BlocksInsteadOfDodging()
        {
            int Steps(FormationType type)
            {
                using var world = World(seed: 3);
                Company(world, 0, type, 10, new float2(20f, 32f), East, L5);
                int b = Company(world, 1, FormationType.Line, 10, new float2(34f, 32f), West, L5);
                world.SubmitFormationOrder(PlayerB, FormationOrder.Advance(b));
                var log = new List<CombatEvent>();
                Fight(world, 3000, log);
                int steps = 0;
                foreach (var e in log)
                    if (e.Type == CombatEventType.Step && world.Agents[e.Attacker].Team == 0) steps++;
                return steps;
            }
            Assert.That(Steps(FormationType.Line), Is.GreaterThan(0), "a line soldier at level 5 sometimes dodges");
            Assert.That(Steps(FormationType.ShieldWall), Is.EqualTo(0), "a shield wall never steps out of the wall");
        }

        [Test]
        public void TwentyAgainstTwenty_IsDecided_AndDeterministic()
        {
            (int2, ulong) Battle(ulong seed)
            {
                using var world = World(seed);
                int a = Company(world, 0, FormationType.Line, 10, new float2(17f, 32f), East, L3);
                int b = Company(world, 1, FormationType.ShieldWall, 10, new float2(47f, 32f), West, L3);
                world.SubmitFormationOrder(PlayerA, FormationOrder.Advance(a));
                world.SubmitFormationOrder(PlayerB, FormationOrder.Advance(b));
                int2 survivors = Fight(world, 120 * 60);
                return (survivors, world.ComputeHash());
            }
            var (survivors, hash) = Battle(1);
            Assert.That(survivors.x == 0 || survivors.y == 0, $"one side is gone within two minutes ({survivors.x} vs {survivors.y})");
            Assert.That(Battle(1).Item2, Is.EqualTo(hash));
            Assert.That(Battle(2).Item2, Is.Not.EqualTo(hash));
        }

        [Test]
        public void Tactics_Decide_WiderFrontWins()
        {
            // Pillar 4: after a defeat the player should think "I should have stood wider", not "the AI cheated"
            int2 Total(FormationType typeA, int filesA)
            {
                int2 sum = int2.zero;
                for (ulong seed = 1; seed <= 4; seed++)
                {
                    using var world = World(seed);
                    Company(world, 0, typeA, filesA, new float2(17f, 32f), East, L3);
                    int b = Company(world, 1, FormationType.Line, 10, new float2(47f, 32f), West, L3);
                    world.SubmitFormationOrder(PlayerB, FormationOrder.Advance(b));
                    sum += Fight(world, 150 * 60);
                }
                return sum;
            }
            var narrow = Total(FormationType.Line, 7);
            Assert.That(narrow.y, Is.GreaterThan(narrow.x + 10), $"a narrower line is flanked ({narrow.x} vs {narrow.y} survivors)");
            var wall = Total(FormationType.ShieldWall, 14);
            Assert.That(wall.x, Is.GreaterThan(wall.y + 10), $"a shield wall as wide as the line puts more soldiers on the front ({wall.x} vs {wall.y} survivors)");
        }
    }
}
