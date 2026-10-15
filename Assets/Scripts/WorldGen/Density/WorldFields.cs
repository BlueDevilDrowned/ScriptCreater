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
            IDensityFunction veinGap = null)
        {
            Temperature = temperature ?? throw new ArgumentNullException(nameof(temperature));
            Vegetation = vegetation ?? throw new ArgumentNullException(nameof(vegetation));
            Continents = continents ?? throw new ArgumentNullException(nameof(continents));
            Erosion = erosion ?? throw new ArgumentNullException(nameof(erosion));
            Depth = depth ?? throw new ArgumentNullException(nameof(depth));
            Ridges = ridges ?? throw new ArgumentNullException(nameof(ridges));
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
            IDensityFunction continents = DensityFunctions.Noise(seed + 1, 0.0025, 1.0);
            IDensityFunction erosion = DensityFunctions.Noise(seed + 2, 0.008, 1.0);
            IDensityFunction ridges = DensityFunctions.Abs(DensityFunctions.Noise(seed + 3, 0.012, 1.0));
            IDensityFunction mountains = DensityFunctions.Multiply(
                DensityFunctions.Clamp(DensityFunctions.Subtract(continents, DensityFunctions.Constant(0.15)), 0, 1),
                DensityFunctions.Constant(55));
            IDensityFunction detail = DensityFunctions.Multiply(DensityFunctions.Noise(seed + 4, 0.035, 1), 8);
            IDensityFunction vertical = DensityFunctions.YClampedGradient(0, 160, 1.0, -1.0);
            IDensityFunction finalDensity = DensityFunctions.Add(
                DensityFunctions.Add(mountains, detail),
                DensityFunctions.Multiply(vertical, 35));

            return new WorldFields(
                DensityFunctions.Noise(seed + 10, 0.004, 1),
                DensityFunctions.Noise(seed + 11, 0.004, 1),
                continents,
                erosion,
                DensityFunctions.Noise(seed + 5, 0.006, 1),
                ridges,
                finalDensity,
                DensityFunctions.Noise(seed + 20, 0.02, 1),
                DensityFunctions.Noise(seed + 21, 0.01, 1),
                DensityFunctions.Noise(seed + 22, 0.02, 1),
                DensityFunctions.Noise(seed + 30, 0.04, 1),
                DensityFunctions.Noise(seed + 31, 0.04, 1),
                DensityFunctions.Noise(seed + 32, 0.04, 1));
        }
    }
}
