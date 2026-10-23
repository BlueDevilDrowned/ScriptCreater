using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ProceduralGeneration.WorldGen.Voxels
{
    /// <summary>
    /// 流式区块生成管线，对应 MC 的 fillFromNoise（后台采样）+ ChunkRenderDispatcher（后台编译网格）：
    ///
    ///   后台线程：采样区块密度 → 产出 VoxelChunk
    ///   主线程：  某区块自身和 ±X/±Z 四邻居都采样完 → 派发建网格任务
    ///   后台线程：读取中心和四邻居（只读）→ 计算顶点数据
    ///   主线程：  上传 Mesh（唯一需要 Unity API 的步骤）→ 释放不再被依赖的区块
    ///
    /// 所有调度状态只在主线程（Pump 的调用者）上修改；后台任务只通过并发队列回传结果。
    /// 本类不调用任何 Unity API，上传动作由 uploadMesh 回调完成。
    /// </summary>
    public sealed class ChunkGenerationPipeline : IDisposable
    {
        public const int ChunkSize = 16;

        private readonly int worldX, worldY, worldZ;
        private readonly int originX, originY, originZ;
        private readonly float voxelSize;
        private readonly int chunksX, chunksZ;
        private readonly Func<ChunkDensitySampler> samplerFactory;
        private readonly Action<ChunkMeshData> uploadMesh;
        private readonly int maxSamplingInFlight;
        private readonly int maxMeshingInFlight;
        private readonly int maxLoadedChunks;

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly ConcurrentBag<ChunkDensitySampler> samplerPool = new ConcurrentBag<ChunkDensitySampler>();
        private readonly ConcurrentBag<ChunkMeshData> meshDataPool = new ConcurrentBag<ChunkMeshData>();
        private readonly ConcurrentQueue<(int index, VoxelChunk chunk)> sampledQueue = new ConcurrentQueue<(int, VoxelChunk)>();
        private readonly ConcurrentQueue<(int index, ChunkMeshData data)> meshedQueue = new ConcurrentQueue<(int, ChunkMeshData)>();
        private readonly ConcurrentQueue<Exception> errors = new ConcurrentQueue<Exception>();
        private readonly AutoResetEvent workCompleted = new AutoResetEvent(false);

        // 以下状态只在主线程访问。
        private readonly Dictionary<int, VoxelChunk> loaded = new Dictionary<int, VoxelChunk>();
        private readonly bool[] meshScheduled;
        private readonly bool[] meshDone;
        private readonly Queue<int> pendingMesh = new Queue<int>();
        private int nextToSample;
        private int samplingInFlight;
        private int meshingInFlight;
        private int meshedCount;
        private readonly Stopwatch wallTimer = new Stopwatch();

        private long sampleTicks;
        private long meshTicks;

        public int TotalChunks { get; }
        public int MeshedChunks => meshedCount;
        public long SolidCount { get; private set; }
        public int PeakLoadedChunks { get; private set; }
        public bool IsComplete => meshedCount == TotalChunks;
        /// <summary>所有后台线程采样耗时之和（CPU 时间，不是墙钟时间）。</summary>
        public double SampleMilliseconds => Interlocked.Read(ref sampleTicks) * 1000.0 / Stopwatch.Frequency;
        /// <summary>所有后台线程计算顶点耗时之和（CPU 时间）。</summary>
        public double MeshMilliseconds => Interlocked.Read(ref meshTicks) * 1000.0 / Stopwatch.Frequency;
        public long ElapsedMilliseconds => wallTimer.ElapsedMilliseconds;

        /// <param name="samplerFactory">每次调用返回一个新的采样器（含独立的函数图），会在后台线程调用。</param>
        /// <param name="uploadMesh">在 Pump 所在线程（主线程）调用，负责把顶点数据变成 Mesh。回调返回后数据会被复用。</param>
        /// <param name="workerCount">同时进行的后台任务数，通常为 CPU 核心数 - 1。</param>
        public ChunkGenerationPipeline(int sizeX, int sizeY, int sizeZ, int originX, int originY, int originZ,
            float voxelSize, Func<ChunkDensitySampler> samplerFactory, Action<ChunkMeshData> uploadMesh, int workerCount)
        {
            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0) throw new ArgumentOutOfRangeException(nameof(sizeX));
            this.samplerFactory = samplerFactory ?? throw new ArgumentNullException(nameof(samplerFactory));
            this.uploadMesh = uploadMesh ?? throw new ArgumentNullException(nameof(uploadMesh));
            worldX = sizeX; worldY = sizeY; worldZ = sizeZ;
            this.originX = originX; this.originY = originY; this.originZ = originZ;
            this.voxelSize = voxelSize;
            chunksX = (sizeX + ChunkSize - 1) / ChunkSize;
            chunksZ = (sizeZ + ChunkSize - 1) / ChunkSize;
            TotalChunks = checked(chunksX * chunksZ);
            meshScheduled = new bool[TotalChunks];
            meshDone = new bool[TotalChunks];
            workerCount = Math.Max(1, workerCount);
            maxSamplingInFlight = workerCount * 2;
            maxMeshingInFlight = workerCount * 2;
            // 背压：按行扫描时网格落后采样约一行，正常驻留约 3 行区块，再留一行余量给乱序完成的任务；
            // 主线程上传跟不上时停止派发新的采样，避免已采样的区块在内存里堆积。
            maxLoadedChunks = chunksX * 4 + maxSamplingInFlight;
        }

        /// <summary>
        /// 主线程每帧调用：回收后台结果、派发新任务，最多上传 maxUploads 个 Mesh。
        /// 后台任务抛出的异常会在这里重新抛出。
        /// </summary>
        public void Pump(int maxUploads)
        {
            if (!wallTimer.IsRunning && !IsComplete) wallTimer.Start();
            if (errors.TryDequeue(out Exception error))
            {
                Cancel();
                throw new InvalidOperationException("Chunk generation failed on a worker thread.", error);
            }

            while (sampledQueue.TryDequeue(out var sampled))
            {
                samplingInFlight--;
                loaded[sampled.index] = sampled.chunk;
                SolidCount += sampled.chunk.SolidCount;
                PeakLoadedChunks = Math.Max(PeakLoadedChunks, loaded.Count);
                int x = sampled.index % chunksX, z = sampled.index / chunksX;
                // 新采样的区块可能让自己或四个邻居满足建网格条件。
                TryScheduleMesh(x, z);
                TryScheduleMesh(x - 1, z);
                TryScheduleMesh(x + 1, z);
                TryScheduleMesh(x, z - 1);
                TryScheduleMesh(x, z + 1);
            }

            int uploads = 0;
            while (uploads < maxUploads && meshedQueue.TryDequeue(out var meshed))
            {
                meshingInFlight--;
                try
                {
                    if (!meshed.data.IsEmpty) { uploadMesh(meshed.data); uploads++; }
                }
                finally
                {
                    meshed.data.Clear();
                    meshDataPool.Add(meshed.data);
                }
                meshDone[meshed.index] = true;
                meshedCount++;
                int x = meshed.index % chunksX, z = meshed.index / chunksX;
                // 自身和四邻居中，不再被任何未完成区块依赖的数据可以释放。
                TryRelease(x, z);
                TryRelease(x + 1, z);
                TryRelease(x - 1, z);
                TryRelease(x, z + 1);
                TryRelease(x, z - 1);
            }

            RetryPendingMeshes();

            while (nextToSample < TotalChunks && samplingInFlight < maxSamplingInFlight
                   && loaded.Count + samplingInFlight < maxLoadedChunks)
            {
                ScheduleSample(nextToSample++);
            }

            if (IsComplete) wallTimer.Stop();
        }

        /// <summary>阻塞直到有后台任务完成或超时，供同步生成循环使用，避免空转。</summary>
        public void WaitForWork(int millisecondsTimeout) => workCompleted.WaitOne(millisecondsTimeout);

        public void Cancel() => cancellation.Cancel();

        public void Dispose()
        {
            cancellation.Cancel();
            // 不 Dispose 等待句柄和 CTS：后台任务可能仍在引用它们，交给 GC 回收。
        }

        private void ScheduleSample(int index)
        {
            samplingInFlight++;
            int ix = index % chunksX, iz = index / chunksX;
            int startX = ix * ChunkSize, startZ = iz * ChunkSize;
            int sizeX = Math.Min(ChunkSize, worldX - startX), sizeZ = Math.Min(ChunkSize, worldZ - startZ);
            CancellationToken token = cancellation.Token;
            Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;
                ChunkDensitySampler sampler = null;
                try
                {
                    if (!samplerPool.TryTake(out sampler)) sampler = samplerFactory();
                    long start = Stopwatch.GetTimestamp();
                    var chunk = new VoxelChunk(startX, startZ, sizeX, worldY, sizeZ);
                    sampler.Fill(chunk, originX, originY, originZ);
                    Interlocked.Add(ref sampleTicks, Stopwatch.GetTimestamp() - start);
                    if (!token.IsCancellationRequested) sampledQueue.Enqueue((index, chunk));
                }
                catch (Exception e)
                {
                    errors.Enqueue(e);
                }
                finally
                {
                    if (sampler != null) samplerPool.Add(sampler);
                    workCompleted.Set();
                }
            }, token);
        }

        private bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < chunksX && z < chunksZ;

        // 世界外视为空气，等同于"已就绪"。未完成区块的邻居只有在其所有邻居都完成后才会释放，所以只查 loaded。
        private bool IsReady(int x, int z) => !InBounds(x, z) || loaded.ContainsKey(x + chunksX * z);

        private VoxelChunk Neighbor(int x, int z) => InBounds(x, z) ? loaded[x + chunksX * z] : null;

        private void TryScheduleMesh(int x, int z)
        {
            if (!InBounds(x, z)) return;
            int index = x + chunksX * z;
            if (meshScheduled[index] || !loaded.ContainsKey(index)) return;
            if (!IsReady(x - 1, z) || !IsReady(x + 1, z) || !IsReady(x, z - 1) || !IsReady(x, z + 1)) return;
            if (meshingInFlight >= maxMeshingInFlight)
            {
                // 稍后由 RetryPendingMeshes 补发。
                pendingMesh.Enqueue(index);
                return;
            }
            ScheduleMesh(index, x, z);
        }

        private void ScheduleMesh(int index, int x, int z)
        {
            meshScheduled[index] = true;
            meshingInFlight++;
            // 在主线程取好引用；区块在本任务完成（meshDone）前不会被释放。
            VoxelChunk center = loaded[index];
            VoxelChunk posX = Neighbor(x + 1, z), negX = Neighbor(x - 1, z);
            VoxelChunk posZ = Neighbor(x, z + 1), negZ = Neighbor(x, z - 1);
            float size = voxelSize;
            CancellationToken token = cancellation.Token;
            Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;
                try
                {
                    if (!meshDataPool.TryTake(out ChunkMeshData data)) data = new ChunkMeshData();
                    long start = Stopwatch.GetTimestamp();
                    VoxelMeshBuilder.BuildChunkData(center, posX, negX, posZ, negZ, size, data);
                    Interlocked.Add(ref meshTicks, Stopwatch.GetTimestamp() - start);
                    meshedQueue.Enqueue((index, data));
                }
                catch (Exception e)
                {
                    errors.Enqueue(e);
                }
                finally
                {
                    workCompleted.Set();
                }
            }, token);
        }

        /// <summary>派发因并发上限而暂缓的建网格任务。</summary>
        private void RetryPendingMeshes()
        {
            while (pendingMesh.Count > 0 && meshingInFlight < maxMeshingInFlight)
            {
                int index = pendingMesh.Dequeue();
                if (meshScheduled[index]) continue;
                ScheduleMesh(index, index % chunksX, index / chunksX);
            }
        }

        private bool Done(int x, int z) => !InBounds(x, z) || meshDone[x + chunksX * z];

        private void TryRelease(int x, int z)
        {
            if (!InBounds(x, z)) return;
            if (Done(x, z) && Done(x + 1, z) && Done(x - 1, z) && Done(x, z + 1) && Done(x, z - 1))
                loaded.Remove(x + chunksX * z);
        }
    }
}
