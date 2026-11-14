#!/bin/bash
# ci-stage: fast
# 에이전트 설정(PIPELINE 5.5):
#   - 모델별 변형(*-opus.md)은 머리말(name·description·model)만 다르고 본문이 기본 파일과 같다(원본 docs/dev/agent-config/agents/ 안에서)
#   - 설치본(루트 CLAUDE.md, .claude/agents/*.md)이 원본(docs/dev/agent-config/CLAUDE.proposed.md, agents/)과 바이트가 같다.
#     설치본에 원본에 없는 에이전트 파일이 있어도 실패다
# 설치(M0-TL-02)는 사용자 승인 뒤에만 한다. 설치 전(루트 CLAUDE.md 도 .claude/agents/*.md 도 없음)에는 원본끼리만 본다.
# 원본 폴더도 설치본도 없는 체크아웃(git 이 추적하지 않는다. 커밋 7e20951)에서는 그 사실을 note 로 남기고 통과한다. 설치본만 있으면 실패다(T-67 9번).
# pre-commit 훅도 이 경로들을 올렸을 때 이 스크립트를 부른다. 저장소 뿌리는 이 파일의 위치에서 구한다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/lib/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
SRC="$ROOT/docs/dev/agent-config"
fail=0

installed=0
[ -f "$ROOT/CLAUDE.md" ] && installed=1
for f in "$ROOT"/.claude/agents/*.md; do
    [ -f "$f" ] && installed=1
done

# 원본 폴더는 git 이 추적하지 않는다(커밋 7e20951). 원본도 설치본도 없는 체크아웃(작업 트리, 새로 받은 저장소)에서는 볼 것이 없다(T-67 9번).
# 건너뜀이 아니다: 저장소가 그 파일들을 싣지 않는 것이다. 설치본만 있고 원본이 없으면 대조할 수 없으므로 실패다.
if [ ! -d "$SRC" ]; then
    if [ $installed -eq 0 ]; then
        ci_note "이 체크아웃에는 에이전트 설정이 없다(git이 추적하지 않는다): docs/dev/agent-config/, CLAUDE.md, .claude/agents/*.md 가 모두 없다"
        echo "agent-config-check: ok"
        exit 0
    fi
    echo "FAIL  CLAUDE.md 또는 .claude/agents/*.md 가 있는데 원본 docs/dev/agent-config/ 가 없다(설치본을 원본과 대조할 수 없다)" >&2
    exit 1
fi
[ -d "$SRC/agents" ] || { echo "FAIL  $SRC/agents missing" >&2; exit 1; }
[ -f "$SRC/CLAUDE.proposed.md" ] || { echo "FAIL  $SRC/CLAUDE.proposed.md missing" >&2; exit 1; }

body() {   # YAML 머리말(--- … ---) 뒤의 본문
    /usr/bin/awk 'BEGIN{n=0} { if (n<2 && $0=="---") { n++; next } if (n>=2) print }' "$1"
}

for variant in "$SRC"/agents/*-opus.md; do
    [ -f "$variant" ] || continue
    base="${variant%-opus.md}.md"
    if [ ! -f "$base" ]; then
        echo "FAIL  ${variant##*/} has no base file" >&2
        fail=1
        continue
    fi
    if [ "$(body "$variant")" != "$(body "$base")" ]; then
        echo "FAIL  body of ${variant##*/} differs from ${base##*/}" >&2
        fail=1
    fi
done

if [ $installed -eq 1 ]; then
    if [ ! -f "$ROOT/CLAUDE.md" ]; then
        echo "FAIL  .claude/agents/ is installed but CLAUDE.md is missing" >&2
        fail=1
    elif ! /usr/bin/cmp -s "$ROOT/CLAUDE.md" "$SRC/CLAUDE.proposed.md"; then
        echo "FAIL  CLAUDE.md differs from docs/dev/agent-config/CLAUDE.proposed.md" >&2
        fail=1
    fi
    for f in "$SRC"/agents/*.md; do
        dest="$ROOT/.claude/agents/${f##*/}"
        if [ ! -f "$dest" ] || ! /usr/bin/cmp -s "$f" "$dest"; then
            echo "FAIL  .claude/agents/${f##*/} is missing or differs from the original" >&2
            fail=1
        fi
    done
    for f in "$ROOT"/.claude/agents/*.md; do
        [ -f "$f" ] || continue
        if [ ! -f "$SRC/agents/${f##*/}" ]; then
            echo "FAIL  .claude/agents/${f##*/} has no original in docs/dev/agent-config/agents/" >&2
            fail=1
        fi
    done
else
    echo "note  CLAUDE.md is not installed yet (M0-TL-02 needs the user's approval); checked originals only"
fi
[ $fail -eq 0 ] && echo "agent-config-check: ok"
exit $fail
