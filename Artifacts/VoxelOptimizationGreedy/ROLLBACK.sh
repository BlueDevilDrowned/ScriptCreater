#!/usr/bin/env sh
set -eu
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
target=${1:?usage: ROLLBACK.sh ROLLBACK_COPY_VoxelMeshBuilder.cs}
case "$(basename "$target")" in
  ROLLBACK_COPY_VoxelMeshBuilder.cs) cp "$script_dir/BASELINE_VoxelMeshBuilder.cs" "$target" ;;
  *) echo "target must be a rollback copy" >&2; exit 2 ;;
esac