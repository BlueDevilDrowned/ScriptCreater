#!/usr/bin/env bash
set -euo pipefail
TARGET=${1:?usage: ROLLBACK.sh <project-root>}
FACTORY="$TARGET/Assets/Scripts/ECS/Factory"
if [ -d "$FACTORY" ]; then
  rm -rf "$FACTORY"
fi
printf 'restored=%s\n' "$TARGET"