using System;
using Unity.Collections;
using Unity.Mathematics;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Grid;

namespace Shieldbreak.Sim.Core
{
    public struct SimConfig
    {
        public ulong Seed;
        /// <summary>Locally submitted inputs execute this many ticks in the future (lockstep latency budget).</summary>
        public int InputDelayTicks;
        /// <summary>Map size in cells and world position of the grid's lower-left corner (from map data later).</summary>
        public int GridWidth;
        public int GridHeight;
        public float2 GridOrigin;
        /// <summary>Capacity of the agent arrays; agents are spawned during scenario setup.</summary>
        public int MaxAgents;
        public AgentMoveSettings Movement;
    }

    /// <summary>
    /// Root of the deterministic simulation. Owns every NativeArray of sim state and advances in fixed ticks.
    /// All external influence goes through the InputQueue.
    /// </summary>
    public sealed class SimWorld : IDisposable
    {
        public readonly SimConfig Config;
        public int Tick { get; private set; }
        public SimRandom Random;
        public readonly WorldGrid Grid;
        public int AgentCount { get; private set; }

        readonly InputQueue inputQueue;
        NativeList<InputCommand> tickInputs;
        NativeArray<int> nextSequence;
        // Running hash of every applied command; diverging input streams show up in the world hash
        // immediately, before they cause visible divergence.
        ulong appliedInputHash;

        NativeArray<AgentData> agents;
        SpatialHash spatial;
        NativeArray<float2> positionScratch;
        NativeArray<float2> velocityScratch;

        public SimWorld(SimConfig config)
        {
            if (config.InputDelayTicks < 0) throw new ArgumentOutOfRangeException(nameof(config), "InputDelayTicks must be >= 0.");
            if (config.MaxAgents < 0) throw new ArgumentOutOfRangeException(nameof(config), "MaxAgents must be >= 0.");
            if (config.MaxAgents > 0) config.Movement.Validate();
            Config = config;
            Random = new SimRandom(config.Seed);
            var layout = new GridLayout(config.GridWidth, config.GridHeight, config.GridOrigin);
            Grid = new WorldGrid(layout, Allocator.Persistent);
            inputQueue = new InputQueue(256, Allocator.Persistent);
            tickInputs = new NativeList<InputCommand>(64, Allocator.Persistent);
            nextSequence = new NativeArray<int>(SimConstants.MaxPlayers, Allocator.Persistent);
            appliedInputHash = SimHash.Create().Value;

            agents = new NativeArray<AgentData>(config.MaxAgents, Allocator.Persistent);
            spatial = new SpatialHash(layout, config.MaxAgents, Allocator.Persistent);
            positionScratch = new NativeArray<float2>(config.MaxAgents, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            velocityScratch = new NativeArray<float2>(config.MaxAgents, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        public int PendingInputCount => inputQueue.Count;

        /// <summary>Live agents for reading (presentation, debug). Valid until the next Step.</summary>
        public NativeArray<AgentData>.ReadOnly Agents => agents.GetSubArray(0, AgentCount).AsReadOnly();

        /// <summary>Spatial hash from the last Step, for debug overlays.</summary>
        public SpatialHash Spatial => spatial;

        /// <summary>
        /// Adds an agent standing at <paramref name="position"/> with its target on the spot. Scenario setup only:
        /// every peer must run the same spawns before tick 0. Returns the agent id.
        /// </summary>
        public int SpawnAgent(int team, float2 position, float radius, float2 facing = default)
        {
            facing = math.normalizesafe(facing, new float2(0f, 1f));
            if (AgentCount >= agents.Length) throw new InvalidOperationException($"Agent capacity {agents.Length} reached.");
            int id = AgentCount++;
            agents[id] = new AgentData
            {
                Id = id,
                Team = team,
                Position = position,
                Velocity = float2.zero,
                Facing = facing,
                Target = position,
                Radius = radius,
            };
            return id;
        }

        /// <summary>Enqueues an already stamped command (from a remote peer or a replayed input log).</summary>
        public void Enqueue(in InputCommand command)
        {
            inputQueue.Enqueue(command);
        }

        /// <summary>
        /// Stamps a local input with tick (now + InputDelayTicks) and the player's next sequence number, enqueues it
        /// and returns the stamped command so it can be sent to peers or recorded.
        /// </summary>
        public InputCommand Submit(ushort playerId, InputCommandType type, int4 intArgs = default, float4 floatArgs = default)
        {
            if (playerId >= SimConstants.MaxPlayers) throw new ArgumentOutOfRangeException(nameof(playerId));
            var command = new InputCommand
            {
                Tick = Tick + Config.InputDelayTicks,
                Sequence = nextSequence[playerId]++,
                PlayerId = playerId,
                Type = type,
                IntArgs = intArgs,
                FloatArgs = floatArgs,
            };
            inputQueue.Enqueue(command);
            return command;
        }

        public void Step()
        {
            inputQueue.DrainTick(Tick, tickInputs);
            for (int i = 0; i < tickInputs.Length; i++)
                ApplyInput(tickInputs[i]);

            AgentSystem.Step(agents, AgentCount, ref spatial, positionScratch, velocityScratch, Config.Movement);

            Tick++;
        }

        void ApplyInput(in InputCommand command)
        {
            var h = new SimHash { Value = appliedInputHash };
            h.Add(command);
            appliedInputHash = h.Value;

            switch (command.Type)
            {
                case InputCommandType.None:
                    break;
                case InputCommandType.SetTargetRange:
                {
                    // Invalid ranges are clamped, never thrown: a remote peer's input must not crash the sim
                    int first = math.max(command.IntArgs.x, 0);
                    int end = (int)Math.Min((long)first + math.max(command.IntArgs.y, 0), AgentCount);
                    float2 target = command.FloatArgs.xy;
                    if (!math.all(math.isfinite(target))) break;
                    for (int i = first; i < end; i++)
                    {
                        AgentData a = agents[i];
                        a.Target = target;
                        agents[i] = a;
                    }
                    break;
                }
            }
        }

        public ulong ComputeHash()
        {
            var h = SimHash.Create();
            h.Add(Tick);
            h.Add(Random.State);
            h.Add(appliedInputHash);
            h.Add(Grid.NavCost);
            h.Add(AgentCount);
            h.Add(agents.GetSubArray(0, AgentCount));
            return h.Value;
        }

        public void Dispose()
        {
            inputQueue.Dispose();
            Grid.Dispose();
            spatial.Dispose();
            if (tickInputs.IsCreated) tickInputs.Dispose();
            if (nextSequence.IsCreated) nextSequence.Dispose();
            if (agents.IsCreated) agents.Dispose();
            if (positionScratch.IsCreated) positionScratch.Dispose();
            if (velocityScratch.IsCreated) velocityScratch.Dispose();
        }
    }
}
