from pathlib import Path
import subprocess,json,shutil,hashlib,difflib,xml.etree.ElementTree as ET
root=Path.cwd(); out=root/'Artifacts/HeightBlendedMigration'; runner=out/'Runner'
def record(label,cmd):
 p=subprocess.run(cmd,capture_output=True,text=True,encoding='utf-8',errors='replace')
 with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write(label+'\ncommand: '+json.dumps(cmd,ensure_ascii=False)+'\nexit: '+str(p.returncode)+'\nstdout:\n'+p.stdout+'\nstderr:\n'+p.stderr+'\n')
 print(label,p.returncode,p.stdout[-2500:],p.stderr)
 if p.returncode: raise SystemExit(p.returncode)
 return p.stdout
def run(label,folder,program='Program.cs'):
 return record(label,['dotnet','run','--project',str(runner/'Runner.csproj'),'-p:SourceDir='+str(folder/'Density'),'-p:TestProgram='+program])
run('MODIFIED',out/'MODIFIED_FILE'); run('MODIFIED_CHECKS',out/'MODIFIED_FILE','Checks.cs')
# Compile all WorldGen sources against installed Unity assemblies, without Editor automation.
original=ET.parse(root/'Assembly-CSharp.csproj').getroot()
proj=ET.Element('Project',Sdk='Microsoft.NET.Sdk'); props=ET.SubElement(proj,'PropertyGroup')
for k,v in {'TargetFramework':'netstandard2.1','EnableDefaultCompileItems':'false','AllowUnsafeBlocks':'true','LangVersion':'9.0','NoWarn':'0649'}.items(): ET.SubElement(props,k).text=v
items=ET.SubElement(proj,'ItemGroup'); ET.SubElement(items,'Compile',Include=str(out/'MODIFIED_FILE/**/*.cs'))
for ref in original.findall('.//Reference'):
 hint=ref.find('HintPath')
 if hint is not None:
  path=Path(hint.text)
  if not path.is_absolute(): path=root/path
  if path.exists() and ('Unity' in path.name):
   new=ET.SubElement(items,'Reference',Include=ref.attrib['Include']); ET.SubElement(new,'HintPath').text=str(path)
for name in ['Unity.Collections','Unity.Burst']:
 path=root/'Library/ScriptAssemblies'/(name+'.dll')
 if path.exists(): ET.SubElement(ET.SubElement(items,'Reference',Include=name),'HintPath').text=str(path)
ET.ElementTree(proj).write(runner/'UnityCompile.csproj',encoding='unicode')
record('UNITY_API_COMPILE',['dotnet','build',str(runner/'UnityCompile.csproj'),'--nologo','-v:q'])
# Portable rollback restores only files belonging to this transaction.
base=out/'BASELINE'; mod=out/'MODIFIED_FILE'
paths=sorted(set(str(p.relative_to(base)).replace('\\','/') for p in base.rglob('*') if p.is_file())|set(str(p.relative_to(mod)).replace('\\','/') for p in mod.rglob('*') if p.is_file()))
changed=[x for x in paths if not (base/x).exists() or not (mod/x).exists() or (base/x).read_bytes()!=(mod/x).read_bytes()]
script='#!/bin/sh\nset -eu\n[ "$#" -eq 1 ] || { echo "Usage: ROLLBACK.sh target-copy" >&2; exit 2; }\nhere=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\ntarget=$1\n[ -d "$target" ] || exit 2\n'
for x in changed:
 if (base/x).exists(): script+=f'mkdir -p "$target/{str(Path(x).parent).replace(chr(92),chr(47))}"\ncp "$here/BASELINE/{x}" "$target/{x}"\n'
 else: script+=f'rm -f "$target/{x}"\n'
script+='echo ROLLBACK_RESTORED\n'; (out/'ROLLBACK.sh').write_text(script,encoding='utf-8',newline='\n')
shutil.copytree(mod,out/'ROLLBACK_COPY',dirs_exist_ok=True)
bash='C:/Program Files/Git/bin/bash.exe'
record('ROLLBACK_EXECUTION',[bash,str(out/'ROLLBACK.sh'),str(out/'ROLLBACK_COPY')])
run('ROLLBACK',out/'ROLLBACK_COPY')
hashes=json.loads((out/'baseline-hashes.json').read_text())
assert all(hashlib.sha256((out/'ROLLBACK_COPY'/x).read_bytes()).hexdigest()==h for x,h in hashes.items())
assert set(str(p.relative_to(out/'ROLLBACK_COPY')) for p in (out/'ROLLBACK_COPY').rglob('*') if p.is_file())==set(hashes)
patch=''
for x in changed:
 a=(base/x).read_text(encoding='utf-8-sig').splitlines(True) if (base/x).exists() else []
 b=(mod/x).read_text(encoding='utf-8-sig').splitlines(True) if (mod/x).exists() else []
 patch+=''.join(difflib.unified_diff(a,b,fromfile='a/'+x if a else '/dev/null',tofile='b/'+x))
(out/'DIFF.patch').write_text(patch,encoding='utf-8',newline='\n')
# Apply only changed files to project; keep all unrelated changes intact.
for x in changed:
 dest=root/'Assets/Scripts/WorldGen'/x; dest.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(mod/x,dest)
with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:
 f.write('ROLLBACK_HASH_MATCH: all baseline files byte-identical; added files removed\nChanged symbols: WorldGenerationSettings, WorldFields.CreateLearningDefaults, FractalNoise, BlendNoise, VoxelWorldPreview.ValidateWindow\n')
 for x in changed:f.write(x+' MODIFIED_SHA256 '+hashlib.sha256((mod/x).read_bytes()).hexdigest()+'\n')
 f.write('Baseline hashes: baseline-hashes.json\nUnity scene visual check: NOT RUN\n')
for role in ['MODIFIED_FILE','DIFF.patch','VERIFICATION.txt','ROLLBACK.sh']:
 path=out/role
 if path.is_dir():
  for x in changed:(path/x).read_bytes()
 else:path.read_bytes()
 print('REOPENED',path)
