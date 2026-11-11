#!/bin/bash
# ci-stage: fast
# ci-timeout: 3600
# 바뀐 프로젝트의 테스트(PIPELINE 4.6): 바뀐 경로를 가진 프로젝트와 그것을 (전이로) 참조하는 프로젝트 가운데 테스트 프로젝트를 Debug 로 돌린다.
# 어느 프로젝트에도 속하지 않는 경로(빌드 설정, data/, docs/spec/, tools/ci/ …)가 바뀌었거나 main 에서 돌면 전부 돈다.
# 실패, 건너뛴 테스트, 결과(TRX)가 없는 실행은 실패다. TRX 는 $WIZARD_CI_TRX_DIR/<프로젝트>/ 에 남고 vector-coverage 가 읽는다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet
ci_tool test-affected --root "$ROOT"
