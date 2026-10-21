using System;
using ProceduralGeneration.WorldGen.Density;

internal static class Checks
{
    static double Sample(IDensityFunction f, int x, int y, int z) => f.Sample(new DensityContext(x,y,z));
    static void Require(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
    }
    static void Main()
    {
        var p = new DensityContext(0,0,0);
        Require(DensityFunctions.HalfNegative(DensityFunctions.Constant(-2)).Sample(p) == -1, "HalfNegative");
        Require(DensityFunctions.NoiseGradientDensity(DensityFunctions.Constant(3), DensityFunctions.Constant(-2)).Sample(p) == -6, "negative branch order");
        Require(DensityFunctions.NoiseGradientDensity(DensityFunctions.Constant(3), DensityFunctions.Constant(2)).Sample(p) == 24, "positive branch order");
        Console.WriteLine("PASS: source gradient positive/negative branches");
        foreach (int seed in new[] { 0, 12345, -317 })
        {
            var w = WorldFields.CreateLearningDefaults(seed);
            IDensityFunction[] columns = {w.Continents,w.Erosion,w.Ridges,w.Offset,w.Factor,w.Jaggedness,w.Temperature,w.Vegetation};
            foreach (int x in new[] {-501, 37, 702})
            foreach (int z in new[] {-81, 0, 419})
            {
                foreach (var f in columns)
                foreach (int y in new[] {-40, 0, 24, 120, 160, 200})
                    Require(Sample(f,x,y,z) == Sample(f,x,0,z), "XZ invariance");
                double difference = Sample(w.Depth,x,120,z) - Sample(w.Depth,x,24,z);
                Require(Math.Abs(difference + 0.9) < 1e-12, "Depth vertical slope");
                Require(Sample(w.FinalDensity,x,0,z) == 1, "bottom");
                Require(Sample(w.FinalDensity,x,160,z) == -1, "top");
                for (int y=0; y<=160; y+=8)
                {
                    double d = Sample(w.FinalDensity,x,y,z);
                    Require(!double.IsNaN(d) && !double.IsInfinity(d), "finite");
                }
            }
        }
        Console.WriteLine("PASS: 3 seeds x 9 columns; XZ invariance, Depth slope, finite density, boundaries");
        var a = WorldFields.CreateLearningDefaults(12345);
        var b = WorldFields.CreateLearningDefaults(54321);
        Require(Sample(a.Offset,37,24,-81) != Sample(a.Offset,702,24,419), "XZ variation");
        Require(Sample(a.Offset,37,24,-81) != Sample(b.Offset,37,24,-81), "seed variation");
        var noise = DensityFunctions.Noise(12349,0.02,1);
        Require(Sample(noise,37,24,-81) != Sample(noise,37,120,-81), "3D variation retained");
        Console.WriteLine("PASS: XZ and seed variation; base noise still varies with Y");
        Console.WriteLine("CHECKS_SUCCESS");
    }
}
