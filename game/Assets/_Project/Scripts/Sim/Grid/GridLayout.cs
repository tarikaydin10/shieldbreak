using System;
using Unity.Mathematics;
using Shieldbreak.Sim.Core;

namespace Shieldbreak.Sim.Grid
{
    /// <summary>
    /// Geometry of the world grid: cell (0,0) has its lower-left corner at Origin, cells are CellSize wide,
    /// index = y * Width + x. Plain value type so Burst jobs can carry it.
    /// </summary>
    public readonly struct GridLayout
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float2 Origin;

        public GridLayout(int width, int height, float2 origin)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Grid dimensions must be positive.");
            Width = width;
            Height = height;
            Origin = origin;
        }

        public int CellCount => Width * Height;

        public int2 WorldToCell(float2 position) => (int2)math.floor((position - Origin) * (1f / SimConstants.CellSize));

        public bool InBounds(int2 cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;

        public int CellIndex(int2 cell) => cell.y * Width + cell.x;

        public int2 IndexToCell(int index) => new int2(index % Width, index / Width);

        /// <summary>Positions outside the map land in the nearest border cell.</summary>
        public int ClampedCellIndex(float2 position)
        {
            int2 cell = math.clamp(WorldToCell(position), int2.zero, new int2(Width - 1, Height - 1));
            return CellIndex(cell);
        }

        public float2 CellCenter(int2 cell) => Origin + ((float2)cell + 0.5f) * SimConstants.CellSize;
    }
}
