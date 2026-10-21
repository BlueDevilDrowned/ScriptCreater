#!/usr/bin/env sh
set -eu
# Usage: sh ROLLBACK.sh <density-source-copy-directory>
target=${1:?supply a density-source-copy directory}
root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
test -d "$target"
test -f "$target/WorldFields.cs"
for source in "$root"/BASELINE/*; do
    cp "$source" "$target/$(basename "$source")"
done
# These two files were added by this transaction.
rm -f "$target/ProjectedXZFunction.cs" "$target/世界生成变更记录.md"
printf '%s\n' ROLLBACK_RESTORED
