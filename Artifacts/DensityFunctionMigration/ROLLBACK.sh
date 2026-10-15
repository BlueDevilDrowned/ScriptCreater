#!/usr/bin/env sh
set -eu

if [ "$#" -ne 1 ]; then
  echo "usage: $0 TARGET_COPY" >&2
  exit 2
fi

target=$1
if [ -e "$target" ]; then
  rm -f -- "$target"
fi
test ! -e "$target"
echo "rollback: restored pristine absent baseline for $target"
