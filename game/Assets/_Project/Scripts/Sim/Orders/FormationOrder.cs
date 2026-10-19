using System;
using Unity.Mathematics;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;

namespace Shieldbreak.Sim.Orders
{
    [Flags]
    public enum FormationOrderFlags
    {
        None = 0,
        /// <summary>March the anchor to Destination; a non-zero Facing turns the formation at once (on the spot).</summary>
        Move = 1 << 0,
        /// <summary>Change the formation type.</summary>
        Shape = 1 << 1,
        /// <summary>Change the number of files (soldiers per rank).</summary>
        Width = 1 << 2,
    }

    /// <summary>
    /// The one order API for formations (CLAUDE.md 3.1): the player's radial menu and command view and the commander
    /// AI are two clients of it. Players send it through the InputQueue (ToCommand / FromCommand); the AI, which runs
    /// inside the sim, applies it directly. Neither has a special path in the formation.
    /// </summary>
    public struct FormationOrder
    {
        public int Formation;
        public FormationOrderFlags Flags;
        public FormationType Type;
        public int Files;
        /// <summary>New anchor: centre of the front rank (centre for Circle).</summary>
        public float2 Destination;
        /// <summary>New facing; zero keeps the current one.</summary>
        public float2 Facing;

        public static FormationOrder MoveTo(int formation, float2 destination, float2 facing = default) => new FormationOrder
        {
            Formation = formation,
            Flags = FormationOrderFlags.Move,
            Destination = destination,
            Facing = facing,
        };

        public static FormationOrder Reshape(int formation, FormationType type) =>
            new FormationOrder { Formation = formation, Flags = FormationOrderFlags.Shape, Type = type };

        public static FormationOrder SetWidth(int formation, int files) =>
            new FormationOrder { Formation = formation, Flags = FormationOrderFlags.Width, Files = files };

        /// <summary>IntArgs = (formation, flags, type | files &lt;&lt; 8, 0), FloatArgs = (destination, facing).</summary>
        public void ToArgs(out int4 intArgs, out float4 floatArgs)
        {
            intArgs = new int4(Formation, (int)Flags, ((int)Type & 0xFF) | (math.clamp(Files, 0, 0xFFFFFF) << 8), 0);
            floatArgs = new float4(Destination, Facing);
        }

        public static FormationOrder FromCommand(in InputCommand command) => new FormationOrder
        {
            Formation = command.IntArgs.x,
            Flags = (FormationOrderFlags)command.IntArgs.y,
            Type = (FormationType)(command.IntArgs.z & 0xFF),
            Files = (int)((uint)command.IntArgs.z >> 8),
            Destination = command.FloatArgs.xy,
            Facing = command.FloatArgs.zw,
        };
    }
}
