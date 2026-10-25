#!/usr/bin/env bash
set -euo pipefail
TARGET=${1:?usage: ROLLBACK.sh <target-copy>}
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BASELINE="$SCRIPT_DIR/manifest.baseline.json"
if [ ! -f "$BASELINE" ]; then echo "missing baseline: $BASELINE" >&2; exit 2; fi
cp "$BASELINE" "$TARGET"
printf 'restored=%s\n' "$TARGET"