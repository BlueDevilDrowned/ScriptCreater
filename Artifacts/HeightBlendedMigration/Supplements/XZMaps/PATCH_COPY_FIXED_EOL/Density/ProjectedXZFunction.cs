using System;

namespace ProceduralGeneration.WorldGen.Density
{
    /// <summary>把输入场投影到 Y=0，使同一 XZ 列的参数保持一致。不是缓存。</summary>
    internal sealed class ProjectedXZFunction : IDensityFunction
    {
        private readonly IDensityFunction input;

        public ProjectedXZFunction(IDensityFunction input)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public double Sample(in DensityContext context)
        {
            var column = new DensityContext(context.X, 0, context.Z);
            return input.Sample(column);
        }
    }
}
