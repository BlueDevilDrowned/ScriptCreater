using System;

namespace ProceduralGeneration.WorldGen.Density
{
public sealed class WorldFields
    {
        public readonly IDensityFunction Temperature;
        public readonly IDensityFunction Vegetation;
        public readonly IDensityFunction Continents;
        public readonly IDensityFunction Erosion;
        public readonly IDensityFunction Depth;
        public readonly IDensityFunction Ridges;
        public readonly IDensityFunction Offset;
        public readonly IDensityFunction Factor;
        public readonly IDensityFunction Jaggedness;
        public readonly IDensityFunction FinalDensity;
        public readonly IDensityFunction FluidFloodedness;
        public readonly IDensityFunction FluidSpread;
        public readonly IDensityFunction Lava;
        public readonly IDensityFunction VeinToggle;
        public readonly IDensityFunction VeinRidged;
        public readonly IDensityFunction VeinGap;

        public WorldFields(
            IDensityFunction temperature,
            IDensityFunction vegetation,
            IDensityFunction continents,
            IDensityFunction erosion,
            IDensityFunction depth,
            IDensityFunction ridges,
            IDensityFunction finalDensity,
            IDensityFunction fluidFloodedness = null,
            IDensityFunction fluidSpread = null,
            IDensityFunction lava = null,
            IDensityFunction veinToggle = null,
            IDensityFunction veinRidged = null,
            IDensityFunction veinGap = null,
            IDensityFunction offset = null,
            IDensityFunction factor = null,
            IDensityFunction jaggedness = null)
        {
            Temperature = temperature ?? throw new ArgumentNullException(nameof(temperature));
            Vegetation = vegetation ?? throw new ArgumentNullException(nameof(vegetation));
            Continents = continents ?? throw new ArgumentNullException(nameof(continents));
            Erosion = erosion ?? throw new ArgumentNullException(nameof(erosion));
            Depth = depth ?? throw new ArgumentNullException(nameof(depth));
            Ridges = ridges ?? throw new ArgumentNullException(nameof(ridges));
            Offset = offset ?? DensityFunctions.Constant(0);
            Factor = factor ?? DensityFunctions.Constant(1);
            Jaggedness = jaggedness ?? DensityFunctions.Constant(0);
            FinalDensity = finalDensity ?? throw new ArgumentNullException(nameof(finalDensity));
            FluidFloodedness = fluidFloodedness ?? DensityFunctions.Constant(0);
            FluidSpread = fluidSpread ?? DensityFunctions.Constant(0);
            Lava = lava ?? DensityFunctions.Constant(0);
            VeinToggle = veinToggle ?? DensityFunctions.Constant(0);
            VeinRidged = veinRidged ?? DensityFunctions.Constant(0);
            VeinGap = veinGap ?? DensityFunctions.Constant(0);
        }

        /// <summary>用一个种子生成可运行的学习版世界场集合。</summary>
        public static WorldFields CreateLearningDefaults(int seed)
        {
            // 地形环境是二维场：同一 XZ 列共享样条参数，不随 Y 漂移。
            IDensityFunction continents = DensityFunctions.Noise2D(seed + 1, 0.0025, 1.0);
            IDensityFunction erosion = DensityFunctions.Noise2D(seed + 2, 0.008, 1.0);
            IDensityFunction ridges = DensityFunctions.Abs(DensityFunctions.Noise2D(seed + 3, 0.012, 1.0));
            IDensityFunction lowlandOffset = DensityFunctions.Spline(erosion,
                new CubicSplinePoint(-1.0, -0.55, 0.0),
                new CubicSplinePoint(0.0, -0.20, 0.1),
                new CubicSplinePoint(1.0, 0.05, 0.0));
            IDensityFunction mountainOffset = DensityFunctions.Spline(erosion,
                new CubicSplinePoint(-1.0, 0.10, 0.0),
                new CubicSplinePoint(0.0, 0.35, 0.2),
                new CubicSplinePoint(1.0, 0.70, 0.0));
            IDensityFunction offset = DensityFunctions.Spline(continents,
                new CubicSplinePoint(-1.0, lowlandOffset, 0.15),
                new CubicSplinePoint(-0.35, lowlandOffset, 0.80),
                new CubicSplinePoint(0.10, DensityFunctions.Constant(0.05), 0.35),
                new CubicSplinePoint(0.55, mountainOffset, 0.55),
                new CubicSplinePoint(1.0, mountainOffset, -0.30));
            IDensityFunction factor = DensityFunctions.Spline(erosion,
                new CubicSplinePoint(-1.0, 1.35, 0.0),
                new CubicSplinePoint(-0.30, 1.10, -0.20),
                new CubicSplinePoint(0.25, 0.65, -0.35),
                new CubicSplinePoint(1.0, 0.30, 0.0));
            IDensityFunction jaggedness = DensityFunctions.Spline(ridges,
                new CubicSplinePoint(0.0, 0.02, 0.0),
                new CubicSplinePoint(0.25, 0.12, 0.40),
                new CubicSplinePoint(0.60, 0.55, 0.80),
                new CubicSplinePoint(1.0, 1.00, 0.20));
            // 垂直渐变只提供中尺度趋势；上下边界由 VerticalSlideFunction 处理。
            IDensityFunction vertical = DensityFunctions.YClampedGradient(0, 160, 0.75, -0.75);
            IDensityFunction depth = DensityFunctions.Add(vertical, offset);
            // 原版 JAGGED 的 Y 采样倍率为 0；Jaggedness 是强度，不是正向密度常量。
            IDensityFunction jaggedNoise = DensityFunctions.Noise2D(seed + 6, 0.035, 1.0);
            IDensityFunction jaggedTerm = DensityFunctions.Multiply(
                jaggedness, DensityFunctions.HalfNegative(jaggedNoise));
            IDensityFunction baseTerrain = DensityFunctions.NoiseGradientDensity(
                factor, DensityFunctions.Add(depth, jaggedTerm));
            IDensityFunction base3DNoise = DensityFunctions.Multiply(
                DensityFunctions.Noise(seed + 4, 0.02, 1.0), 0.35);
            IDensityFunction rawDensity = DensityFunctions.Add(baseTerrain, base3DNoise);
            IDensityFunction finalDensity = DensityFunctions.VerticalSlide(
                rawDensity,
                minY: 0,
                maxY: 160,
                bottomSize: 24,
                bottomTarget: 1.0,
                topSize: 24,
                topTarget: -1.0);

            return new WorldFields(
                DensityFunctions.Noise2D(seed + 10, 0.004, 1),
                DensityFunctions.Noise2D(seed + 11, 0.004, 1),
                continents,
                erosion,
                depth,
                ridges,
                finalDensity,
                DensityFunctions.Noise(seed + 20, 0.02, 1),
                DensityFunctions.Noise(seed + 21, 0.01, 1),
                DensityFunctions.Noise(seed + 22, 0.02, 1),
                DensityFunctions.Noise(seed + 30, 0.04, 1),
                DensityFunctions.Noise(seed + 31, 0.04, 1),
                DensityFunctions.Noise(seed + 32, 0.04, 1),
                offset, factor, jaggedness);
        }
    }
}
