using System.Collections.Generic;
using System.Diagnostics;
using Shieldbreak.Data;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation
{
    /// <summary>
    /// Drives the SimWorld at fixed 60 Hz from real time and exposes interpolated positions between tick N-1 and N.
    /// Pause runs zero ticks; slow motion and hitstop run fewer ticks per real second (singleplayer only).
    /// SimConstants.DT never changes. The world comes from any ISimScenario and can be rebuilt (Restart).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SimRunner : MonoBehaviour, IAdminStatus
    {
        [Tooltip("A ScriptableObject implementing ISimScenario (sandbox, duel, ...).")]
        public ScriptableObject scenario;
        public AgentMovementDefinition movement;
        [Tooltip("Real-time factor while slow motion is on (singleplayer only).")]
        [Range(0.05f, 1f)] public float slowMotionScale = 0.3f;
        [Tooltip("Upper bound of ticks per frame; a longer hitch drops the backlog instead of spiralling.")]
        [Min(1)] public int maxTicksPerFrame = 8;

        public SimWorld World { get; private set; }
        /// <summary>Agent controlled by the local player, or -1.</summary>
        public int PlayerAgent { get; private set; } = -1;
        /// <summary>0 = previous tick, 1 = current tick.</summary>
        public float Alpha { get; private set; }
        public bool SlowMotion { get; private set; }
        public double LastStepMs { get; private set; }
        public ulong LastHash { get; private set; }
        /// <summary>Combat events of every tick run this frame (cleared at the start of the next frame).</summary>
        public readonly List<CombatEvent> FrameEvents = new List<CombatEvent>();
        /// <summary>Raised after the world was (re)built.</summary>
        public event System.Action WorldRebuilt;

        static readonly ProfilerMarker StepMarker = new ProfilerMarker("SimWorld.Step");

        NativeArray<float2> previous;
        float accumulator;
        float hitstopRemaining;
        double averageStepMs;
        readonly Stopwatch watch = new Stopwatch();
        InputActionMap map;
        InputAction slowMotionAction, stepOnceAction;

        public string AdminStatusLabel => "Sim";
        public string AdminStatus => World == null ? "nicht gestartet" :
            $"Tick {World.Tick} · {averageStepMs:0.00} ms/Tick · {World.AgentCount} Agenten · Hash {LastHash:X16}" +
            (SlowMotion ? $" · Zeitlupe {slowMotionScale:0.0}x" : "");

        void Awake() => Restart();

        /// <summary>Rebuilds the world from the scenario (restart, preset or level change).</summary>
        public void Restart()
        {
            DisposeWorld();
            if (!(scenario is ISimScenario source))
            {
                UnityEngine.Debug.LogError($"{name}: scenario {scenario} does not implement ISimScenario.");
                return;
            }
            World = source.CreateWorld(movement.ToSettings(), out int playerAgent);
            PlayerAgent = playerAgent;
            previous = new NativeArray<float2>(math.max(1, World.Config.MaxAgents), Allocator.Persistent);
            CapturePrevious();
            LastHash = World.ComputeHash();
            Alpha = 1f;
            accumulator = 0f;
            hitstopRemaining = 0f;
            FrameEvents.Clear();
            WorldRebuilt?.Invoke();
        }

        /// <summary>Freezes the simulation for a moment of impact (singleplayer feel; multiplayer will make this visual only).</summary>
        public void RequestHitstop(float seconds) => hitstopRemaining = math.max(hitstopRemaining, seconds);

        public bool InHitstop => hitstopRemaining > 0f;

        void OnEnable()
        {
            map = GameInput.Map("Sim");
            slowMotionAction = map.FindAction("SlowMotion", throwIfNotFound: true);
            stepOnceAction = map.FindAction("StepOnce", throwIfNotFound: true);
            map.Enable();
        }

        void OnDisable() => map?.Disable();

        void OnDestroy() => DisposeWorld();

        void DisposeWorld()
        {
            World?.Dispose();
            World = null;
            if (previous.IsCreated) previous.Dispose();
        }

        void Update()
        {
            FrameEvents.Clear();
            if (World == null) return;
            if (slowMotionAction.WasPressedThisFrame()) SlowMotion = !SlowMotion;

            if (GamePause.IsPaused)
            {
                if (stepOnceAction.WasPressedThisFrame())
                {
                    RunTick();
                    Alpha = 1f;
                }
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (hitstopRemaining > 0f)
            {
                hitstopRemaining -= dt;
                return;
            }

            accumulator += dt * (SlowMotion ? slowMotionScale : 1f);
            int ticks = 0;
            while (accumulator >= SimConstants.DT && ticks < maxTicksPerFrame && hitstopRemaining <= 0f)
            {
                RunTick();
                accumulator -= SimConstants.DT;
                ticks++;
            }
            if (ticks == maxTicksPerFrame) accumulator = math.min(accumulator, SimConstants.DT);
            Alpha = math.saturate(accumulator / SimConstants.DT);
        }

        void RunTick()
        {
            CapturePrevious();
            watch.Restart();
            using (StepMarker.Auto()) World.Step();
            watch.Stop();
            LastStepMs = watch.Elapsed.TotalMilliseconds;
            averageStepMs = averageStepMs <= 0 ? LastStepMs : averageStepMs * 0.95 + LastStepMs * 0.05;
            if (World.Tick % 30 == 0) LastHash = World.ComputeHash();
            foreach (var e in World.LastEvents) FrameEvents.Add(e);
            HitstopHook?.Invoke(this);
        }

        /// <summary>Called after each tick so feedback can request a hitstop before the next tick runs.</summary>
        public System.Action<SimRunner> HitstopHook;

        void CapturePrevious()
        {
            var agents = World.Agents;
            for (int i = 0; i < agents.Length; i++) previous[i] = agents[i].Position;
        }

        /// <summary>Render position of agent i in sim coordinates (x, y = world x, z).</summary>
        public float2 InterpolatedPosition(int i) => math.lerp(previous[i], World.Agents[i].Position, Alpha);

        public static Vector3 ToWorld(float2 p, float height = 0f) => new Vector3(p.x, height, p.y);
    }
}
