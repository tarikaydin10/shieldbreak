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
        [Tooltip("Maximum turning speed (degrees per second). Limits how fast a soldier can face a new threat.")]
        [Min(1f)] public float maxTurnRateDeg = 540f;
        [Tooltip("Push away from walls and blocked cells within Separation Radius, relative to top speed.")]
        [Min(0f)] public float wallWeight = 2f;

        [Header("Slopes")]
        [Tooltip("Speed lost per unit of grade walking uphill: 2 = a 10 % slope costs 20 %.")]
        [Min(0f)] public float uphillSlowdown = 2f;
        [Tooltip("Speed gained per unit of grade walking downhill.")]
        [Min(0f)] public float downhillBoost = 1f;
        [Tooltip("Slowest a slope can make a walker, as a factor.")]
        [Range(0.1f, 1f)] public float slopeSpeedMin = 0.5f;
        [Tooltip("Fastest a slope can make a walker, as a factor.")]
        [Range(1f, 2f)] public float slopeSpeedMax = 1.25f;

        [Header("Ground")]
        [Tooltip("Factor on the speed in the forest.")]
        [Range(0.1f, 1f)] public float forestSpeed = 0.7f;
        [Tooltip("Factor on the speed in the swamp.")]
        [Range(0.1f, 1f)] public float swampSpeed = 0.5f;
        [Tooltip("Cost of a forest cell on the way (open ground = 1).")]
        [Range(1, 254)] public int forestNavCost = 3;
        [Tooltip("Cost of a swamp cell on the way (open ground = 1).")]
        [Range(1, 254)] public int swampNavCost = 5;

        [Header("Horse (rider and horse are one agent)")]
        [Tooltip("Gallop (m/s).")]
        [Min(0.1f)] public float mountedMaxSpeed = 9f;
        [Tooltip("Speeding up (m/s²): from a stand to the gallop takes mountedMaxSpeed / this seconds.")]
        [Min(0.1f)] public float mountedAcceleration = 3f;
        [Tooltip("Slowing down (m/s²).")]
        [Min(0.1f)] public float mountedBraking = 6f;
        [Tooltip("Most sideways acceleration (m/s²); the turning circle at speed v has the radius v² / this.")]
        [Min(0.1f)] public float mountedLateralAcceleration = 8f;
        [Tooltip("Turning at a walk (degrees per second).")]
        [Min(1f)] public float mountedTurnRateDeg = 120f;
        [Tooltip("Factor on a horse's speed in the forest.")]
        [Range(0.1f, 1f)] public float mountedForestSpeed = 0.45f;
        [Tooltip("Factor on a horse's speed in the swamp.")]
        [Range(0.1f, 1f)] public float mountedSwampSpeed = 0.3f;

        public AgentMoveSettings ToSettings() => new AgentMoveSettings
        {
            MaxSpeed = maxSpeed,
            MaxAcceleration = maxAcceleration,
            SlowRadius = slowRadius,
            StopRadius = stopRadius,
            SeparationRadius = separationRadius,
            SeparationWeight = separationWeight,
            MaxTurnRateDeg = maxTurnRateDeg,
            WallWeight = wallWeight,
            UphillSlowdown = uphillSlowdown,
            DownhillBoost = downhillBoost,
            SlopeSpeedMin = slopeSpeedMin,
            SlopeSpeedMax = slopeSpeedMax,
            ForestSpeed = forestSpeed,
            SwampSpeed = swampSpeed,
            ForestNavCost = forestNavCost,
            SwampNavCost = swampNavCost,
            MountedMaxSpeed = mountedMaxSpeed,
            MountedAcceleration = mountedAcceleration,
            MountedBraking = mountedBraking,
            MountedLateralAcceleration = mountedLateralAcceleration,
            MountedTurnRateDeg = mountedTurnRateDeg,
            MountedForestSpeed = mountedForestSpeed,
            MountedSwampSpeed = mountedSwampSpeed,
        };
    }
}
