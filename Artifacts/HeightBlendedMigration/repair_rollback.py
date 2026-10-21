from pathlib import Path
import subprocess,hashlib
out=Path.cwd()/'Artifacts/HeightBlendedMigration'; base=out/'BASELINE'; mod=out/'MODIFIED_FILE'
script='#!/bin/sh\nset -eu\n[ "$#" -eq 1 ] || exit 2\nhere=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\ntarget=$1\n[ -d "$target" ] || exit 2\n'
for p in mod.rglob('*'):
 if not p.is_file():continue
 rel=p.relative_to(mod); b=base/rel
 if b.exists() and b.read_bytes()==p.read_bytes():continue
 if b.exists(): script+=f'cp "$here/BASELINE/{rel.as_posix()}" "$target/{rel.as_posix()}"\n'
 else:script+=f'rm -f "$target/{rel.as_posix()}"\n'
script+='echo ROLLBACK_RESTORED\n'
(out/'ROLLBACK.sh').write_text(script,encoding='utf-8',newline='\n')
p=subprocess.run(['C:/Program Files/Git/bin/bash.exe',str(out/'ROLLBACK.sh'),str(out/'ROLLBACK_FINAL')],capture_output=True,encoding='utf-8')
with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write(f'ROLLBACK_REPAIR: regenerated paths from filesystem after encoded path corruption\ncommand: bash ROLLBACK.sh ROLLBACK_FINAL\nexit: {p.returncode}\nstdout:\n{p.stdout}\nstderr:\n{p.stderr}\n')
assert p.returncode==0,p.stderr
for p in base.rglob('*'):
 if p.is_file():assert p.read_bytes()==(out/'ROLLBACK_FINAL'/p.relative_to(base)).read_bytes()
assert len([p for p in base.rglob('*') if p.is_file()])==len([p for p in (out/'ROLLBACK_FINAL').rglob('*') if p.is_file()])
for p in mod.rglob('*'):
 if p.is_file():assert p.read_bytes()==(out/'PATCH_FINAL'/p.relative_to(mod)).read_bytes()
with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write('FINAL_PATCH_BYTE_MATCH=True; FINAL_ROLLBACK_BYTE_MATCH=True\n')
for role in ['MODIFIED_FILE','DIFF.patch','VERIFICATION.txt','ROLLBACK.sh']:
 p=out/role
 if p.is_dir():
  for f in p.rglob('*'):
   if f.is_file():f.read_bytes()
 else:p.read_bytes()
 print('REOPENED',p)
print('FINAL_PATCH_AND_ROLLBACK_PASS')
