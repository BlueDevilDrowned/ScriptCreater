from pathlib import Path
import subprocess, shutil, hashlib, difflib, json

root = Path(__file__).resolve().parent
source = root.parents[1] / 'Assets/Scripts/WorldGen/Density'
baseline = root / 'BASELINE'
modified = root / 'MODIFIED_FILE'
restored = root / 'ROLLBACK_COPY'
for file in source.iterdir():
    if file.suffix in ('.cs', '.md') and (file.suffix == '.cs' or file.name in ('世界生成变更记录.md', 'DensityFunction方法解读.md')):
        shutil.copy2(file, modified / file.name)
shutil.copytree(modified, restored, dirs_exist_ok=True)

def hashes(folder):
    return {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(folder.iterdir()) if p.is_file()}

ledger = ['TARGET: ' + str(source), 'Changed: Noise2D, ProjectXZ, HalfNegative, QuarterNegative, NoiseGradientDensity, WorldFields.CreateLearningDefaults, Depth', '']
def execute(label, command):
    result = subprocess.run(command, cwd=root, capture_output=True, text=True, encoding='utf-8', errors='replace')
    ledger.extend([label, 'command: '+json.dumps(command, ensure_ascii=False), 'exit: '+str(result.returncode), 'stdout:', result.stdout, 'stderr:', result.stderr])
    (root/'VERIFICATION.txt').write_text('\n'.join(ledger), encoding='utf-8')
    print(label, 'exit=', result.returncode, '\n'+result.stdout, result.stderr)
    if result.returncode: raise RuntimeError(label)
    return result.stdout

def sample(label, folder, program='Program.cs'):
    return execute(label, ['dotnet','run','--project',str(root/'Runner/Runner.csproj'),'-p:SourceDir='+folder.as_posix(),'-p:TestProgram='+program])

before = hashes(baseline)
ledger.extend(['BASELINE hashes:', json.dumps(before, ensure_ascii=False, indent=2)])
b = sample('BASELINE', baseline)
m = sample('MODIFIED', modified)
sample('MODIFIED_CHECKS', modified, 'Checks.cs')
execute('ROLLBACK_EXECUTION', ['C:/Program Files/Git/bin/bash.exe', str(root/'ROLLBACK.sh').replace('\\','/'), str(restored).replace('\\','/')])
r = sample('ROLLBACK', restored)
assert hashes(restored) == before, 'rollback hash mismatch'
assert b == r, 'rollback behavior mismatch'
assert hashes(baseline) == before, 'baseline changed'
for name, value in hashes(modified).items():
    assert hashlib.sha256((source/name).read_bytes()).hexdigest() == value, name

diff = []
for name in sorted(set(hashes(baseline)) | set(hashes(modified))):
    old = (baseline/name).read_text(encoding='utf-8-sig').splitlines(True) if (baseline/name).exists() else []
    new = (modified/name).read_text(encoding='utf-8-sig').splitlines(True) if (modified/name).exists() else []
    diff.extend(difflib.unified_diff(old,new,fromfile='a/'+name if old else '/dev/null',tofile='b/'+name))
(root/'DIFF.patch').write_text(''.join(diff), encoding='utf-8')
ledger.extend(['MODIFIED hashes:',json.dumps(hashes(modified),ensure_ascii=False,indent=2),'ROLLBACK hashes equal BASELINE: True', 'ROLLBACK stdout equals BASELINE: True', 'Working source equals MODIFIED_FILE: True', 'Unity Editor scene/render verification: not run', 'VERIFICATION_SUCCESS'])
(root/'VERIFICATION.txt').write_text('\n'.join(ledger),encoding='utf-8')
print('VERIFICATION_SUCCESS')
