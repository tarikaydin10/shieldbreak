using Shieldbreak.Sim.Agents;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>Tuning for agent movement; converted to the sim's AgentMoveSettings when a SimWorld is created.</summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Agent Movement", fileName = "AgentMovement")]
    public class AgentMovementDefinition : ScriptableObject
    {
        [Tooltip("Top speed in m/s.")]
        [Min(0.01f)] public float maxSpeed = 1.6f;
        [Tooltip("Acceleration and braking in m/s².")]
        [Min(0.01f)] public float maxAcceleration = 4f;
        [Tooltip("Inside this distance to the target the agent slows down linearly (m).")]
        [Min(0.01f)] public float slowRadius = 1.5f;
        [Tooltip("Inside this distance the agent counts as arrived (m). Must be smaller than Slow Radius.")]
        [Min(0f)] public float stopRadius = 0.1f;
        [Tooltip("Neighbours closer than this push each other apart (m). At most the grid cell size (1 m).")]
        [Range(0.05f, 1f)] public float separationRadius = 0.8f;
        [Tooltip("Strength of the push, relative to top speed.")]
        [Min(0f)] public float separationWeight = 1.5f;

        public AgentMoveSettings ToSettings() => new AgentMoveSettings
        {
            MaxSpeed = maxSpeed,
            MaxAcceleration = maxAcceleration,
            SlowRadius = slowRadius,
            StopRadius = stopRadius,
            SeparationRadius = separationRadius,
            SeparationWeight = separationWeight,
        };
    }
}
