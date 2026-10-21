using System.Collections.Generic;
using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shieldbreak.Tests
{
    /// <summary>
    /// The hero while the player is in the command view (CLAUDE.md 3.9, M2 task 7): entering and leaving are sim
    /// inputs; meanwhile the hero falls back behind its own line, defends itself at the configured bot level and
    /// ignores the player's commands; taking it back leaves nothing of the bot behind.
    /// </summary>
    public class HeroUnattendedTests
    {
        const ushort Player = CompanyScenarioDefinition.PlayerId;
        readonly List<Object> created = new List<Object>();
        CompanyScenarioDefinition scenario;
        AgentMovementDefinition movement;

        [SetUp]
        public void SetUp()
        {
            scenario = ScriptableObject.CreateInstance<CompanyScenarioDefinition>();
            scenario.ApplyDefaultLayout();
            scenario.weapons = TestCombat.Weapons(created, out var tuning);
            scenario.combatTuning = tuning;
            scenario.morale = ScriptableObject.CreateInstance<MoraleDefinition>();
            movement = ScriptableObject.CreateInstance<AgentMovementDefinition>();
            created.Add(scenario);
            created.Add(scenario.morale);
            created.Add(movement);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        SimWorld Company(out int hero) => scenario.CreateWorld(movement.ToSettings(), out hero);

        static void Leave(SimWorld world, int hero, bool unattended) =>
            world.Submit(Player, InputCommandType.HeroUnattended, new int4(hero, unattended ? 1 : 0, 0, 0));

        static void Run(SimWorld world, int ticks, List<CombatEvent> log = null)
        {
            for (int t = 0; t < ticks; t++)
            {
                world.Step();
                if (log != null) foreach (var e in world.LastEvents) log.Add(e);
            }
        }

        /// <summary>Where the hero should stand: behind the rear rank of formation <paramref name="f"/>.</summary>
        static float2 PostBehind(SimWorld world, int f)
        {
            var line = world.Formations[f];
            var members = world.FormationMembers;
            float rear = 0f;
            for (int i = 0; i < members.Length; i++)
                if (members[i].Formation == f) rear = math.min(rear, members[i].Local.y);
            return line.ToWorld(new float2(0f, rear - world.FormationSettings.HeroRetreatGap));
        }

        [Test]
        public void Unattended_HeroFallsBackBehindItsLine_AndIgnoresThePlayer()
        {
            using var world = Company(out int hero);
            Assert.That(world.HasCommandView);
            Leave(world, hero, true);
            // Walking south and swinging are ignored while the player is in the command view
            world.Submit(Player, InputCommandType.PlayerMove, new int4(hero, 1, 0, 0), new float4(0f, -1f, 0f, -1f));
            world.Submit(Player, InputCommandType.CombatAttack, new int4(hero, AttackDirection.Overhead, 0, 0));
            Run(world, scenario.inputDelayTicks + 1);
            Assert.That(world.IsUnattended(Player));
            Assert.That(world.Agents[hero].IsUnattended);
            Assert.That(world.Combat[hero].BotProfile, Is.EqualTo(world.FormationSettings.HeroUnattendedLevel), "the bot brain fights at the configured level");
            Assert.That(world.Combat[hero].State, Is.Not.EqualTo(CombatState.Windup), "the attack command was ignored");

            Run(world, 360);
            // Both own formations stand 8 m from the hero; the first (the line) wins the tie
            float2 post = PostBehind(world, 0);
            Assert.That(math.distance(world.Agents[hero].Position, post), Is.LessThan(1.5f), $"hero at {world.Agents[hero].Position}, post {post}");
            Assert.That(math.dot(world.Agents[hero].Facing, world.Formations[0].Facing), Is.GreaterThan(0.7f), "faces the front of its line");
        }

        [Test]
        public void TakenBack_TheHeroObeysThePlayerAgain()
        {
            using var world = Company(out int hero);
            Leave(world, hero, true);
            Run(world, 120);
            Leave(world, hero, false);
            Run(world, scenario.inputDelayTicks + 1);
            Assert.That(world.IsUnattended(Player), Is.False);
            var a = world.Agents[hero];
            var c = world.Combat[hero];
            Assert.That(a.IsUnattended, Is.False);
            Assert.That(c.BotProfile, Is.EqualTo(0), "nothing of the bot stays");
            Assert.That(c.BlockHeld, Is.EqualTo(0));

            float2 before = world.Agents[hero].Position;
            for (int t = 0; t < 120; t++)
            {
                if (t % 30 == 0) world.Submit(Player, InputCommandType.PlayerMove, new int4(hero, 0, 0, 0), new float4(1f, 0f, 1f, 0f));
                world.Step();
            }
            Assert.That(world.Agents[hero].Position.x - before.x, Is.GreaterThan(2f), "walks where the player says");
        }

        [Test]
        public void Unattended_HeroDefendsItself()
        {
            // No own formation to fall back to, so the hero holds where it was left; an L3 enemy right in front.
            // Counts the hero's blocks and own swings. (With a line behind it the hero gets away: the retreat is faster
            // than the enemy closes in, and the enemy runs into the line instead, see the test above.)
            int Defences(bool unattended)
            {
                using var setup = TestCombat.Setup();
                var config = TestScenarios.Config(5, 40, 4);
                config.Formations = TestScenarios.Formations;
                using var world = new SimWorld(config, setup);
                int hero = world.SpawnAgent(0, new float2(20f, 20f), 0.3f, new float2(0f, 1f));
                world.AssignPlayerAgent(Player, hero);
                world.SpawnAgent(1, new float2(20f, 21.5f), 0.3f, new float2(0f, -1f), TestCombat.OneHand, 3);
                if (unattended) Leave(world, hero, true);
                var log = new List<CombatEvent>();
                Run(world, 6 * 60, log);
                if (unattended) Assert.That(math.distance(world.Agents[hero].Position, new float2(20f, 20f)), Is.LessThan(2.5f), "holds its ground (leash)");
                return log.FindAll(e => (e.Target == hero && (e.Type == CombatEventType.Blocked || e.Type == CombatEventType.PerfectBlock)) ||
                                        (e.Attacker == hero && e.Type == CombatEventType.AttackStart)).Count;
            }
            Assert.That(Defences(false), Is.EqualTo(0), "a hero nobody plays does nothing");
            Assert.That(Defences(true), Is.GreaterThan(0), "the unattended hero blocks or strikes back");
        }

        [Test]
        public void Unattended_FollowedHeroHolds_InsteadOfBackingAwayWithItsTroops()
        {
            using var world = Company(out int hero);
            foreach (int f in new[] { 0, 1 })
            {
                Assert.That(QuickOrders.TryBuild(QuickCommand.Follow, f, hero, out var order));
                world.SubmitFormationOrder(Player, order);
            }
            Leave(world, hero, true);
            Run(world, scenario.inputDelayTicks + 1);
            float2 start = world.Agents[hero].Position;
            Run(world, 600);
            Assert.That(math.distance(world.Agents[hero].Position, start), Is.LessThan(2f), "no retreat chain with the formations that follow it");
        }

        [Test]
        public void Ignored_WithoutCommandView_OrForAnotherPlayersHero()
        {
            using var setup = TestCombat.Setup();
            var config = TestScenarios.Config(1, 32, 4);
            using var world = new SimWorld(config, setup);
            int hero = world.SpawnAgent(0, new float2(10f, 10f), 0.3f);
            int other = world.SpawnAgent(1, new float2(20f, 20f), 0.3f);
            world.AssignPlayerAgent(Player, hero);
            world.AssignPlayerAgent(1, other);
            Assert.That(world.HasCommandView, Is.False, "no hero level configured (duel)");
            Leave(world, hero, true);
            Run(world, config.InputDelayTicks + 1);
            Assert.That(world.IsUnattended(Player), Is.False);
            Assert.That(world.Combat[hero].BotProfile, Is.EqualTo(0));

            using var company = Company(out int companyHero);
            // Player 1 tries to leave player 0's hero
            company.Submit(1, InputCommandType.HeroUnattended, new int4(companyHero, 1, 0, 0));
            Run(company, scenario.inputDelayTicks + 1);
            Assert.That(company.IsUnattended(Player), Is.False);
            Assert.That(company.Agents[companyHero].IsUnattended, Is.False);
        }

        [Test]
        public void SameInputs_SameHash_AndTheCommandViewChangesIt()
        {
            ulong Battle(bool commandView)
            {
                using var world = Company(out int hero);
                for (int t = 0; t < 1800; t++)
                {
                    if (commandView && t == 60) Leave(world, hero, true);
                    if (commandView && t == 900) Leave(world, hero, false);
                    world.Step();
                }
                return world.ComputeHash();
            }
            Assert.That(Battle(true), Is.EqualTo(Battle(true)));
            Assert.That(Battle(true), Is.Not.EqualTo(Battle(false)));
        }
    }
}
