using System;

namespace ProceduralGeneration.WorldGen.Density
{
internal sealed class YGradientFunction : IDensityFunction
    {
        private readonly int fromY;
        private readonly int toY;
        private readonly double fromValue;
        private readonly double toValue;
        public YGradientFunction(int fromY, int toY, double fromValue, double toValue)
        {
            if (fromY == toY) throw new ArgumentException("fromY and toY must differ");
            this.fromY = fromY;
            this.toY = toY;
            this.fromValue = fromValue;
            this.toValue = toValue;
        }
        public double Sample(in DensityContext context)
        {
            double t = (context.Y - fromY) / (double)(toY - fromY);
            t = Math.Max(0.0, Math.Min(1.0, t));
            return fromValue + (toValue - fromValue) * t;
        }
    }
}
