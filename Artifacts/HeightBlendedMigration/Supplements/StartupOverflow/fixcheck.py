from pathlib import Path
for name in ['BASELINEStartup.csproj','MODIFIEDStartup.csproj']:
 p=Path('Artifacts/HeightBlendedMigration/Runner')/name
 if not p.exists():
  folder='BASELINE_CURRENT' if name.startswith('BASELINE') else 'MODIFIED_FILE'; src=Path('Artifacts/HeightBlendedMigration/Supplements/StartupOverflow')/folder
  cs=Path('Artifacts/HeightBlendedMigration/Runner')/(name.replace('.csproj','.cs'))
  cs.write_text('using System; using ProceduralGeneration.WorldGen.Voxels; class Startup { static void Main(){ try { var v=new VoxelVolume(1280,384,1280); Console.WriteLine("ALLOCATED="+(v.Width*v.Height*v.Depth)); } catch(Exception e) { Console.WriteLine(e.GetType().Name+":"+e.Message); } var safe=new VoxelVolume(128,160,128); Console.WriteLine("SAFE="+(safe.Width*safe.Height*safe.Depth)); }}',encoding='utf-8')
  p.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="{cs}"/><Compile Include="{src}/Density/*.cs"/><Compile Include="{src}/Voxels/VoxelVolume.cs"/><Reference Include="Unity.Collections"><HintPath>D:/UnityHub/Project/程序化生成/Library/ScriptAssemblies/Unity.Collections.dll</HintPath></Reference></ItemGroup></Project>',encoding='utf-8')
 else:
  s=p.read_text(encoding='utf-8'); s=s.replace('</ItemGroup>','<Reference Include="Unity.Collections"><HintPath>D:/UnityHub/Project/程序化生成/Library/ScriptAssemblies/Unity.Collections.dll</HintPath></Reference></ItemGroup>'); p.write_text(s,encoding='utf-8')
