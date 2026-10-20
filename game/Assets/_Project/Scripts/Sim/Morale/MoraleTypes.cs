using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Shieldbreak.Sim.Core;

namespace Shieldbreak.Sim.Morale
{
    /// <summary>Morale of one agent (same index as the agent). Hashed raw: 16 bytes, no padding.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MoraleData
    {
        /// <summary>1 = steady, 0 = broken; at RoutThreshold the soldier flees.</summary>
        public float Morale;
        /// <summary>Tick the rout began, -1 = not routing.</summary>
        public int RoutTick;
        /// <summary>Unit vector a routing soldier runs along (away from the enemy, smoothed).</summary>
        public float2 FleeDirection;

        public static MoraleData Steady => new MoraleData { Morale = 1f, RoutTick = -1 };
    }

    /// <summary>
    /// Morale tuning (CLAUDE.md 3.4, M2 task 5). Filled from the MoraleDefinition ScriptableObject; the sim only sees
    /// this plain struct. Panic is measured in "panic units" per 1-m cell: a death adds DeathPanic to its cell, the
    /// field spreads (Diffusion) and fades (Decay), and soldiers lose morale in proportion to the panic where they stand.
    /// Negative panic is confidence (enemies dying or breaking nearby) and raises morale the same way.
    /// </summary>
    public struct MoraleSettings
    {
        /// <summary>1 = morale runs; 0 = soldiers never panic (duel, worlds without morale).</summary>
        public int Enabled;
        /// <summary>Field and morale update every this many ticks (cheaper than every tick, still smooth).</summary>
        public int FieldIntervalTicks;
        /// <summary>D in P += D·ΔP − k·P: how fast panic spreads to neighbouring cells (m²/s).</summary>
        public float Diffusion;
        /// <summary>k: share of the panic that fades per second.</summary>
        public float Decay;
        /// <summary>Panic added to the own team's field where a soldier dies.</summary>
        public float DeathPanic;
        /// <summary>Panic added where a soldier is hit from the side or behind (outside FlankArcCos of its facing).</summary>
        public float FlankHitPanic;
        /// <summary>Cosine of half the front arc; a hit from outside it is a flank hit.</summary>
        public float FlankArcCos;
        /// <summary>Panic removed from the other team's field where an enemy dies or breaks (success steadies).</summary>
        public float KillCourage;
        /// <summary>Panic a routing soldier spreads around itself per second (rout is contagious).</summary>
        public float RoutPanicPerSecond;
        /// <summary>Leaders (player heroes) calm their own team within this radius (m)...</summary>
        public float LeaderRadius;
        /// <summary>...removing this share of the panic there per second (a sink in the field).</summary>
        public float LeaderCalm;
        /// <summary>Morale lost per second per panic unit where the soldier stands (gained per unit of confidence).</summary>
        public float PanicDrain;
        /// <summary>Factor on that loss while the soldier holds its place in a formation (not charging, not alone).</summary>
        public float FormationSteadiness;
        /// <summary>Below this panic (confidence included) a soldier recovers...</summary>
        public float CalmPanic;
        /// <summary>...this much morale per second.</summary>
        public float Recovery;
        /// <summary>At or below this morale the soldier routs (never comes back in v1).</summary>
        public float RoutThreshold;
        /// <summary>A routing soldier runs away from the enemies within this range (m).</summary>
        public float FleeSenseRange;
        /// <summary>A routing soldier this close to the map edge leaves the field (m).</summary>
        public float EdgeMargin;

        public float FieldDeltaTime => FieldIntervalTicks * SimConstants.DT;

        public void Validate()
        {
            if (Enabled == 0) return;
            if (FieldIntervalTicks < 1) throw new ArgumentException("FieldIntervalTicks must be >= 1.");
            if (Diffusion < 0f || Decay < 0f) throw new ArgumentException("Diffusion and Decay must be >= 0.");
            // Explicit diffusion step: stays stable and non-negative only while 4·D·dt/h² + k·dt <= 1
            float h = SimConstants.CellSize, dt = FieldDeltaTime;
            if (4f * Diffusion * dt / (h * h) + Decay * dt > 1f)
                throw new ArgumentException($"Diffusion {Diffusion} and Decay {Decay} are too large for a field step of {dt} s (unstable).");
            if (DeathPanic < 0f || FlankHitPanic < 0f || KillCourage < 0f || RoutPanicPerSecond < 0f) throw new ArgumentException("Panic sources must be >= 0.");
            if (FlankArcCos < -1f || FlankArcCos > 1f) throw new ArgumentException("FlankArcCos must be in [-1, 1].");
            if (LeaderRadius < 0f || LeaderCalm < 0f || LeaderCalm * dt > 1f) throw new ArgumentException("LeaderRadius >= 0 and LeaderCalm in [0, 1/dt].");
            if (PanicDrain < 0f || Recovery < 0f || CalmPanic < 0f) throw new ArgumentException("PanicDrain, Recovery and CalmPanic must be >= 0.");
            if (FormationSteadiness < 0f || FormationSteadiness > 1f) throw new ArgumentException("FormationSteadiness must be in [0, 1].");
            if (RoutThreshold < 0f || RoutThreshold >= 1f) throw new ArgumentException("RoutThreshold must be in [0, 1).");
            if (FleeSenseRange <= 0f || EdgeMargin < 0f) throw new ArgumentException("FleeSenseRange > 0 and EdgeMargin >= 0.");
        }
    }
}
