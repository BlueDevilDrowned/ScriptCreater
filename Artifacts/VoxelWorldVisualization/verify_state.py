from pathlib import Path
import sys, hashlib, subprocess
root=Path(sys.argv[1]).resolve()
source=root/'Assets/Scripts/WorldGen/Density'
combined=hashlib.sha256()
for path in sorted(source.glob('*.cs')):
    combined.update(path.name.encode()); combined.update(path.read_bytes())
print('DENSITY_SHA256='+combined.hexdigest(), flush=True)
print('VOXEL_FEATURE='+str((root/'Assets/Scripts/WorldGen/Voxels/VoxelWorldPreview.cs').exists()), flush=True)
p=root/'Temp/TransactionSmoke'; p.mkdir(parents=True,exist_ok=True)
(p/'Smoke.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="'+source.as_posix()+'/*.cs"/><Compile Include="Program.cs"/></ItemGroup></Project>')
(p/'Program.cs').write_text('using System; using ProceduralGeneration.WorldGen.Density; class Program { static void Main(){var f=WorldFields.CreateLearningDefaults(12345); var p=new DensityContext(32,64,-17); Console.WriteLine("final="+f.FinalDensity.Sample(p).ToString("R",System.Globalization.CultureInfo.InvariantCulture)); Console.WriteLine("temp="+f.Temperature.Sample(p).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}}')
r=subprocess.run(['dotnet','run','--project',str(p/'Smoke.csproj')],capture_output=True,text=True,encoding='utf-8',errors='replace')
print(r.stdout,end=''); print(r.stderr,end='',file=sys.stderr); sys.exit(r.returncode)
