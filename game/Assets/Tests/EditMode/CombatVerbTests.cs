using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>Step, shield bash, sprint attack and attack tokens (CLAUDE.md 3.5, vocabulary and context D).</summary>
    public class CombatVerbTests
    {
        const ushort PlayerA = 0, PlayerB = 1;
        CombatSetup setup;
        SimWorld world;
        InputDriver input;
        int a, b;
        readonly List<CombatEvent> log = new List<CombatEvent>();

        void Build(float distance = 1.25f, int size = 32)
        {
            setup ??= TestCombat.Setup();
            world?.Dispose();
            world = TestCombat.World(setup, 8, size);
            a = world.SpawnAgent(0, new float2(10f, 10f), 0.3f, new float2(0f, 1f));
            b = world.SpawnAgent(1, new float2(10f, 10f + distance), 0.3f, new float2(0f, -1f));
            world.AssignPlayerAgent(PlayerA, a);
            world.AssignPlayerAgent(PlayerB, b);
            world.SetAgentMotion(a, 1.6f, 4f);
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

        [Test]
        public void Step_MovesItsDistance_CostsStamina_AndIsNotInterrupted()
        {
            Build();
            var rules = setup.Tuning;
            float2 start = world.Agents[a].Position;
            input.At(10, PlayerA, InputCommandType.CombatStep, a, StepDirection.Back);
            input.At(12, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            input.At(12, PlayerA, InputCommandType.CombatSpecial, a);
            input.RunUntil(14, log);
            Assert.That(C(a).State, Is.EqualTo(CombatState.Stepping), "attack and special do not cut a step short");
            Assert.That(log.Count(e => e.Type == CombatEventType.Step), Is.EqualTo(1));
            Assert.That(log.Any(e => e.Type == CombatEventType.SpecialStart), Is.False);

            input.RunUntil(11 + rules.StepTicks);
            float2 moved = world.Agents[a].Position - start;
            Assert.That(moved.y, Is.EqualTo(-rules.StepDistance).Within(0.15f), "steps back along the facing");
            Assert.That(math.abs(moved.x), Is.LessThan(0.1f));
            Assert.That(C(a).Stamina, Is.LessThanOrEqualTo(rules.StaminaMax - rules.StepStaminaCost + 0.01f));
            Assert.That(C(a).State, Is.Not.EqualTo(CombatState.Stepping), "step ends after its ticks");
        }

        [TestCase(StepDirection.Left, -1f, 0f)]
        [TestCase(StepDirection.Right, 1f, 0f)]
        public void Step_SidewaysIsRelativeToTheFacing(int direction, float x, float y)
        {
            Build(4f);
            float2 start = world.Agents[a].Position;
            input.At(10, PlayerA, InputCommandType.CombatStep, a, direction);
            input.RunUntil(11 + setup.Tuning.StepTicks);
            float2 moved = world.Agents[a].Position - start;
            Assert.That(math.dot(math.normalizesafe(moved), new float2(x, y)), Is.GreaterThan(0.98f));
        }

        [Test]
        public void ShieldBash_BreaksARaisedBlock()
        {
            Build();
            input.At(8, PlayerB, InputCommandType.CombatBlock, b, 1);
            input.At(10, PlayerA, InputCommandType.CombatSpecial, a);
            input.RunUntil(60, log);
            Assert.That(log.Any(e => e.Type == CombatEventType.GuardBreak && e.Attacker == a && e.Target == b), $"events: {string.Join(",", log.Select(e => e.Type))}");
            Assert.That(C(b).Health, Is.EqualTo(setup.Tuning.HealthMax), "a guard break hurts nothing");
            Assert.That(C(b).BlockHeld, Is.EqualTo(0));
        }

        [Test]
        public void ShieldBash_StaggersAnOpenEnemy_AndTheQuickThrustLandsBeforeTheGuardIsBack()
        {
            Build();
            var rules = setup.Tuning;
            input.At(10, PlayerA, InputCommandType.CombatSpecial, a);
            // A buffers a thrust just before the bash recovery ends; B mashes block from the moment it is hit
            int bashEnd = 10 + 1 + rules.SpecialWindupTicks + rules.SpecialActiveTicks + rules.SpecialRecoveryTicks;
            input.At(bashEnd - 3, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Thrust);
            for (int t = 11 + rules.SpecialWindupTicks; t < bashEnd + 60; t++) input.At(t, PlayerB, InputCommandType.CombatBlock, b, 1);
            input.RunUntil(bashEnd + 60, log);

            var bash = log.FirstOrDefault(e => e.Type == CombatEventType.Hit && e.Target == b);
            Assert.That(log.Any(e => e.Type == CombatEventType.Hit && e.Target == b), $"events: {string.Join(",", log.Select(e => e.Type))}");
            Assert.That(bash.Damage, Is.EqualTo(rules.SpecialDamage));
            var follow = log.Where(e => e.Type == CombatEventType.Hit && e.Target == b).Skip(1).FirstOrDefault();
            Assert.That(log.Count(e => e.Type == CombatEventType.Hit && e.Target == b), Is.EqualTo(2),
                $"the follow-up thrust lands; events: {string.Join(",", log.Select(e => e.Type))}");
            Assert.That(follow.Damage, Is.GreaterThan(rules.SpecialDamage));
        }

        [Test]
        public void ShieldBash_MissesBehindTheBack()
        {
            Build();
            var w = world;
            // Turn A away from B: the bash cone is in front only
            w.Enqueue(new InputCommand { Tick = 5, PlayerId = PlayerA, Sequence = 100, Type = InputCommandType.PlayerMove, IntArgs = new int4(a, 0, 0, 0), FloatArgs = new float4(0f, 0f, 0f, -1f) });
            input.At(40, PlayerA, InputCommandType.CombatSpecial, a);
            input.RunUntil(90, log);
            Assert.That(log.Any(e => e.Target == b && (e.Type == CombatEventType.Hit || e.Type == CombatEventType.GuardBreak)), Is.False);
        }

        /// <summary>First hit energy on B when A starts <paramref name="distance"/> away and attacks at <paramref name="attackTick"/>.</summary>
        float HitEnergy(float distance, bool sprint, int attackTick)
        {
            Build(distance);
            if (sprint)
                world.Enqueue(new InputCommand { Tick = 5, PlayerId = PlayerA, Sequence = 100, Type = InputCommandType.PlayerMove, IntArgs = new int4(a, 1, 0, 0), FloatArgs = new float4(0f, 1f, 0f, 1f) });
            input.At(attackTick, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Thrust);
            input.RunUntil(attackTick + 90, log);
            var hits = log.Where(e => e.Type == CombatEventType.Hit && e.Target == b).ToList();
            return hits.Count > 0 ? hits[0].Energy : 0f;
        }

        [Test]
        public void SprintAttack_HitsHarderThanStanding()
        {
            float standing = HitEnergy(1.25f, false, 10);
            Assert.That(standing, Is.GreaterThan(0f), "standing thrust connects");
            float best = 0f;
            for (int t = 10; t <= 150; t += 2) best = math.max(best, HitEnergy(5f, true, t));
            TestContext.WriteLine($"thrust energy standing {standing:0} J, from the sprint {best:0} J");
            Assert.That(best, Is.GreaterThan(standing * 1.1f), "the charge into the line is rewarded");
        }

        /// <summary>Largest distance B gets pushed from its start by A's overhead (B blocking or not).</summary>
        float PushOnB(bool block)
        {
            Build();
            float2 start = world.Agents[b].Position;
            if (block) input.At(5, PlayerB, InputCommandType.CombatBlock, b, 1);
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            float most = 0f;
            for (int t = 0; t < 90; t++)
            {
                world.Step();
                if (world.Tick > 6) most = math.max(most, world.Agents[b].Position.y - start.y);
            }
            return most;
        }

        [Test]
        public void Hit_PushesTheTargetBack_ABlockLess()
        {
            float hit = PushOnB(false), blocked = PushOnB(true);
            TestContext.WriteLine($"stagger push: hit {hit:0.00} m, blocked {blocked:0.00} m");
            Assert.That(hit, Is.InRange(0.15f, 0.8f), "a hit is a visible stagger step, not a launch");
            Assert.That(blocked, Is.GreaterThan(0.02f));
            Assert.That(blocked, Is.LessThan(hit));
        }

        [Test]
        public void ShieldBash_ShovesTheTarget()
        {
            Build();
            float2 start = world.Agents[b].Position;
            input.At(10, PlayerA, InputCommandType.CombatSpecial, a);
            input.RunUntil(80);
            Assert.That(world.Agents[b].Position.y - start.y, Is.GreaterThan(0.1f));
        }

        [Test]
        public void InputToVisibleWindup_IsWithinFiftyMilliseconds()
        {
            // Game feel spec: input -> visible windup start <= 50 ms. The duel's input delay plus one tick of motion must fit.
            var duel = UnityEditor.AssetDatabase.LoadAssetAtPath<Shieldbreak.Data.DuelScenarioDefinition>("Assets/_Project/Data/Scenarios/Duel_M1.asset");
            Assert.That(duel, Is.Not.Null);
            setup = TestCombat.Setup();
            var config = TestScenarios.Config(1, 32, 2);
            config.InputDelayTicks = duel.inputDelayTicks;
            world = new SimWorld(config, setup);
            a = world.SpawnAgent(0, new float2(10f, 10f), 0.3f, new float2(0f, 1f));
            world.AssignPlayerAgent(PlayerA, a);
            for (int t = 0; t < 5; t++) world.Step();
            world.Submit(PlayerA, InputCommandType.CombatAttack, new int4(a, AttackDirection.Overhead, 0, 0));
            int ticks = 0;
            while (!(C(a).State == CombatState.Windup && C(a).StateTick >= 1) && ticks < 10)
            {
                world.Step();
                ticks++;
            }
            float ms = ticks * SimConstants.DT * 1000f;
            TestContext.WriteLine($"input delay {duel.inputDelayTicks} ticks: weapon moves after {ticks} ticks = {ms:0} ms");
            Assert.That(ms, Is.LessThanOrEqualTo(50f));
        }

        [Test]
        public void Jump_LastsItsTicks_CostsStamina_BuffersInputs_AndKeepsRunning()
        {
            Build(40f, 128);
            var rules = setup.Tuning;
            world.SetAgentMotion(a, 1.6f, 4f);
            world.Enqueue(new InputCommand { Tick = 5, PlayerId = PlayerA, Sequence = 100, Type = InputCommandType.PlayerMove, IntArgs = new int4(a, 0, 0, 0), FloatArgs = new float4(0f, 1f, 0f, 1f) });
            input.RunUntil(40);
            float2 before = world.Agents[a].Position;
            input.At(40, PlayerA, InputCommandType.CombatJump, a);
            input.At(41 + rules.JumpTicks + rules.JumpLandTicks - 4, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            input.RunUntil(44, log);
            Assert.That(C(a).State, Is.EqualTo(CombatState.Jumping));
            Assert.That(log.Count(e => e.Type == CombatEventType.Jump), Is.EqualTo(1));
            Assert.That(C(a).Stamina, Is.LessThanOrEqualTo(rules.StaminaMax - rules.JumpStaminaCost + 0.01f));
            input.RunUntil(41 + rules.JumpTicks + rules.JumpLandTicks + 2);
            Assert.That(world.Agents[a].Position.y - before.y, Is.GreaterThan(0.5f), "keeps running while airborne");
            Assert.That(C(a).State, Is.EqualTo(CombatState.Windup), "the attack pressed in the air starts after landing");
        }

        [Test]
        public void Jump_LegsCannotBeHitWhileAirborne()
        {
            // A low side cut at the legs: the ground-level path of the right cut, lowered to shin height
            setup = TestCombat.Setup();
            var low = setup.Attacks[setup.Weapons[0].FirstAttack + AttackDirection.Right];
            var keys = setup.Keys;
            for (int k = low.FirstKey; k < low.FirstKey + low.KeyCount; k++)
            {
                var key = keys[k];
                key.Hand.y = 0.45f;
                key.Blade.y = 0f;
                keys[k] = key;
            }
            world = TestCombat.World(setup, 4);
            a = world.SpawnAgent(0, new float2(10f, 10f), 0.3f, new float2(0f, 1f));
            b = world.SpawnAgent(1, new float2(10f, 11.25f), 0.3f, new float2(0f, -1f));
            world.AssignPlayerAgent(PlayerA, a);
            world.AssignPlayerAgent(PlayerB, b);
            input = new InputDriver(world);

            int windup = low.Timing.WindupTicks;
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Right);
            input.RunUntil(80, log);
            var standing = log.Where(e => e.Type == CombatEventType.Hit && e.Target == b).ToList();
            Assert.That(standing, Is.Not.Empty, "the low cut connects with a standing target");
            Assert.That(standing[0].Zone, Is.EqualTo(BodyZone.Legs), "and hits the legs");

            log.Clear();
            input.At(100, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Right);
            input.At(100 + windup - 4, PlayerB, InputCommandType.CombatJump, b);
            input.RunUntil(180, log);
            Assert.That(log.Any(e => e.Type == CombatEventType.Jump && e.Attacker == b));
            Assert.That(log.Any(e => e.Type == CombatEventType.Hit && e.Target == b), Is.False, $"the jumper is not hit; events {string.Join(",", log.Select(e => e.Type))}");
        }

        [Test]
        public void Sprint_DrainsStamina_AndStopsWhenEmpty()
        {
            Build(40f, 128);
            world.Enqueue(new InputCommand { Tick = 5, PlayerId = PlayerA, Sequence = 100, Type = InputCommandType.PlayerMove, IntArgs = new int4(a, 1, 0, 0), FloatArgs = new float4(1f, 0f, 1f, 0f) });
            input.RunUntil(5 + 60);
            Assert.That(C(a).Sprinting, Is.EqualTo(1));
            Assert.That(C(a).Stamina, Is.LessThan(setup.Tuning.StaminaMax));
            input.RunUntil(5 + 60 * 20);
            Assert.That(C(a).Sprinting, Is.EqualTo(0), "an empty tank ends the sprint");
        }

        [Test]
        public void AttackTokens_AtMostTwoBotsSwingAtTheHeroAtOnce()
        {
            setup = TestCombat.Setup(t => t.healthMax = 100000f);
            world = TestCombat.World(setup, 8);
            a = world.SpawnAgent(0, new float2(16f, 16f), 0.3f, new float2(0f, 1f));
            world.AssignPlayerAgent(PlayerA, a);
            for (int k = 0; k < 5; k++)
            {
                float angle = k * math.PI * 2f / 5f;
                world.SpawnAgent(1, new float2(16f, 16f) + 2.5f * new float2(math.cos(angle), math.sin(angle)), 0.3f, new float2(-math.cos(angle), -math.sin(angle)), 0, 5);
            }
            int maxSwinging = 0, ticksWithTwo = 0;
            for (int t = 0; t < 1800; t++)
            {
                world.Step();
                int swinging = 0;
                for (int i = 1; i < world.Combat.Length; i++)
                {
                    var c = world.Combat[i];
                    if ((c.State == CombatState.Windup || c.State == CombatState.Release) && c.BotTarget == a) swinging++;
                }
                maxSwinging = math.max(maxSwinging, swinging);
                if (swinging >= 2) ticksWithTwo++;
            }
            TestContext.WriteLine($"max bots swinging at once: {maxSwinging}, ticks with two: {ticksWithTwo}");
            Assert.That(maxSwinging, Is.LessThanOrEqualTo(setup.Tuning.MaxAttackersPerTarget));
            Assert.That(ticksWithTwo, Is.GreaterThan(0), "the ring still presses with two at a time");
        }

        [Test]
        public void HigherBots_UseStepsAndSpecials()
        {
            setup = TestCombat.Setup();
            int steps = 0, specials = 0, specialsL1 = 0;
            for (ulong seed = 1; seed <= 4; seed++)
            {
                using var w = TestCombat.World(setup, 2, 32, seed);
                int strong = w.SpawnAgent(0, new float2(12f, 16f), 0.3f, new float2(1f, 0f), 0, 5);
                int weak = w.SpawnAgent(1, new float2(19f, 16f), 0.3f, new float2(-1f, 0f), 0, 1);
                for (int t = 0; t < 3600; t++)
                {
                    w.Step();
                    foreach (var e in w.LastEvents)
                    {
                        if (e.Type == CombatEventType.Step && e.Attacker == strong) steps++;
                        if (e.Type == CombatEventType.SpecialStart && e.Attacker == strong) specials++;
                        if (e.Type == CombatEventType.SpecialStart && e.Attacker == weak) specialsL1++;
                    }
                }
            }
            TestContext.WriteLine($"L5 vs L1, 4 minutes: {steps} steps, {specials} shield bashes by L5, {specialsL1} by L1");
            Assert.That(specials, Is.GreaterThan(0), "L5 bashes a blocking L1");
            Assert.That(specialsL1, Is.EqualTo(0), "L1 has no special technique");
        }
    }
}
