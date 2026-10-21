from pathlib import Path
import shutil, subprocess, json, hashlib
root=Path.cwd(); out=root/'Artifacts/HeightBlendedMigration'; src=root/'Assets/Scripts/WorldGen'
shutil.copytree(src,out/'BASELINE',dirs_exist_ok=True)
shutil.copytree(src,out/'MODIFIED_FILE',dirs_exist_ok=True)
runner=out/'Runner'; runner.mkdir(exist_ok=True)
for name in ['Runner.csproj','Program.cs']:
 shutil.copy2(root/'Artifacts/XZTerrainMigration/Runner'/name,runner/name)
def run(label,folder,program='Program.cs'):
 cmd=['dotnet','run','--project',str(runner/'Runner.csproj'),'-p:SourceDir='+str(folder/'Density'),'-p:TestProgram='+program]
 p=subprocess.run(cmd,capture_output=True,text=True)
 with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write(label+'\ncommand: '+json.dumps(cmd)+'\nexit: '+str(p.returncode)+'\nstdout:\n'+p.stdout+'\nstderr:\n'+p.stderr+'\n')
 print(label,p.returncode,p.stdout,p.stderr)
 if p.returncode: raise SystemExit(p.returncode)
 return p.stdout
hashes={str(p.relative_to(src)):hashlib.sha256(p.read_bytes()).hexdigest() for p in src.rglob('*') if p.is_file()}
(out/'baseline-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
run('BASELINE',out/'BASELINE')
