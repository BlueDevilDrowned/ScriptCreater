using System;
using System.Globalization;
using System.IO;
using ProceduralGeneration.WorldGen.Density;
class MapChecks
{
 static void Assert(bool ok,string message) { if(!ok) throw new Exception(message); }
 static void Near(double a,double b) => Assert(Math.Abs(a-b)<1e-12,$"{a} != {b}");
 static void Reject(Action action) { try { action(); } catch(ArgumentException) { return; } throw new Exception("Expected rejection"); }
 static void Main()
 {
  CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
  var fields=WorldFields.CreateLearningDefaults(12345);
  var map=new XZFieldMap(fields,-512,-512,1024,1024,256);
  Near(map.GetX(0),-512); Near(map.GetX(255),511); Near(map.GetZ(255),511);
  foreach(int x in new[]{0,1,127,255}) foreach(int z in new[]{0,1,127,255})
  {
   var p=new DensityContext(map.GetX(x),0,map.GetZ(z));
   Near(map.GetValue(XZFieldMap.Field.Continents,x,z),fields.Continents.Sample(p));
   Near(map.GetValue(XZFieldMap.Field.Erosion,x,z),fields.Erosion.Sample(p));
   double r=map.GetValue(XZFieldMap.Field.Ridges,x,z);
   Near(r,fields.Ridges.Sample(p));
   Near(map.GetValue(XZFieldMap.Field.PeaksAndValleys,x,z),-3*(Math.Abs(Math.Abs(r)-2.0/3)-1.0/3));
   Near(map.GetValue(XZFieldMap.Field.Continents,x,z),fields.Continents.Sample(new DensityContext(p.X,120,p.Z)));
  }
  var same=new XZFieldMap(fields,-512,-512,1024,1024,256);
  var other=new XZFieldMap(WorldFields.CreateLearningDefaults(-7),-512,-512,1024,1024,256);
  bool varies=false;
  foreach(XZFieldMap.Field field in Enum.GetValues(typeof(XZFieldMap.Field)))
  {
   for(int x=0;x<256;x+=17) for(int z=0;z<256;z+=17)
   {
    double v=map.GetValue(field,x,z); Assert(double.IsFinite(v)&&v>=-1.0000001&&v<=1.0000001,"range");
    Near(v,same.GetValue(field,x,z)); varies |= Math.Abs(v-other.GetValue(field,x,z))>1e-7;
   }
   Console.WriteLine($"{field}: min={map.Minimum(field):F6}, max={map.Maximum(field):F6}");
  }
  Assert(varies,"seed variation");
  var small=new XZFieldMap(fields,37,-81,1,1,2);
  Near(small.GetValue(XZFieldMap.Field.Erosion,1,1),fields.Erosion.Sample(new DensityContext(37,0,-81)));
  var edge=new XZFieldMap(fields,int.MaxValue-1,int.MinValue,2,1,2);
  Near(edge.GetX(1),int.MaxValue);
  Reject(()=>new XZFieldMap(fields,int.MaxValue,0,2,1,2));
  Reject(()=>new XZFieldMap(fields,0,0,0,1,2));
  Reject(()=>new XZFieldMap(fields,0,0,1,1,513));
  Reject(()=>map.GetValue(XZFieldMap.Field.Erosion,256,0));
  // Overlapping windows must sample the same global coordinates, independent of resolution.
  var overlap=new XZFieldMap(fields,map.GetX(127),map.GetZ(127),1,1,2);
  Near(overlap.GetValue(XZFieldMap.Field.Ridges,0,0),map.GetValue(XZFieldMap.Field.Ridges,127,127));
  Console.WriteLine("PASS: production-field equality, Y independence, P formula, fixed bounds, deterministic seed, endpoints, degenerate span, overflow, overlapping windows");
  string output=Environment.GetEnvironmentVariable("XZ_MAP_EXPORT");
  if(!string.IsNullOrEmpty(output))
  {
   using(var w=new StreamWriter(output))
   {
    w.WriteLine("x,z,C,E,R,P");
    for(int z=0;z<map.Resolution;z++) for(int x=0;x<map.Resolution;x++)
     w.WriteLine($"{map.GetX(x)},{map.GetZ(z)},{map.GetValue(XZFieldMap.Field.Continents,x,z):R},{map.GetValue(XZFieldMap.Field.Erosion,x,z):R},{map.GetValue(XZFieldMap.Field.Ridges,x,z):R},{map.GetValue(XZFieldMap.Field.PeaksAndValleys,x,z):R}");
   }
   Console.WriteLine("EXPORTED: seed=12345 X=[-512,511] Z=[-512,511] resolution=256; 65536 rows");
  }
  Console.WriteLine("ALL_MAP_CHECKS_PASSED");
 }
}
