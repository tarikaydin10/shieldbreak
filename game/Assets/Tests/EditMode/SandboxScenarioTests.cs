using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
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
                new SandboxScenarioDefinition.SpawnGroup { team = 1, count = 7, files = 3, front = new Vector2(44f, 32f), faceTowards = new Vector2(20f, 32f),
                    formationType = FormationType.Wedge },
                new SandboxScenarioDefinition.SpawnGroup { team = 1, count = 4, files = 2, front = new Vector2(44f, 50f), faceTowards = new Vector2(20f, 50f),
                    asFormation = false },
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
                Assert.That(world.AgentCount, Is.EqualTo(23));
                Assert.That(world.Formations.Length, Is.EqualTo(2), "two formations, one loose group");
                Assert.That(world.Formations[1].Type, Is.EqualTo(FormationType.Wedge));
                for (int i = 0; i < world.AgentCount; i++)
                {
                    var a = world.Agents[i];
                    Assert.That(a.Team, Is.EqualTo(i < 12 ? 0 : 1));
                    Assert.That(world.FormationMembers[i].Formation, Is.EqualTo(i < 12 ? 0 : i < 19 ? 1 : -1), $"agent {i}");
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
        public void SameScenario_GivesSameHashAfterOrders()
        {
            var s = Scenario();
            var movement = ScriptableObject.CreateInstance<AgentMovementDefinition>();
            try
            {
                ulong Run()
                {
                    using var world = new SimWorld(s.ToConfig(movement.ToSettings()));
                    s.Spawn(world);
                    world.SubmitFormationOrder(0, FormationOrder.MoveTo(0, new float2(40f, 30f), new float2(0f, 1f)));
                    world.Submit(0, InputCommandType.SetTargetRange, new int4(19, 4, 0, 0), new float4(30f, 50f, 0f, 0f));
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
