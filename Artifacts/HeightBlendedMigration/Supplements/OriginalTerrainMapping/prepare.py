from pathlib import Path
import uuid
m=Path('Artifacts/HeightBlendedMigration/MODIFIED_FILE')
for rel in ['Density/OriginalTerrainProvider.cs']:
 (m/(rel+'.meta')).write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8',newline='\n')
p=m/'Density/世界生成变更记录.md'; s=p.read_text(encoding='utf-8-sig')+'''\n\n## 2026-10-21：原版 C/E/R 地形参数映射\n\n- 新增 `OriginalTerrainProvider`，移植原版 `TerrainProvider` 的大陆性控制点与嵌套样条结构；Offset 使用 C 控制点 `[-1.10,-1.02,-0.51,-0.44,-0.18,-0.16,-0.10,0.25,1.0]`，Factor 使用 `[-0.19,-0.15,-0.10,0.03,0.06]`，Jaggedness 使用 `-0.11、0.03、0.65`。\n- Ridges 改为保留原始带符号二维场；增加原版 `P=-3*(abs(abs(R)-2/3)-1/3)`，P 参与 Offset、Factor、Jaggedness。\n- 保留当前项目的坐标尺度、学习版三维混合噪声和垂直滑动；尚未实现原版 shiftedNoise2d、-0.50375 校准及全部辅助曲线逐点数值等价。\n'''; p.write_text(s,encoding='utf-8',newline='\n')
