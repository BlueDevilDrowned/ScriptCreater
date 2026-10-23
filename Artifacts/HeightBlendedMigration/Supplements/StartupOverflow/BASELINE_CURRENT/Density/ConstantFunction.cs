using System;

namespace ProceduralGeneration.WorldGen.Density
{
internal sealed class ConstantFunction : IDensityFunction
    {
        private readonly double value;
        public ConstantFunction(double value) { this.value = value; }
        public double Sample(in DensityContext context) => value;
    }
}
