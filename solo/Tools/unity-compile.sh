#!/usr/bin/env bash
# Unity 배치 모드로 프로젝트를 열어 진짜 컴파일(임포트 포함)을 확인한다. 선택으로 에디터 메서드 하나를 실행한다.
#   Tools/unity-compile.sh                                         # 열고 컴파일만
#   Tools/unity-compile.sh -m Wizard.Solo.Editor.ProjectSetup.Run  # 장면·빌드 설정 생성
#   Tools/unity-compile.sh -m Wizard.Solo.Editor.ArtSync.Run       # 정본 그림 복사 + 임포트
#   -l <로그 파일>   기본 Logs/unity/compile.log
# 끝 코드: 0 = 컴파일 오류 없음(메서드도 성공) / 1 = 컴파일 오류 또는 메서드 실패 / 2 = 도구 문제(Unity 없음, 이미 떠 있음).
# 병렬 작업 중에는 쓰지 않는다(Unity는 한 번에 한 프로세스). 그때는 Tools/compile.sh.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/unity-common.sh"

METHOD=""
LOG=""
while getopts "m:l:h" opt; do
  case "$opt" in
    m) METHOD="$OPTARG" ;;
    l) LOG="$OPTARG" ;;
    h) sed -n '2,10p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) exit 2 ;;
  esac
done
unity_precheck || exit 2
[ -n "$LOG" ] || LOG="$UNITY_LOG_DIR/compile.log"
rm -f "$LOG"

ARGS=(-batchmode -projectPath "$(winpath "$SOLO_ROOT")" -quit -logFile "$(winpath "$LOG")")
[ -n "$METHOD" ] && ARGS+=(-executeMethod "$METHOD")

"$UNITY_EXE" "${ARGS[@]}"
CODE=$?

ERRS="$(unity_compile_errors "$LOG")"
if [ -n "$ERRS" ]; then
  echo "$ERRS"
  echo "unity-compile: 컴파일 오류 $(printf '%s\n' "$ERRS" | wc -l)줄, Unity 끝 코드 $CODE, 로그 $LOG"
  exit 1
fi
if [ "$CODE" -ne 0 ]; then
  grep -nE "Exception|\] failed|Aborting batchmode|executeMethod" "$LOG" | head -20
  echo "unity-compile: Unity 끝 코드 $CODE (컴파일 오류 줄은 없음), 로그 $LOG"
  exit 1
fi
echo "unity-compile: ok${METHOD:+ ($METHOD)}, 로그 $LOG"
exit 0
