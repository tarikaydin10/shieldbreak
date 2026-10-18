using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>Two-hand archetype (CLAUDE.md 3.5, Schicht B): heavier blows, 90° weapon block, leaping overhead; weapon switching.</summary>
    public class TwoHandTests
    {
        const ushort PlayerA = 0, PlayerB = 1;
        CombatSetup setup;
        SimWorld world;
        InputDriver input;
        int a, b;
        readonly List<CombatEvent> log = new List<CombatEvent>();

        /// <summary>A faces +y, B stands <paramref name="distance"/> in front, facing <paramref name="defenderFacing"/> (default: towards A).</summary>
        void Build(int weaponA, int weaponB = TestCombat.OneHand, float distance = 1.25f, float2 defenderFacing = default)
        {
            setup ??= TestCombat.Setup();
            world?.Dispose();
            world = TestCombat.World(setup, 4);
            a = world.SpawnAgent(0, new float2(10f, 10f), 0.3f, new float2(0f, 1f), weaponA);
            b = world.SpawnAgent(1, new float2(10f, 10f + distance), 0.3f, math.lengthsq(defenderFacing) > 0f ? defenderFacing : new float2(0f, -1f), weaponB);
            world.AssignPlayerAgent(PlayerA, a);
            world.AssignPlayerAgent(PlayerB, b);
            input = new InputDriver(world);
            log.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            world?.Dispose();
            setup?.Dispose();
            world = null;
            setup = null;
        }

        CombatData C(int id) => world.Combat[id];

        CombatEvent FirstHitOnB(int weaponA, int direction)
        {
            Build(weaponA);
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, direction);
            input.RunUntil(120, log);
            Assert.That(log.Any(e => e.Type == CombatEventType.Hit && e.Target == b), $"weapon {weaponA} direction {direction}: no hit; events {string.Join(",", log.Select(e => e.Type))}");
            return log.First(e => e.Type == CombatEventType.Hit && e.Target == b);
        }

        [TestCase(AttackDirection.Overhead)]
        [TestCase(AttackDirection.Left)]
        [TestCase(AttackDirection.Right)]
        [TestCase(AttackDirection.Thrust)]
        public void TwoHand_EveryDirectionHits_HarderThanTheOneHander(int direction)
        {
            var one = FirstHitOnB(TestCombat.OneHand, direction);
            var two = FirstHitOnB(TestCombat.TwoHand, direction);
            TestContext.WriteLine($"direction {direction}: one-hand {one.Energy:0} J / {one.Damage:0.0} dmg (zone {one.Zone}), two-hand {two.Energy:0} J / {two.Damage:0.0} dmg (zone {two.Zone})");
            Assert.That(two.Energy, Is.GreaterThan(one.Energy));
        }

        [Test]
        public void TwoHand_PressesABlockerHarder()
        {
            float StaminaLost(int weaponA)
            {
                Build(weaponA);
                input.At(5, PlayerB, InputCommandType.CombatBlock, b, 1);
                input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Right);
                input.RunUntil(80, log);
                Assert.That(log.Any(e => e.Type == CombatEventType.Blocked || e.Type == CombatEventType.GuardBreak), $"events {string.Join(",", log.Select(e => e.Type))}");
                return setup.Tuning.StaminaMax - C(b).Stamina;
            }
            float one = StaminaLost(TestCombat.OneHand), two = StaminaLost(TestCombat.TwoHand);
            TestContext.WriteLine($"blocker stamina lost: one-hand {one:0.0}, two-hand {two:0.0}");
            Assert.That(two, Is.GreaterThan(one * 1.5f));
        }

        [TestCase(TestCombat.OneHand, true)]
        [TestCase(TestCombat.TwoHand, false)]
        public void Block_ShieldCoversWiderThanTheTwoHander(int defenderWeapon, bool blocked)
        {
            // The attacker stands 55° off the defender's facing: inside the shield's 120°, outside the weapon's 90°
            float angle = math.radians(55f);
            Build(TestCombat.OneHand, defenderWeapon, 1.2f, new float2(math.sin(angle), -math.cos(angle)));
            input.At(5, PlayerB, InputCommandType.CombatBlock, b, 1);
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Thrust);
            input.RunUntil(80, log);
            bool wasBlocked = log.Any(e => e.Type == CombatEventType.Blocked);
            Assert.That(wasBlocked, Is.EqualTo(blocked), $"events {string.Join(",", log.Select(e => e.Type))}");
        }

        [Test]
        public void Leap_ClosesTheGap_AndHitsHarderThanTheStandingOverhead()
        {
            var standing = FirstHitOnB(TestCombat.TwoHand, AttackDirection.Overhead);
            Build(TestCombat.TwoHand, TestCombat.OneHand, 2.6f);
            float2 start = world.Agents[a].Position;
            input.At(10, PlayerA, InputCommandType.CombatSpecial, a);
            input.RunUntil(150, log);
            Assert.That(log.Any(e => e.Type == CombatEventType.SpecialStart && e.Attacker == a && e.Direction == AttackDirection.Special));
            var hits = log.Where(e => e.Type == CombatEventType.Hit && e.Target == b).ToList();
            Assert.That(hits, Is.Not.Empty, $"leap hits from 2.6 m; events {string.Join(",", log.Select(e => e.Type))}");
            float moved = math.distance(world.Agents[a].Position, start);
            TestContext.WriteLine($"leap: moved {moved:0.00} m, {hits[0].Energy:0} J / {hits[0].Damage:0.0} dmg; standing overhead {standing.Energy:0} J");
            Assert.That(moved, Is.GreaterThan(1f));
            Assert.That(hits[0].Energy, Is.GreaterThan(standing.Energy));
        }

        [Test]
        public void Leap_StopsShortOfTheEnemy_AndCannotBeMorphed()
        {
            Build(TestCombat.TwoHand, TestCombat.OneHand, 1.25f);
            input.At(10, PlayerA, InputCommandType.CombatSpecial, a);
            input.At(12, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Left);
            input.RunUntil(13);
            Assert.That(C(a).Direction, Is.EqualTo(AttackDirection.Special), "the leap is committed");
            float closest = float.MaxValue;
            for (int t = 0; t < 80; t++)
            {
                world.Step();
                closest = math.min(closest, math.distance(world.Agents[a].Position, world.Agents[b].Position));
            }
            Assert.That(closest, Is.GreaterThan(0.6f + setup.Tuning.LungeStopGap - 0.05f), "no running through the enemy");
        }

        [Test]
        public void OneHandSpecial_IsStillTheShieldBash()
        {
            Build(TestCombat.OneHand);
            input.At(10, PlayerA, InputCommandType.CombatSpecial, a);
            input.RunUntil(12);
            Assert.That(C(a).State, Is.EqualTo(CombatState.Special));
        }

        [Test]
        public void WeaponSwitch_TakesItsTime_ChangesTheWeapon_AndBuffersAnAttack()
        {
            Build(TestCombat.OneHand);
            world.SetLoadout(a, new int4(TestCombat.OneHand, TestCombat.TwoHand, -1, -1));
            int switchTicks = setup.Tuning.WeaponSwitchTicks;
            input.At(10, PlayerA, InputCommandType.CombatSwitchWeapon, a, 1);
            input.RunUntil(12, log);
            Assert.That(C(a).State, Is.EqualTo(CombatState.Switching));
            Assert.That(C(a).WeaponId, Is.EqualTo(TestCombat.TwoHand));
            Assert.That(C(a).PreviousWeaponId, Is.EqualTo(TestCombat.OneHand));
            Assert.That(log.Count(e => e.Type == CombatEventType.WeaponSwitch), Is.EqualTo(1));

            // An attack pressed shortly before the switch ends starts right after it
            int end = 10 + switchTicks;
            input.At(end - 4, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            input.RunUntil(end - 1);
            Assert.That(C(a).State, Is.EqualTo(CombatState.Switching), "the switch is not cut short");
            input.RunUntil(end + 2);
            Assert.That(C(a).State, Is.EqualTo(CombatState.Windup), "buffered attack starts after the switch");
        }

        [Test]
        public void WeaponSwitch_IgnoresEmptyInvalidAndSameSlots()
        {
            Build(TestCombat.OneHand);
            world.SetLoadout(a, new int4(TestCombat.OneHand, TestCombat.TwoHand, -1, -1));
            input.At(10, PlayerA, InputCommandType.CombatSwitchWeapon, a, 0);
            input.At(11, PlayerA, InputCommandType.CombatSwitchWeapon, a, 2);
            input.At(12, PlayerA, InputCommandType.CombatSwitchWeapon, a, 9);
            input.At(13, PlayerA, InputCommandType.CombatSwitchWeapon, a, -1);
            input.At(14, PlayerB, InputCommandType.CombatSwitchWeapon, a, 1);
            input.RunUntil(20, log);
            Assert.That(C(a).State, Is.EqualTo(CombatState.Idle));
            Assert.That(C(a).WeaponId, Is.EqualTo(TestCombat.OneHand));
            Assert.That(log, Is.Empty);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => world.SetLoadout(a, new int4(-1, 0, -1, -1)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => world.SetLoadout(a, new int4(0, 7, -1, -1)));
        }

        static ulong TwoHandBotDuel(CombatSetup setup, ulong seed, List<CombatEvent> log)
        {
            using var w = TestCombat.World(setup, 2, 32, seed);
            w.SpawnAgent(0, new float2(12f, 16f), 0.3f, new float2(1f, 0f), TestCombat.TwoHand, 5);
            w.SpawnAgent(1, new float2(19f, 16f), 0.3f, new float2(-1f, 0f), TestCombat.OneHand, 3);
            for (int t = 0; t < 3600; t++)
            {
                w.Step();
                if (log != null) foreach (var e in w.LastEvents) log.Add(e);
            }
            return w.ComputeHash();
        }

        [Test]
        public void TwoHandBots_FightLeapAndStayDeterministic()
        {
            setup = TestCombat.Setup();
            var events = new List<CombatEvent>();
            int leaps = 0, hits = 0, deaths = 0;
            for (ulong seed = 1; seed <= 4; seed++)
            {
                events.Clear();
                ulong first = TwoHandBotDuel(setup, seed, events);
                Assert.That(TwoHandBotDuel(setup, seed, null), Is.EqualTo(first), $"seed {seed}");
                leaps += events.Count(e => e.Type == CombatEventType.SpecialStart && e.Attacker == 0);
                hits += events.Count(e => e.Type == CombatEventType.Hit);
                deaths += events.Count(e => e.Type == CombatEventType.Death);
            }
            TestContext.WriteLine($"L5 two-hand vs L3 one-hand, 4 x 60 s: {leaps} leaps, {hits} hits, {deaths} deaths");
            Assert.That(leaps, Is.GreaterThan(0));
            Assert.That(deaths, Is.GreaterThan(0));
        }
    }
}
