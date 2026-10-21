from pathlib import Path
import subprocess, json, hashlib, shutil, uuid, os, xml.etree.ElementTree as ET, difflib
root=Path.cwd(); out=root/'Artifacts/HeightBlendedMigration'; sup=out/'Supplements/XZMaps'; src=root/'Assets/Scripts/WorldGen'; mod=out/'MODIFIED_FILE'; runner=out/'Runner'
os.environ['DOTNET_CLI_UI_LANGUAGE']='en-US'
def decode(b):
 try:return b.decode('utf-8')
 except UnicodeDecodeError:return b.decode('gbk')
def record(label,cmd,env=None,cwd=None):
 p=subprocess.run(cmd,capture_output=True,env=env,cwd=cwd)
 (sup/(label+'.stdout.bin')).write_bytes(p.stdout); (sup/(label+'.stderr.bin')).write_bytes(p.stderr)
 stdout=decode(p.stdout); stderr=decode(p.stderr)
 with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:f.write('\nXZ_MAPS_'+label+'\ncommand: '+json.dumps(cmd,ensure_ascii=False)+'\ncwd: '+str(cwd or root)+'\nexit: '+str(p.returncode)+'\nstdout:\n'+stdout+'\nstderr:\n'+stderr+'\n')
 print(label,p.returncode,stdout[-2300:],stderr)
 if p.returncode:raise SystemExit(p.returncode)
 return stdout
def sample(label,folder):return record(label,['dotnet','run','--project',str(runner/'Runner.csproj'),'-p:SourceDir='+str(folder/'Density'),'-p:TestProgram=Program.cs'])
prehash={p.relative_to(src).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in src.rglob('*') if p.is_file()}
(sup/'prechange-hashes.json').write_text(json.dumps(prehash,indent=2,ensure_ascii=False),encoding='utf-8')
current=sample('PRECHANGE',src)
# Keep original transaction BASELINE and the same four role paths; extend MODIFIED_FILE and ledger.
sample('BASELINE',out/'BASELINE')
for rel in ['Density/XZFieldMap.cs','Editor/WorldFieldMapWindow.cs']:
 p=mod/rel; meta=Path(str(p)+'.meta')
 meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8',newline='\n')
editor_meta=mod/'Editor.meta'; editor_meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8',newline='\n')
log=mod/'Density/世界生成变更记录.md'
text=(src/'Density/世界生成变更记录.md').read_text(encoding='utf-8-sig')+'''

## 2026-10-19：XZ 环境参数可视化

- 新增独立 `XZFieldMap`，直接采样 `WorldFields.Continents/Erosion/Ridges`，不修改地形场。采样坐标单位为体素，Y 固定为 0；这些参数已是二维场。
- 新增编辑器窗口 `Window/Procedural Generation/XZ Parameter Maps`。默认 seed=12345，X/Z 范围 [-512,511]，每图 256×256；可调整种子、起点、范围、分辨率，或读取选中 `VoxelWorldPreview` 的种子与 XZ 窗口。
- C/E 色阶固定 [-1,1]，当前 R=|noise| 色阶固定 [0,1]。低值蓝色、中值浅色、高值橙色；联动十字线同时显示 C/E/R/参考 P 的精确采样值与坐标。地图向右 +X、向上 +Z；局部最值仅作读数，不参与自动归一化。
- 第三张可切换到 P=-3*(abs(abs(R)-2/3)-1/3)，色阶 [-1,1]，标注为参考；P 未接入当前地形，也不意味着已复刻原版噪声。
- 纯 C# 验证采样与实际世界场一致、坐标端点、跨窗口一致性、种子与 Y 不变性、P 公式及边界；同源采样导出 CSV 和 PNG 供静态观察。编译与完整命令见原四角色中的 `Artifacts/HeightBlendedMigration/VERIFICATION.txt`，补充材料在 `Supplements/XZMaps`。
- 本次不改变地形公式与已有场景。编辑器窗口的实际 Unity 交互尚未运行检查。
'''
log.write_text(text,encoding='utf-8',newline='\n')
modified=sample('MODIFIED',mod)
assert current==modified,'Visualization changed terrain output'
env=os.environ.copy(); env['XZ_MAP_EXPORT']=str(sup/'samples.csv')
record('MAP_CHECKS',['dotnet','run','--project',str(runner/'Runner.csproj'),'-p:SourceDir='+str(mod/'Density'),'-p:TestProgram=MapChecks.cs'],env=env)
# Extend existing compile harness with Editor assembly references.
proj=ET.parse(runner/'UnityCompile.csproj'); items=proj.getroot().find('ItemGroup')
existing={x.attrib['Include'] for x in items.findall('Reference')}
for ref in ET.parse(root/'Assembly-CSharp-Editor.csproj').getroot().findall('.//Reference'):
 if not ref.attrib['Include'].startswith('UnityEditor') or ref.attrib['Include'] in existing:continue
 hint=ref.find('HintPath')
 if hint is not None and Path(hint.text).exists():
  new=ET.SubElement(items,'Reference',Include=ref.attrib['Include']); ET.SubElement(new,'HintPath').text=hint.text
