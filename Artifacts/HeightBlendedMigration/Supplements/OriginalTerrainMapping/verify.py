from pathlib import Path
import shutil,subprocess,hashlib,difflib,json,os
root=Path.cwd(); out=root/'Artifacts/HeightBlendedMigration'; sup=out/'Supplements/OriginalTerrainMapping'; src=root/'Assets/Scripts/WorldGen'; mod=out/'MODIFIED_FILE'; base=sup/'BASELINE_CURRENT'; runner=out/'Runner'
if base.exists():shutil.rmtree(base)
shutil.copytree(src,base)
def run(label,folder,program='Program.cs'):
 cmd=['dotnet','run','--project',str(runner/'Runner.csproj'),'-p:SourceDir='+str(folder/'Density'),'-p:TestProgram='+program]
 p=subprocess.run(cmd,capture_output=True)
 outp=p.stdout.decode('utf-8',errors='replace'); err=p.stderr.decode('utf-8',errors='replace')
 with (sup/(label+'.stdout.txt')).open('w',encoding='utf-8') as f:f.write(outp)
 with (sup/(label+'.stderr.txt')).open('w',encoding='utf-8') as f:f.write(err)
 with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write(f'\nORIGINAL_TERRAIN_{label}\ncommand: {json.dumps(cmd,ensure_ascii=False)}\nexit: {p.returncode}\nstdout:\n{outp}\nstderr:\n{err}\n')
 print(label,p.returncode,outp[-1300:])
 if p.returncode:raise SystemExit(p.returncode)
 return outp
baseline=run('BASELINE_CURRENT',base); modified=run('MODIFIED_CURRENT',mod); checks=run('MODIFIED_CHECKS',mod,'Checks.cs')
assert baseline!=modified,'mapping did not change output'
# patch and rollback from current baseline to candidate
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
 if (base/rel).exists(): script+=f'mkdir -p "$target/{Path(rel).parent.as_posix()}"\ncp "$here/Supplements/OriginalTerrainMapping/BASELINE_CURRENT/{rel}" "$target/{rel}"\n'
 else:script+=f'rm -f "$target/{rel}"\n'
script+='echo ROLLBACK_RESTORED\n'; (out/'ROLLBACK.sh').write_text(script,encoding='utf-8',newline='\n'); os.chmod(out/'ROLLBACK.sh',0o755)
rebuild=sup/'PATCH_COPY'; shutil.copytree(base,rebuild)
p=subprocess.run(['git','-c','core.autocrlf=false','-c','core.eol=lf','apply','--ignore-space-change',str(out/'DIFF.patch')],cwd=rebuild,capture_output=True)
assert p.returncode==0,p.stderr.decode(errors='replace')
for pth in mod.rglob('*'):
 if pth.is_file(): assert pth.read_bytes()==(rebuild/pth.relative_to(mod)).read_bytes(),pth
restore=sup/'ROLLBACK_COPY'; shutil.copytree(mod,restore)
p=subprocess.run(['C:/Program Files/Git/bin/bash.exe',str(out/'ROLLBACK.sh'),str(restore)],capture_output=True,text=True); assert p.returncode==0,p.stderr
rollback=run('ROLLBACK_CURRENT',restore)
hashes={p.relative_to(base).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in base.rglob('*') if p.is_file()}; restored={p.relative_to(restore).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in restore.rglob('*') if p.is_file()}; assert hashes==restored
# apply candidate to project
for rel in changed:
 dst=src/rel
 if (mod/rel).exists():dst.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(mod/rel,dst)
 elif dst.exists():dst.unlink()
with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:
 f.write('ORIGINAL_TERRAIN_PATCH_BYTE_MATCH=True\nORIGINAL_TERRAIN_ROLLBACK_HASH_MATCH=True\nChanged symbols: OriginalTerrainProvider, WorldFields C/E/R mapping, folded P\n')
 for rel in changed:f.write(rel+' MODIFIED_SHA256 '+hashlib.sha256((mod/rel).read_bytes()).hexdigest()+'\n')
# reopen roles
for role in ['MODIFIED_FILE','DIFF.patch','VERIFICATION.txt','ROLLBACK.sh']:
 p=out/role
 if p.is_dir():
  for q in p.rglob('*'):
   if q.is_file():q.read_bytes()
 else:p.read_bytes()
 print('REOPENED',p.resolve())
print('ORIGINAL_TERRAIN_TRANSACTION_COMPLETE')
