using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class MonoBehaviour {}
 public class MinAttribute : Attribute { public MinAttribute(float v) {} }
 public static class Time { public static float unscaledTime => 10f; }
 public static class Debug {
  public static List<string> Messages = new List<string>();
  public static void Log(object x) { Messages.Add(x.ToString()); Console.WriteLine(x); }
  public static void LogWarning(object x) => Log("WARNING: " + x);
  public static void LogError(object x) => throw new Exception(x.ToString());
 }
}
namespace Unity.Burst {
 public class BurstCompileAttribute : Attribute { public bool CompileSynchronously {get;set;} }
 public static class BurstCompiler { public static bool IsEnabled = true; }
}
namespace Unity.Collections {
 public enum Allocator { TempJob }
 public struct NativeArray<T> : IDisposable {
  T[] data;
  public NativeArray(int n, Allocator a) { data = new T[n]; }
  public T this[int i] {get => data[i]; set => data[i]=value;}
  public void Dispose() {}
 }
}
namespace Unity.Jobs {
 public interface IJob { void Execute(); }
 public static class JobExtensions { public static void Run<T>(this T job) where T:struct,IJob => job.Execute(); }
}
class Program {
 static void Main() {
  Console.WriteLine("LOGIC HARNESS ONLY: Unity APIs substituted; no native Burst compilation or performance validation.");
  var t = new BurstTest { num=1000, startAll=true };
  t.Update();
  if(t.startAll || t.starttest || t.startburst) throw new Exception("Flags not reset");
  Console.WriteLine("Flags reset: PASS");
  bool modified = typeof(BurstTest).GetField("repetitions") != null;
  if(modified) {
   string logs=string.Join("\n",UnityEngine.Debug.Messages);
   if(!logs.Contains("checksum=1486834589") || !logs.Contains("Checksums match")) throw new Exception("Wrong checksum");
   UnityEngine.Debug.Messages.Clear();
   Unity.Burst.BurstCompiler.IsEnabled=false;
   t.startAll=true; t.Update();
   logs=string.Join("\n",UnityEngine.Debug.Messages);
   if(!logs.Contains("Burst measurement skipped") || logs.Contains("Burst Job:")) throw new Exception("Disabled gate failed");
   t.num=0; t.test();
  }
 }
}
