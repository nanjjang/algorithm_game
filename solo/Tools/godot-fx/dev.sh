#!/usr/bin/env bash
# Dev loop: bake a subset and write a preview to $TEMP/fx_dev.png
set -euo pipefail
export PYTHONDONTWRITEBYTECODE=1
HERE="$(cd "$(dirname "$0")" && pwd)"
GODOT="${GODOT:-/c/Users/lovey/Downloads/Godot_v4.7.2-stable_win64.exe/Godot_v4.7.2-stable_win64_console.exe}"
RAW="${FX_RAW:-${TEMP:-/tmp}/godot-fx-raw}"
python -I "$HERE/jobs.py" >/dev/null
"$GODOT" --path "$HERE" --script res://bake.gd -- --raw "$RAW" --only "$1" 2>&1 | grep -E "ERROR|error|baked" | grep -v "^\s*at:" | head -20
python -I "$HERE/post.py" "$RAW" --only "$1"
python -I "$HERE/preview.py" --only "$1" --out "${TEMP:-/tmp}/fx_dev.png"
