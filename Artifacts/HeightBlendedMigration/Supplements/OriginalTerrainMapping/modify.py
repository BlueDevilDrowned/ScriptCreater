from pathlib import Path
p=Path('Artifacts/HeightBlendedMigration/MODIFIED_FILE/Density/WorldFields.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('IDensityFunction ridges = DensityFunctions.Abs(DensityFunctions.Noise2D(seed + 3, 0.012, 1.0));','IDensityFunction ridges = DensityFunctions.Noise2D(seed + 3, 0.012, 1.0);\n            IDensityFunction foldedRidges = OriginalTerrainProvider.PeaksAndValleys(ridges);')
a=s.index('            IDensityFunction lowlandOffset ='); b=s.index('            // 垂直渐变',a)
s=s[:a]+'''            // 1.20.1 TerrainProvider：C 选择大陆性分支，E/P 作为嵌套样条坐标。
            IDensityFunction offset = OriginalTerrainProvider.Offset(continents, erosion, foldedRidges);
            IDensityFunction factor = OriginalTerrainProvider.Factor(continents, erosion, ridges, foldedRidges);
            IDensityFunction jaggedness = OriginalTerrainProvider.Jaggedness(continents, erosion, ridges, foldedRidges);
'''+s[b:]
p.write_text(s,encoding='utf-8',newline='\n')
p=Path('Artifacts/HeightBlendedMigration/MODIFIED_FILE/Density/世界生成变更记录.md')
s=p.read_text(encoding='utf-8-sig')+'''\n\n## 2026-10-21：原版 C/E/R 地形参数映射\n\n- 新增 `OriginalTerrainProvider`，移植 1.20.1 `TerrainProvider` 的大陆性控制点 `[-1.10,-1.02,-0.51,-0.44,-0.18,-0.16,-0.10,0.25,1.0]`，并将 E 与 PeaksAndValleys 作为嵌套样条坐标。\n- `Factor` 使用原版大陆性控制点 `[-0.19,-0.15,-0.10,0.03,0.06]`；`Jaggedness` 使用原版大陆性起点 `-0.11、0.03、0.65`。\n- Ridges 保留原始带符号二维场；新增 `P=-3*(abs(abs(R)-2/3)-1/3)`，P 进入 Offset/Factor/Jaggedness 映射。\n- 保留当前项目坐标尺度、学习版三维混合噪声和垂直滑动；尚未实现原版 shiftedNoise2d、-0.50375 校准及全部辅助曲线的逐点数值等价。\n'''
p.write_text(s,encoding='utf-8',newline='\n')
