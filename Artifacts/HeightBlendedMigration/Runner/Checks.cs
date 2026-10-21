using System;
using ProceduralGeneration.WorldGen.Density;
class Checks {
 static void Assert(bool ok,string message) { if(!ok) throw new Exception(message); }
 static void Near(double a,double b) => Assert(Math.Abs(a-b)<1e-12,$"{a} != {b}");
 static void Reject(Action a) { try { a(); } catch(ArgumentException) { return; } throw new Exception("Expected rejection"); }
 static void Main() {
  var s=new WorldGenerationSettings(); s.ValidateWindow(0,160); s.ValidateWindow(32,64);
  Reject(()=>s.ValidateWindow(-1,128)); Reject(()=>s.ValidateWindow(33,128)); Reject(()=>s.ValidateWindow(0,0));
  var moved=new WorldGenerationSettings { MinY=-64, MaxYExclusive=192, GradientFromY=-64, GradientToY=192 };
  moved.ValidateWindow(-64,256); var f=WorldFields.CreateLearningDefaults(12345,moved);
  Near(f.FinalDensity.Sample(new DensityContext(37,-64,-81)),1); Near(f.FinalDensity.Sample(new DensityContext(37,192,-81)),-1);
  Near(f.Depth.Sample(new DensityContext(37,20,-81))-f.Depth.Sample(new DensityContext(37,21,-81)),1.5/256);
  Reject(()=>new WorldGenerationSettings { MaxYExclusive=0 }.Validate());
  Reject(()=>new WorldGenerationSettings { TopSlideSize=150 }.Validate());
  Reject(()=>new WorldGenerationSettings { BaseNoiseStrength=double.NaN }.Validate());
  Reject(()=>new WorldGenerationSettings { NoiseOctaves=16 }.Validate());
  Console.WriteLine("PASS: exclusive world bounds, independent windows, moved gradient/slides, invalid settings");
  var point=new DensityContext(37,51,-81);
  foreach(double t in new[]{-2.0,-1,0,1,2}) {
   var b=DensityFunctions.BlendNoise(DensityFunctions.Constant(0.6),DensityFunctions.Constant(-0.4),DensityFunctions.Constant(t));
   Near(b.Sample(point),0.6-Math.Max(0,Math.Min(1,(t+1)/2)));
  }
  var one=DensityFunctions.FractalNoise(17,0.02,0.02,1); Near(one.Sample(point),DensityFunctions.Noise(17,0.02).Sample(point));
  foreach(int seed in new[]{-7,0,12345}) {
   var a=DensityFunctions.FractalNoise(seed); var b=DensityFunctions.FractalNoise(seed+10004); var m=DensityFunctions.FractalNoise(seed+20004,0.008,0.004);
   var blend=DensityFunctions.BlendNoise(a,b,m); bool variesY=false, variesSeed=false;
   var world=WorldFields.CreateLearningDefaults(seed);
   for(int x=-100;x<=100;x+=25) for(int y=0;y<=160;y+=8) {
    var p=new DensityContext(x,y,-x); double v=blend.Sample(p); Assert(!double.IsNaN(v)&&Math.Abs(v)<=1,"bound");
    Near(v,blend.Sample(p)); Near(v,a.Sample(p)+(b.Sample(p)-a.Sample(p))*((m.Sample(p)+1)/2));
    variesY |= Math.Abs(v-blend.Sample(new DensityContext(x,y+1,-x)))>1e-8;
    variesSeed |= Math.Abs(a.Sample(p)-b.Sample(p))>1e-8;
    Near(world.Offset.Sample(p),world.Offset.Sample(new DensityContext(x,y+1,-x)));
    Assert(double.IsFinite(world.FinalDensity.Sample(p)),"finite world");
   }
   Assert(variesY&&variesSeed,"variation");
  }
  Console.WriteLine("PASS: blend clamp/endpoints/midpoint, one-octave identity, 3 seeds x 189 samples, bounds/determinism/Y/seed/XZ columns");
  var zero=new WorldGenerationSettings { BaseNoiseStrength=0 }; var z=WorldFields.CreateLearningDefaults(12345,zero);
  var jag=DensityFunctions.Noise2D(12351,0.035,1); double j=jag.Sample(point); double term=z.Factor.Sample(point)*(z.Depth.Sample(point)+z.Jaggedness.Sample(point)*(j<0?j*0.5:j));
  Near(z.FinalDensity.Sample(point),term<0?term:term*4);
  Console.WriteLine("PASS: zero-strength preserves terrain formula; ALL_CHECKS_PASSED");
 }
}
