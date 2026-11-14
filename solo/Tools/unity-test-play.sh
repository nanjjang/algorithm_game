#!/usr/bin/env bash
# Unity 테스트 러너로 PlayMode 테스트를 돌린다(Wizard.Solo.Tests.PlayMode 어셈블리만).
# 그래픽 장치가 있어야 한다(-nographics를 붙이지 않는다). 스크린숏은 Logs/shots/ 에 남는다.
# 배치 모드에는 게임 창이 없어 화면이 저절로 그려지지 않는다 → 테스트(SmokeCapture)가 1920×1080 RenderTexture에 직접 그려 갈무리한다.
#   -w   창을 띄워 돌린다(-batchmode 없이). 게임 창에 실제로 나온 그림을 ScreenCapture로 갈무리한다(크기는 에디터의 게임 창 크기).
#        에디터 창이 화면에 뜨고, 대화 상자가 뜨면 멈출 수 있다 — 사람이 보는 자리에서만 쓴다.
#   Tools/unity-test-play.sh                       # 전부
#   Tools/unity-test-play.sh -f "이름 또는 정규식"   # -testFilter (이름·네임스페이스·정규식. ; 로 여러 개)
#   -c <분류>      -testCategory
#   -r <결과 xml>  기본 Logs/unity/test-play.xml      -l <로그>  기본 Logs/unity/test-play.log
# **-quit을 붙이지 않는다**(붙이면 테스트가 돌지 않고 0이 나온다). 러너가 끝나면 Unity가 스스로 닫힌다.
# 끝 코드: 0 = 1개 이상 돌았고 전부 통과 / 1 = 실패 있음 또는 컴파일 오류 / 2 = 도구 문제 / 3 = 결과 없음(돌지 않음 — 통과가 아니다).
set -u
. "$(dirname "${BASH_SOURCE[0]}")/unity-common.sh"

PLATFORM=PlayMode
ASSEMBLY=Wizard.Solo.Tests.PlayMode
FILTER=""
CATEGORY=""
XML=""
LOG=""
WINDOWED=0
while getopts "f:c:r:l:wh" opt; do
  case "$opt" in
    w) WINDOWED=1 ;;
    f) FILTER="$OPTARG" ;;
    c) CATEGORY="$OPTARG" ;;
    r) XML="$OPTARG" ;;
    l) LOG="$OPTARG" ;;
    h) sed -n '2,13p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) exit 2 ;;
  esac
done
unity_precheck || exit 2
[ -n "$XML" ] || XML="$UNITY_LOG_DIR/test-play.xml"
[ -n "$LOG" ] || LOG="$UNITY_LOG_DIR/test-play.log"
rm -f "$XML" "$LOG"
mkdir -p "$SOLO_ROOT/Logs/shots"

ARGS=(-projectPath "$(winpath "$SOLO_ROOT")" -runTests -testPlatform "$PLATFORM"
      -assemblyNames "$ASSEMBLY" -testResults "$(winpath "$XML")" -logFile "$(winpath "$LOG")")
[ "$WINDOWED" -eq 1 ] || ARGS=(-batchmode "${ARGS[@]}")
[ -n "$FILTER" ] && ARGS+=(-testFilter "$FILTER")
[ -n "$CATEGORY" ] && ARGS+=(-testCategory "$CATEGORY")

"$UNITY_EXE" "${ARGS[@]}"
CODE=$?

ERRS="$(unity_compile_errors "$LOG")"
if [ -n "$ERRS" ]; then
  echo "$ERRS"
  echo "unity-test-play: 컴파일 오류 — 테스트가 돌지 않았다. Unity 끝 코드 $CODE, 로그 $LOG"
  exit 1
fi
unity_test_summary "$XML"
RC=$?
echo "unity-test-play: Unity 끝 코드 $CODE, 로그 $LOG"
exit $RC
