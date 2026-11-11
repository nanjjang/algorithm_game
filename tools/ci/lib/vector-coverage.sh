#!/bin/bash
# ci-stage: fast
# 벡터 대응(PIPELINE 7.1 G1, CLAUDE.md 8절):
#   전역: 명세 벡터마다 정확히 한 작업에 배정(미배정·이중 배정은 실패), 끝난 작업의 벡터마다 테스트, 벡터 테스트 이름이 명세의 벡터를 가리킴
#   작업: 브랜치가 task/<id> 이면 vector-coverage --task <id>(이번 실행의 테스트 결과 TRX 가 있으면 그 테스트가 통과했는지도 본다)
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet

ART="$ROOT/artifacts/ci-runner"
dotnet build "$ROOT/tools/Wizard.VectorCoverage/Wizard.VectorCoverage.csproj" -c Release -nologo -v quiet -p:ArtifactsPath="$ART" >/dev/null \
    || ci_die "tools/Wizard.VectorCoverage 를 빌드하지 못했다"
VC="$ART/bin/Wizard.VectorCoverage/release/Wizard.VectorCoverage.dll"

fail=0
dotnet "$VC" --root "$ROOT" || fail=1

branch="$(command git -C "$ROOT" symbolic-ref --quiet --short HEAD 2>/dev/null)" || branch=""
case "$branch" in
    task/*)
        id="${branch#task/}"
        set -- --task "$id" --root "$ROOT"
        trx_dir="${WIZARD_CI_TRX_DIR:-}"
        if [ -n "$trx_dir" ] && [ -d "$trx_dir" ] && [ -n "$(/usr/bin/find "$trx_dir" -name '*.trx' -type f 2>/dev/null | /usr/bin/head -1)" ]; then
            set -- "$@" --trx "$trx_dir"
        fi
        dotnet "$VC" "$@" || fail=1
        ;;
    *)
        ci_note "브랜치가 task/<id> 가 아니라(${branch:-분리된 HEAD}) 작업 벡터 검사(--task)는 돌리지 않았다"
        ;;
esac
exit $fail
