#!/usr/bin/env bash
# 협동 전용 서버(Docs/11-online.md 3단계).
#   Tools/coop-server.sh build          # solo/Builds/CoopServer/Wizard.app 만들기 (Unity 배치 모드, 에디터를 닫고)
#   Tools/coop-server.sh run [포트]     # 화면 없이 서버 띄우기 (기본 포트 27870, TCP). Ctrl+C로 끈다
# 로그: solo/Logs/unity/coop-server-build.log, 서버 출력은 터미널(그리고 -logFile -).
set -u
. "$(dirname "${BASH_SOURCE[0]}")/unity-common.sh"

APP="$SOLO_ROOT/Builds/CoopServer/Wizard.app"

case "${1:-}" in
  build)
    unity_precheck || exit $?
    LOG="$UNITY_LOG_DIR/coop-server-build.log"
    "$UNITY_EXE" -batchmode -quit -projectPath "$(winpath "$SOLO_ROOT")" \
      -executeMethod Wizard.Solo.EditorTools.CoopServerBuild.Build -logFile "$(winpath "$LOG")"
    code=$?
    unity_compile_errors "$LOG"
    grep -E "\[coop-server-build\]" "$LOG" | tail -1
    exit $code
    ;;
  run)
    PORT="${2:-27870}"
    BIN="$(ls "$APP/Contents/MacOS/"* 2>/dev/null | head -1)"
    if [ -z "$BIN" ]; then echo "coop-server: 빌드가 없다 ($APP). 먼저 'Tools/coop-server.sh build'" >&2; exit 2; fi
    exec "$BIN" -batchmode -nographics -coopServer -port "$PORT" -logFile -
    ;;
  *)
    sed -n '2,5p' "$0"
    exit 2
    ;;
esac
