using System;
using ProceduralGeneration.WorldGen.Density;


namespace ProceduralGeneration.WorldGen.Voxels
{
    /// <summary>有限大小的体素数据。坐标单位是体素，不是 Unity 米。</summary>
    public sealed class VoxelVolume
    {
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }
        public int SolidCount { get; private set; }
        private readonly bool[] solid;

        public VoxelVolume(int width, int height, int depth)
        {
            if (width <= 0 || height <= 0 || depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Dimensions must be positive.");
            Width = width;
            Height = height;
            Depth = depth;
            solid = new bool[checked(width * height * depth)];
        }

        public void Fill(IDensityFunction density, int originX = 0, int originY = 0, int originZ = 0)
        {
            if (density == null) throw new ArgumentNullException(nameof(density));
            SolidCount = 0;
            for (int z = 0; z < Depth; z++)
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var context = new DensityContext(checked(originX + x), checked(originY + y), checked(originZ + z));
                bool value = density.Sample(context) > 0.0;
                solid[x + Width * (y + Height * z)] = value;
                if (value) SolidCount++;
            }
        }

        /// <summary>只生成一个 XZ 区块范围，适合分帧生成。调用前可重复填充不同区域。</summary>
        public void FillRegion(IDensityFunction density, int startX, int endX, int startZ, int endZ,
            int originX = 0, int originY = 0, int originZ = 0)
        {
            if (density == null) throw new ArgumentNullException(nameof(density));
            if (startX < 0 || endX > Width || startX >= endX || startZ < 0 || endZ > Depth || startZ >= endZ)
                throw new ArgumentOutOfRangeException(nameof(startX));
            for (int z = startZ; z < endZ; z++)
            for (int y = 0; y < Height; y++)
            for (int x = startX; x < endX; x++)
            {
                var context = new DensityContext(checked(originX + x), checked(originY + y), checked(originZ + z));
                int index = x + Width * (y + Height * z);
                bool value = density.Sample(context) > 0.0;
                if (solid[index] == value) continue;
                solid[index] = value;
                if (value) SolidCount++; else SolidCount--;
            }
        }

        /// <summary>区域外视为空气，因此世界边缘也会生成封闭表面。</summary>
        public bool IsSolid(int x, int y, int z)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Depth
                && solid[x + Width * (y + Height * z)];
        }

    }
}
