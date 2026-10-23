using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private WorldGenerationSettings generationSettings = new WorldGenerationSettings();
        [SerializeField] private int seed = 12345;
        [SerializeField] private Vector3Int sampleOrigin = Vector3Int.zero;
        [SerializeField] private Color solidColor = new Color(0.5f, 0.55f, 0.6f, 1f);
        [SerializeField] private Material material;
        [SerializeField] private bool generateOnStart = true;
        [Tooltip("每帧最多上传的区块 Mesh 数。采样和顶点计算在后台线程，主线程只负责上传。")]
        [SerializeField, Min(1)] private int chunksPerFrame = 4;
        [Tooltip("对应 MC NoiseInterpolator：只在 4x8x4 cell 角点计算密度，内部三线性插值。关闭则逐体素精确采样。")]
        [SerializeField] private bool cellInterpolation = true;
        [Tooltip("后台线程数，0 = CPU 核心数 - 1。")]
        [SerializeField, Min(0)] private int workerThreads = 0;
        // 对应 MC overworld：NoiseSettings.create(-64, 384, 1, 2) → cellWidth 4, cellHeight 8。
        private const int CellWidth = 4;
        private const int CellHeight = 8;
        private ChunkGenerationPipeline activePipeline;
        private int lastWorkerCount;
        private Mesh generatedMesh;
        private readonly System.Collections.Generic.List<Mesh> generatedChunkMeshes = new System.Collections.Generic.List<Mesh>();
        private Transform chunkRoot;
        private Material generatedMaterial;
        private Coroutine generationRoutine;
        public int ChunksPerFrame => chunksPerFrame;
        public void SetChunksPerFrame(int value) { chunksPerFrame = Math.Max(1, value); }

        /// <summary>已采样的实心体素总数；分帧生成期间会逐步增长。</summary>
        public long SolidCount { get; private set; }
        public bool IsGenerating { get; private set; }
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
                int chunkVoxels = 16 * 16 * size.y; // 单个区块须能放进一个数组
                int endX = origin.x + size.x - 1;
                int endY = origin.y + size.y - 1;
                int endZ = origin.z + size.z - 1;
            }
            ValidateWindow(origin.y, size.y);
            worldSize = size;
            seed = worldSeed;
            sampleOrigin = origin;
            voxelSize = cellSize;
            Generate();
        }

        private void ValidateWindow(int originY, int height)
        {
            generationSettings = generationSettings ?? new WorldGenerationSettings();
            generationSettings.ValidateWindow(originY, height);
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
            ValidateWindow(sampleOrigin.y, worldSize.y);
            CancelGeneration();
            generationRoutine = StartCoroutine(GenerateOverFrames());
        }

        /// <summary>编辑器下一次性跑完同一条流式区块管线，不分帧（后台线程仍然并行）。</summary>
        private void GenerateSynchronously()
        {
            using (ChunkGenerationPipeline pipeline = CreatePipeline())
            {
                activePipeline = pipeline;
                try
                {
                    while (!pipeline.IsComplete)
                    {
                        pipeline.Pump(int.MaxValue);
                        if (!pipeline.IsComplete) pipeline.WaitForWork(50);
                    }
                    SolidCount = pipeline.SolidCount;
                    LogCompletion(pipeline);
                }
                finally
                {
                    activePipeline = null;
                }
            }
        }

        private IEnumerator GenerateOverFrames()
        {
            IsGenerating = true;
            ChunkGenerationPipeline pipeline = CreatePipeline();
            activePipeline = pipeline;
            try
            {
                while (!pipeline.IsComplete)
                {
                    // 主线程每帧只做上传：chunksPerFrame 限制每帧创建的 Mesh 数，采样和顶点计算都在后台。
                    pipeline.Pump(Math.Max(1, chunksPerFrame));
                    SolidCount = pipeline.SolidCount;
                    if (!pipeline.IsComplete) yield return null;
                }
                LogCompletion(pipeline);
            }
            finally
            {
                // 正常结束或抛异常时复位；StopCoroutine 不会走到这里，由 CancelGeneration 复位。
                pipeline.Dispose();
                if (activePipeline == pipeline) activePipeline = null;
                IsGenerating = false;
                generationRoutine = null;
            }
        }

        /// <summary>
        /// 准备场景并创建流式区块管线：不分配全局体积，每个区块独立采样，
        /// 只在构建自身及邻居 Mesh 期间保留在内存中，驻留量约 3~4 行区块，与世界深度无关。
        /// 某区块只要自身和 ±X/±Z 四个邻居都已采样，就能得到正确的边界剔除结果。
        /// </summary>
        private ChunkGenerationPipeline CreatePipeline()
        {
            if (worldSize.x <= 0 || worldSize.y <= 0 || worldSize.z <= 0 || voxelSize <= 0
                || float.IsNaN(voxelSize) || float.IsInfinity(voxelSize))
                throw new ArgumentOutOfRangeException(nameof(worldSize), "Size and voxel size must be positive and finite.");
            ValidateWindow(sampleOrigin.y, worldSize.y);
            ClearGeneratedMeshes();
            SolidCount = 0;
            GetComponent<MeshFilter>().sharedMesh = null;
            GetComponent<MeshRenderer>().enabled = false;
            chunkRoot = new GameObject("Voxel Chunks XZ16").transform;
            chunkRoot.SetParent(transform, false);

            // 后台线程只读这份快照，避免 Inspector 修改与采样并发。
            WorldGenerationSettings settings = generationSettings.Clone();
            int worldSeed = seed;
            int cellW = cellInterpolation ? CellWidth : 1;
            int cellH = cellInterpolation ? CellHeight : 1;
            if (cellInterpolation)
            {
                string warning = settings.GetCellAliasingWarning(cellW, cellH);
                if (warning != null) UnityEngine.Debug.LogWarning(warning, this);
            }
            int workers = workerThreads > 0 ? workerThreads : Math.Max(1, SystemInfo.processorCount - 1);
            lastWorkerCount = workers;
            // 每个采样器有自己的 WorldFields：函数图里的列缓存带可变状态，不能跨线程共享。
            return new ChunkGenerationPipeline(
                worldSize.x, worldSize.y, worldSize.z, sampleOrigin.x, sampleOrigin.y, sampleOrigin.z, voxelSize,
                () => new ChunkDensitySampler(WorldFields.CreateLearningDefaults(worldSeed, settings).FinalDensity, cellW, cellH),
                data => AddChunkObject(VoxelMeshBuilder.CreateMesh(data)),
                workers);
        }

        private void LogCompletion(ChunkGenerationPipeline pipeline)
        {
            UnityEngine.Debug.Log($"Voxel world generation complete: {worldSize}, chunks={pipeline.MeshedChunks}, solid={pipeline.SolidCount}, "
                + $"cell={(cellInterpolation ? $"{CellWidth}x{CellHeight}x{CellWidth}" : "off")}, workers={lastWorkerCount}, "
                + $"peakLoadedChunks={pipeline.PeakLoadedChunks}, sampleCpu={pipeline.SampleMilliseconds:F0}ms, "
                + $"meshCpu={pipeline.MeshMilliseconds:F0}ms, elapsed={pipeline.ElapsedMilliseconds}ms", this);
        }

        private void CancelGeneration()
        {
            if (generationRoutine != null)
            {
                StopCoroutine(generationRoutine);
                generationRoutine = null;
            }
            activePipeline?.Dispose();
            activePipeline = null;
            // StopCoroutine 不会触发协程内的 finally，这里手动复位。
            IsGenerating = false;
        }

        private void AddChunkObject(Mesh mesh)
        {
            generatedChunkMeshes.Add(mesh);
            var child = new GameObject(mesh.name);
            child.transform.SetParent(chunkRoot, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial();
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
            CancelGeneration();
            ClearGeneratedMeshes();
            SolidCount = 0;
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

        private void OnDisable()
        {
            // 组件禁用时 Unity 会停止协程但不走 finally，这里取消后台任务。
            CancelGeneration();
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
