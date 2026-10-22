using System;

namespace ProceduralGeneration.WorldGen.Density
{
public interface IDensityFunction
    {
        double Sample(in DensityContext context);
    }
}
