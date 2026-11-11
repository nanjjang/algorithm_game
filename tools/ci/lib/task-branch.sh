#!/bin/bash
# ci-stage: fast
# 작업 브랜치의 G0 두 가지(PIPELINE 7.1, 4.4):
#   - 변경이 owned_paths 안인가: taskgraph check-diff <id>(main 과의 갈림점 이후 커밋한 변경. 공유 추가 전용 파일, 다시 만드는 파일의 규칙 포함)
#   - 커밋 메시지 형식: ci commit-msgs --task <id>(갈림점..HEAD 의 커밋마다 CLAUDE.md 10절 형식, 첫 줄의 작업 id 가 <id>)
# commit-msg 훅이 걸리지 않았거나(--no-verify, 설치 전) 소유 밖을 커밋해도 fast 가 실패하게 하려는 것이다.
# 브랜치가 task/<id> 가 아니면(main, 분리된 HEAD) 볼 작업이 없어 건너뛰고 그 사실을 남긴다.
# 커밋하지 않은 변경은 check-diff 가 보지 않는다. 있으면 그 수를 남긴다(커밋한 뒤 다시 돌린다).
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2

branch="$(command git -C "$ROOT" symbolic-ref --quiet --short HEAD 2>/dev/null)" || branch=""
case "$branch" in
    task/*) id="${branch#task/}" ;;
    *) ci_skip "브랜치가 task/<id> 가 아니다(${branch:-분리된 HEAD}): 소유 경로와 커밋 메시지를 볼 작업이 없다" ;;
esac
ci_need_dotnet

base="${WIZARD_CI_BASE:-main}"
fail=0

# 커밋 메시지
ci_tool commit-msgs --task "$id" --base "$base" --root "$ROOT" || fail=1

# 소유 경로
# 테스트는 이미 빌드된 taskgraph 를 WIZARD_TASKGRAPH_DLL 로 준다(임시 저장소에는 tools/Wizard.TaskGraph 가 없다)
if [ -n "${WIZARD_TASKGRAPH_DLL:-}" ] && [ -f "${WIZARD_TASKGRAPH_DLL}" ]; then
    TG="$WIZARD_TASKGRAPH_DLL"
else
    ART="$ROOT/artifacts/ci-runner"
    dotnet build "$ROOT/tools/Wizard.TaskGraph/Wizard.TaskGraph.csproj" -c Release -nologo -v quiet -p:ArtifactsPath="$ART" >/dev/null \
        || ci_die "tools/Wizard.TaskGraph 를 빌드하지 못했다"
    TG="$ART/bin/Wizard.TaskGraph/release/Wizard.TaskGraph.dll"
fi
if ! dotnet "$TG" check-diff "$id" --base "$base" --repo "$ROOT"; then
    echo "FAIL  taskgraph check-diff $id: 소유 경로(owned_paths) 밖의 변경이 있다. 위 줄의 파일을 되돌리거나(command git -C $ROOT checkout $base -- <경로>), 소유 작업에 넘긴다" >&2
    fail=1
fi

dirty="$(command git -C "$ROOT" status --porcelain --untracked-files=normal 2>/dev/null | /usr/bin/wc -l | /usr/bin/tr -d ' ')"
if [ "${dirty:-0}" != "0" ]; then
    ci_note "커밋하지 않은 변경 ${dirty}개는 check-diff·커밋 메시지 검사가 보지 않았다(커밋한 뒤 다시 돌린다)"
fi
exit $fail
