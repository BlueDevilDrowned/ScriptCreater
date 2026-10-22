using System;

namespace ProceduralGeneration.WorldGen.Density
{
internal sealed class RangeChoiceFunction : IDensityFunction
    {
        private readonly IDensityFunction input;
        private readonly double minInclusive;
        private readonly double maxExclusive;
        private readonly IDensityFunction inRange;
        private readonly IDensityFunction outOfRange;
        public RangeChoiceFunction(IDensityFunction input, double minInclusive, double maxExclusive,
            IDensityFunction inRange, IDensityFunction outOfRange)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.inRange = inRange ?? throw new ArgumentNullException(nameof(inRange));
            this.outOfRange = outOfRange ?? throw new ArgumentNullException(nameof(outOfRange));
            this.minInclusive = minInclusive;
            this.maxExclusive = maxExclusive;
        }
        public double Sample(in DensityContext context)
        {
            double value = input.Sample(context);
            return value >= minInclusive && value < maxExclusive
                ? inRange.Sample(context)
                : outOfRange.Sample(context);
        }
    }
}
