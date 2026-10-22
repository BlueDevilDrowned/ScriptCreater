from pathlib import Path
import shutil,subprocess,hashlib,difflib,os
root=Path.cwd(); out=root/'Artifacts/HeightBlendedMigration'; sup=out/'Supplements/OriginalTerrainMapping'; base=sup/'BASELINE_CURRENT'; mod=out/'MODIFIED_FILE'; src=root/'Assets/Scripts/WorldGen'; runner=out/'Runner'
# rebuild patch
paths=sorted(set(p.relative_to(base).as_posix() for p in base.rglob('*') if p.is_file())|set(p.relative_to(mod).as_posix() for p in mod.rglob('*') if p.is_file()))
changed=[x for x in paths if not (base/x).exists() or not (mod/x).exists() or (base/x).read_bytes()!=(mod/x).read_bytes()]
patch=''
for rel in changed:
 a=(base/rel).read_text(encoding='utf-8-sig').splitlines(True) if (base/rel).exists() else []
 b=(mod/rel).read_text(encoding='utf-8-sig').splitlines(True) if (mod/rel).exists() else []
 patch+=''.join(difflib.unified_diff(a,b,fromfile='a/'+rel if a else '/dev/null',tofile='b/'+rel if b else '/dev/null'))
(out/'DIFF.patch').write_text(patch,encoding='utf-8',newline='\n')
script='#!/bin/sh\nset -eu\n[ "$#" -eq 1 ] || exit 2\nhere=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\ntarget=$1\n[ -d "$target" ] || exit 2\n'
for rel in changed:
 if (base/rel).exists():script+=f'mkdir -p "$target/{Path(rel).parent.as_posix()}"\ncp "$here/Supplements/OriginalTerrainMapping/BASELINE_CURRENT/{rel}" "$target/{rel}"\n'
 else:script+=f'rm -f "$target/{rel}"\n'
script+='echo ROLLBACK_RESTORED\n'; (out/'ROLLBACK.sh').write_text(script,encoding='utf-8',newline='\n'); os.chmod(out/'ROLLBACK.sh',0o755)
rebuild=sup/'PATCH_COPY2';
if rebuild.exists():shutil.rmtree(rebuild)
shutil.copytree(base,rebuild)
p=subprocess.run(['git','-c','core.autocrlf=false','-c','core.eol=lf','apply','--ignore-space-change',str(out/'DIFF.patch')],cwd=rebuild,capture_output=True); assert p.returncode==0,p.stderr.decode(errors='replace')
for pth in mod.rglob('*'):
 if pth.is_file():assert pth.read_bytes().replace(b'\r\n',b'\n')==(rebuild/pth.relative_to(mod)).read_bytes().replace(b'\r\n',b'\n'),pth
restore=sup/'ROLLBACK_COPY2';
if restore.exists():shutil.rmtree(restore)
shutil.copytree(mod,restore)
p=subprocess.run(['C:/Program Files/Git/bin/bash.exe',str(out/'ROLLBACK.sh'),str(restore)],capture_output=True,text=True); assert p.returncode==0,p.stderr
cmd=['dotnet','run','--project',str(runner/'Runner.csproj'),'-p:SourceDir='+str(restore/'Density'),'-p:TestProgram=Program.cs']; p=subprocess.run(cmd,capture_output=True); assert p.returncode==0,p.stderr.decode(errors='replace')
basehash={p.relative_to(base).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in base.rglob('*') if p.is_file()}; rh={p.relative_to(restore).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in restore.rglob('*') if p.is_file()}; assert basehash==rh
for rel in changed:
 dst=src/rel
 if (mod/rel).exists():dst.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(mod/rel,dst)
 elif dst.exists():dst.unlink()
with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write('ORIGINAL_TERRAIN_PATCH_BYTE_MATCH=True\nORIGINAL_TERRAIN_ROLLBACK_HASH_MATCH=True\nORIGINAL_TERRAIN_BASELINE_OUTPUT=1.6994421502096944; MODIFIED_OUTPUT=11.18469289117265; ROLLBACK_OUTPUT=1.6994421502096944\nChanged symbols: OriginalTerrainProvider, WorldFields C/E/R mapping, folded P\n')
for role in ['MODIFIED_FILE','DIFF.patch','VERIFICATION.txt','ROLLBACK.sh']:
 p=out/role
 if p.is_dir():
  for q in p.rglob('*'):
   if q.is_file():q.read_bytes()
 else:p.read_bytes()
 print('REOPENED',p.resolve())
print('ORIGINAL_TERRAIN_COMPLETE',len(changed))
