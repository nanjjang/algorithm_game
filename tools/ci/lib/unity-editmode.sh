#!/bin/bash
# ci-stage: full
# ci-timeout: 5400
# Unity EditMode 테스트(PIPELINE 4.6 병합 게이트). unity.sh test editmode: -quit 없이 -runTests -testPlatform EditMode -testResults <절대 경로>.
# 실행 0개, 실패, 건너뜀은 실패다(unity.sh 가 거짓 통과를 막는다). 바뀐 경로가 Unity 에 닿지 않으면 건너뛰고 결과에 적는다(PIPELINE 3.8).
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet
ci_unity_prepare scoped
/bin/bash "$UNITY_SH" test editmode "$ROOT/artifacts/ci/unity/editmode.xml"
