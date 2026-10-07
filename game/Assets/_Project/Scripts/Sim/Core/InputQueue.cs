using System;
using Unity.Collections;

namespace Shieldbreak.Sim.Core
{
    /// <summary>
    /// Pending inputs for future ticks. Arrival order is irrelevant: each tick's commands are drained
    /// sorted by (Tick, PlayerId, Sequence), so every peer applies them identically.
    /// </summary>
    public sealed class InputQueue : IDisposable
    {
        NativeList<InputCommand> pending;

        public InputQueue(int initialCapacity, Allocator allocator)
        {
            pending = new NativeList<InputCommand>(initialCapacity, allocator);
        }

        public int Count => pending.Length;

        public void Enqueue(in InputCommand command)
        {
            pending.Add(command);
        }

        /// <summary>
        /// Moves every command for <paramref name="tick"/> into <paramref name="output"/> in deterministic order.
        /// A pending command for an earlier tick means it arrived too late to be applied; that is a lockstep
        /// violation and throws instead of silently dropping the input.
        /// </summary>
        public void DrainTick(int tick, NativeList<InputCommand> output)
        {
            output.Clear();
            int write = 0;
            for (int read = 0; read < pending.Length; read++)
            {
                InputCommand command = pending[read];
                if (command.Tick < tick)
                    throw new InvalidOperationException($"Input for tick {command.Tick} arrived after tick {tick} started (player {command.PlayerId}, seq {command.Sequence}).");
                if (command.Tick == tick)
                    output.Add(command);
                else
                    pending[write++] = command;
            }
            pending.Length = write;

            output.Sort(new InputCommandComparer());
            for (int i = 1; i < output.Length; i++)
            {
                if (output[i - 1].PlayerId == output[i].PlayerId && output[i - 1].Sequence == output[i].Sequence)
                    throw new InvalidOperationException($"Duplicate input (player {output[i].PlayerId}, seq {output[i].Sequence}) on tick {tick}.");
            }
        }

        public void Dispose()
        {
            if (pending.IsCreated) pending.Dispose();
        }
    }
}
