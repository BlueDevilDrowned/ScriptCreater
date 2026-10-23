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
        public static WorldFields CreateLearningDefaults(int seed, WorldGenerationSettings settings = null)
        {
            settings = settings ?? new WorldGenerationSettings();
            settings.Validate();
            // 地形环境是二维场：同一 XZ 列共享样条参数，不随 Y 漂移。
            // 对应 MC 的 flatCache/cache2d：二维场和三条样条都按列缓存，同一列只算一次。
            // 样条内部会多次读取同一个基础噪声，所以基础噪声本身也要缓存。
            // 注意：缓存带可变状态，WorldFields 实例不是线程安全的，每个线程/任务各建一份。
            IDensityFunction continents = DensityFunctions.Cache2D(DensityFunctions.Noise2D(seed + 1, 0.0025, 1.0));
            IDensityFunction erosion = DensityFunctions.Cache2D(DensityFunctions.Noise2D(seed + 2, 0.008, 1.0));
            IDensityFunction ridges = DensityFunctions.Cache2D(DensityFunctions.Noise2D(seed + 3, 0.012, 1.0));
            IDensityFunction foldedRidges = DensityFunctions.Cache2D(OriginalTerrainProvider.PeaksAndValleys(ridges));
            IDensityFunction offset = DensityFunctions.Cache2D(DensityFunctions.Add(DensityFunctions.Constant(-0.50375), OriginalTerrainProvider.Offset(continents, erosion, foldedRidges)));
            IDensityFunction factor = DensityFunctions.Cache2D(OriginalTerrainProvider.Factor(continents, erosion, foldedRidges));
            IDensityFunction jaggedness = DensityFunctions.Cache2D(OriginalTerrainProvider.Jaggedness(continents, erosion, foldedRidges));
            // 垂直渐变只提供中尺度趋势；上下边界由 VerticalSlideFunction 处理。
            IDensityFunction vertical = DensityFunctions.YClampedGradient(settings.GradientFromY, settings.GradientToY, settings.GradientFromValue, settings.GradientToValue);
            IDensityFunction depth = DensityFunctions.Add(vertical, offset);
            // 原版 JAGGED 的 Y 采样倍率为 0；Jaggedness 是强度，不是正向密度常量。
            IDensityFunction jaggedNoise = DensityFunctions.Cache2D(DensityFunctions.Noise2D(seed + 6, 0.035, 1.0));
            IDensityFunction jaggedTerm = DensityFunctions.Multiply(
                jaggedness, DensityFunctions.HalfNegative(jaggedNoise));
            IDensityFunction baseTerrain = DensityFunctions.NoiseGradientDensity(
                factor, DensityFunctions.Add(depth, jaggedTerm));
            IDensityFunction noiseA = DensityFunctions.FractalNoise(unchecked(seed + 4), settings.NoiseXZFrequency, settings.NoiseYFrequency, settings.NoiseOctaves);
            IDensityFunction noiseB = DensityFunctions.FractalNoise(unchecked(seed + 10004), settings.NoiseXZFrequency, settings.NoiseYFrequency, settings.NoiseOctaves);
            IDensityFunction selector = DensityFunctions.FractalNoise(unchecked(seed + 20004), settings.SelectorXZFrequency, settings.SelectorYFrequency, settings.NoiseOctaves);
            IDensityFunction base3DNoise = DensityFunctions.Multiply(
                DensityFunctions.BlendNoise(noiseA, noiseB, selector), settings.BaseNoiseStrength);
            IDensityFunction rawDensity = DensityFunctions.Add(baseTerrain, base3DNoise);
            IDensityFunction finalDensity = DensityFunctions.VerticalSlide(
                rawDensity,
                minY: settings.MinY,
                maxY: settings.MaxYExclusive,
                bottomSize: settings.BottomSlideSize,
                bottomTarget: settings.BottomSlideTarget,
                topSize: settings.TopSlideSize,
                topTarget: settings.TopSlideTarget,
                topOffset: settings.TopSlideOffset);

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
