using System;

namespace ProceduralGeneration.WorldGen.Density
{
internal sealed class UnaryFunction : IDensityFunction
    {
        private readonly IDensityFunction input;
        private readonly Func<double, double> operation;
        public UnaryFunction(IDensityFunction input, Func<double, double> operation)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }
        public double Sample(in DensityContext context) => operation(input.Sample(context));
    }
}
