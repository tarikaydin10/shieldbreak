using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Shieldbreak.Sim.Core
{
    public enum InputCommandType : ushort
    {
        None = 0,
    }

    /// <summary>
    /// One input for the sim, stamped with the tick it executes on. This is the only thing that crosses the
    /// network in lockstep. Layout has no padding (4+4+2+2+16+16 bytes) because it is hashed as raw bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct InputCommand
    {
        public int Tick;
        /// <summary>Per-player counter assigned by the sender; (PlayerId, Sequence) is unique.</summary>
        public int Sequence;
        public ushort PlayerId;
        public InputCommandType Type;
        public int4 IntArgs;
        public float4 FloatArgs;
    }

    /// <summary>Total order used to apply commands of one tick: Tick, then PlayerId, then Sequence.</summary>
    public struct InputCommandComparer : IComparer<InputCommand>
    {
        public int Compare(InputCommand a, InputCommand b)
        {
            if (a.Tick != b.Tick) return a.Tick < b.Tick ? -1 : 1;
            if (a.PlayerId != b.PlayerId) return a.PlayerId < b.PlayerId ? -1 : 1;
            if (a.Sequence != b.Sequence) return a.Sequence < b.Sequence ? -1 : 1;
            return 0;
        }
    }
}
