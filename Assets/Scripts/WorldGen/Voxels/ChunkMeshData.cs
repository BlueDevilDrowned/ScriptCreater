using System.Collections.Generic;
using UnityEngine;

namespace ProceduralGeneration.WorldGen.Voxels
{
    /// <summary>
    /// 网格的 CPU 端数据，可在后台线程填充，由主线程调用 VoxelMeshBuilder.CreateMesh 上传。
    /// 可以重复使用：Clear 后保留容量，减少 GC 分配。
    /// </summary>
    public sealed class ChunkMeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<int> Triangles = new List<int>();
        public string Name;

        public bool IsEmpty => Vertices.Count == 0;

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            Triangles.Clear();
            Name = null;
        }
    }
}
