#!/bin/bash
# ci-stage: integration
# ci-timeout: 5400
# Unity PlayMode 테스트(PIPELINE 4.6 통합 빌드, 7.2). 바뀐 경로로 줄이지 않는다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet
ci_unity_prepare
/bin/bash "$UNITY_SH" test playmode "$ROOT/artifacts/ci/unity/playmode.xml"
