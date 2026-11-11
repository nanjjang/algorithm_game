#!/bin/bash
# ci-stage: fast
# 에이전트 설정(PIPELINE 5.5): 설치본(루트 CLAUDE.md, .claude/agents/)이 docs/dev/agent-config 원본과 바이트가 같고,
# 모델별 변형(*-opus.md)은 머리말의 name·description·model 만 다르고 본문이 기본 파일과 같다.
# 설치(M0-TL-02)는 사용자 승인 뒤에만 한다. 설치 전에는 원본끼리만 본다.
set -u
ROOT="${WIZARD_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
SRC="$ROOT/docs/dev/agent-config"
fail=0
[ -d "$SRC/agents" ] || { echo "FAIL  $SRC/agents missing" >&2; exit 1; }

body() {   # YAML 머리말(--- … ---) 뒤의 본문
    /usr/bin/awk 'BEGIN{n=0} { if (n<2 && $0=="---") { n++; next } if (n>=2) print }' "$1"
}

for variant in "$SRC"/agents/*-opus.md; do
    [ -f "$variant" ] || continue
    base="${variant%-opus.md}.md"
    if [ ! -f "$base" ]; then
        echo "FAIL  $(basename "$variant") has no base file" >&2
        fail=1
        continue
    fi
    if [ "$(body "$variant")" != "$(body "$base")" ]; then
        echo "FAIL  body of $(basename "$variant") differs from $(basename "$base")" >&2
        fail=1
    fi
done

if [ -f "$ROOT/CLAUDE.md" ]; then
    /usr/bin/cmp -s "$ROOT/CLAUDE.md" "$SRC/CLAUDE.proposed.md" || { echo "FAIL  CLAUDE.md differs from agent-config/CLAUDE.proposed.md" >&2; fail=1; }
    for f in "$SRC"/agents/*.md; do
        installed="$ROOT/.claude/agents/$(basename "$f")"
        if [ ! -f "$installed" ] || ! /usr/bin/cmp -s "$f" "$installed"; then
            echo "FAIL  .claude/agents/$(basename "$f") is missing or differs from the original" >&2
            fail=1
        fi
    done
else
    echo "note  CLAUDE.md is not installed yet (M0-TL-02 needs the user's approval); checked originals only"
fi
[ $fail -eq 0 ] && echo "agent-config-check: ok"
exit $fail
