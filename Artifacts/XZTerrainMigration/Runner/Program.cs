using System;
using ProceduralGeneration.WorldGen.Density;

internal static class Program
{
    static void Main()
    {
        var fields = WorldFields.CreateLearningDefaults(12345);
        string[] names = { "Continents", "Erosion", "Ridges", "Offset", "Factor", "Jaggedness" };
        foreach (string name in names)
        {
            var field = (IDensityFunction)typeof(WorldFields).GetField(name).GetValue(fields);
            double a = field.Sample(new DensityContext(37, 24, -81));
            double b = field.Sample(new DensityContext(37, 120, -81));
            Console.WriteLine($"{name}: y24={a:R} y120={b:R} same={a == b}");
        }
        foreach (int y in new[] { 0, 24, 80, 120, 160 })
        {
            var p = new DensityContext(37, y, -81);
            double value = fields.FinalDensity.Sample(p);
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new Exception("Non-finite density");
            Console.WriteLine($"y={y} depth={fields.Depth.Sample(p):R} density={value:R}");
        }
        Console.WriteLine("SAMPLE_SUCCESS");
    }
}
