using System.Collections.Generic;
using NUnit.Framework;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>The spear (CLAUDE.md 3.5): reach, a poor block, the sweep, and bots that use all three.</summary>
    public class PolearmTests
    {
        const ushort PlayerA = 0, PlayerB = 1;
        CombatSetup setup;
        SimWorld world;
        InputDriver input;
        readonly List<CombatEvent> log = new List<CombatEvent>();

        void Build(int weaponA, int weaponB, float distance, int maxAgents = 8)
        {
            setup = TestCombat.Setup();
            world = TestCombat.World(setup, maxAgents);
            int a = world.SpawnAgent(0, new float2(10f, 10f), 0.3f, new float2(0f, 1f), weaponA);
            int b = world.SpawnAgent(1, new float2(10f, 10f + distance), 0.3f, new float2(0f, -1f), weaponB);
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

        bool ThrustHitsAt(int weapon, float distance)
        {
            Build(weapon, TestCombat.OneHand, distance);
            input.At(10, PlayerA, InputCommandType.CombatAttack, 0, AttackDirection.Thrust);
            input.RunUntil(120, log);
            bool hit = log.Exists(e => e.Type == CombatEventType.Hit && e.Target == 1);
            TearDown();
            return hit;
        }

        [Test]
        public void Spear_ReachesFurtherThanASword()
        {
            Assert.That(ThrustHitsAt(TestCombat.Polearm, 1.9f), "the spear reaches 1.9 m");
            Assert.That(ThrustHitsAt(TestCombat.OneHand, 1.9f), Is.False, "the sword does not");
        }

        [Test]
        public void Spear_Thrust_Wounds_ButDoesNotKillAtOneBlow()
        {
            Build(TestCombat.Polearm, TestCombat.OneHand, 1.8f);
            input.At(10, PlayerA, InputCommandType.CombatAttack, 0, AttackDirection.Thrust);
            input.RunUntil(120, log);
            var hit = log.Find(e => e.Type == CombatEventType.Hit && e.Target == 1);
            TestContext.WriteLine($"thrust at 1.8 m: {hit.Energy:0} J, {hit.Damage:0} damage in zone {hit.Zone}");
            Assert.That(hit.Damage, Is.GreaterThan(5f), "it goes in");
            Assert.That(world.Combat[1].Health, Is.GreaterThan(0f));
        }

        float StaminaLostBlocking(int defenderWeapon)
        {
            Build(TestCombat.OneHand, defenderWeapon, 1.25f);
            input.At(4, PlayerB, InputCommandType.CombatBlock, 1, 1);
            input.At(30, PlayerA, InputCommandType.CombatAttack, 0, AttackDirection.Thrust);
            input.RunUntil(29, log);
            float before = world.Combat[1].Stamina;
            input.RunUntil(100, log);
            float lost = before - world.Combat[1].Stamina;
            Assert.That(log.Exists(e => e.Type == CombatEventType.Hit && e.Target == 1), Is.False, "the block holds");
            TearDown();
            return lost;
        }

        [Test]
        public void Spear_IsAPoorShield()
        {
            float sword = StaminaLostBlocking(TestCombat.TwoHand);
            float spear = StaminaLostBlocking(TestCombat.Polearm);
            TestContext.WriteLine($"stamina for one blocked thrust: two-hand {sword:0.0}, spear {spear:0.0}");
            Assert.That(spear, Is.GreaterThan(sword * 1.3f));
        }

        [Test]
        public void Sweep_StrikesEveryoneInTheArc()
        {
            Build(TestCombat.Polearm, TestCombat.OneHand, 1.5f);
            world.SpawnAgent(1, new float2(8.9f, 11f), 0.3f, new float2(0.7f, -0.7f));
            world.SpawnAgent(1, new float2(11.1f, 11f), 0.3f, new float2(-0.7f, -0.7f));
            input.At(10, PlayerA, InputCommandType.CombatSpecial, 0);
            input.RunUntil(150, log);
            var struck = new HashSet<int>();
            foreach (var e in log)
                if (e.Type == CombatEventType.Hit && e.Attacker == 0) struck.Add(e.Target);
            Assert.That(struck.Count, Is.EqualTo(3), "all three in one sweep");
        }

        [Test]
        public void Sweep_LeavesTheWielderOpenLongerThanAThrust()
        {
            setup = TestCombat.Setup();
            var thrust = setup.Attacks[setup.Weapons[TestCombat.Polearm].AttackIndex(AttackDirection.Thrust)].Timing;
            var sweep = setup.Attacks[setup.Weapons[TestCombat.Polearm].AttackIndex(AttackDirection.Special)].Timing;
            Assert.That(sweep.RecoveryTicks, Is.GreaterThan(thrust.RecoveryTicks * 1.5f));
        }

        [Test]
        public void Bots_FightFromFurtherAway_WithASpear()
        {
            float FirstHitDistance(int weapon)
            {
                setup = TestCombat.Setup();
                world = TestCombat.World(setup, 4);
                world.SpawnAgent(0, new float2(16f, 11f), 0.3f, new float2(0f, 1f), weapon, 3);
                world.SpawnAgent(1, new float2(16f, 16f), 0.3f, new float2(0f, -1f), TestCombat.OneHand, 1);
                float distance = 0f;
                for (int t = 0; t < 1800 && distance == 0f; t++)
                {
                    world.Step();
                    foreach (var e in world.LastEvents)
                        if (e.Attacker == 0 && e.Target == 1 && (e.Type == CombatEventType.Hit || e.Type == CombatEventType.Blocked)) distance = math.distance(world.Agents[0].Position, world.Agents[1].Position);
                }
                TearDown();
                return distance;
            }

            float spear = FirstHitDistance(TestCombat.Polearm);
            float sword = FirstHitDistance(TestCombat.OneHand);
            TestContext.WriteLine($"first hit from {spear:0.00} m with the spear, {sword:0.00} m with the sword");
            Assert.That(spear, Is.GreaterThan(sword + 0.3f));
        }

        [Test]
        public void SameSeed_SameSpears_SameHash()
        {
            ulong Fight()
            {
                setup = TestCombat.Setup();
                world = TestCombat.World(setup, 6);
                world.SpawnAgent(0, new float2(14f, 12f), 0.3f, new float2(0f, 1f), TestCombat.Polearm, 3);
                world.SpawnAgent(0, new float2(18f, 12f), 0.3f, new float2(0f, 1f), TestCombat.Polearm, 5);
                world.SpawnAgent(1, new float2(14f, 18f), 0.3f, new float2(0f, -1f), TestCombat.OneHand, 3);
                world.SpawnAgent(1, new float2(18f, 18f), 0.3f, new float2(0f, -1f), TestCombat.TwoHand, 5);
                for (int t = 0; t < 1800; t++) world.Step();
                ulong hash = world.ComputeHash();
                TearDown();
                return hash;
            }

            Assert.That(Fight(), Is.EqualTo(Fight()));
        }
    }
}
