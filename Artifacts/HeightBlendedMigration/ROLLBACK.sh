#!/bin/sh
set -eu
[ "$#" -eq 1 ] || { echo "Usage: ROLLBACK.sh target-copy" >&2; exit 2; }
here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
target=$1
[ -d "$target" ] || exit 2
rm -f "$target/Density/BlendedNoiseFunction.cs"
rm -f "$target/Density/BlendedNoiseFunction.cs.meta"
mkdir -p "$target/Density"
cp "$here/BASELINE/Density/DensityFunctions.cs" "$target/Density/DensityFunctions.cs"
rm -f "$target/Density/OctaveNoiseFunction.cs"
rm -f "$target/Density/OctaveNoiseFunction.cs.meta"
mkdir -p "$target/Density"
cp "$here/BASELINE/Density/ValueNoiseFunction.cs" "$target/Density/ValueNoiseFunction.cs"
mkdir -p "$target/Density"
cp "$here/BASELINE/Density/WorldFields.cs" "$target/Density/WorldFields.cs"
rm -f "$target/Density/WorldGenerationSettings.cs"
rm -f "$target/Density/WorldGenerationSettings.cs.meta"
rm -f "$target/Density/XZFieldMap.cs"
rm -f "$target/Density/XZFieldMap.cs.meta"
mkdir -p "$target/Density"
cp "$here/BASELINE/Density/世界生成变更记录.md" "$target/Density/世界生成变更记录.md"
rm -f "$target/Editor.meta"
rm -f "$target/Editor/WorldFieldMapWindow.cs"
rm -f "$target/Editor/WorldFieldMapWindow.cs.meta"
mkdir -p "$target/Voxels"
cp "$here/BASELINE/Voxels/VoxelWorldControls.cs" "$target/Voxels/VoxelWorldControls.cs"
mkdir -p "$target/Voxels"
cp "$here/BASELINE/Voxels/VoxelWorldPreview.cs" "$target/Voxels/VoxelWorldPreview.cs"
echo ROLLBACK_RESTORED
