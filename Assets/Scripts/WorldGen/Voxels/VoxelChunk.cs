using System;
using ProceduralGeneration.WorldGen.Density;

namespace ProceduralGeneration.WorldGen.Voxels
{
    /// <summary>
    /// 单个 XZ 区块（Y 全高）的体素数据。只在构建自身及邻居 Mesh 期间存活，
    /// 因此世界总大小不再受单块连续内存限制。坐标单位是体素。
    /// </summary>
    public sealed class VoxelChunk
    {
        public int StartX { get; }
        public int StartZ { get; }
        public int SizeX { get; }
        public int SizeY { get; }
        public int SizeZ { get; }
        public int SolidCount { get; private set; }
        private readonly byte[] solid;

        public VoxelChunk(int startX, int startZ, int sizeX, int sizeY, int sizeZ)
        {
            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
                throw new ArgumentOutOfRangeException(nameof(sizeX), "Chunk dimensions must be positive.");
            StartX = startX;
            StartZ = startZ;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            solid = new byte[checked(sizeX * sizeY * sizeZ)];
        }

        /// <summary>origin 为世界采样原点；区块内体素 (x,y,z) 采样 origin + Start + (x,y,z)。</summary>
        public void Fill(IDensityFunction density, int originX, int originY, int originZ)
        {
            if (density == null) throw new ArgumentNullException(nameof(density));
            int count = 0;
            // Y 放在最内层：同一 XZ 列连续采样，二维场的列缓存（Cache2D）才能命中。
            for (int x = 0; x < SizeX; x++)
            for (int z = 0; z < SizeZ; z++)
            for (int y = 0; y < SizeY; y++)
            {
                var context = new DensityContext(checked(originX + StartX + x), checked(originY + y), checked(originZ + StartZ + z));
                bool value = density.Sample(context) > 0.0;
                solid[x + SizeX * (y + SizeY * z)] = value ? (byte)1 : (byte)0;
                if (value) count++;
            }
            SolidCount = count;
        }

        /// <summary>局部坐标读取，调用者保证坐标在区块内。</summary>
        public byte Get(int x, int y, int z) => solid[x + SizeX * (y + SizeY * z)];

        /// <summary>供采样器直接写入，索引为 x + SizeX * (y + SizeY * z)。写完后调用 SetSolidCount。</summary>
        internal byte[] RawSolid => solid;
        internal void SetSolidCount(int count) => SolidCount = count;
    }
}
