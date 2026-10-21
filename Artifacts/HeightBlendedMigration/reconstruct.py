from pathlib import Path
import shutil,subprocess,hashlib,json,os
root=Path.cwd(); o=root/'Artifacts/HeightBlendedMigration'
rebuild=o/'PATCH_COPY'; shutil.copytree(o/'BASELINE',rebuild,dirs_exist_ok=True)
p=subprocess.run(['git','apply','--ignore-space-change',str(o/'DIFF.patch')],cwd=rebuild,capture_output=True)
with (o/'VERIFICATION.txt').open('a',encoding='utf-8') as f:
 f.write('PATCH_RECONSTRUCTION\ncommand: git apply --ignore-space-change DIFF.patch (cwd PATCH_COPY)\nexit: '+str(p.returncode)+'\nstdout:\n'+p.stdout.decode('utf-8',errors='replace')+'\nstderr:\n'+p.stderr.decode('utf-8',errors='replace')+'\n')
print('patch exit',p.returncode,p.stderr.decode('utf-8',errors='replace'))
if p.returncode: raise SystemExit(p.returncode)
mismatches=[]
for p in (o/'MODIFIED_FILE').rglob('*'):
 if p.is_file() and p.read_bytes()!=(rebuild/p.relative_to(o/'MODIFIED_FILE')).read_bytes(): mismatches.append(str(p))
print('mismatches',mismatches)
with (o/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write('PATCH_BYTE_MATCH: '+str(not mismatches)+'\n')
if mismatches:raise SystemExit(1)
# Mark executable on systems supporting POSIX permissions; Git Bash executes it on Windows.
os.chmod(o/'ROLLBACK.sh',0o755)
for role in ['MODIFIED_FILE','DIFF.patch','VERIFICATION.txt','ROLLBACK.sh']:
 p=o/role
 if p.is_dir():
  for q in p.rglob('*'):
   if q.is_file():q.read_bytes()
 else:p.read_bytes()
 print('REOPENED',str(p))
