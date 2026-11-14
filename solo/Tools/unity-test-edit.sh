#!/usr/bin/env bash
# Unity 테스트 러너로 EditMode 테스트를 돌린다(Wizard.Solo.Tests.EditMode 어셈블리만).
# 그래픽이 필요 없는 규칙 테스트.
#   Tools/unity-test-edit.sh                       # 전부
#   Tools/unity-test-edit.sh -f "이름 또는 정규식"   # -testFilter (이름·네임스페이스·정규식. ; 로 여러 개)
#   -c <분류>      -testCategory
#   -r <결과 xml>  기본 Logs/unity/test-edit.xml      -l <로그>  기본 Logs/unity/test-edit.log
# **-quit을 붙이지 않는다**(붙이면 테스트가 돌지 않고 0이 나온다). 러너가 끝나면 Unity가 스스로 닫힌다.
# 끝 코드: 0 = 1개 이상 돌았고 전부 통과 / 1 = 실패 있음 또는 컴파일 오류 / 2 = 도구 문제 / 3 = 결과 없음(돌지 않음 — 통과가 아니다).
set -u
. "$(dirname "${BASH_SOURCE[0]}")/unity-common.sh"

PLATFORM=EditMode
ASSEMBLY=Wizard.Solo.Tests.EditMode
FILTER=""
CATEGORY=""
XML=""
LOG=""
while getopts "f:c:r:l:h" opt; do
  case "$opt" in
    f) FILTER="$OPTARG" ;;
    c) CATEGORY="$OPTARG" ;;
    r) XML="$OPTARG" ;;
    l) LOG="$OPTARG" ;;
    h) sed -n '2,10p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) exit 2 ;;
  esac
done
unity_precheck || exit 2
[ -n "$XML" ] || XML="$UNITY_LOG_DIR/test-edit.xml"
[ -n "$LOG" ] || LOG="$UNITY_LOG_DIR/test-edit.log"
rm -f "$XML" "$LOG"
mkdir -p "$SOLO_ROOT/Logs/shots"

ARGS=(-batchmode -projectPath "$(winpath "$SOLO_ROOT")" -runTests -testPlatform "$PLATFORM"
      -assemblyNames "$ASSEMBLY" -testResults "$(winpath "$XML")" -logFile "$(winpath "$LOG")")
[ -n "$FILTER" ] && ARGS+=(-testFilter "$FILTER")
[ -n "$CATEGORY" ] && ARGS+=(-testCategory "$CATEGORY")

"$UNITY_EXE" "${ARGS[@]}"
CODE=$?

ERRS="$(unity_compile_errors "$LOG")"
if [ -n "$ERRS" ]; then
  echo "$ERRS"
  echo "unity-test-edit: 컴파일 오류 — 테스트가 돌지 않았다. Unity 끝 코드 $CODE, 로그 $LOG"
  exit 1
fi
unity_test_summary "$XML"
RC=$?
echo "unity-test-edit: Unity 끝 코드 $CODE, 로그 $LOG"
exit $RC
