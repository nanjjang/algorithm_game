#!/bin/bash
# ci-stage: fast
# docs/dev/tasks.json 검사: node tools/check-tasks.mjs(JSON·필수 필드, 위상정렬, 묶음 안 경로 겹침, 겹치는 경로의 deps, 마일스톤 집계,
# 벡터 배정). node 표준 모듈만 쓴다. pre-commit 훅도 tasks.json·명세를 올렸을 때 이 스크립트를 부른다.
# 저장소 뿌리는 이 파일의 위치에서 구한다(check-tasks.mjs 도 자기 위치에서 구한다).
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/lib/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
command -v node >/dev/null 2>&1 || ci_die "node 가 없다. 사용자가 할 일: Node.js 를 설치한다(tools/check-tasks.mjs 가 쓴다)"
node "$ROOT/tools/check-tasks.mjs"
