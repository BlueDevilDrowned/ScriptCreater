#!/usr/bin/env sh
set -eu
TARGET=${1:?usage: ROLLBACK.sh <target-copy>}
BASELINE="$(dirname "$TARGET")/BASELINE_FILE.cs"
if [ ! -f "$BASELINE" ]; then echo "missing baseline: $BASELINE" >&2; exit 2; fi
cp "$BASELINE" "$TARGET"
