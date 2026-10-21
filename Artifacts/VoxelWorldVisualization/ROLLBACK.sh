#!/usr/bin/env sh
# Restore this additive transaction on a project COPY. No baseline source was modified.
set -eu
[ "$#" -eq 1 ] || { echo "usage: ROLLBACK.sh PROJECT_COPY" >&2; exit 2; }
[ -d "$1/Assets/Scripts/WorldGen/Density" ] || { echo "not a project copy" >&2; exit 2; }
for name in VoxelVolume.cs VoxelMeshBuilder.cs VoxelWorldPreview.cs VoxelWorldControls.cs README.md; do
    rm -f -- "$1/Assets/Scripts/WorldGen/Voxels/$name" "$1/Assets/Scripts/WorldGen/Voxels/$name.meta"
done
rmdir -- "$1/Assets/Scripts/WorldGen/Voxels" 2>/dev/null || true
rm -f -- "$1/Assets/Scripts/WorldGen/Voxels.meta"
rm -f -- "$1/Assets/Editor/VoxelWorldPreviewMenu.cs" "$1/Assets/Editor/VoxelWorldPreviewMenu.cs.meta"
echo "ROLLBACK: voxel preview additions removed; baseline density sources preserved"
