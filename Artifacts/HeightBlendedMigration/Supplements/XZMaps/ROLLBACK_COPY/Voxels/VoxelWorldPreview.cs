using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using ProceduralGeneration.WorldGen.Density;
using UnityEngine;

namespace ProceduralGeneration.WorldGen.Voxels
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [RequireComponent(typeof(VoxelWorldControls))]
    public sealed class VoxelWorldPreview : MonoBehaviour
    {
        [SerializeField] private Vector3Int worldSize = new Vector3Int(128, 128, 128);
        [SerializeField, Min(0.001f)] private float voxelSize = 0.5f;
        [SerializeField] private int seed = 12345;
        [SerializeField] private Vector3Int sampleOrigin = Vector3Int.zero;
        [SerializeField] private Color solidColor = new Color(0.5f, 0.55f, 0.6f, 1f);
        [SerializeField] private Material material;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool useJobsForMesh = true;
        [SerializeField, Min(1)] private int chunksPerFrame = 1;
        private Mesh generatedMesh;
        private readonly System.Collections.Generic.List<Mesh> generatedChunkMeshes = new System.Collections.Generic.List<Mesh>();
        private Transform chunkRoot;
        private Material generatedMaterial;
        private Coroutine generationRoutine;
        public int ChunksPerFrame => chunksPerFrame;
        public void SetChunksPerFrame(int value) { chunksPerFrame = Math.Max(1, value); }

        public VoxelVolume Volume { get; private set; }
        public Vector3Int WorldSize => worldSize;
        public float VoxelSize => voxelSize;
        public int Seed => seed;
        public Vector3Int SampleOrigin => sampleOrigin;

        /// <summary>运行时设置生成参数。偏移以整数体素坐标为单位。</summary>
        public void Generate(Vector3Int size, int worldSeed, Vector3Int origin, float cellSize)
        {
            if (size.x <= 0 || size.y <= 0 || size.z <= 0 || cellSize <= 0
                || float.IsNaN(cellSize) || float.IsInfinity(cellSize))
                throw new ArgumentOutOfRangeException(nameof(size), "Size and voxel size must be positive and finite.");
            checked
            {
                int count = size.x * size.y * size.z;
                int endX = origin.x + size.x - 1;
                int endY = origin.y + size.y - 1;
                int endZ = origin.z + size.z - 1;
            }
            worldSize = size;
            seed = worldSeed;
            sampleOrigin = origin;
            voxelSize = cellSize;
            Generate();
        }

        private void Start()
        {
            if (generateOnStart) StartGeneration();
        }

        [ContextMenu("Generate Voxel World")]
        public void Generate()
        {
            if (Application.isPlaying)
            {
                StartGeneration();
                return;
            }
            GenerateSynchronously();
        }

        /// <summary>运行时开始分帧生成；已完成的区块会立即显示。</summary>
        public void StartGeneration()
        {
            if (generationRoutine != null) StopCoroutine(generationRoutine);
            generationRoutine = StartCoroutine(GenerateOverFrames());
        }

        private void GenerateSynchronously()
        {
            if (worldSize.x <= 0 || worldSize.y <= 0 || worldSize.z <= 0 || voxelSize <= 0
                || float.IsNaN(voxelSize) || float.IsInfinity(voxelSize))
                throw new ArgumentOutOfRangeException(nameof(worldSize), "Size and voxel size must be positive and finite.");
            var timer = Stopwatch.StartNew();
            var fields = WorldFields.CreateLearningDefaults(seed);
            var volume = new VoxelVolume(worldSize.x, worldSize.y, worldSize.z);
            volume.Fill(fields.FinalDensity, sampleOrigin.x, sampleOrigin.y, sampleOrigin.z);
            long samplingMilliseconds = timer.ElapsedMilliseconds;
            ClearGeneratedMeshes();
            var meshes = useJobsForMesh
                ? VoxelMeshBuilder.BuildChunkMeshesWithJobs(volume, voxelSize, 16)
                : BuildChunkMeshesWithoutJobs(volume);
            Volume = volume;
            GetComponent<MeshFilter>().sharedMesh = null;
            GetComponent<MeshRenderer>().enabled = false;
            chunkRoot = new GameObject("Voxel Chunks XZ16").transform;
            chunkRoot.SetParent(transform, false);
            foreach (Mesh mesh in meshes)
            {
                generatedChunkMeshes.Add(mesh);
                var child = new GameObject(mesh.name);
                child.transform.SetParent(chunkRoot, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial();
            }
            UnityEngine.Debug.Log($"Voxel world: {worldSize}, voxel={voxelSize}, solid={volume.SolidCount}, "
                + $"chunks={meshes.Count}, jobs={useJobsForMesh}, sample={samplingMilliseconds}ms, total={timer.ElapsedMilliseconds}ms", this);
        }

        private IEnumerator GenerateOverFrames()
        {
            if (worldSize.x <= 0 || worldSize.y <= 0 || worldSize.z <= 0 || voxelSize <= 0
                || float.IsNaN(voxelSize) || float.IsInfinity(voxelSize))
                throw new ArgumentOutOfRangeException(nameof(worldSize), "Size and voxel size must be positive and finite.");
            var timer = Stopwatch.StartNew();
            ClearGeneratedMeshes();
            var fields = WorldFields.CreateLearningDefaults(seed);
            var volume = new VoxelVolume(worldSize.x, worldSize.y, worldSize.z);
            Volume = volume;
            GetComponent<MeshFilter>().sharedMesh = null;
            GetComponent<MeshRenderer>().enabled = false;
            chunkRoot = new GameObject("Voxel Chunks XZ16").transform;
            chunkRoot.SetParent(transform, false);
            int completed = 0;
            int total = ((worldSize.x + 15) / 16) * ((worldSize.z + 15) / 16);
            for (int startZ = 0; startZ < worldSize.z; startZ += 16)
            for (int startX = 0; startX < worldSize.x; startX += 16)
            {
                int endX = Math.Min(startX + 16, worldSize.x);
                int endZ = Math.Min(startZ + 16, worldSize.z);
                volume.FillRegion(fields.FinalDensity, startX, endX, startZ, endZ, sampleOrigin.x, sampleOrigin.y, sampleOrigin.z);
                Mesh mesh = VoxelMeshBuilder.BuildRegion(volume, voxelSize, startX, endX, startZ, endZ);
                generatedChunkMeshes.Add(mesh);
                var child = new GameObject(mesh.name);
                child.transform.SetParent(chunkRoot, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial();
                completed++;
                UnityEngine.Debug.Log($"Voxel chunk {completed}/{total} generated ({startX},{startZ})", this);
                if (completed % Math.Max(1, chunksPerFrame) == 0)
                    yield return null;
            }
            generationRoutine = null;
            UnityEngine.Debug.Log($"Voxel world generation complete: {worldSize}, chunks={completed}, solid={volume.SolidCount}, elapsed={timer.ElapsedMilliseconds}ms", this);
        }

        private System.Collections.Generic.List<Mesh> BuildChunkMeshesWithoutJobs(VoxelVolume volume)
        {
            var result = new System.Collections.Generic.List<Mesh>();
            for (int z = 0; z < volume.Depth; z += 16)
            for (int x = 0; x < volume.Width; x += 16)
                result.Add(VoxelMeshBuilder.BuildRegion(volume, voxelSize, x, Math.Min(x + 16, volume.Width), z, Math.Min(z + 16, volume.Depth)));
            return result;
        }

        private void ApplyMaterial()
        {
            if (material != null)
            {
                return;
            }
            if (generatedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Assign a material supported by the active rendering pipeline.");
                generatedMaterial = new Material(shader) { name = "Voxel Preview Material" };
            }
            generatedMaterial.color = solidColor;
        }

        private Material GetMaterial()
        {
            ApplyMaterial();
            return material != null ? material : generatedMaterial;
        }

        [ContextMenu("Clear Voxel World")]
        public void Clear()
        {
            if (generationRoutine != null)
            {
                StopCoroutine(generationRoutine);
                generationRoutine = null;
            }
            ClearGeneratedMeshes();
            Volume = null;
        }

        private void ClearGeneratedMeshes()
        {
            foreach (var mesh in generatedChunkMeshes) ReleaseGeneratedObject(mesh);
            generatedChunkMeshes.Clear();
            if (chunkRoot != null) ReleaseGeneratedObject(chunkRoot.gameObject);
            chunkRoot = null;
            ReleaseGeneratedObject(generatedMesh);
            generatedMesh = null;
            GetComponent<MeshFilter>().sharedMesh = null;
            GetComponent<MeshRenderer>().enabled = true;
        }

        private void OnDestroy()
        {
            Clear();
            ReleaseGeneratedObject(generatedMaterial);
        }

        private static void ReleaseGeneratedObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
