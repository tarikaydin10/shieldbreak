using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Shieldbreak.Sim.Core;

namespace Shieldbreak.Sim.Agents
{
    public static class AgentFlags
    {
        public const int Dead = 1 << 0;
    }

    /// <summary>
    /// One soldier in the sim. Hashed as raw bytes, so the layout has no padding (only 4-byte fields: 56 bytes).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AgentData
    {
        public int Id;
        public int Team;
        public float2 Position;
        public float2 Velocity;
        /// <summary>Unit vector the agent looks along.</summary>
        public float2 Facing;
        public float2 Target;
        public float Radius;
        /// <summary>Multiplier on MaxSpeed set by the combat system (slower while attacking or blocking).</summary>
        public float SpeedScale;
        /// <summary>Agent id to keep facing (combat), or -1 to face the walking direction.</summary>
        public int FaceAgent;
        /// <summary>AgentFlags bits.</summary>
        public int Flags;

        public bool IsDead => (Flags & AgentFlags.Dead) != 0;
    }

    /// <summary>
    /// Movement tuning for all agents. Filled from the AgentMovementDefinition ScriptableObject; the sim only sees
    /// this plain struct.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AgentMoveSettings
    {
        /// <summary>m/s.</summary>
        public float MaxSpeed;
        /// <summary>m/s².</summary>
        public float MaxAcceleration;
        /// <summary>Within this distance to the target the desired speed ramps down linearly.</summary>
        public float SlowRadius;
        /// <summary>Within this distance the agent counts as arrived and only separates.</summary>
        public float StopRadius;
        /// <summary>Neighbours closer than this push each other apart. Must not exceed the grid cell size.</summary>
        public float SeparationRadius;
        /// <summary>Strength of the push relative to MaxSpeed.</summary>
        public float SeparationWeight;
        /// <summary>Maximum turning speed in degrees per second.</summary>
        public float MaxTurnRateDeg;

        public void Validate()
        {
            if (MaxSpeed <= 0f || MaxAcceleration <= 0f) throw new ArgumentException("MaxSpeed and MaxAcceleration must be positive.");
            if (SlowRadius <= 0f || StopRadius < 0f || StopRadius >= SlowRadius) throw new ArgumentException("Require 0 <= StopRadius < SlowRadius.");
            if (SeparationRadius <= 0f || SeparationRadius > SimConstants.CellSize)
                throw new ArgumentException("SeparationRadius must be in (0, CellSize] for the 3x3 neighbour query.");
            if (SeparationWeight < 0f) throw new ArgumentException("SeparationWeight must be >= 0.");
            if (MaxTurnRateDeg <= 0f) throw new ArgumentException("MaxTurnRateDeg must be positive.");
        }
    }
}
