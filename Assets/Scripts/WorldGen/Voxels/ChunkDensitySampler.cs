using System;
using ProceduralGeneration.WorldGen.Density;

namespace ProceduralGeneration.WorldGen.Voxels
{
    /// <summary>
    /// 区块密度采样器，对应 MC 的 NoiseChunk + NoiseInterpolator：
    /// 只在 cellWidth x cellHeight x cellWidth 的 cell 角点上计算完整密度，cell 内部做三线性插值。
    ///
    /// 角点对齐到世界坐标网格（同 MC 的 floorDiv(block, cellSize)），因此：
    /// 相邻区块在交界处读到的是同一批角点，不会有接缝；地形也不随 sampleOrigin 的取值漂移。
    ///
    /// 采样器持有自己的密度函数图（含列缓存）和角点缓冲，不是线程安全的：
    /// 一个线程/任务同一时间只用一个采样器。
    /// </summary>
    public sealed class ChunkDensitySampler
    {
        private readonly IDensityFunction density;
        private readonly int cellWidth;
        private readonly int cellHeight;
        private double[] corners = Array.Empty<double>();

        public int CellWidth => cellWidth;
        public int CellHeight => cellHeight;

        public ChunkDensitySampler(IDensityFunction density, int cellWidth = 4, int cellHeight = 8)
        {
            this.density = density ?? throw new ArgumentNullException(nameof(density));
            if (cellWidth <= 0) throw new ArgumentOutOfRangeException(nameof(cellWidth));
            if (cellHeight <= 0) throw new ArgumentOutOfRangeException(nameof(cellHeight));
            this.cellWidth = cellWidth;
            this.cellHeight = cellHeight;
        }

        /// <summary>
        /// 用 cell 插值填充区块。区块内体素 (x,y,z) 的世界采样坐标为
        /// (originX + StartX + x, originY + y, originZ + StartZ + z)。
        /// </summary>
        public void Fill(VoxelChunk chunk, int originX, int originY, int originZ)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            // 1x1x1 等于逐体素精确采样，直接走 VoxelChunk.Fill，省掉多出的一圈角点。
            if (cellWidth == 1 && cellHeight == 1)
            {
                chunk.Fill(density, originX, originY, originZ);
                return;
            }
            int sx = chunk.SizeX, sy = chunk.SizeY, sz = chunk.SizeZ;
            int baseX = checked(originX + chunk.StartX);
            int baseY = originY;
            int baseZ = checked(originZ + chunk.StartZ);
            checked
            {
                // 覆盖本区块所需的世界 cell 范围 [first, first + count)。
                int firstX = FloorDiv(baseX, cellWidth), cellsX = FloorDiv(baseX + sx - 1, cellWidth) - firstX + 1;
                int firstY = FloorDiv(baseY, cellHeight), cellsY = FloorDiv(baseY + sy - 1, cellHeight) - firstY + 1;
                int firstZ = FloorDiv(baseZ, cellWidth), cellsZ = FloorDiv(baseZ + sz - 1, cellWidth) - firstZ + 1;
                int nx = cellsX + 1, ny = cellsY + 1, nz = cellsZ + 1;
                int cornerCount = nx * ny * nz;
                if (corners.Length < cornerCount) corners = new double[cornerCount];

                // 1. 角点网格。Y 放在最内层，同一列的角点连续采样，二维场的列缓存才能命中。
                for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    int wx = (firstX + ix) * cellWidth;
                    int wz = (firstZ + iz) * cellWidth;
                    int columnBase = (ix * nz + iz) * ny;
                    for (int iy = 0; iy < ny; iy++)
                        corners[columnBase + iy] = density.Sample(new DensityContext(wx, (firstY + iy) * cellHeight, wz));
                }

                // 2. 逐 cell 填充。
                byte[] solid = chunk.RawSolid;
                int solidCount = 0;
                double invW = 1.0 / cellWidth, invH = 1.0 / cellHeight;
                for (int cx = 0; cx < cellsX; cx++)
                for (int cz = 0; cz < cellsZ; cz++)
                for (int cy = 0; cy < cellsY; cy++)
                {
                    double c000 = Corner(cx, cy, cz, ny, nz), c100 = Corner(cx + 1, cy, cz, ny, nz);
                    double c010 = Corner(cx, cy + 1, cz, ny, nz), c110 = Corner(cx + 1, cy + 1, cz, ny, nz);
                    double c001 = Corner(cx, cy, cz + 1, ny, nz), c101 = Corner(cx + 1, cy, cz + 1, ny, nz);
                    double c011 = Corner(cx, cy + 1, cz + 1, ny, nz), c111 = Corner(cx + 1, cy + 1, cz + 1, ny, nz);

                    // cell 的世界起点换算成区块局部坐标，并裁剪到区块范围内。
                    int cellX = (firstX + cx) * cellWidth - baseX;
                    int cellY = (firstY + cy) * cellHeight - baseY;
                    int cellZ = (firstZ + cz) * cellWidth - baseZ;
                    int x0 = Math.Max(0, cellX), x1 = Math.Min(sx, cellX + cellWidth);
                    int y0 = Math.Max(0, cellY), y1 = Math.Min(sy, cellY + cellHeight);
                    int z0 = Math.Max(0, cellZ), z1 = Math.Min(sz, cellZ + cellWidth);

                    // 同号快速填充：三线性插值是 8 个角点的凸组合（权重 >= 0、和为 1），
                    // 角点全 > 0 则内部全 > 0，全 <= 0 则内部全 <= 0，结果与逐点插值完全一致。
                    bool allSolid = c000 > 0 && c100 > 0 && c010 > 0 && c110 > 0
                        && c001 > 0 && c101 > 0 && c011 > 0 && c111 > 0;
                    bool allAir = c000 <= 0 && c100 <= 0 && c010 <= 0 && c110 <= 0
                        && c001 <= 0 && c101 <= 0 && c011 <= 0 && c111 <= 0;
                    if (allSolid || allAir)
                    {
                        byte value = allSolid ? (byte)1 : (byte)0;
                        for (int z = z0; z < z1; z++)
                        for (int y = y0; y < y1; y++)
                        {
                            int row = sx * (y + sy * z);
                            for (int x = x0; x < x1; x++) solid[row + x] = value;
                        }
                        if (allSolid) solidCount += (x1 - x0) * (y1 - y0) * (z1 - z0);
                        continue;
                    }

                    // 逐轴插值，对应 MC doFill 中 updateForY → updateForX → updateForZ。
                    for (int y = y0; y < y1; y++)
                    {
                        double ty = (y - cellY) * invH;
                        double v00 = Lerp(c000, c010, ty), v10 = Lerp(c100, c110, ty);
                        double v01 = Lerp(c001, c011, ty), v11 = Lerp(c101, c111, ty);
                        for (int x = x0; x < x1; x++)
                        {
                            double tx = (x - cellX) * invW;
                            double vz0 = Lerp(v00, v10, tx), vz1 = Lerp(v01, v11, tx);
                            for (int z = z0; z < z1; z++)
                            {
                                double tz = (z - cellZ) * invW;
                                bool isSolid = Lerp(vz0, vz1, tz) > 0;
                                solid[x + sx * (y + sy * z)] = isSolid ? (byte)1 : (byte)0;
                                if (isSolid) solidCount++;
                            }
                        }
                    }
                }
                chunk.SetSolidCount(solidCount);
            }
        }

        private double Corner(int ix, int iy, int iz, int ny, int nz) => corners[(ix * nz + iz) * ny + iy];

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static int FloorDiv(int a, int b)
        {
            int q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }
    }
}
