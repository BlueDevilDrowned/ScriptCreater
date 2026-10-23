using System;

namespace ProceduralGeneration.WorldGen.Density
{
    /// <summary>所有高度使用体素坐标；世界范围为 [MinY, MaxYExclusive)。</summary>
    [Serializable]
    public sealed class WorldGenerationSettings
    {
        // 默认值取自 Minecraft Java 1.20.1 主世界（NoiseSettings.OVERWORLD / NoiseRouterData）。
        // 世界范围：minY = -64，height = 384。
        public int MinY = -64;
        public int MaxYExclusive = 320;
        // overworld/depth：yClampedGradient(-64, 320, 1.5, -1.5)。
        public int GradientFromY = -64;
        public int GradientToY = 320;
        public double GradientFromValue = 1.5;
        public double GradientToValue = -1.5;
        // NoiseRouterData.slideOverworld(false, ...) = slide(d, -64, 384, 80, 64, -0.078125, 0, 24, 0.1171875)：
        // 底部：Y 从 minY 到 minY + 24，由 0.1171875 渐变回原密度。
        // 顶部：Y 从 240 到 256（= maxY - 80 到 maxY - 64），由原密度渐变到 -0.078125；256 以上恒为该值。
        public int BottomSlideSize = 24;
        public double BottomSlideTarget = 0.1171875;
        public int TopSlideSize = 16;      // 渐变带宽度 = 80 - 64
        public int TopSlideOffset = 64;    // 渐变结束处距 maxY 的距离
        public double TopSlideTarget = -0.078125;
        // 原版 BlendedNoise 直接加到密度上、没有额外系数；本项目的分形噪声已按权重归一化，对应 1.0。
        public double BaseNoiseStrength = 1.0;
        // 原版 limit 噪声 16 层、selector 8 层；这里只取低频的前几层。
        // 3 层时最高层 selector XZ 0.067 / Y 0.017，在 4x8x4 cell 插值的奈奎斯特极限（0.125 / 0.0625）内。
        public int NoiseOctaves = 3;
        // 原版 BlendedNoise(xz_scale 0.25, y_scale 0.125, xz_factor 80, y_factor 160) 的最低频层，单位 次/方块：
        //   limit 噪声：684.412 * scale / 2^15
        //   selector：684.412 * scale / factor / 2^7
        public double NoiseXZFrequency = 684.412 * 0.25 / 32768.0;          // ≈ 0.005222
        public double NoiseYFrequency = 684.412 * 0.125 / 32768.0;          // ≈ 0.002611
        public double SelectorXZFrequency = 684.412 * 0.25 / 80.0 / 128.0;  // ≈ 0.016709
        public double SelectorYFrequency = 684.412 * 0.125 / 160.0 / 128.0; // ≈ 0.004177

        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>浅拷贝。所有字段都是值类型，适合在后台线程中读取的快照。</summary>
        public WorldGenerationSettings Clone() => (WorldGenerationSettings)MemberwiseClone();

        /// <summary>
        /// Cell 插值的频率检查：角点间距为 d 时，能还原的最高频率是 1/(2d)（奈奎斯特极限）。
        /// 3D 噪声最高层超过这个值会被插值抹平并产生失真。返回 null 表示没问题。
        /// </summary>
        public string GetCellAliasingWarning(int cellWidth, int cellHeight)
        {
            double top = Math.Pow(2, NoiseOctaves - 1);
            double limitXZ = 0.5 / cellWidth;
            double limitY = 0.5 / cellHeight;
            double maxXZ = Math.Max(NoiseXZFrequency, SelectorXZFrequency) * top;
            double maxY = Math.Max(NoiseYFrequency, SelectorYFrequency) * top;
            if (maxXZ <= limitXZ && maxY <= limitY) return null;
            return $"Cell interpolation ({cellWidth}x{cellHeight}x{cellWidth}) cannot represent the highest noise octave: "
                + $"XZ {maxXZ:0.###} (limit {limitXZ:0.###}), Y {maxY:0.###} (limit {limitY:0.###}). "
                + "Reduce NoiseOctaves (3 fits the default frequencies) or the base frequencies, or disable cell interpolation.";
        }
        public void Validate()
        {
            long height = (long)MaxYExclusive - MinY;
            if (height <= 0 || GradientFromY >= GradientToY ||
                (long)GradientToY - GradientFromY > int.MaxValue ||
                BottomSlideSize < 0 || TopSlideSize < 0 || TopSlideOffset < 0 ||
                (long)BottomSlideSize + TopSlideOffset + TopSlideSize > height)
                throw new ArgumentException("Invalid world bounds, gradient range or slide sizes.");
            if (!Finite(BottomSlideTarget) || !Finite(TopSlideTarget))
                throw new ArgumentException("Slide targets must be finite.");
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
