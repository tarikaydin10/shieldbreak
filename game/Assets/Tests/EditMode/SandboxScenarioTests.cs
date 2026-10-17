using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.Sim.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Shieldbreak.Tests
{
    public class SandboxScenarioTests
    {
        static SandboxScenarioDefinition Scenario()
        {
            var s = ScriptableObject.CreateInstance<SandboxScenarioDefinition>();
            s.mapWidth = 64;
            s.mapHeight = 64;
            s.groups = new[]
            {
                new SandboxScenarioDefinition.SpawnGroup { team = 0, count = 12, files = 5, front = new Vector2(20f, 32f), faceTowards = new Vector2(44f, 32f) },
                new SandboxScenarioDefinition.SpawnGroup { team = 1, count = 7, files = 3, front = new Vector2(44f, 32f), faceTowards = new Vector2(20f, 32f) },
            };
            return s;
        }

        [Test]
        public void Spawn_CreatesEveryGroupOnTheMapFacingTheEnemy()
        {
            var s = Scenario();
            var movement = ScriptableObject.CreateInstance<AgentMovementDefinition>();
            try
            {
                using var world = new SimWorld(s.ToConfig(movement.ToSettings()));
                s.Spawn(world);
                Assert.That(world.AgentCount, Is.EqualTo(19));
                for (int i = 0; i < world.AgentCount; i++)
                {
                    var a = world.Agents[i];
                    Assert.That(a.Team, Is.EqualTo(i < 12 ? 0 : 1));
                    Assert.That(math.all(a.Position > 0f) && math.all(a.Position < 64f), $"agent {i} at {a.Position}");
                    Assert.That(a.Facing.x, Is.EqualTo(a.Team == 0 ? 1f : -1f).Within(1e-5f));
                }
            }
            finally
            {
                Object.DestroyImmediate(s);
                Object.DestroyImmediate(movement);
            }
        }

        [Test]
        public void SameScenario_GivesSameHashAfterMarching()
        {
            var s = Scenario();
            var movement = ScriptableObject.CreateInstance<AgentMovementDefinition>();
            try
            {
                ulong Run()
                {
                    using var world = new SimWorld(s.ToConfig(movement.ToSettings()));
                    s.Spawn(world);
                    world.Submit(0, InputCommandType.SetTargetRange, new int4(0, 12, 0, 0), new float4(40f, 30f, 0f, 0f));
                    for (int t = 0; t < 600; t++) world.Step();
                    return world.ComputeHash();
                }
                Assert.That(Run(), Is.EqualTo(Run()));
            }
            finally
            {
                Object.DestroyImmediate(s);
                Object.DestroyImmediate(movement);
            }
        }
    }
}