proj.write(sup/'UnityCompileMaps.csproj',encoding='unicode')
record('UNITY_API_COMPILE',['dotnet','build',str(sup/'UnityCompileMaps.csproj'),'--nologo','-v:q'])
# Reconstruct cumulative patch from the preserved original baseline.
base=out/'BASELINE'; paths=sorted(set(p.relative_to(base).as_posix() for p in base.rglob('*') if p.is_file())|set(p.relative_to(mod).as_posix() for p in mod.rglob('*') if p.is_file()))
changed=[x for x in paths if not (base/x).exists() or not (mod/x).exists() or (base/x).read_bytes()!=(mod/x).read_bytes()]
patch=''
for rel in changed:
 a=(base/rel).read_text(encoding='utf-8-sig').splitlines(True) if (base/rel).exists() else []
 b=(mod/rel).read_text(encoding='utf-8-sig').splitlines(True) if (mod/rel).exists() else []
 patch+=''.join(difflib.unified_diff(a,b,fromfile='a/'+rel if a else '/dev/null',tofile='b/'+rel if b else '/dev/null'))
(out/'DIFF.patch').write_text(patch,encoding='utf-8',newline='\n')
script='#!/bin/sh\nset -eu\n[ "$#" -eq 1 ] || { echo "Usage: ROLLBACK.sh target-copy" >&2; exit 2; }\nhere=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\ntarget=$1\n[ -d "$target" ] || exit 2\n'
for rel in changed:
 if (base/rel).exists():script+=f'mkdir -p "$target/{Path(rel).parent.as_posix()}"\ncp "$here/BASELINE/{rel}" "$target/{rel}"\n'
 else:script+=f'rm -f "$target/{rel}"\n'
script+='echo ROLLBACK_RESTORED\n'; (out/'ROLLBACK.sh').write_text(script,encoding='utf-8',newline='\n'); os.chmod(out/'ROLLBACK.sh',0o755)
rebuild=sup/'PATCH_COPY'; shutil.copytree(base,rebuild)
record('PATCH_RECONSTRUCTION',['git','apply','--ignore-space-change',str(out/'DIFF.patch')],cwd=rebuild)
for p in mod.rglob('*'):
 if p.is_file():assert p.read_bytes()==(rebuild/p.relative_to(mod)).read_bytes()
restore=sup/'ROLLBACK_COPY'; shutil.copytree(mod,restore)
record('ROLLBACK_EXECUTION',['C:/Program Files/Git/bin/bash.exe',str(out/'ROLLBACK.sh'),str(restore)])
sample('ROLLBACK',restore)
basehash={p.relative_to(base).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in base.rglob('*') if p.is_file()}
restorehash={p.relative_to(restore).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in restore.rglob('*') if p.is_file()}
assert basehash==restorehash
# Commit only this follow-up's additions and change log to the actual project.
apply=['Density/XZFieldMap.cs','Density/XZFieldMap.cs.meta','Editor/WorldFieldMapWindow.cs','Editor/WorldFieldMapWindow.cs.meta','Editor.meta','Density/世界生成变更记录.md']
for rel in apply:
 target=src/rel; target.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(mod/rel,target)
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
