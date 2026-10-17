using System;
using Unity.Collections;
using Unity.Mathematics;
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

        readonly InputQueue inputQueue;
        NativeList<InputCommand> tickInputs;
        NativeArray<int> nextSequence;
        // Running hash of every applied command; diverging input streams show up in the world hash
        // immediately, before they cause visible divergence.
        ulong appliedInputHash;

        public SimWorld(SimConfig config)
        {
            if (config.InputDelayTicks < 0) throw new ArgumentOutOfRangeException(nameof(config), "InputDelayTicks must be >= 0.");
            Config = config;
            Random = new SimRandom(config.Seed);
            Grid = new WorldGrid(new GridLayout(config.GridWidth, config.GridHeight, config.GridOrigin), Allocator.Persistent);
            inputQueue = new InputQueue(256, Allocator.Persistent);
            tickInputs = new NativeList<InputCommand>(64, Allocator.Persistent);
            nextSequence = new NativeArray<int>(SimConstants.MaxPlayers, Allocator.Persistent);
            appliedInputHash = SimHash.Create().Value;
        }

        public int PendingInputCount => inputQueue.Count;

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

            // Systems (grid, agents, combat) run here from M0 task 3 on.

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
            }
        }

        public ulong ComputeHash()
        {
            var h = SimHash.Create();
            h.Add(Tick);
            h.Add(Random.State);
            h.Add(appliedInputHash);
            h.Add(Grid.NavCost);
            return h.Value;
        }

        public void Dispose()
        {
            inputQueue.Dispose();
            Grid.Dispose();
            if (tickInputs.IsCreated) tickInputs.Dispose();
            if (nextSequence.IsCreated) nextSequence.Dispose();
        }
    }
}
