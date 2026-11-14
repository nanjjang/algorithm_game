#!/bin/bash
# ci-stage: full
# Release 구성에서도 같은 테스트가 통과한다(디버그 빌드도 결과가 같다. ARCHITECTURE 11 연산 규칙).
# 실패, 건너뛴 테스트, 결과(TRX)가 없거나 모자란 실행은 실패다. 판정은 TRX 로 한다(ci trx-check. 콘솔 요약 줄의 언어에 기대지 않는다).
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/lib/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet
TRX="$ROOT/artifacts/ci/test-release-trx"
/bin/rm -rf "$TRX"
/bin/mkdir -p "$ROOT/artifacts/ci"
dotnet test "$ROOT/Wizard.sln" -c Release -nologo --logger trx --results-directory "$TRX" || exit $?
ci_tool trx-check "$TRX" --expect solution --root "$ROOT"
