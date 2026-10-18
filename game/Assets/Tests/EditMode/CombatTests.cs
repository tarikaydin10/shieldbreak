using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>The combat grammar rules (CLAUDE.md 3.5) on a two-soldier setup: attacker A (player 0) faces defender B (player 1).</summary>
    public class CombatTests
    {
        const ushort PlayerA = 0, PlayerB = 1;
        CombatSetup setup;
        SimWorld world;
        InputDriver input;
        int a, b;
        readonly List<CombatEvent> log = new List<CombatEvent>();

        void Build(System.Action<Shieldbreak.Data.CombatTuningDefinition> tune = null, bool defenderFacesAway = false)
        {
            setup = TestCombat.Setup(tune);
            world = TestCombat.World(setup, 4);
            a = world.SpawnAgent(0, new float2(10f, 10f), 0.3f, new float2(0f, 1f));
            b = world.SpawnAgent(1, new float2(10f, 11.25f), 0.3f, new float2(0f, defenderFacesAway ? 1f : -1f));
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
        float MaxHealth => setup.Tuning.HealthMax;

        [Test]
        public void SimStructs_HaveNoPadding()
        {
            Assert.That(UnsafeUtility.SizeOf<AgentData>(), Is.EqualTo(72));
            Assert.That(UnsafeUtility.SizeOf<CombatData>(), Is.EqualTo(152));
        }

        [Test]
        public void Attack_FollowsWindupReleaseRecovery_TickExact()
        {
            Build();
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Thrust);
            input.RunUntil(11);
            var timing = setup.Attacks[setup.Weapons[0].FirstAttack + AttackDirection.Thrust].Timing;
            Assert.That(C(a).State, Is.EqualTo(CombatState.Windup));
            int seen = 0;
            while (C(a).State != CombatState.Idle && seen < 200)
            {
                var phase = timing.PhaseAt(C(a).StateTick);
                int expected = phase == AttackPhase.Windup ? CombatState.Windup : phase == AttackPhase.Release ? CombatState.Release : CombatState.Recovery;
                if (C(a).State != CombatState.Stunned) Assert.That(C(a).State, Is.EqualTo(expected), $"StateTick {C(a).StateTick}");
                world.Step();
                seen++;
            }
            Assert.That(seen, Is.EqualTo(timing.TotalTicks - 1), "attack lasts exactly its total ticks");
        }

        [TestCase(AttackDirection.Overhead)]
        [TestCase(AttackDirection.Left)]
        [TestCase(AttackDirection.Right)]
        [TestCase(AttackDirection.Thrust)]
        public void EveryDirection_HitsAStandingEnemy(int direction)
        {
            Build();
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, direction);
            input.RunUntil(80, log);
            var hit = log.FirstOrDefault(e => e.Type == CombatEventType.Hit && e.Target == b);
            Assert.That(log.Any(e => e.Type == CombatEventType.Hit && e.Target == b), $"no hit; events: {string.Join(",", log.Select(e => e.Type))}");
            Assert.That(C(b).Health, Is.LessThan(MaxHealth));
            TestContext.WriteLine($"direction {direction}: zone {hit.Zone}, effective energy {hit.Energy:0} J, damage {hit.Damage:0.0}");
            if (direction == AttackDirection.Overhead) Assert.That(hit.Zone, Is.Not.EqualTo(BodyZone.Legs));
        }

        [Test]
        public void Block_InFrontArc_StopsDamage_CostsStamina_BouncesAttacker()
        {
            Build();
            input.At(8, PlayerB, InputCommandType.CombatBlock, b, 1);
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            input.RunUntil(80, log);
            Assert.That(log.Any(e => e.Type == CombatEventType.Blocked), $"events: {string.Join(",", log.Select(e => e.Type))}");
            Assert.That(C(b).Health, Is.EqualTo(MaxHealth));
            Assert.That(C(b).Stamina, Is.LessThan(setup.Tuning.StaminaMax));
        }

        [Test]
        public void Block_FromBehind_DoesNotProtect()
        {
            Build(defenderFacesAway: true);
            input.At(8, PlayerB, InputCommandType.CombatBlock, b, 1);
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            input.RunUntil(80, log);
            Assert.That(log.Any(e => e.Type == CombatEventType.Hit && e.Target == b));
            Assert.That(C(b).Health, Is.LessThan(MaxHealth));
        }

        [Test]
        public void PerfectBlock_CostsNothing_AndStunsTheAttacker()
        {
            Build();
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            // Windup is 24 ticks: the block lands a few ticks before the release reaches the defender
            input.At(10 + 22, PlayerB, InputCommandType.CombatBlock, b, 1);
            input.RunUntil(80, log);
            Assert.That(log.Any(e => e.Type == CombatEventType.PerfectBlock), $"events: {string.Join(",", log.Select(e => e.Type))}");
            Assert.That(C(b).Stamina, Is.EqualTo(setup.Tuning.StaminaMax));
            Assert.That(C(b).Health, Is.EqualTo(MaxHealth));
            var stun = log.First(e => e.Type == CombatEventType.PerfectBlock);
            Assert.That(stun.Attacker, Is.EqualTo(a));
        }

        [Test]
        public void GuardBreak_WhenBlockEmptiesStamina()
        {
            Build(t => t.blockStaminaPerJoule = 10f);
            input.At(8, PlayerB, InputCommandType.CombatBlock, b, 1);
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            input.RunUntil(60, log);
            Assert.That(log.Any(e => e.Type == CombatEventType.GuardBreak));
            Assert.That(C(b).State, Is.EqualTo(CombatState.Stunned));
            Assert.That(C(b).BlockHeld, Is.EqualTo(0));
        }

        [Test]
        public void Feint_WorksEarly_IsIgnoredLate()
        {
            Build();
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            input.At(14, PlayerA, InputCommandType.CombatFeint, a);
            input.RunUntil(16, log);
            Assert.That(C(a).State, Is.EqualTo(CombatState.Idle));
            Assert.That(log.Count(e => e.Type == CombatEventType.Feint), Is.EqualTo(1));

            input.At(30, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            input.At(30 + 20, PlayerA, InputCommandType.CombatFeint, a);   // 20/24 of the windup: too late
            input.RunUntil(52, log);
            Assert.That(C(a).State, Is.Not.EqualTo(CombatState.Idle));
            Assert.That(log.Count(e => e.Type == CombatEventType.Feint), Is.EqualTo(1));
        }

        [Test]
        public void Morph_ChangesDirectionEarlyInWindup()
        {
            Build();
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            input.At(13, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Left);
            input.RunUntil(15);
            Assert.That(C(a).Direction, Is.EqualTo(AttackDirection.Left));
            Assert.That(C(a).State, Is.EqualTo(CombatState.Windup));
        }

        [Test]
        public void HeavyHold_MorphsALightSideCutIntoOverhead_EvenAtThirtyFps()
        {
            // Simple control scheme: the light cut starts at once, holding sends Overhead ~0.1 s later. At 30 fps the
            // hold is detected up to 8 ticks after the press; that must still be inside the morph window.
            Build();
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Right);
            input.At(18, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Overhead);
            input.RunUntil(20, log);
            Assert.That(C(a).Direction, Is.EqualTo(AttackDirection.Overhead));
            Assert.That(log.Count(e => e.Type == CombatEventType.AttackStart && e.Attacker == a), Is.EqualTo(1), "one attack, morphed");
        }

        [Test]
        public void InputBuffer_AttackDuringRecovery_StartsWhenFree()
        {
            Build();
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Thrust);
            var timing = setup.Attacks[setup.Weapons[0].FirstAttack + AttackDirection.Thrust].Timing;
            int idleAt = 10 + timing.TotalTicks;
            input.At(idleAt - 4, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Left);
            input.RunUntil(idleAt + 3, log);
            Assert.That(log.Count(e => e.Type == CombatEventType.AttackStart && e.Attacker == a), Is.EqualTo(2));
            Assert.That(C(a).Direction, Is.EqualTo(AttackDirection.Left));
        }

        [Test]
        public void PlayerMove_WalksWhileLookingElsewhere_AndKeepsWalkingWithoutNewCommands()
        {
            Build();
            world.SetAgentMotion(a, 1.6f, 4f);
            // Walk +x while looking +y (strafing), one command only
            world.Enqueue(new InputCommand { Tick = 10, PlayerId = PlayerA, Sequence = 0, Type = InputCommandType.PlayerMove, IntArgs = new int4(a, 0, 0, 0), FloatArgs = new float4(1f, 0f, 0f, 1f) });
            for (int t = 0; t < 130; t++) world.Step();
            var agent = world.Agents[a];
            Assert.That(agent.Position.x, Is.GreaterThan(13f), "keeps walking from a single command");
            Assert.That(math.dot(agent.Facing, new float2(0f, 1f)), Is.GreaterThan(0.99f), "faces the look direction, not the walk direction");

            world.Enqueue(new InputCommand { Tick = 140, PlayerId = PlayerA, Sequence = 1, Type = InputCommandType.PlayerMove, IntArgs = new int4(a, 0, 0, 0), FloatArgs = new float4(0f, 0f, 0f, 1f) });
            for (int t = 0; t < 40; t++) world.Step();
            Assert.That(math.length(world.Agents[a].Velocity), Is.LessThan(0.05f), "stops when the intent is zero");

            // Foreign or broken commands change nothing
            float2 before = world.Agents[b].Position;
            world.Enqueue(new InputCommand { Tick = 180, PlayerId = PlayerA, Sequence = 2, Type = InputCommandType.PlayerMove, IntArgs = new int4(b, 0, 0, 0), FloatArgs = new float4(1f, 0f, 0f, 1f) });
            world.Enqueue(new InputCommand { Tick = 180, PlayerId = PlayerB, Sequence = 0, Type = InputCommandType.PlayerMove, IntArgs = new int4(b, 0, 0, 0), FloatArgs = new float4(float.NaN, 0f, 0f, 1f) });
            for (int t = 0; t < 60; t++) world.Step();
            Assert.That(math.distance(world.Agents[b].Position, before), Is.LessThan(0.05f));
        }

        [Test]
        public void CommandsForForeignAgents_AreIgnored()
        {
            Build();
            input.At(10, PlayerA, InputCommandType.CombatAttack, b, AttackDirection.Overhead);
            input.At(10, PlayerA, InputCommandType.CombatAttack, 99, AttackDirection.Overhead);
            input.RunUntil(20, log);
            Assert.That(C(b).State, Is.EqualTo(CombatState.Idle));
            Assert.That(log, Is.Empty);
        }

        [Test]
        public void LethalHit_KillsAndStopsTheBody()
        {
            Build(t => t.damagePerJoule = 50f);
            input.At(10, PlayerA, InputCommandType.CombatAttack, a, AttackDirection.Thrust);
            input.RunUntil(120, log);
            Assert.That(log.Any(e => e.Type == CombatEventType.Death && e.Target == b));
            Assert.That(C(b).State, Is.EqualTo(CombatState.Dead));
            Assert.That(world.Agents[b].IsDead);
            Assert.That(math.length(world.Agents[b].Velocity), Is.EqualTo(0f));
        }

        static ulong RunBotDuel(CombatSetup setup, int levelA, int levelB, int ticks, List<CombatEvent> log)
        {
            using var w = TestCombat.World(setup, 2, 32, 7);
            w.SpawnAgent(0, new float2(12f, 16f), 0.3f, new float2(1f, 0f), 0, levelA);
            w.SpawnAgent(1, new float2(19f, 16f), 0.3f, new float2(-1f, 0f), 0, levelB);
            for (int t = 0; t < ticks; t++)
            {
                w.Step();
                if (log != null) foreach (var e in w.LastEvents) log.Add(e);
            }
            return w.ComputeHash();
        }

        /// <summary>Returns 0 or 1 for the winning level slot (A or B), -1 if nobody died.</summary>
        static int DuelWinner(CombatSetup setup, int levelA, int levelB, ulong seed, bool swapSides, out int hits, out int blocks)
        {
            using var w = TestCombat.World(setup, 2, 32, seed);
            int idA = w.SpawnAgent(0, new float2(swapSides ? 19f : 12f, 16f), 0.3f, new float2(swapSides ? -1f : 1f, 0f), 0, levelA);
            int idB = w.SpawnAgent(1, new float2(swapSides ? 12f : 19f, 16f), 0.3f, new float2(swapSides ? 1f : -1f, 0f), 0, levelB);
            hits = blocks = 0;
            for (int t = 0; t < 3600; t++)
            {
                w.Step();
                foreach (var e in w.LastEvents)
                {
                    if (e.Type == CombatEventType.Hit) hits++;
                    if (e.Type == CombatEventType.Blocked || e.Type == CombatEventType.PerfectBlock) blocks++;
                    if (e.Type == CombatEventType.Death) return e.Target == idB ? 0 : 1;
                }
            }
            return -1;
        }

        [TestCase(5, 1)]
        [TestCase(5, 3)]
        [TestCase(3, 1)]
        public void HigherBotLevel_WinsMoreDuels(int strong, int weak)
        {
            setup = TestCombat.Setup();
            int strongWins = 0, weakWins = 0, draws = 0, hits = 0, blocks = 0;
            for (ulong seed = 1; seed <= 12; seed++)
            {
                int winner = DuelWinner(setup, strong, weak, seed, seed % 2 == 0, out int h, out int b);
                hits += h;
                blocks += b;
                if (winner == 0) strongWins++;
                else if (winner == 1) weakWins++;
                else draws++;
            }
            TestContext.WriteLine($"L{strong} vs L{weak}, 12 duels of 60 s: {strongWins} : {weakWins}, {draws} undecided, {hits} hits, {blocks} blocks");
            Assert.That(strongWins, Is.GreaterThan(weakWins));
        }

        [Test]
        public void BotDuel_IsDeterministic_AndBotsActuallyFight()
        {
            setup = TestCombat.Setup();
            var events = new List<CombatEvent>();
            ulong first = RunBotDuel(setup, 5, 3, 3600, events);
            ulong second = RunBotDuel(setup, 5, 3, 3600, null);
            Assert.That(second, Is.EqualTo(first));

            int attacks = events.Count(e => e.Type == CombatEventType.AttackStart);
            int hits = events.Count(e => e.Type == CombatEventType.Hit);
            int blocks = events.Count(e => e.Type == CombatEventType.Blocked || e.Type == CombatEventType.PerfectBlock);
            var death = events.FirstOrDefault(e => e.Type == CombatEventType.Death);
            TestContext.WriteLine($"L5 vs L3, 60 s: {attacks} attacks, {hits} hits, {blocks} blocks " +
                                  $"({events.Count(e => e.Type == CombatEventType.PerfectBlock)} perfect), " +
                                  $"{events.Count(e => e.Type == CombatEventType.Feint)} feints, winner: " +
                                  (events.Any(e => e.Type == CombatEventType.Death) ? (death.Attacker == 0 ? "L5" : "L3") : "none"));
            Assert.That(attacks, Is.GreaterThan(5), "bots must attack");
            Assert.That(hits + blocks, Is.GreaterThan(0), "swings must connect");
        }
    }
}
