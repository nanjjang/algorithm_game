#!/bin/bash
# ci-stage: full
# ci-timeout: 5400
# Unity 배치 컴파일(PIPELINE 4.6 병합 게이트, 4.5). unity.sh compile: metagen → Unity(-quit, Wizard.Unity.Editor.Ci.Compile) → Unity 가 고쳐 쓴 파일 되돌리기.
# 바뀐 경로가 Unity 에 닿지 않으면 건너뛰고 결과에 적는다(PIPELINE 3.8). 게이트(WIZARD_CI_FORCE_ALL=1)는 건너뛰지 않는다. 편집기가 없으면 실패한다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet
ci_unity_prepare scoped
/bin/bash "$UNITY_SH" compile
