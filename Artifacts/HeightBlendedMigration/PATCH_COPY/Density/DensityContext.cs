using System;

namespace ProceduralGeneration.WorldGen.Density
{
public readonly struct DensityContext
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public DensityContext(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
