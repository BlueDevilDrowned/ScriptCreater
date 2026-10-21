using System;

namespace ProceduralGeneration.WorldGen.Density
{
    /// <summary>归一化分形 ValueNoise：频率逐层翻倍，振幅逐层减半。</summary>
    internal sealed class OctaveNoiseFunction : IDensityFunction
    {
        private readonly ValueNoiseFunction[] layers;
        private readonly double weightSum;
        public OctaveNoiseFunction(int seed, double xzFrequency, double yFrequency, int octaves)
        {
            if (octaves < 1 || octaves > 16) throw new ArgumentOutOfRangeException(nameof(octaves));
            if (!WorldGenerationSettings.Finite(xzFrequency) || !WorldGenerationSettings.Finite(yFrequency) ||
                xzFrequency <= 0 || yFrequency <= 0 ||
                Math.Max(xzFrequency, yFrequency) * Math.Pow(2, octaves - 1) > 0.5)
                throw new ArgumentOutOfRangeException(nameof(xzFrequency));
            layers = new ValueNoiseFunction[octaves];
            double frequency = 1, weight = 1;
            for (int i = 0; i < octaves; i++)
            {
                layers[i] = new ValueNoiseFunction(unchecked(seed + i * 1013), xzFrequency * frequency, yFrequency * frequency, weight);
                weightSum += weight;
                frequency *= 2;
                weight *= 0.5;
            }
        }
        public double Sample(in DensityContext context)
        {
            double value = 0;
            foreach (var layer in layers) value += layer.Sample(context);
            return value / weightSum;
        }
    }
}
