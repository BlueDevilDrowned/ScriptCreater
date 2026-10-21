using System;

namespace ProceduralGeneration.WorldGen.Density
{
    /// <summary>学习版空间混合；selector 的 [-1,1] 映射至权重 [0,1]。</summary>
    internal sealed class BlendedNoiseFunction : IDensityFunction
    {
        private readonly IDensityFunction a, b, selector;
        public BlendedNoiseFunction(IDensityFunction a, IDensityFunction b, IDensityFunction selector)
        {
            this.a = a ?? throw new ArgumentNullException(nameof(a));
            this.b = b ?? throw new ArgumentNullException(nameof(b));
            this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
        }
        public double Sample(in DensityContext context)
        {
            double t = Math.Max(0, Math.Min(1, (selector.Sample(context) + 1) * 0.5));
            if (t == 0) return a.Sample(context);
            if (t == 1) return b.Sample(context);
            double value = a.Sample(context);
            return value + (b.Sample(context) - value) * t;
        }
    }
}
