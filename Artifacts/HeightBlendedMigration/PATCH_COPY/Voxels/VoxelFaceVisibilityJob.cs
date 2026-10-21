using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace ProceduralGeneration.WorldGen.Voxels
{
    /// <summary>每个工作项处理一个体素，输出六个面的可见性位掩码。</summary>
    [BurstCompile]
    internal struct VoxelFaceVisibilityJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> Solid;
        [WriteOnly] public NativeArray<byte> FaceMask;
        public int Width;
        public int Height;
        public int Depth;

        public void Execute(int index)
        {
            if (Solid[index] == 0) { FaceMask[index] = 0; return; }
            int x = index % Width;
            int yz = index / Width;
            int y = yz % Height;
            int z = yz / Height;
            byte mask = 0;
            if (!IsSolid(x + 1, y, z)) mask |= 1 << 0;
            if (!IsSolid(x - 1, y, z)) mask |= 1 << 1;
            if (!IsSolid(x, y + 1, z)) mask |= 1 << 2;
            if (!IsSolid(x, y - 1, z)) mask |= 1 << 3;
            if (!IsSolid(x, y, z + 1)) mask |= 1 << 4;
            if (!IsSolid(x, y, z - 1)) mask |= 1 << 5;
            FaceMask[index] = mask;
        }

        private bool IsSolid(int x, int y, int z)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Depth
                && Solid[x + Width * (y + Height * z)] != 0;
        }
    }
}
