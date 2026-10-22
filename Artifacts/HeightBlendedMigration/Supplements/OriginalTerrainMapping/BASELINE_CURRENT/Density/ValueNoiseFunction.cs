using System;

namespace ProceduralGeneration.WorldGen.Density
{
internal sealed class ValueNoiseFunction : IDensityFunction
    {
        private readonly int seed;
        private readonly double scale;
        private readonly double amplitude;
        private readonly double yScale;
        public ValueNoiseFunction(int seed, double scale, double amplitude)
            : this(seed, scale, scale, amplitude) { }
        public ValueNoiseFunction(int seed, double scale, double yScale, double amplitude)
        {
            if (!WorldGenerationSettings.Finite(yScale) || yScale <= 0) throw new ArgumentOutOfRangeException(nameof(yScale));
            this.yScale = yScale;
            if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            this.seed = seed;
            this.scale = scale;
            this.amplitude = amplitude;
        }
        public double Sample(in DensityContext context)
        {
            double x = context.X * scale;
            double y = context.Y * yScale;
            double z = context.Z * scale;
            int x0 = FloorToInt(x), y0 = FloorToInt(y), z0 = FloorToInt(z);
            double tx = Smooth(x - x0), ty = Smooth(y - y0), tz = Smooth(z - z0);
            double c000 = Lattice(x0, y0, z0), c100 = Lattice(x0 + 1, y0, z0);
            double c010 = Lattice(x0, y0 + 1, z0), c110 = Lattice(x0 + 1, y0 + 1, z0);
            double c001 = Lattice(x0, y0, z0 + 1), c101 = Lattice(x0 + 1, y0, z0 + 1);
            double c011 = Lattice(x0, y0 + 1, z0 + 1), c111 = Lattice(x0 + 1, y0 + 1, z0 + 1);
            double x00 = Lerp(c000, c100, tx), x10 = Lerp(c010, c110, tx);
            double x01 = Lerp(c001, c101, tx), x11 = Lerp(c011, c111, tx);
            return Lerp(Lerp(x00, x10, ty), Lerp(x01, x11, ty), tz) * amplitude;
        }
        private double Lattice(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)x * 374761393u;
                h ^= (uint)y * 668265263u;
                h ^= (uint)z * 2147483647u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h / (double)uint.MaxValue) * 2.0 - 1.0;
            }
        }
        private static int FloorToInt(double value) => (int)Math.Floor(value);
        private static double Smooth(double value) => value * value * (3.0 - 2.0 * value);
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
