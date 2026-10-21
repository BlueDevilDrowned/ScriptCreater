using System;

namespace ProceduralGeneration.WorldGen.Density
{
    /// <summary>只读采样当前世界场。P 是参考变换，不会修改世界的山脊输入。</summary>
    public sealed class XZFieldMap
    {
        public enum Field { Continents, Erosion, Ridges, PeaksAndValleys }

        public int OriginX { get; }
        public int OriginZ { get; }
        public int Width { get; }
        public int Depth { get; }
        public int Resolution { get; }
        private readonly double[][] values;
        private readonly double[] minima = new double[4];
        private readonly double[] maxima = new double[4];

        public XZFieldMap(WorldFields fields, int originX, int originZ, int width, int depth, int resolution)
        {
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            if (width < 1 || depth < 1 || resolution < 2 || resolution > 512)
                throw new ArgumentOutOfRangeException(nameof(resolution), "Positive spans and resolution 2..512 are required.");
            if ((long)originX + width - 1 > int.MaxValue || (long)originZ + depth - 1 > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(originX), "Sample bounds exceed integer coordinates.");
            OriginX = originX;
            OriginZ = originZ;
            Width = width;
            Depth = depth;
            Resolution = resolution;
            values = new double[4][];
            for (int f = 0; f < 4; f++)
            {
                values[f] = new double[resolution * resolution];
                minima[f] = double.PositiveInfinity;
                maxima[f] = double.NegativeInfinity;
            }
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                var point = new DensityContext(GetX(x), 0, GetZ(z));
                int index = z * resolution + x;
                Store(0, index, fields.Continents.Sample(point));
                Store(1, index, fields.Erosion.Sample(point));
                double ridge = fields.Ridges.Sample(point);
                Store(2, index, ridge);
                Store(3, index, -(Math.Abs(Math.Abs(ridge) - 2.0 / 3.0) - 1.0 / 3.0) * 3.0);
            }
        }

        private void Store(int field, int index, double value)
        {
            values[field][index] = value;
            minima[field] = Math.Min(minima[field], value);
            maxima[field] = Math.Max(maxima[field], value);
        }

        private int Coordinate(int origin, int span, int index)
        {
            if (index < 0 || index >= Resolution) throw new ArgumentOutOfRangeException(nameof(index));
            return (int)(origin + (long)index * (span - 1) / (Resolution - 1));
        }

        public int GetX(int x) => Coordinate(OriginX, Width, x);
        public int GetZ(int z) => Coordinate(OriginZ, Depth, z);
        public double Minimum(Field field) => minima[(int)field];
        public double Maximum(Field field) => maxima[(int)field];
        public double GetValue(Field field, int x, int z)
        {
            if (x < 0 || x >= Resolution || z < 0 || z >= Resolution)
                throw new ArgumentOutOfRangeException(nameof(x));
            return values[(int)field][z * Resolution + x];
        }
    }
}
