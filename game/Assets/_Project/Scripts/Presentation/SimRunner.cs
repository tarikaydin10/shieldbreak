using System.Diagnostics;
using Shieldbreak.Data;
using Shieldbreak.Presentation.Admin;
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
    /// Pause runs zero ticks; slow motion runs fewer ticks per real second. SimConstants.DT never changes.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SimRunner : MonoBehaviour, IAdminStatus
    {
        public SandboxScenarioDefinition scenario;
        public AgentMovementDefinition movement;
        [Tooltip("Real-time factor while slow motion is on (singleplayer only).")]
        [Range(0.05f, 1f)] public float slowMotionScale = 0.3f;
        [Tooltip("Upper bound of ticks per frame; a longer hitch drops the backlog instead of spiralling.")]
        [Min(1)] public int maxTicksPerFrame = 8;

        public SimWorld World { get; private set; }
        /// <summary>0 = previous tick, 1 = current tick.</summary>
        public float Alpha { get; private set; }
        public bool SlowMotion { get; private set; }
        public double LastStepMs { get; private set; }
        public ulong LastHash { get; private set; }

        static readonly ProfilerMarker StepMarker = new ProfilerMarker("SimWorld.Step");

        NativeArray<float2> previous;
        float accumulator;
        double averageStepMs;
        readonly Stopwatch watch = new Stopwatch();
        InputActionMap map;
        InputAction slowMotionAction, stepOnceAction;

        public string AdminStatusLabel => "Sim";
        public string AdminStatus => World == null ? "nicht gestartet" :
            $"Tick {World.Tick} · {averageStepMs:0.00} ms/Tick · {World.AgentCount} Agenten · Hash {LastHash:X16}" +
            (SlowMotion ? $" · Zeitlupe {slowMotionScale:0.0}x" : "");

        void Awake()
        {
            World = new SimWorld(scenario.ToConfig(movement.ToSettings()));
            scenario.Spawn(World);
            previous = new NativeArray<float2>(World.Config.MaxAgents, Allocator.Persistent);
            CapturePrevious();
            LastHash = World.ComputeHash();
            Alpha = 1f;
        }

        void OnEnable()
        {
            map = GameInput.Map("Sim");
            slowMotionAction = map.FindAction("SlowMotion", throwIfNotFound: true);
            stepOnceAction = map.FindAction("StepOnce", throwIfNotFound: true);
            map.Enable();
        }

        void OnDisable() => map?.Disable();

        void OnDestroy()
        {
            World?.Dispose();
            if (previous.IsCreated) previous.Dispose();
        }

        void Update()
        {
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

            accumulator += Time.unscaledDeltaTime * (SlowMotion ? slowMotionScale : 1f);
            int ticks = 0;
            while (accumulator >= SimConstants.DT && ticks < maxTicksPerFrame)
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
        }

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
