from pathlib import Path
import shutil
root=Path.cwd(); src=root/'Assets/Scripts/WorldGen'; base=root/'Artifacts/HeightBlendedMigration/Supplements/OriginalTerrainMapping/BASELINE_CURRENT'; mod=root/'Artifacts/HeightBlendedMigration/MODIFIED_FILE'
def norm(p):return p.read_bytes().replace(b'\r\n',b'\n')
for p in base.rglob('*'):
 if p.is_file():
  rel=p.relative_to(base); target=src/rel
  if target.exists() and norm(target)!=norm(p):
   # semantic candidate only if candidate differs from baseline normalized
   c=mod/rel
   shutil.copy2(c if c.exists() and norm(c)!=norm(p) else p,target)
for p in mod.rglob('*'):
 if not p.is_file():continue
 rel=p.relative_to(mod); b=base/rel; target=src/rel
 if not b.exists():
  shutil.copy2(p,target)
print('restored unrelated bytes; applied semantic changes')
