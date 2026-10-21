using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;

namespace ProceduralGeneration.WorldGen.Voxels
{
    /// <summary>最直接的立方体面构建：一个可见面对应 4 个顶点和 2 个三角形。</summary>
    public static class VoxelMeshBuilder
    {
        // 每组四点按外侧正面绕序排列，使 Cross(b-a,c-a) 指向体素外侧。
        private static readonly Vector3[] Corners =
        {
            new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1),
            new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0),
            new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0),
            new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1),
            new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1),
            new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0)
        };
        private static readonly Vector3Int[] Directions =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up,
            Vector3Int.down, Vector3Int.forward, Vector3Int.back
        };

        // 保留的基线实现：全部在主线程执行，便于对照 Job 版本。
        public static Mesh Build(VoxelVolume volume, float voxelSize)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));
            if (voxelSize <= 0 || float.IsNaN(voxelSize) || float.IsInfinity(voxelSize))
                throw new ArgumentOutOfRangeException(nameof(voxelSize));
            var vertices = new List<Vector3>(Math.Min(volume.SolidCount * 24, 1_000_000));
            var normals = new List<Vector3>(vertices.Capacity);
            var triangles = new List<int>(Math.Min(volume.SolidCount * 36, 1_500_000));

            for (int z = 0; z < volume.Depth; z++)
            for (int y = 0; y < volume.Height; y++)
            for (int x = 0; x < volume.Width; x++)
            {
                if (!volume.IsSolid(x, y, z)) continue;
                var position = new Vector3(x, y, z);
                for (int face = 0; face < 6; face++)
                {
                    var direction = Directions[face];
                    if (volume.IsSolid(x + direction.x, y + direction.y, z + direction.z)) continue;
                    int start = vertices.Count;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        vertices.Add((position + Corners[face * 4 + corner]) * voxelSize);
                        normals.Add((Vector3)direction);
                    }
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
            }

            var mesh = new Mesh { name = "Density Voxel World", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>无 Job 的区域基线，用于关闭 Jobs 做性能对照。</summary>
        public static Mesh BuildRegion(VoxelVolume volume, float voxelSize, int startX, int endX, int startZ, int endZ)
        {
            int voxelCount = Math.Max(0, endX - startX) * volume.Height * Math.Max(0, endZ - startZ);
            var vertices = new List<Vector3>(Math.Min(voxelCount * 24, 250_000));
            var normals = new List<Vector3>(vertices.Capacity);
            var triangles = new List<int>(Math.Min(voxelCount * 36, 375_000));
            for (int z = startZ; z < endZ; z++) for (int y = 0; y < volume.Height; y++) for (int x = startX; x < endX; x++)
            {
                if (!volume.IsSolid(x, y, z)) continue;
                var position = new Vector3(x, y, z);
                for (int face = 0; face < 6; face++)
                {
                    var direction = Directions[face];
                    if (volume.IsSolid(x + direction.x, y + direction.y, z + direction.z)) continue;
                    int start = vertices.Count;
                    for (int corner = 0; corner < 4; corner++) { vertices.Add((position + Corners[face * 4 + corner]) * voxelSize); normals.Add((Vector3)direction); }
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
            }
            var mesh = new Mesh { name = $"Density Voxel Chunk {startX},{startZ}", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); mesh.UploadMeshData(true); return mesh;
        }

        /// <summary>
        /// Job 版本：并行计算每个体素的暴露面，主线程仍负责创建 Unity Mesh。
        /// 这保持密度采样结果不变，第一步只优化网格判定。
        /// </summary>
        public static Mesh BuildWithJobs(VoxelVolume volume, float voxelSize, int batchSize = 64)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));
            if (voxelSize <= 0 || float.IsNaN(voxelSize) || float.IsInfinity(voxelSize))
                throw new ArgumentOutOfRangeException(nameof(voxelSize));
            if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
            int count = checked(volume.Width * volume.Height * volume.Depth);
            using var solids = volume.CopySolidToNative(Allocator.TempJob);
            using var faceMask = new NativeArray<byte>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var job = new VoxelFaceVisibilityJob
            {
                Solid = solids,
                FaceMask = faceMask,
                Width = volume.Width,
                Height = volume.Height,
                Depth = volume.Depth
            };
            JobHandle handle = job.Schedule(count, batchSize);
            handle.Complete();

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int z = 0; z < volume.Depth; z++)
            for (int y = 0; y < volume.Height; y++)
            for (int x = 0; x < volume.Width; x++)
            {
                int index = x + volume.Width * (y + volume.Height * z);
                byte mask = faceMask[index];
                if (mask == 0) continue;
                var position = new Vector3(x, y, z);
                for (int face = 0; face < 6; face++)
                {
                    if ((mask & (1 << face)) == 0) continue;
                    int start = vertices.Count;
                    var direction = Directions[face];
                    for (int corner = 0; corner < 4; corner++)
                    {
                        vertices.Add((position + Corners[face * 4 + corner]) * voxelSize);
                        normals.Add((Vector3)direction);
                    }
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
            }
            var mesh = new Mesh { name = "Density Voxel World (Jobs)", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>一次计算全世界暴露面，再按 XZ 16x16、Y 全高拆成独立 Mesh。</summary>
        public static List<Mesh> BuildChunkMeshesWithJobs(VoxelVolume volume, float voxelSize, int chunkSize = 16, int batchSize = 64)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            if (voxelSize <= 0 || float.IsNaN(voxelSize) || float.IsInfinity(voxelSize))
                throw new ArgumentOutOfRangeException(nameof(voxelSize));
            int count = checked(volume.Width * volume.Height * volume.Depth);
            using var solids = volume.CopySolidToNative(Allocator.TempJob);
            using var faceMask = new NativeArray<byte>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var job = new VoxelFaceVisibilityJob { Solid = solids, FaceMask = faceMask,
                Width = volume.Width, Height = volume.Height, Depth = volume.Depth };
            job.Schedule(count, batchSize).Complete();
            var meshes = new List<Mesh>();
            for (int startZ = 0; startZ < volume.Depth; startZ += chunkSize)
            for (int startX = 0; startX < volume.Width; startX += chunkSize)
            {
                int endX = Math.Min(startX + chunkSize, volume.Width);
                int endZ = Math.Min(startZ + chunkSize, volume.Depth);
                meshes.Add(BuildMeshFromMask(volume, faceMask, voxelSize, startX, endX, startZ, endZ));
            }
            return meshes;
        }

        private static Mesh BuildMeshFromMask(VoxelVolume volume, NativeArray<byte> faceMask, float voxelSize,
            int startX, int endX, int startZ, int endZ)
        {
            int voxelCount = Math.Max(0, endX - startX) * volume.Height * Math.Max(0, endZ - startZ);
            var vertices = new List<Vector3>(Math.Min(voxelCount * 24, 250_000));
            var normals = new List<Vector3>(vertices.Capacity);
            var triangles = new List<int>(Math.Min(voxelCount * 36, 375_000));
            for (int z = startZ; z < endZ; z++)
            for (int y = 0; y < volume.Height; y++)
            for (int x = startX; x < endX; x++)
            {
                int index = x + volume.Width * (y + volume.Height * z);
                byte mask = faceMask[index];
                if (mask == 0) continue;
                var position = new Vector3(x, y, z);
                for (int face = 0; face < 6; face++)
                {
                    if ((mask & (1 << face)) == 0) continue;
                    int start = vertices.Count;
                    var direction = Directions[face];
                    for (int corner = 0; corner < 4; corner++)
                    {
                        vertices.Add((position + Corners[face * 4 + corner]) * voxelSize);
                        normals.Add((Vector3)direction);
                    }
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
            }
            var mesh = new Mesh { name = $"Density Voxel Chunk {startX},{startZ}", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
