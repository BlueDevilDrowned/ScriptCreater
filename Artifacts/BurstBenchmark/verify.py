from pathlib import Path
import subprocess, hashlib, difflib, xml.etree.ElementTree as ET, shutil, os

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
target = root / 'Assets/Scripts/Learn/burstTest.cs'
baseline = out / 'BASELINE.cs'
ledger = []
def run(cmd, cwd=out):
    env = dict(os.environ, DOTNET_CLI_UI_LANGUAGE='en-US')
    p = subprocess.run([str(x) for x in cmd], cwd=cwd, env=env, capture_output=True, text=True, encoding='utf-8', errors='replace')
    ledger.append(f'COMMAND: {cmd}\nSTDOUT:\n{p.stdout}\nSTDERR:\n{p.stderr}\nEXIT: {p.returncode}\n')
    print(p.stdout, end='')
    print(p.stderr, end='')
    return p
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()

# Compile only this source against the project's actual Unity reference assemblies.
tree = ET.parse(root / 'Assembly-CSharp.csproj')
project = tree.getroot()
for group in list(project):
    if group.tag == 'Import' or group.tag == 'PropertyGroup': project.remove(group)
    elif group.tag == 'ItemGroup':
        for child in list(group):
            if child.tag != 'Reference' or child.attrib.get('Include') not in ['UnityEngine.ManagedKernelModule', 'UnityEngine.CoreModule', 'UnityEngine.BurstModule']:
                group.remove(child)
            else:
                hint = child.find('HintPath')
                if hint is not None and not Path(hint.text).is_absolute(): hint.text = str(root / hint.text)
project.set('Sdk', 'Microsoft.NET.Sdk')
props = ET.SubElement(project, 'PropertyGroup')
for k,v in {'TargetFramework':'netstandard2.1','EnableDefaultCompileItems':'false','AllowUnsafeBlocks':'true','LangVersion':'9.0'}.items(): ET.SubElement(props,k).text=v
items = ET.SubElement(project,'ItemGroup')
ET.SubElement(items,'Compile',Include=str(target))
for name in ['Unity.Burst', 'Unity.Collections']:
    ref = ET.SubElement(items, 'Reference', Include=name)
    ET.SubElement(ref, 'HintPath').text = str(root / 'Library/ScriptAssemblies' / (name + '.dll'))
tree.write(out / 'UnityCompile.csproj',encoding='utf-8',xml_declaration=True)
compile_result = run(['dotnet','build','UnityCompile.csproj','--nologo','-v:q'])

runner = out / 'Runner'
runner.mkdir(exist_ok=True)
(runner/'Runner.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Source.cs"/><Compile Include="Harness.cs"/></ItemGroup></Project>',encoding='utf-8')
(runner/'Harness.cs').write_text('''using System;
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
   string logs=string.Join("\\n",UnityEngine.Debug.Messages);
   if(!logs.Contains("checksum=1486834589") || !logs.Contains("Checksums match")) throw new Exception("Wrong checksum");
   UnityEngine.Debug.Messages.Clear();
   Unity.Burst.BurstCompiler.IsEnabled=false;
   t.startAll=true; t.Update();
   logs=string.Join("\\n",UnityEngine.Debug.Messages);
   if(!logs.Contains("Burst measurement skipped") || logs.Contains("Burst Job:")) throw new Exception("Disabled gate failed");
   t.num=0; t.test();
  }
 }
}
''',encoding='utf-8')

def behavior(label, source):
    ledger.append(label + ': same input num=1000, startAll=true, Update(); modified also exercises disabled/invalid branches.')
    shutil.copyfile(source, runner/'Source.cs')
    return run(['dotnet','run','--project',runner/'Runner.csproj','-c','Release','--nologo'])

