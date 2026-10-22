from pathlib import Path
for path in [Path('Assets/Scripts/WorldGen/Density/WorldFields.cs'),Path('Artifacts/HeightBlendedMigration/MODIFIED_FILE/Density/WorldFields.cs')]:
 s=path.read_text(encoding='utf-8-sig'); s=s.replace('IDensityFunction offset = OriginalTerrainProvider.Offset(continents, erosion, foldedRidges);','IDensityFunction offset = DensityFunctions.Add(DensityFunctions.Constant(-0.50375), OriginalTerrainProvider.Offset(continents, erosion, foldedRidges));'); path.write_text(s,encoding='utf-8',newline='\r\n')
for path in [Path('Assets/Scripts/WorldGen/Density/世界生成变更记录.md'),Path('Artifacts/HeightBlendedMigration/MODIFIED_FILE/Density/世界生成变更记录.md')]:
 s=path.read_text(encoding='utf-8-sig'); s=s.replace('P 参与 Offset、Factor、Jaggedness。','P 参与 Offset、Factor、Jaggedness；Offset 加入原版 GLOBAL_OFFSET=-0.50375。'); path.write_text(s,encoding='utf-8',newline='\r\n')
