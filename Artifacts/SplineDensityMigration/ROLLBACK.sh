#!/usr/bin/env sh
set -eu
[ "$#" -eq 1 ] || { echo "usage: ROLLBACK.sh PROJECT_COPY" >&2; exit 2; }
for name in CubicSplinePoint.cs CubicSplineFunction.cs; do rm -f -- "$1/Assets/Scripts/WorldGen/Density/$name"; done
echo "ROLLBACK: newly added spline files removed; existing density files require the saved project snapshot for restoration"