b = behavior('BASELINE', baseline)
m = behavior('MODIFIED', target)
rollback = out/'ROLLBACK.sh'
rollback.write_text('#!/bin/sh\nset -eu\n[ "$#" -eq 1 ] || { echo "Usage: ROLLBACK.sh target-copy" >&2; exit 2; }\nbase=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\ncp "$base/BASELINE.cs" "$1"\n',encoding='utf-8',newline='\n')
rollback.chmod(0o755)
restored = out/'Restored.cs'
shutil.copyfile(target,restored)
r = run([r'C:\Program Files\Git\bin\bash.exe',rollback,restored])
rb = behavior('ROLLBACK', restored)
assert digest(restored)==digest(baseline)
diff = subprocess.run(['git','-c','core.autocrlf=false','diff','--no-index','--binary','--no-ext-diff',str(baseline),str(target)],capture_output=True)
assert diff.returncode == 1
lines = diff.stdout.splitlines(keepends=True)
for i, line in enumerate(lines):
    if line.startswith(b'diff --git '): lines[i] = b'diff --git a/burstTest.cs b/burstTest.cs\n'
    elif line.startswith(b'--- '): lines[i] = b'--- a/burstTest.cs\n'
    elif line.startswith(b'+++ '): lines[i] = b'+++ b/burstTest.cs\n'
(out/'DIFF.patch').write_bytes(b''.join(lines))
check = out/'PatchCheck'
check.mkdir(exist_ok=True)
shutil.copyfile(baseline, check/'burstTest.cs')
applied = run(['git','-c','core.autocrlf=false','apply','--unsafe-paths','--directory='+check.relative_to(root).as_posix(),out/'DIFF.patch'],cwd=root)
assert applied.returncode == 0 and digest(check/'burstTest.cs') == digest(target)
ledger.append('Patch reconstruction verified byte-for-byte against MODIFIED_FILE.')
# Reapply the preserved modified bytes to the target after the rollback-copy check.
modified_bytes = target.read_bytes()
target.write_bytes(modified_bytes)
ledger += [f'BASELINE SHA256: {digest(baseline)}',f'MODIFIED SHA256: {digest(target)}',f'ROLLBACK SHA256: {digest(restored)}',
 'Changed: test/test1 -> RunBenchmark, ManagedJob/BurstJob, Calculate, Stopwatch, repetitions; original flags retained.',
 'Original baseline bytes preserved in BASELINE.cs. Rollback executed on separate Restored.cs.',
 'Prior preparation errors: BurstCompiler.cs lookup exit 1 (incorrect package source path); apply_patch delete/add rejected for duplicate path; subsequent patch context rejected (CRLF source). Corrected using literal UTF-8 file write. Neither failed patch modified target.',
 'Previous harness attempt exit 1: expected checksum literal was incorrect. Independently recalculated uint algorithm in Python: 1486834589. Corrected expected value. First attempt retained in VERIFICATION.first-attempt.txt.',
 'Second verification exit 1: isolated compile references omitted ManagedKernelModule and Burst; corrected reference selection. Record retained in VERIFICATION.second-attempt.txt.',
 'Third verification exit 1: Unity 7 BurstCompileAttribute is in UnityEngine.BurstModule; added actual engine module reference. Record retained in VERIFICATION.third-attempt.txt.',
 'Patch reconstruction checks initially failed byte-hash comparison because git apply preserved original CRLF (4388 vs 4265 bytes; normalized text matched). Preserved original CRLF convention in modified source for byte-exact reconstruction. A diagnostic reapplication to an already changed copy exited 1; reset check copy from baseline before final apply.',
 'LIMIT: harness substitutes Unity APIs. Checksums/control flow only. Actual Unity native Burst execution and timings not verified.']
(out/'VERIFICATION.txt').write_text('\n'.join(ledger),encoding='utf-8')
for p in [target,out/'DIFF.patch',out/'VERIFICATION.txt',rollback]:
    data=p.read_bytes()
    print(f'REOPENED: {p} ({len(data)} bytes)')
assert compile_result.returncode == b.returncode == m.returncode == r.returncode == rb.returncode == 0
