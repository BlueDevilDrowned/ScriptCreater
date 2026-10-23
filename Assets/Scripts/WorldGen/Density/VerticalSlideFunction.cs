using System;

namespace ProceduralGeneration.WorldGen.Density
{
    /// <summary>
    /// Minecraft NoiseRouterData.slide 的学习版：
    /// 在世界底部和顶部把密度平滑推向指定目标，中间区域保持原值。
    /// 底部渐变带为 [minY, minY + bottomSize]；
    /// 顶部渐变带为 [maxY - topOffset - topSize, maxY - topOffset]，带以上恒为 topTarget。
    /// topOffset = 0 时顶部带紧贴世界上沿。
    /// </summary>
    internal sealed class VerticalSlideFunction : IDensityFunction
    {
        private readonly IDensityFunction input;
        private readonly int minY;
        private readonly int maxY;
        private readonly int bottomSize;
        private readonly double bottomTarget;
        private readonly int topSize;
        private readonly double topTarget;
        private readonly int topEnd;

        public VerticalSlideFunction(
            IDensityFunction input,
            int minY,
            int maxY,
            int bottomSize,
            double bottomTarget,
            int topSize,
            double topTarget,
            int topOffset = 0)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            if (minY >= maxY) throw new ArgumentException("minY must be less than maxY");
            if (bottomSize < 0 || topSize < 0 || topOffset < 0) throw new ArgumentOutOfRangeException(nameof(bottomSize));
            this.minY = minY;
            this.maxY = maxY;
            this.bottomSize = bottomSize;
            this.bottomTarget = bottomTarget;
            this.topSize = topSize;
            this.topTarget = topTarget;
            topEnd = checked(maxY - topOffset);
        }

        public double Sample(in DensityContext context)
        {
            double value = input.Sample(context);

            if (bottomSize > 0 && context.Y < minY + bottomSize)
            {
                double t = Clamp01((context.Y - minY) / (double)bottomSize);
                value = Lerp(bottomTarget, value, t);
            }

            if (topSize > 0 && context.Y > topEnd - topSize)
            {
                double t = Clamp01((topEnd - context.Y) / (double)topSize);
                value = Lerp(topTarget, value, t);
            }

            return value;
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        private static double Clamp01(double value) => Math.Max(0.0, Math.Min(1.0, value));
    }
}
