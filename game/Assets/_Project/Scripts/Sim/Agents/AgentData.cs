using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Shieldbreak.Sim.Core;

namespace Shieldbreak.Sim.Agents
{
    public static class AgentFlags
    {
        /// <summary>No longer on the field: killed, or fled over the map edge (then Fled is set too).</summary>
        public const int Dead = 1 << 0;
        /// <summary>Morale broke: the soldier runs from the enemy and no longer fights (Sim/Morale).</summary>
        public const int Routing = 1 << 1;
        /// <summary>Left the field at the map edge while routing; also Dead, so every system ignores it.</summary>
        public const int Fled = 1 << 2;
        /// <summary>A player's hero while the player is in the command view: its bot brain fights (HeroUnattended).</summary>
        public const int Unattended = 1 << 3;
        /// <summary>Rides a horse: rider and horse are one agent with the horse's speed and inertia (AgentSystem) and its weight in a charge (CombatSystem).</summary>
        public const int Mounted = 1 << 4;
    }

    /// <summary>
    /// One soldier in the sim. Hashed as raw bytes, so the layout has no padding (only 4-byte fields: 72 bytes).
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
        /// <summary>Agent id to keep facing (combat), -1 to face the walking direction, FaceFixed to use FaceDirection.</summary>
        public int FaceAgent;
        /// <summary>AgentFlags bits.</summary>
        public int Flags;
        /// <summary>Facing wanted when FaceAgent == FaceFixed (a player looking along the camera).</summary>
        public float2 FaceDirection;
        /// <summary>Per-agent multiplier on MaxSpeed (heroes run faster than line soldiers march).</summary>
        public float BaseSpeedScale;
        /// <summary>Per-agent multiplier on MaxAcceleration (the player reacts in ~100 ms).</summary>
        public float AccelScale;

        public const int FaceFixed = -2;

        public bool IsDead => (Flags & AgentFlags.Dead) != 0;
        public bool IsRouting => (Flags & AgentFlags.Routing) != 0;
        public bool HasFled => (Flags & AgentFlags.Fled) != 0;
        public bool IsUnattended => (Flags & AgentFlags.Unattended) != 0;
        public bool IsMounted => (Flags & AgentFlags.Mounted) != 0;
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
        /// <summary>Push away from blocked cells within SeparationRadius, relative to MaxSpeed (like SeparationWeight).</summary>
        public float WallWeight;
        /// <summary>Speed lost per unit of grade walking uphill: 2 = a 10 % slope costs 20 %.</summary>
        public float UphillSlowdown;
        /// <summary>Speed gained per unit of grade walking downhill.</summary>
        public float DownhillBoost;
        /// <summary>Limits of the factor a slope puts on the speed.</summary>
        public float SlopeSpeedMin, SlopeSpeedMax;
        /// <summary>Factor on the speed in the forest and in the swamp.</summary>
        public float ForestSpeed, SwampSpeed;
        /// <summary>Cost of these cells on the way (open ground = 1).</summary>
        public int ForestNavCost, SwampNavCost;

        /// <summary>Horse: top speed (m/s, gallop), speeding up and slowing down (m/s²).</summary>
        public float MountedMaxSpeed, MountedAcceleration, MountedBraking;
        /// <summary>Horse: the most sideways acceleration (m/s²), which sets the turning circle at speed (radius = v²/a).</summary>
        public float MountedLateralAcceleration;
        /// <summary>Horse: turning speed at a walk (degrees per second).</summary>
        public float MountedTurnRateDeg;
        /// <summary>Horse: factor on the speed in the forest and in the swamp (worse than on foot).</summary>
        public float MountedForestSpeed, MountedSwampSpeed;

        /// <summary>Factor a kind of ground puts on the speed.</summary>
        public float GroundSpeed(byte kind) => kind == (byte)Shieldbreak.Sim.Grid.GroundKind.Forest ? ForestSpeed : kind == (byte)Shieldbreak.Sim.Grid.GroundKind.Swamp ? SwampSpeed : 1f;

        /// <summary>Factor a kind of ground puts on the speed of a horse.</summary>
        public float MountedGroundSpeed(byte kind) => kind == (byte)Shieldbreak.Sim.Grid.GroundKind.Forest ? MountedForestSpeed : kind == (byte)Shieldbreak.Sim.Grid.GroundKind.Swamp ? MountedSwampSpeed : 1f;

        /// <summary>Factor on the speed along <paramref name="direction"/> over this slope.</summary>
        public float SlopeFactor(float2 slope, float2 direction) => Shieldbreak.Sim.Grid.Terrain.SpeedFactor(slope, direction, UphillSlowdown, DownhillBoost, SlopeSpeedMin, SlopeSpeedMax);

        public void Validate()
        {
            if (MaxSpeed <= 0f || MaxAcceleration <= 0f) throw new ArgumentException("MaxSpeed and MaxAcceleration must be positive.");
            if (SlowRadius <= 0f || StopRadius < 0f || StopRadius >= SlowRadius) throw new ArgumentException("Require 0 <= StopRadius < SlowRadius.");
            if (SeparationRadius <= 0f || SeparationRadius > SimConstants.CellSize)
                throw new ArgumentException("SeparationRadius must be in (0, CellSize] for the 3x3 neighbour query.");
            if (SeparationWeight < 0f) throw new ArgumentException("SeparationWeight must be >= 0.");
            if (MaxTurnRateDeg <= 0f) throw new ArgumentException("MaxTurnRateDeg must be positive.");
            if (WallWeight < 0f) throw new ArgumentException("WallWeight must be >= 0.");
            if (ForestSpeed < 0f || SwampSpeed < 0f) throw new ArgumentException("ForestSpeed and SwampSpeed must be >= 0.");
            if (UphillSlowdown < 0f || DownhillBoost < 0f) throw new ArgumentException("UphillSlowdown and DownhillBoost must be >= 0.");
            if (MountedMaxSpeed < 0f || MountedAcceleration < 0f || MountedBraking < 0f || MountedLateralAcceleration < 0f || MountedTurnRateDeg < 0f || MountedForestSpeed < 0f || MountedSwampSpeed < 0f)
                throw new ArgumentException("Horse values must be >= 0.");
            if ((UphillSlowdown > 0f || DownhillBoost > 0f) && (SlopeSpeedMin <= 0f || SlopeSpeedMax < 1f)) throw new ArgumentException("Require SlopeSpeedMin > 0 and SlopeSpeedMax >= 1.");
        }
    }
}
