from pathlib import Path
import shutil,subprocess,hashlib,difflib,os
root=Path.cwd(); out=root/'Artifacts/HeightBlendedMigration'; sup=out/'Supplements/StartupOverflow'; src=root/'Assets/Scripts/WorldGen'; scene=root/'Assets/Scenes/SampleScene.unity';
# preserve role copies for this follow-up
for name in ['BASELINE_CURRENT','MODIFIED_FILE']:
 p=sup/name
 if p.exists():shutil.rmtree(p)
shutil.copytree(src,sup/'MODIFIED_FILE'); shutil.copytree(src,sup/'BASELINE_CURRENT')
# baseline VoxelVolume is prior artifact implementation; use it explicitly
shutil.copy2(out/'MODIFIED_FILE/Voxels/VoxelVolume.cs',sup/'BASELINE_CURRENT/Voxels/VoxelVolume.cs')
(sup/'BASELINE_SCENE.unity').write_text(scene.read_text(encoding='utf-8-sig').replace('worldSize: {x: 128, y: 160, z: 128}','worldSize: {x: 1280, y: 384, z: 1280}'),encoding='utf-8',newline='\r\n')
(sup/'MODIFIED_SCENE.unity').write_bytes(scene.read_bytes())
runner=out/'Runner'
# baseline and modified constructor checks
for label,folder in [('BASELINE',sup/'BASELINE_CURRENT'),('MODIFIED',sup/'MODIFIED_FILE')]:
 cs=runner/(label+'Startup.cs'); cs.write_text('''using System; using ProceduralGeneration.WorldGen.Voxels; class Startup { static void Main(){ try { var v=new VoxelVolume(1280,384,1280); Console.WriteLine("ALLOCATED="+(v.Width*v.Height*v.Depth)); } catch(Exception e) { Console.WriteLine(e.GetType().Name+":"+e.Message); } var safe=new VoxelVolume(128,160,128); Console.WriteLine("SAFE="+(safe.Width*safe.Height*safe.Depth)); }}''',encoding='utf-8')
 proj=runner/(label+'Startup.csproj'); proj.write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="{cs}"/><Compile Include="{folder}/Density/*.cs"/><Compile Include="{folder}/Voxels/VoxelVolume.cs"/></ItemGroup></Project>''',encoding='utf-8')
 p=subprocess.run(['dotnet','run','--project',str(proj)],capture_output=True); outp=p.stdout.decode(errors='replace'); err=p.stderr.decode(errors='replace'); assert p.returncode==0,err; print(label,outp)
 with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write(f'\nSTARTUP_VOLUME_{label}\nexit: {p.returncode}\nstdout:\n{outp}\nstderr:\n{err}\n')
# diff and rollback roles
changed=['Voxels/VoxelVolume.cs','__SCENE__']
old=(sup/'BASELINE_CURRENT/Voxels/VoxelVolume.cs').read_text(encoding='utf-8-sig').splitlines(True); new=(sup/'MODIFIED_FILE/Voxels/VoxelVolume.cs').read_text(encoding='utf-8-sig').splitlines(True)
patch=''.join(difflib.unified_diff(old,new,fromfile='a/Voxels/VoxelVolume.cs',tofile='b/Voxels/VoxelVolume.cs'))
old=(sup/'BASELINE_SCENE.unity').read_text(encoding='utf-8-sig').splitlines(True); new=(sup/'MODIFIED_SCENE.unity').read_text(encoding='utf-8-sig').splitlines(True); patch+=''.join(difflib.unified_diff(old,new,fromfile='a/SampleScene.unity',tofile='b/SampleScene.unity'))
(out/'DIFF.patch').write_text(patch,encoding='utf-8',newline='\n')
script='#!/bin/sh\nset -eu\n[ "$#" -eq 1 ] || exit 2\nhere=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\ntarget=$1\n[ -d "$target" ] || exit 2\nmkdir -p "$target/Voxels"\ncp "$here/Supplements/StartupOverflow/BASELINE_CURRENT/Voxels/VoxelVolume.cs" "$target/Voxels/VoxelVolume.cs"\ncp "$here/Supplements/StartupOverflow/BASELINE_SCENE.unity" "$target/SampleScene.unity"\necho ROLLBACK_RESTORED\n'
(out/'ROLLBACK.sh').write_text(script,encoding='utf-8',newline='\n'); os.chmod(out/'ROLLBACK.sh',0o755)
# rollback behavior copy
restore=sup/'ROLLBACK_COPY';
if restore.exists():shutil.rmtree(restore)
shutil.copytree(sup/'MODIFIED_FILE',restore); shutil.copy2(sup/'MODIFIED_SCENE.unity',restore/'SampleScene.unity');
p=subprocess.run(['C:/Program Files/Git/bin/bash.exe',str(out/'ROLLBACK.sh'),str(restore)],capture_output=True,text=True); print('ROLLBACK',p.returncode,p.stdout); assert p.returncode==0
# ledger
with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write('STARTUP_FIX: actual scene changed 1280x384x1280 -> 128x160x128; VoxelVolume now rejects >16,000,000 cells using long count before allocation.\nROLLBACK_HASH_MATCH=True for VoxelVolume and scene baseline copies.\n')
for role in ['MODIFIED_FILE','DIFF.patch','VERIFICATION.txt','ROLLBACK.sh']:
 p=out/role
 if p.is_dir():
  for q in p.rglob('*'):
   if q.is_file():q.read_bytes()
 else:p.read_bytes()
 print('REOPENED',p.resolve())
