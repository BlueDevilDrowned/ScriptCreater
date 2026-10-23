from pathlib import Path
import re,subprocess
root=Path.cwd(); run=root/'Artifacts/HeightBlendedMigration/Runner'; sup=root/'Artifacts/HeightBlendedMigration/Supplements/StartupOverflow'
for label,folder in [('BASELINE','BASELINE_CURRENT'),('MODIFIED','MODIFIED_FILE')]:
 src=sup/folder/'Voxels/VoxelVolume.cs'; t=src.read_text(encoding='utf-8-sig'); t=t.replace('using Unity.Collections;','').replace('using ProceduralGeneration.WorldGen.Density;','using ProceduralGeneration.WorldGen.Density;')
 t=re.sub(r'\n        /// <summary>复制为 Job System 可读的连续缓冲区。调用者必须 Dispose。</summary>.*?\n        }\n', '\n', t, flags=re.S)
 lite=run/(label+'VoxelVolumeLite.cs'); lite.write_text(t,encoding='utf-8')
 cs=run/(label+'Startup.cs'); cs.write_text('using System; using ProceduralGeneration.WorldGen.Voxels; class Startup { static void Main(){ try { var v=new VoxelVolume(1280,384,1280); Console.WriteLine("ALLOCATED="+(v.Width*v.Height*v.Depth)); } catch(Exception e) { Console.WriteLine(e.GetType().Name+":"+e.Message); } var safe=new VoxelVolume(128,160,128); Console.WriteLine("SAFE="+(safe.Width*safe.Height*safe.Depth)); }}',encoding='utf-8')
 proj=run/(label+'StartupLite.csproj'); proj.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="{cs.resolve()}"/><Compile Include="{lite.resolve()}"/><Compile Include="{(sup/folder/"Density").resolve()}/*.cs"/></ItemGroup></Project>',encoding='utf-8')
 p=subprocess.run(['dotnet','run','--project',str(proj)],capture_output=True); print(label,p.returncode,p.stdout.decode(errors='replace'),p.stderr.decode(errors='replace')); assert p.returncode==0
