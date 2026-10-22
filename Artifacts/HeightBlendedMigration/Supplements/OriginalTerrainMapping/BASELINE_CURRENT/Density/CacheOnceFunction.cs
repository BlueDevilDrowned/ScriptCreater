using System;

namespace ProceduralGeneration.WorldGen.Density
{
internal sealed class CacheOnceFunction : IDensityFunction
    {
        private readonly IDensityFunction input;
        private bool hasValue;
        private DensityContext lastContext;
        private double lastValue;
        public CacheOnceFunction(IDensityFunction input) { this.input = input ?? throw new ArgumentNullException(nameof(input)); }
        public double Sample(in DensityContext context)
        {
            if (!hasValue || context.X != lastContext.X || context.Y != lastContext.Y || context.Z != lastContext.Z)
            {
                lastContext = context;
                lastValue = input.Sample(context);
                hasValue = true;
            }
            return lastValue;
        }
    }
}
