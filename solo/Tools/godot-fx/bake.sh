#!/usr/bin/env bash
# Rebakes every Godot FX sheet and the preview.
#   solo/Tools/godot-fx/bake.sh [--only <substr>]
# GODOT env var overrides the Godot console executable path.
set -euo pipefail
export PYTHONDONTWRITEBYTECODE=1
HERE="$(cd "$(dirname "$0")" && pwd)"
if [ "$(uname -s)" = "Darwin" ]; then
  GODOT="${GODOT:-/Applications/Godot.app/Contents/MacOS/Godot}"
else
  GODOT="${GODOT:-/c/Users/lovey/Downloads/Godot_v4.7.2-stable_win64.exe/Godot_v4.7.2-stable_win64_console.exe}"
fi
RAW="${FX_RAW:-${TEMP:-/tmp}/godot-fx-raw}"
ONLY=()
if [ "${1:-}" = "--only" ]; then ONLY=(--only "$2"); fi
mkdir -p "$RAW"
python -I "$HERE/jobs.py"
# windowed on purpose: --headless has no renderer
"$GODOT" --path "$HERE" --script res://bake.gd -- --raw "$RAW" "${ONLY[@]}"
python -I "$HERE/post.py" "$RAW" "${ONLY[@]}"
python -I "$HERE/tiles.py" "$RAW" "${ONLY[@]}"
python -I "$HERE/compose.py" "$RAW" "${ONLY[@]}"   # style sheets drawn from shapes / GPT parts
python -I "$HERE/manifest.py"                       # effects/styles/<group>/manifest.json
python -I "$HERE/preview.py"
python -I "$HERE/preview_styles.py"                 # preview-styles.png
python -I "$HERE/audit.py"
echo "bake done"
