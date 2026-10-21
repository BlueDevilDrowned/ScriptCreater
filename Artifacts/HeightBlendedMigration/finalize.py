from pathlib import Path
import uuid,shutil,difflib,hashlib,subprocess
root=Path.cwd(); o=root/'Artifacts/HeightBlendedMigration'; mod=o/'MODIFIED_FILE'
for name in ['WorldGenerationSettings','OctaveNoiseFunction','BlendedNoiseFunction']:
 rel=Path('Density')/(name+'.cs.meta')
 content='fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'
 (mod/rel).write_text(content,encoding='utf-8',newline='\n'); shutil.copy2(mod/rel,root/'Assets/Scripts/WorldGen'/rel)
 script=(o/'ROLLBACK.sh').read_text(encoding='utf-8'); script=script.replace('echo ROLLBACK_RESTORED',f'rm -f "$target/{rel.as_posix()}"\necho ROLLBACK_RESTORED'); (o/'ROLLBACK.sh').write_text(script,encoding='utf-8',newline='\n')
 with (o/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write(rel.as_posix()+' MODIFIED_SHA256 '+hashlib.sha256((mod/rel).read_bytes()).hexdigest()+'\n')
patch=''
for p in sorted(mod.rglob('*')):
 if not p.is_file():continue
 rel=p.relative_to(mod); old=o/'BASELINE'/rel
 if old.exists() and old.read_bytes()==p.read_bytes():continue
 a=old.read_text(encoding='utf-8-sig').splitlines(True) if old.exists() else []
 b=p.read_text(encoding='utf-8-sig').splitlines(True)
 patch+=''.join(difflib.unified_diff(a,b,fromfile='a/'+rel.as_posix() if old.exists() else '/dev/null',tofile='b/'+rel.as_posix()))
(o/'DIFF.patch').write_text(patch,encoding='utf-8',newline='\n')
def call(label,cmd,cwd=None):
 p=subprocess.run(cmd,cwd=cwd,capture_output=True,text=True)
 with (o/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write(f'{label}\ncommand: {cmd}\nexit: {p.returncode}\nstdout:\n{p.stdout}\nstderr:\n{p.stderr}\n')
 assert p.returncode==0,p.stderr
rebuild=o/'PATCH_FINAL'; shutil.copytree(o/'BASELINE',rebuild)
call('FINAL_PATCH',['git','apply','--ignore-space-change',str(o/'DIFF.patch')],rebuild)
for p in mod.rglob('*'):
 if p.is_file(): assert p.read_bytes()==(rebuild/p.relative_to(mod)).read_bytes()
restored=o/'ROLLBACK_FINAL'; shutil.copytree(mod,restored)
call('FINAL_ROLLBACK',['C:/Program Files/Git/bin/bash.exe',str(o/'ROLLBACK.sh'),str(restored)])
for p in (o/'BASELINE').rglob('*'):
 if p.is_file():assert p.read_bytes()==(restored/p.relative_to(o/'BASELINE')).read_bytes()
assert len(list(p for p in restored.rglob('*') if p.is_file()))==len(list(p for p in (o/'BASELINE').rglob('*') if p.is_file()))
with (o/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write('FINAL_PATCH_BYTE_MATCH=True; FINAL_ROLLBACK_BYTE_MATCH=True (including Unity metadata)\n')
for role in ['MODIFIED_FILE','DIFF.patch','VERIFICATION.txt','ROLLBACK.sh']:
 path=o/role
 if path.is_dir():
  for p in path.rglob('*'):
   if p.is_file():p.read_bytes()
 else:path.read_bytes()
 print('REOPENED',path)
print('FINAL_PATCH_AND_ROLLBACK_PASS')
