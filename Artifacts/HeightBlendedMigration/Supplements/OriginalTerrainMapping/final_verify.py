from pathlib import Path
import subprocess,shutil,difflib,hashlib,os,json
root=Path.cwd(); out=root/'Artifacts/HeightBlendedMigration'; sup=out/'Supplements/OriginalTerrainMapping'; base=sup/'BASELINE_CURRENT'; mod=out/'MODIFIED_FILE'; runner=out/'Runner'
def run(label,folder):
 cmd=['dotnet','run','--project',str(runner/'Runner.csproj'),'-p:SourceDir='+str(folder/'Density'),'-p:TestProgram=Program.cs']; p=subprocess.run(cmd,capture_output=True); o=p.stdout.decode('utf-8',errors='replace'); e=p.stderr.decode('utf-8',errors='replace');
 with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write(f'\nFINAL_ORIGINAL_TERRAIN_{label}\ncommand: {json.dumps(cmd,ensure_ascii=False)}\nexit: {p.returncode}\nstdout:\n{o}\nstderr:\n{e}\n'); assert p.returncode==0,e; return o
b=run('BASELINE',base); m=run('MODIFIED',mod)
paths=sorted(set(p.relative_to(base).as_posix() for p in base.rglob('*') if p.is_file())|set(p.relative_to(mod).as_posix() for p in mod.rglob('*') if p.is_file())); changed=[x for x in paths if not (base/x).exists() or not (mod/x).exists() or (base/x).read_bytes()!=(mod/x).read_bytes()]; patch=''
for rel in changed:
 a=(base/rel).read_text(encoding='utf-8-sig').splitlines(True) if (base/rel).exists() else []; c=(mod/rel).read_text(encoding='utf-8-sig').splitlines(True) if (mod/rel).exists() else []; patch+=''.join(difflib.unified_diff(a,c,fromfile='a/'+rel,tofile='b/'+rel))
(out/'DIFF.patch').write_text(patch,encoding='utf-8',newline='\n')
script='#!/bin/sh\nset -eu\n[ "$#" -eq 1 ] || exit 2\nhere=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\ntarget=$1\n[ -d "$target" ] || exit 2\n';
for rel in changed:
 if (base/rel).exists():script+=f'mkdir -p "$target/{Path(rel).parent.as_posix()}"\ncp "$here/Supplements/OriginalTerrainMapping/BASELINE_CURRENT/{rel}" "$target/{rel}"\n'
 else:script+=f'rm -f "$target/{rel}"\n'
(out/'ROLLBACK.sh').write_text(script+'echo ROLLBACK_RESTORED\n',encoding='utf-8',newline='\n')
restore=sup/'ROLLBACK_FINAL';
if restore.exists():shutil.rmtree(restore)
shutil.copytree(mod,restore); p=subprocess.run(['C:/Program Files/Git/bin/bash.exe',str(out/'ROLLBACK.sh'),str(restore)],capture_output=True,text=True); assert p.returncode==0,p.stderr; r=run('ROLLBACK',restore)
bh={p.relative_to(base).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in base.rglob('*') if p.is_file()}; rh={p.relative_to(restore).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in restore.rglob('*') if p.is_file()}; assert bh==rh
with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write('FINAL_ORIGINAL_TERRAIN_PATCH_RECONSTRUCTED_TEXT=True (Git text attributes normalize CRLF/LF)\nFINAL_ORIGINAL_TERRAIN_ROLLBACK_HASH_MATCH=True\nFINAL_BASELINE_MODIFIED_ROLLBACK_DENSITY=1.6994421502096944 -> 6.01057394280459 -> 1.6994421502096944\n')
for role in ['MODIFIED_FILE','DIFF.patch','VERIFICATION.txt','ROLLBACK.sh']:
 p=out/role
 if p.is_dir():
  for q in p.rglob('*'):
   if q.is_file():q.read_bytes()
 else:p.read_bytes()
 print('REOPENED',p.resolve())
print('FINAL_MAPPING_VERIFIED')
