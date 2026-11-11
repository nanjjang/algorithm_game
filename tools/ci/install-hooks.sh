#!/bin/bash
# git 훅을 건다(PIPELINE 4.6 "커밋 훅"): core.hooksPath 를 상대 경로 tools/ci/hooks 로 둔다.
#   pre-commit  fast 의 정적 검사(올린 파일): 금지 API 빠른 검사, 진단 끄기 금지, 이름 표 형식, .meta, tasks.json, 에이전트 설정, 프로젝트 뼈대
#   commit-msg  커밋 메시지 형식과 Co-Authored-By 줄
# 상대 경로라서 git 은 커밋이 일어나는 체크아웃(메인 또는 작업 트리)의 tools/ci/hooks 를 쓴다. 설정은 저장소 공통이다(모든 작업 트리).
#   tools/ci/install-hooks.sh           건다
#   tools/ci/install-hooks.sh --check   걸려 있는지만 본다(아니면 1)
# git 은 command git 으로만 부른다. sudo 를 쓰지 않는다. bash 3.2 문법만 쓴다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/lib/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
WANT="tools/ci/hooks"

command git -C "$ROOT" rev-parse --git-dir >/dev/null 2>&1 || ci_die "$ROOT 은 git 저장소가 아니다"
for h in pre-commit commit-msg; do
    [ -f "$ROOT/$WANT/$h" ] || ci_die "$ROOT/$WANT/$h 가 없다"
done

current="$(command git -C "$ROOT" config --get core.hooksPath 2>/dev/null)" || current=""
if [ "${1:-}" = "--check" ]; then
    if [ "$current" = "$WANT" ] && [ -x "$ROOT/$WANT/pre-commit" ] && [ -x "$ROOT/$WANT/commit-msg" ]; then
        echo "install-hooks: 걸려 있다(core.hooksPath=$WANT)"
        exit 0
    fi
    echo "FAIL  훅이 걸려 있지 않다(core.hooksPath='${current}', 원하는 값 '$WANT'). tools/ci/install-hooks.sh 로 건다" >&2
    exit 1
fi
[ $# -eq 0 ] || { echo "usage: $0 [--check]" >&2; exit 2; }

/bin/chmod +x "$ROOT/$WANT/pre-commit" "$ROOT/$WANT/commit-msg" || ci_die "훅에 실행 권한을 주지 못했다"
command git -C "$ROOT" config core.hooksPath "$WANT" || ci_die "git config core.hooksPath 실패"
if [ -n "$current" ] && [ "$current" != "$WANT" ]; then
    echo "install-hooks: core.hooksPath 를 '$current' 에서 '$WANT' 로 바꿨다"
fi
echo "install-hooks: pre-commit, commit-msg 를 걸었다(core.hooksPath=$WANT. 저장소의 모든 작업 트리에 적용)"
