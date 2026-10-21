from pathlib import Path
import shutil
root=Path.cwd(); m=root/'Artifacts/HeightBlendedMigration/MODIFIED_FILE'; src=root/'Assets/Scripts/WorldGen'
if m.exists():shutil.rmtree(m)
shutil.copytree(src,m)
# helper source was lost; recover from patch not easy; use previous implementation file? create from stored patch impossible. 
print('reset',m)
