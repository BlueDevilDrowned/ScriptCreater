from pathlib import Path
import difflib, shutil, hashlib
path=Path('Artifacts/HeightBlendedMigration/Supplements/XZMaps/verify_maps.py')
exec(path.read_text(encoding='utf-8-sig').split('prehash=')[0])
with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write('XZ_MAPS_PATCH_ASSERTION exit=1: git apply converted 3 changed files from LF to CRLF under core.autocrlf=true. Retry sets core.autocrlf=false; source bytes remain unchanged.\n')
meta=mod/'Editor.meta'; meta.write_text(meta.read_text(encoding='utf-8').replace(': \n',':\n'),encoding='utf-8',newline='\n')
base=out/'BASELINE'; paths=sorted(set(p.relative_to(base).as_posix() for p in base.rglob('*') if p.is_file())|set(p.relative_to(mod).as_posix() for p in mod.rglob('*') if p.is_file()))
changed=[x for x in paths if not (base/x).exists() or not (mod/x).exists() or (base/x).read_bytes()!=(mod/x).read_bytes()]
patch=''
for rel in changed:
 a=(base/rel).read_text(encoding='utf-8-sig').splitlines(True) if (base/rel).exists() else []
 b=(mod/rel).read_text(encoding='utf-8-sig').splitlines(True) if (mod/rel).exists() else []
 patch+=''.join(difflib.unified_diff(a,b,fromfile='a/'+rel if a else '/dev/null',tofile='b/'+rel if b else '/dev/null'))
(out/'DIFF.patch').write_text(patch,encoding='utf-8',newline='\n')
rebuild=sup/'PATCH_COPY_CANONICAL'; shutil.copytree(base,rebuild)
record('PATCH_RECONSTRUCTION_LF',['git','-c','core.autocrlf=false','-c','core.eol=lf','apply','--ignore-space-change',str(out/'DIFF.patch')],cwd=rebuild)
for p in mod.rglob('*'):
 if p.is_file():assert p.read_bytes()==(rebuild/p.relative_to(mod)).read_bytes(),str(p)
restore=sup/'ROLLBACK_COPY'; shutil.copytree(mod,restore)
record('ROLLBACK_EXECUTION',['C:/Program Files/Git/bin/bash.exe',str(out/'ROLLBACK.sh'),str(restore)])
sample('ROLLBACK',restore)
basehash={p.relative_to(base).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in base.rglob('*') if p.is_file()}
restorehash={p.relative_to(restore).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in restore.rglob('*') if p.is_file()}
assert basehash==restorehash
apply=['Density/XZFieldMap.cs','Density/XZFieldMap.cs.meta','Editor/WorldFieldMapWindow.cs','Editor/WorldFieldMapWindow.cs.meta','Editor.meta','Density/世界生成变更记录.md']
for rel in apply:
 target=src/rel; target.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(mod/rel,target)
prehash=json.loads((sup/'prechange-hashes.json').read_text(encoding='utf-8'))
assert hashlib.sha256((src/'Density/WorldFields.cs').read_bytes()).hexdigest()==prehash['Density/WorldFields.cs']
with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:
 f.write('XZ_MAPS_PATCH_BYTE_MATCH=True\nXZ_MAPS_ROLLBACK_HASH_MATCH=True\nXZ_MAPS_TERRAIN_PRECHANGE_EQUALS_MODIFIED=True\nChanged symbols: added XZFieldMap and WorldFieldMapWindow; WorldFields unchanged\nPrechange hashes: Supplements/XZMaps/prechange-hashes.json\n')
 for rel in apply:f.write(rel+' SHA256 '+hashlib.sha256((src/rel).read_bytes()).hexdigest()+'\n')
for role in ['MODIFIED_FILE','DIFF.patch','VERIFICATION.txt','ROLLBACK.sh']:
 p=out/role
 if p.is_dir():
  for q in p.rglob('*'):
   if q.is_file():q.read_bytes()
 else:p.read_bytes()
 print('REOPENED',p)
print('MAP_TRANSACTION_COMPLETE')
