#!/bin/bash
# ci-stage: fast
# 전체 단위 테스트(Debug). 실패, 건너뛴 테스트, 결과(TRX)가 없거나 모자란 실행은 실패다(테스트를 건너뛰게 표시하지 않는다).
# 판정은 TRX 로 한다(ci trx-check). 콘솔 요약 줄은 dotnet 의 표시 언어를 따라 바뀌므로(이 PC 는 한국어) 세지 않는다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/lib/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet
LOG="$ROOT/artifacts/ci/test.log"
TRX="$ROOT/artifacts/ci/test-trx"
/bin/rm -rf "$TRX"
/bin/mkdir -p "$ROOT/artifacts/ci"
dotnet test "$ROOT/Wizard.sln" -c Debug -nologo --logger trx --results-directory "$TRX" 2>&1 | /usr/bin/tee "$LOG"
code=${PIPESTATUS[0]}
[ "$code" -eq 0 ] || exit "$code"
ci_tool trx-check "$TRX" --expect solution --root "$ROOT"
