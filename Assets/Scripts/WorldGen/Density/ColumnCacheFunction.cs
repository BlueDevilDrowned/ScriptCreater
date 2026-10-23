using System;

namespace ProceduralGeneration.WorldGen.Density
{
    /// <summary>
    /// 对应 MC NoiseChunk.Cache2D：记住上一次的 (X, Z) 和结果，同一列连续采样时直接返回。
    /// 只适合输出与 Y 无关的函数。带可变状态，不是线程安全的：每个线程/任务需要自己的函数图。
    /// </summary>
    internal sealed class ColumnCacheFunction : IDensityFunction
    {
        private readonly IDensityFunction input;
        private bool hasValue;
        private int lastX;
        private int lastZ;
        private double lastValue;

        public ColumnCacheFunction(IDensityFunction input)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public double Sample(in DensityContext context)
        {
            if (hasValue && context.X == lastX && context.Z == lastZ) return lastValue;
            lastX = context.X;
            lastZ = context.Z;
            lastValue = input.Sample(context);
            hasValue = true;
            return lastValue;
        }
    }
}
