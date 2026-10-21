using System;

namespace ProceduralGeneration.WorldGen.Density
{
public static class DensityFunctions
    {
        public static IDensityFunction Constant(double value) => new ConstantFunction(value);
        public static IDensityFunction Noise(int seed, double scale = 0.01, double amplitude = 1.0)
            => new ValueNoiseFunction(seed, scale, amplitude);
        public static IDensityFunction Add(IDensityFunction a, IDensityFunction b) => new BinaryFunction(a, b, (x, y) => x + y);
        public static IDensityFunction Subtract(IDensityFunction a, IDensityFunction b) => new BinaryFunction(a, b, (x, y) => x - y);
        public static IDensityFunction Multiply(IDensityFunction a, IDensityFunction b) => new BinaryFunction(a, b, (x, y) => x * y);
        public static IDensityFunction Multiply(IDensityFunction a, double b) => Multiply(a, Constant(b));
        public static IDensityFunction Min(IDensityFunction a, IDensityFunction b) => new BinaryFunction(a, b, Math.Min);
        public static IDensityFunction Max(IDensityFunction a, IDensityFunction b) => new BinaryFunction(a, b, Math.Max);
        public static IDensityFunction Abs(IDensityFunction input) => new UnaryFunction(input, Math.Abs);
        public static IDensityFunction Clamp(IDensityFunction input, double min, double max)
            => new ClampFunction(input, min, max);
        public static IDensityFunction RangeChoice(
            IDensityFunction input, double minInclusive, double maxExclusive,
            IDensityFunction inRange, IDensityFunction outOfRange)
            => new RangeChoiceFunction(input, minInclusive, maxExclusive, inRange, outOfRange);
        public static IDensityFunction YClampedGradient(
            int fromY, int toY, double fromValue, double toValue)
            => new YGradientFunction(fromY, toY, fromValue, toValue);
        public static IDensityFunction VerticalSlide(
            IDensityFunction input,
            int minY, int maxY,
            int bottomSize, double bottomTarget,
            int topSize, double topTarget)
            => new VerticalSlideFunction(input, minY, maxY,
                bottomSize, bottomTarget, topSize, topTarget);
        public static IDensityFunction Spline(IDensityFunction coordinate, params CubicSplinePoint[] points)
            => new CubicSplineFunction(coordinate, points);

        /// <summary>以一次采样结果为常量的简单缓存，适合固定坐标的重复读取。</summary>
        public static IDensityFunction CacheOnce(IDensityFunction input) => new CacheOnceFunction(input);
    }
}
