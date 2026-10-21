using System;

namespace ProceduralGeneration.WorldGen.Density
{
internal sealed class BinaryFunction : IDensityFunction
    {
        private readonly IDensityFunction left;
        private readonly IDensityFunction right;
        private readonly Func<double, double, double> operation;
        public BinaryFunction(IDensityFunction left, IDensityFunction right, Func<double, double, double> operation)
        {
            this.left = left ?? throw new ArgumentNullException(nameof(left));
            this.right = right ?? throw new ArgumentNullException(nameof(right));
            this.operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }
        public double Sample(in DensityContext context)
            => operation(left.Sample(context), right.Sample(context));
    }
}
