using System;

namespace ProceduralGeneration.WorldGen.Density
{
    /// <summary>所有高度使用体素坐标；世界范围为 [MinY, MaxYExclusive)。</summary>
    [Serializable]
    public sealed class WorldGenerationSettings
    {
        public int MinY = 0;
        public int MaxYExclusive = 160;
        public int GradientFromY = 0;
        public int GradientToY = 160;
        public double GradientFromValue = 0.75;
        public double GradientToValue = -0.75;
        public int BottomSlideSize = 24;
        public int TopSlideSize = 24;
        public double BaseNoiseStrength = 0.35;
        public int NoiseOctaves = 4;
        public double NoiseXZFrequency = 0.02;
        public double NoiseYFrequency = 0.01;
        public double SelectorXZFrequency = 0.008;
        public double SelectorYFrequency = 0.004;

        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public void Validate()
        {
            long height = (long)MaxYExclusive - MinY;
            if (height <= 0 || GradientFromY >= GradientToY ||
                (long)GradientToY - GradientFromY > int.MaxValue ||
                BottomSlideSize < 0 || TopSlideSize < 0 ||
                (long)BottomSlideSize + TopSlideSize > height)
                throw new ArgumentException("Invalid world bounds, gradient range or slide sizes.");
            if (!Finite(GradientFromValue) || !Finite(GradientToValue) ||
                !Finite(BaseNoiseStrength) || BaseNoiseStrength < 0 || NoiseOctaves < 1 || NoiseOctaves > 16)
                throw new ArgumentException("Invalid gradient values, noise strength or octave count.");
            foreach (double f in new[] { NoiseXZFrequency, NoiseYFrequency, SelectorXZFrequency, SelectorYFrequency })
                if (!Finite(f) || f <= 0 || f * Math.Pow(2, NoiseOctaves - 1) > 0.5)
                    throw new ArgumentException("Noise frequencies must be positive; highest octave must not exceed 0.5 per voxel.");
        }

        public void ValidateWindow(int originY, int height)
        {
            Validate();
            if (height <= 0 || originY < MinY || (long)originY + height > MaxYExclusive)
                throw new ArgumentException($"Preview Y window must fit [{MinY}, {MaxYExclusive}).");
        }
    }
}
