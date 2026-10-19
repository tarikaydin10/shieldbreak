using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Shieldbreak.Sim.Formations
{
    public enum FormationType
    {
        Line = 0,
        ShieldWall = 1,
        Wedge = 2,
        Circle = 3,
        /// <summary>Loose order for ranged troops: wide spacing, staggered ranks.</summary>
        Loose = 4,
    }

    public static class FormationLimits
    {
        public const int TypeCount = 5;
        /// <summary>Upper bound per formation; slot assignment is O(n³) and runs only on reforms.</summary>
        public const int MaxMembers = 256;
        /// <summary>Waypoints stored per formation; a longer path is replanned when the anchor reaches the last one.</summary>
        public const int MaxPathPoints = 32;
        public const int MaxFiles = MaxMembers;
    }

    /// <summary>
    /// One formation. Position is the anchor: centre of the front rank (centre of the ring for Circle).
    /// Hashed as raw bytes, so only 4-byte fields (no padding).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FormationData
    {
        public int Id;
        public int Team;
        /// <summary>Player id that may give orders; the commander AI later owns its formations the same way.</summary>
        public int Owner;
        public FormationType Type;
        /// <summary>Soldiers per rank (outer ring size for Circle, rear width for Wedge).</summary>
        public int Files;
        public int MemberCount;
        /// <summary>0 = slots valid, 1 = reassign keeping the anchor (losses), 2 = reassign around the members (orders).</summary>
        public int Reform;
        /// <summary>Earliest tick for a loss reform (orders reform at once).</summary>
        public int NextReformTick;
        public float2 Position;
        /// <summary>Unit vector the formation faces.</summary>
        public float2 Facing;
        /// <summary>Where the anchor marches to.</summary>
        public float2 Destination;
        /// <summary>Anchor velocity of the last tick (m/s).</summary>
        public float2 Velocity;
        /// <summary>Waypoints of the current path (in the system's path buffer) and the one marched to next.</summary>
        public int PathCount;
        public int PathIndex;
        /// <summary>1 = plan a path to Destination on the next tick (new order, map change, path longer than the buffer).</summary>
        public int NeedsPath;
        /// <summary>1 = the path was cut at MaxPathPoints; reaching its end plans the rest.</summary>
        public int PathTruncated;
        /// <summary>WorldGrid.Version the path was planned on; a cost change replans marching formations.</summary>
        public int PathGridVersion;
        /// <summary>Facing ordered for the destination; while marching a long way the formation faces its path instead.</summary>
        public float2 TargetFacing;
        /// <summary>1 = this march is long enough to face the way it walks (decided when the path is planned).</summary>
        public int FaceMarch;

        public const int ReformNone = 0, ReformKeepAnchor = 1, ReformAroundMembers = 2;

        /// <summary>Local slot (x = right, y = forward) to world.</summary>
        public float2 ToWorld(float2 local) => Position + Rotate(local, Facing);

        public static float2 Rotate(float2 local, float2 facing) => new float2(facing.y, -facing.x) * local.x + facing * local.y;

        public static float2 InverseRotate(float2 world, float2 facing) => new float2(math.dot(world, new float2(facing.y, -facing.x)), math.dot(world, facing));
    }

    /// <summary>Formation membership of one agent (same index as the agent). 24 bytes, no padding.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FormationMember
    {
        /// <summary>Formation id, -1 = none.</summary>
        public int Formation;
        public int Slot;
        /// <summary>Slot offset in the formation frame (x = right, y = forward).</summary>
        public float2 Local;
        /// <summary>Facing of the slot in the formation frame (outwards for Circle).</summary>
        public float2 LocalFacing;

        public static FormationMember None => new FormationMember { Formation = -1, Slot = -1 };
    }

    /// <summary>Distance between neighbouring slots: side by side and rank to rank (m).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FormationSpacing
    {
        public float Lateral;
        public float Depth;
    }

    /// <summary>
    /// Formation tuning. Filled from the FormationDefinition ScriptableObject; the sim only sees this plain struct.
    /// </summary>
    public struct FormationSettings
    {
        public FormationSpacing Line;
        public FormationSpacing ShieldWall;
        public FormationSpacing Wedge;
        public FormationSpacing Circle;
        public FormationSpacing Loose;
        /// <summary>Anchor speed in m/s; must stay below the soldiers' top speed so stragglers catch up.</summary>
        public float MarchSpeed;
        /// <summary>Average slot distance (m) the formation tolerates before it slows down to wait.</summary>
        public float CohesionSlack;
        /// <summary>Additional average slot distance (m) over which the march slows from full to MinMarchFactor.</summary>
        public float CohesionRange;
        /// <summary>Slowest march as a fraction of MarchSpeed (never 0, so one blocked soldier cannot stall the formation).</summary>
        public float MinMarchFactor;
        /// <summary>Closer than this to its slot a soldier turns to the slot facing instead of the walking direction (m).</summary>
        public float FaceSlotDistance;
        /// <summary>Ticks between reforms after losses (closing gaps), so a melee does not reassign every tick.</summary>
        public int LossReformIntervalTicks;
        /// <summary>The anchor's path keeps up to this many cells to obstacles (half the front, capped), so wide formations avoid narrow gaps.</summary>
        public int PathMaxClearance;
        /// <summary>Extra path cost per cell and missing cell of clearance (a straight step costs 10 per cell cost unit); 0 = ignore width.</summary>
        public int PathClearancePenalty;
        /// <summary>Marches at least this long (m) face the way they walk, so the body follows the anchor round corners; shorter ones keep the ordered facing.</summary>
        public float FaceMarchMinDistance;
        /// <summary>Wheeling speed limit: the outermost slot never moves faster than this sideways (m/s).</summary>
        public float WheelFlankSpeed;
        /// <summary>
        /// Cosine of the largest turn that wheels; larger turns re-form facing the new way (rear rank becomes the front).
        /// A cosine, not degrees, so the sim needs no trigonometry for it.
        /// </summary>
        public float ReformTurnCos;

        public FormationSpacing Spacing(FormationType type) => type switch
        {
            FormationType.ShieldWall => ShieldWall,
            FormationType.Wedge => Wedge,
            FormationType.Circle => Circle,
            FormationType.Loose => Loose,
            _ => Line,
        };

        public void Validate(float soldierMaxSpeed)
        {
            for (int t = 0; t < FormationLimits.TypeCount; t++)
            {
                var s = Spacing((FormationType)t);
                if (s.Lateral <= 0f || s.Depth <= 0f) throw new ArgumentException($"Spacing of {(FormationType)t} must be positive.");
            }
            if (MarchSpeed <= 0f || MarchSpeed >= soldierMaxSpeed) throw new ArgumentException("MarchSpeed must be in (0, soldier MaxSpeed).");
            if (CohesionSlack < 0f || CohesionRange <= 0f) throw new ArgumentException("Require CohesionSlack >= 0 and CohesionRange > 0.");
            if (MinMarchFactor <= 0f || MinMarchFactor > 1f) throw new ArgumentException("MinMarchFactor must be in (0, 1].");
            if (FaceSlotDistance < 0f) throw new ArgumentException("FaceSlotDistance must be >= 0.");
            if (LossReformIntervalTicks < 0) throw new ArgumentException("LossReformIntervalTicks must be >= 0.");
            if (FaceMarchMinDistance < 0f) throw new ArgumentException("FaceMarchMinDistance must be >= 0.");
            if (WheelFlankSpeed <= 0f) throw new ArgumentException("WheelFlankSpeed must be positive.");
            if (ReformTurnCos < -1f || ReformTurnCos >= 1f) throw new ArgumentException("ReformTurnCos must be in [-1, 1).");
            if (PathMaxClearance < 0 || PathMaxClearance > 32) throw new ArgumentException("PathMaxClearance must be in [0, 32] cells.");
            if (PathClearancePenalty < 0 || PathClearancePenalty > 100) throw new ArgumentException("PathClearancePenalty must be in [0, 100] (integer path costs must not overflow).");
        }
    }
}
