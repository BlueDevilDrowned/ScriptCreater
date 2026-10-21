from pathlib import Path
p=Path('Artifacts/HeightBlendedMigration/MODIFIED_FILE'); d=p/'Density'
def write(name,text): (d/name).write_text(text,encoding='utf-8')
write('WorldGenerationSettings.cs','''using System;

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
''')
write('OctaveNoiseFunction.cs','''using System;

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
''')
write('BlendedNoiseFunction.cs','''using System;

namespace ProceduralGeneration.WorldGen.Density
{
    /// <summary>学习版空间混合；selector 的 [-1,1] 映射至权重 [0,1]。</summary>
    internal sealed class BlendedNoiseFunction : IDensityFunction
    {
        private readonly IDensityFunction a, b, selector;
        public BlendedNoiseFunction(IDensityFunction a, IDensityFunction b, IDensityFunction selector)
        {
            this.a = a ?? throw new ArgumentNullException(nameof(a));
            this.b = b ?? throw new ArgumentNullException(nameof(b));
            this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
        }
        public double Sample(in DensityContext context)
        {
            double t = Math.Max(0, Math.Min(1, (selector.Sample(context) + 1) * 0.5));
            if (t == 0) return a.Sample(context);
            if (t == 1) return b.Sample(context);
            double value = a.Sample(context);
            return value + (b.Sample(context) - value) * t;
        }
    }
}
''')
f=d/'ValueNoiseFunction.cs'; s=f.read_text(encoding='utf-8-sig'); s=s.replace('private readonly double amplitude;', 'private readonly double amplitude;\n        private readonly double yScale;'); s=s.replace('''        {
            if (scale <= 0)''','''            : this(seed, scale, scale, amplitude) { }
        public ValueNoiseFunction(int seed, double scale, double yScale, double amplitude)
        {
            if (!WorldGenerationSettings.Finite(yScale) || yScale <= 0) throw new ArgumentOutOfRangeException(nameof(yScale));
            this.yScale = yScale;
            if (scale <= 0)''',1); s=s.replace('context.Y * scale','context.Y * yScale'); f.write_text(s,encoding='utf-8')
f=d/'DensityFunctions.cs'; s=f.read_text(encoding='utf-8-sig'); s=s.replace('        /// <summary>固定输入场', '''        public static IDensityFunction FractalNoise(int seed, double xzFrequency = 0.02, double yFrequency = 0.01, int octaves = 4)
            => new OctaveNoiseFunction(seed, xzFrequency, yFrequency, octaves);
        public static IDensityFunction BlendNoise(IDensityFunction a, IDensityFunction b, IDensityFunction selector)
            => new BlendedNoiseFunction(a, b, selector);
        /// <summary>固定输入场''',1); f.write_text(s,encoding='utf-8')
f=d/'WorldFields.cs'; s=f.read_text(encoding='utf-8-sig'); s=s.replace('CreateLearningDefaults(int seed)','CreateLearningDefaults(int seed, WorldGenerationSettings settings = null)'); s=s.replace('''            // 地形环境''','''            settings = settings ?? new WorldGenerationSettings();
            settings.Validate();
            // 地形环境''',1); s=s.replace('DensityFunctions.YClampedGradient(0, 160, 0.75, -0.75)','DensityFunctions.YClampedGradient(settings.GradientFromY, settings.GradientToY, settings.GradientFromValue, settings.GradientToValue)'); s=s.replace('''            IDensityFunction base3DNoise = DensityFunctions.Multiply(
                DensityFunctions.Noise(seed + 4, 0.02, 1.0), 0.35);''','''            IDensityFunction noiseA = DensityFunctions.FractalNoise(unchecked(seed + 4), settings.NoiseXZFrequency, settings.NoiseYFrequency, settings.NoiseOctaves);
            IDensityFunction noiseB = DensityFunctions.FractalNoise(unchecked(seed + 10004), settings.NoiseXZFrequency, settings.NoiseYFrequency, settings.NoiseOctaves);
            IDensityFunction selector = DensityFunctions.FractalNoise(unchecked(seed + 20004), settings.SelectorXZFrequency, settings.SelectorYFrequency, settings.NoiseOctaves);
            IDensityFunction base3DNoise = DensityFunctions.Multiply(
                DensityFunctions.BlendNoise(noiseA, noiseB, selector), settings.BaseNoiseStrength);'''); s=s.replace('minY: 0','minY: settings.MinY').replace('maxY: 160','maxY: settings.MaxYExclusive').replace('bottomSize: 24','bottomSize: settings.BottomSlideSize').replace('topSize: 24','topSize: settings.TopSlideSize'); f.write_text(s,encoding='utf-8')
