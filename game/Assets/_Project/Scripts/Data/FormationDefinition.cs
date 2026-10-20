using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>
    /// Formation tuning (spacing per type, march, cohesion, fighting in formation, behaviors); converted to the sim's
    /// FormationSettings.
    /// </summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Formation", fileName = "Formation")]
    public class FormationDefinition : ScriptableObject
    {
        [Tooltip("Line: distance side by side / rank to rank (m).")]
        public Vector2 lineSpacing = new Vector2(1.1f, 1.3f);
        [Tooltip("Shield wall: shoulder to shoulder, ranks close behind (m).")]
        public Vector2 shieldWallSpacing = new Vector2(0.8f, 1.0f);
        [Tooltip("Wedge: side by side / rank to rank (m).")]
        public Vector2 wedgeSpacing = new Vector2(1.0f, 1.1f);
        [Tooltip("Circle: distance along the ring / ring to ring (m).")]
        public Vector2 circleSpacing = new Vector2(1.0f, 1.2f);
        [Tooltip("Loose order (ranged): wide gaps, staggered ranks (m).")]
        public Vector2 looseSpacing = new Vector2(2.2f, 2.4f);

        [Tooltip("March speed of the formation (m/s). Below the soldiers' top speed so stragglers catch up.")]
        [Min(0.05f)] public float marchSpeed = 1.2f;
        [Tooltip("Average distance of the soldiers to their slots (m) before the formation slows down to wait.")]
        [Min(0f)] public float cohesionSlack = 0.6f;
        [Tooltip("Further average slot distance (m) over which the march slows down to the minimum.")]
        [Min(0.05f)] public float cohesionRange = 1.5f;
        [Tooltip("Slowest march while waiting for stragglers, as a fraction of the march speed.")]
        [Range(0.05f, 1f)] public float minMarchFactor = 0.2f;
        [Tooltip("Closer than this to the slot a soldier turns to the formation's direction (m).")]
        [Min(0f)] public float faceSlotDistance = 1.0f;
        [Tooltip("After losses the formation closes gaps at most this often (ms).")]
        [Min(0)] public int lossReformIntervalMs = 1000;
        [Tooltip("Paths keep up to this many metres to walls (half the front, capped), so wide formations avoid narrow gaps.")]
        [Range(0, 32)] public int pathMaxClearance = 6;
        [Tooltip("How much a path avoids being cramped: extra cost per metre walked and metre of missing room. 0 = squeeze through anything.")]
        [Range(0, 100)] public int pathClearancePenalty = 4;
        [Tooltip("Marches at least this long (m) face the way they walk, so the body follows round corners; shorter ones keep the ordered facing.")]
        [Min(0f)] public float faceMarchMinDistance = 8f;
        [Tooltip("Wheeling speed: the outermost soldier never moves sideways faster than this (m/s).")]
        [Min(0.1f)] public float wheelFlankSpeed = 1.0f;
        [Tooltip("Turns larger than this do not wheel: the formation re-forms facing the new way, the rear rank becomes the front (degrees).")]
        [Range(5f, 180f)] public float reformTurnDeg = 60f;

        [Header("Fighting in formation")]
        [Tooltip("How far a soldier may step out of its slot to fight while the formation holds, advances or follows (m). 2 m lets the second rank step up into gaps.")]
        [Min(0f)] public float engageLeash = 2.0f;
        [Tooltip("Arc in front of the formation in which soldiers pick their enemies (degrees, whole arc).")]
        [Range(0f, 360f)] public float frontArcDeg = 120f;
        [Tooltip("Enemies closer than this are fought from any side (m).")]
        [Min(0f)] public float selfDefenseRange = 1.6f;

        [Header("Behaviors")]
        [Tooltip("Advance stops with its front this far from the nearest enemy soldier (m).")]
        [Min(0f)] public float advanceContactGap = 1.5f;
        [Tooltip("Retreat without a destination falls back this far, away from the nearest enemy formation (m).")]
        [Min(1f)] public float retreatDistance = 20f;
        [Tooltip("Follow keeps the front this far behind the followed soldier (m).")]
        [Min(0f)] public float followDistance = 4f;
        [Tooltip("Advance, charge and follow look at their target this often (ms).")]
        [Min(17f)] public float behaviorIntervalMs = 500f;
        [Tooltip("The march is only re-ordered when the target point moved further than this (m).")]
        [Min(0f)] public float retargetDistance = 2f;

        static FormationSpacing Spacing(Vector2 v) => new FormationSpacing { Lateral = v.x, Depth = v.y };

        public FormationSettings ToSettings() => new FormationSettings
        {
            Line = Spacing(lineSpacing),
            ShieldWall = Spacing(shieldWallSpacing),
            Wedge = Spacing(wedgeSpacing),
            Circle = Spacing(circleSpacing),
            Loose = Spacing(looseSpacing),
            MarchSpeed = marchSpeed,
            CohesionSlack = cohesionSlack,
            CohesionRange = cohesionRange,
            MinMarchFactor = minMarchFactor,
            FaceSlotDistance = faceSlotDistance,
            LossReformIntervalTicks = Mathf.RoundToInt(lossReformIntervalMs * SimConstants.TickRate / 1000f),
            PathMaxClearance = pathMaxClearance,
            PathClearancePenalty = pathClearancePenalty,
            FaceMarchMinDistance = faceMarchMinDistance,
            WheelFlankSpeed = wheelFlankSpeed,
            // Converted once here (presentation side), so the sim compares cosines and needs no trigonometry
            ReformTurnCos = reformTurnDeg >= 180f ? -1f : Mathf.Cos(reformTurnDeg * Mathf.Deg2Rad),
            EngageLeash = engageLeash,
            FrontArcCos = frontArcDeg >= 360f ? -1f : Mathf.Cos(frontArcDeg * 0.5f * Mathf.Deg2Rad),
            SelfDefenseRange = selfDefenseRange,
            AdvanceContactGap = advanceContactGap,
            RetreatDistance = retreatDistance,
            FollowDistance = followDistance,
            BehaviorIntervalTicks = Mathf.Max(1, Mathf.RoundToInt(behaviorIntervalMs * SimConstants.TickRate / 1000f)),
            RetargetDistance = retargetDistance,
        };
    }
}
