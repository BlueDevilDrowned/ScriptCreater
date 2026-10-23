#!/bin/sh
set -eu
[ "$#" -eq 1 ] || { echo "Usage: ROLLBACK.sh target-copy" >&2; exit 2; }
base=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cp "$base/BASELINE.cs" "$1"
