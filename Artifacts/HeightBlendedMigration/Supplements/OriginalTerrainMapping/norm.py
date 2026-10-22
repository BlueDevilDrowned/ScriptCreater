from pathlib import Path
m=Path('Artifacts/HeightBlendedMigration/MODIFIED_FILE'); base=Path('Artifacts/HeightBlendedMigration/Supplements/OriginalTerrainMapping/BASELINE_CURRENT')
for p in m.rglob('*'):
 if p.is_file():
  rel=p.relative_to(m); b=base/rel
  if not b.exists() or p.read_bytes()!=b.read_bytes():
   try:p.write_text(p.read_text(encoding='utf-8-sig'),encoding='utf-8',newline='\n')
   except UnicodeDecodeError:pass
