using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Shieldbreak.Sim.Core
{
    public enum InputCommandType : ushort
    {
        None = 0,
        /// <summary>IntArgs.x = first agent id, IntArgs.y = count, FloatArgs.xy = target. Clamped to existing agents.</summary>
        SetTargetRange = 1,
        /// <summary>IntArgs.x = own agent id, IntArgs.y = AttackDirection. Starts, morphs or buffers an attack.</summary>
        CombatAttack = 2,
        /// <summary>IntArgs.x = own agent id, IntArgs.y = 1 raise / 0 lower the block.</summary>
        CombatBlock = 3,
        /// <summary>IntArgs.x = own agent id. Cancels a young windup.</summary>
        CombatFeint = 4,
        /// <summary>IntArgs.x = own agent id, IntArgs.y = 1 sprint, FloatArgs.xy = walk direction (length 0..1), FloatArgs.zw = look direction.</summary>
        PlayerMove = 5,
        /// <summary>IntArgs.x = own agent id, IntArgs.y = StepDirection (relative to the facing).</summary>
        CombatStep = 6,
        /// <summary>IntArgs.x = own agent id. Archetype special (one-hand: shield bash, two-hand: leaping overhead).</summary>
        CombatSpecial = 7,
        /// <summary>IntArgs.x = own agent id, IntArgs.y = loadout slot (0..3). Draws that weapon.</summary>
        CombatSwitchWeapon = 8,
        /// <summary>IntArgs.x = own agent id. Jump (legs cannot be hit while airborne).</summary>
        CombatJump = 9,
        /// <summary>A FormationOrder (Sim/Orders): IntArgs = (formation, flags, type | files &lt;&lt; 8, 0), FloatArgs = (destination, facing).</summary>
        FormationOrder = 10,
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
