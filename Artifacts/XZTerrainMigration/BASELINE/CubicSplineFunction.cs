using System;

namespace ProceduralGeneration.WorldGen.Density
{
    /// <summary>
    /// Minecraft CubicSpline.Multipoint 的学习版：使用 Hermite 三次插值，
    /// 将一个 DensityFunction 的输出映射到另一套地形控制值。
    /// </summary>
    internal sealed class CubicSplineFunction : IDensityFunction
    {
        private readonly IDensityFunction coordinate;
        private readonly CubicSplinePoint[] points;

        public CubicSplineFunction(IDensityFunction coordinate, CubicSplinePoint[] points)
        {
            this.coordinate = coordinate ?? throw new ArgumentNullException(nameof(coordinate));
            if (points == null || points.Length == 0) throw new ArgumentException("Spline needs at least one point.");
            for (int i = 1; i < points.Length; i++)
                if (points[i].Location <= points[i - 1].Location)
                    throw new ArgumentException("Spline locations must be strictly ascending.");
            this.points = (CubicSplinePoint[])points.Clone();
        }

        public double Sample(in DensityContext context)
        {
            double input = coordinate.Sample(context);
            int last = points.Length - 1;
            if (input < points[0].Location)
                return LinearExtend(points[0], input, context);
            if (input >= points[last].Location)
                return LinearExtend(points[last], input, context);

            int index = FindSegment(input);
            CubicSplinePoint left = points[index];
            CubicSplinePoint right = points[index + 1];
            double length = right.Location - left.Location;
            double t = (input - left.Location) / length;
            double leftValue = EvaluateValue(left, context);
            double rightValue = EvaluateValue(right, context);
            double delta = rightValue - leftValue;
            double p = left.Derivative * length - delta;
            double q = delta - right.Derivative * length;
            return Lerp(leftValue, rightValue, t)
                + t * (1.0 - t) * Lerp(p, q, t);
        }

        private static double EvaluateValue(CubicSplinePoint point, in DensityContext context)
            => point.ValueFunction == null ? point.Value : point.ValueFunction.Sample(context);

        private int FindSegment(double input)
        {
            int low = 0;
            int high = points.Length - 1;
            while (low + 1 < high)
            {
                int middle = (low + high) / 2;
                if (input < points[middle].Location) high = middle;
                else low = middle;
            }
            return low;
        }

        private static double LinearExtend(CubicSplinePoint point, double input, in DensityContext context)
            => EvaluateValue(point, context) + point.Derivative * (input - point.Location);

        private static double Lerp(double a, double b, double t)
            => a + (b - a) * t;
    }
}
