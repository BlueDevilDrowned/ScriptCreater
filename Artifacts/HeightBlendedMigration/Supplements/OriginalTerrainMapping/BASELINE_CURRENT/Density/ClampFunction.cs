using System;

namespace ProceduralGeneration.WorldGen.Density
{
internal sealed class ClampFunction : IDensityFunction
    {
        private readonly IDensityFunction input;
        private readonly double min;
        private readonly double max;
        public ClampFunction(IDensityFunction input, double min, double max)
        {
            if (min > max) throw new ArgumentException("min must be <= max");
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.min = min;
            this.max = max;
        }
        public double Sample(in DensityContext context)
            => Math.Max(min, Math.Min(max, input.Sample(context)));
    }
}
