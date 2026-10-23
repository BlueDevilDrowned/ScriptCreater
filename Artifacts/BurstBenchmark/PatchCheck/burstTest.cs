using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class BurstTest : MonoBehaviour
{
    [Min(1)] public long num = 1000000;
    [Min(1)] public int repetitions = 5;
    public bool starttest;
    public bool startburst;
    public bool startAll;

    public void Update()
    {
        bool managed = starttest || startAll;
        bool burst = startburst || startAll;
        starttest = startburst = startAll = false;
        if (managed || burst) RunBenchmark(managed, burst);
    }

    // Preserve the original entry points for Inspector events / other scripts.
    public void test() => RunBenchmark(true, false);
    public void test1() => RunBenchmark(false, true);

    private void RunBenchmark(bool managed, bool burst)
    {
        if (num < 1 || repetitions < 1)
        {
            Debug.LogWarning("num and repetitions must both be >= 1.");
            return;
        }
        if (burst && !BurstCompiler.IsEnabled)
        {
            Debug.LogWarning("Burst is disabled. Enable Jobs > Burst > Enable Compilation; Burst measurement skipped.");
            burst = false;
        }
        if (!managed && !burst) return;

        // Allocation, disposal, warm-up and logging are outside the timed region.
        using (var output = new NativeArray<uint>(1, Allocator.TempJob))
        {
            var managedJob = new ManagedJob { Count = num, Output = output };
            var burstJob = new BurstJob { Count = num, Output = output };
            if (managed) managedJob.Run();
            if (burst) burstJob.Run();

            var timer = new Stopwatch();
            double managedMs = 0;
            double burstMs = 0;
            uint managedResult = 0;
            uint burstResult = 0;
            for (int sample = 0; sample < repetitions; sample++)
            {
                // Alternate order to reduce systematic first/second-run bias.
                for (int pass = 0; pass < 2; pass++)
                {
                    bool runBurst = ((sample + pass) & 1) != 0;
                    if (runBurst ? !burst : !managed) continue;
                    timer.Restart();
                    if (runBurst) burstJob.Run();
                    else managedJob.Run();
                    timer.Stop();
                    if (runBurst)
                    {
                        burstMs += timer.Elapsed.TotalMilliseconds;
                        burstResult = output[0];
                    }
                    else
                    {
                        managedMs += timer.Elapsed.TotalMilliseconds;
                        managedResult = output[0];
                    }
                }
            }
            if (managed)
                Debug.Log($"Managed Job: num={num}, average={managedMs / repetitions:F4} ms, checksum={managedResult}");
            if (burst)
                Debug.Log($"Burst Job: num={num}, average={burstMs / repetitions:F4} ms, checksum={burstResult}");
            if (managed && burst)
            {
                if (managedResult != burstResult)
                    Debug.LogError("Checksum mismatch: benchmark results are invalid.");
                else if (burstMs > 0)
                    Debug.Log($"Checksums match. Managed/Burst time ratio: {managedMs / burstMs:F2}x");
            }
        }
    }

    // Both jobs execute the same observable workload with dependent iterations.
    private static uint Calculate(long count)
    {
        uint value = 123456789u;
        unchecked
        {
            for (long i = 0; i < count; i++)
            {
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                value += (uint)i;
            }
        }
        return value;
    }

    private struct ManagedJob : IJob
    {
        public long Count;
        public NativeArray<uint> Output;
        public void Execute() => Output[0] = Calculate(Count);
    }

    [BurstCompile(CompileSynchronously = true)]
    private struct BurstJob : IJob
    {
        public long Count;
        public NativeArray<uint> Output;
        public void Execute() => Output[0] = Calculate(Count);
    }
}