f=p/'Voxels/VoxelWorldPreview.cs'; s=f.read_text(encoding='utf-8-sig'); s=s.replace('        [SerializeField] private int seed', '        [SerializeField] private WorldGenerationSettings generationSettings = new WorldGenerationSettings();\n        [SerializeField] private int seed'); s=s.replace('            worldSize = size;','            ValidateWindow(origin.y, size.y);\n            worldSize = size;'); s=s.replace('''        private void Start()
''','''        private void ValidateWindow(int originY, int height)
        {
            generationSettings = generationSettings ?? new WorldGenerationSettings();
            generationSettings.ValidateWindow(originY, height);
        }

        private void Start()
'''); s=s.replace('''            if (generationRoutine != null) StopCoroutine(generationRoutine);''','''            ValidateWindow(sampleOrigin.y, worldSize.y);
            if (generationRoutine != null) StopCoroutine(generationRoutine);''',1); s=s.replace('            var timer = Stopwatch.StartNew();','            ValidateWindow(sampleOrigin.y, worldSize.y);\n            var timer = Stopwatch.StartNew();'); s=s.replace('WorldFields.CreateLearningDefaults(seed)','WorldFields.CreateLearningDefaults(seed, generationSettings)'); f.write_text(s,encoding='utf-8')
f=p/'Voxels/VoxelWorldControls.cs'; s=f.read_text(encoding='utf-8-sig'); s=s.replace('catch (ArgumentException)','catch (ArgumentException error)').replace('message = "World size and voxel size must be positive and finite.";','message = error.Message;'); f.write_text(s,encoding='utf-8')
f=d/'世界生成变更记录.md'; s=f.read_text(encoding='utf-8-sig'); s+='''

## 2026-10-19：统一世界高度与分形混合基础噪声

- 新增独立 `WorldGenerationSettings`，世界范围为 `[MinY, MaxYExclusive)`，默认 `[0,160)`，单位是体素坐标。Inspector 可编辑；预览窗口仍独立，越界时报错，密度函数自身仍可在任意 Y 采样。
- 垂直梯度端点单独配置，不随预览窗口移动。上下滑动使用统一世界边界，默认厚度 24；拒绝倒置范围、重叠滑动、非有限参数等。
- 新增独立 `OctaveNoiseFunction`、`BlendedNoiseFunction`；分形采用频率翻倍、振幅减半、除以权重总和的 ValueNoise。每层独立稳定种子，支持不同 XZ/Y 频率。
- A/B/选择场各 4 层。A/B 默认 XZ=0.02、Y=0.01；选择场 XZ=0.008、Y=0.004。最高层频率限制为每体素 0.5，避免配置明显超出体素采样能力。
- `t=clamp((selector+1)/2,0,1)`，`Base3D=Strength*lerp(A,B,t)`；默认 Strength=0.35。替换原单层基础噪声，不改变二维环境场与算子顺序。
- 这是学习版结构，不复刻原版 ImprovedNoise、16/16/8 octave、smear、随机序列和数值尺度。分形与混合同时使用。
- 验证记录位于 `Artifacts/HeightBlendedMigration/VERIFICATION.txt`，包含原状态、修改、回滚及独立检查。Unity 场景视觉结果尚未检查。
'''; f.write_text(s,encoding='utf-8')
