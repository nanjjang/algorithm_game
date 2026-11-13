#!/bin/bash
# ci-stage: fast
# 개정 장부 검사(DECISIONS-TECH T-65 7번, PIPELINE 4.4·7.1 G0): taskgraph ledger
#   - docs/dev/DECISIONS-TECH.md 의 '## T-<번호>' 절 제목과 요약 표의 '| T-<번호> |' 줄의 번호가 겹치지 않고, 요약 줄의 번호는 절이 있다
#   - docs/dev/OPEN-QUESTIONS.md 의 '| Q-<번호> |' 줄의 번호가 겹치지 않는다
#   - 세 장부(그리고 docs/dev/tasks.json)에 충돌 표식 줄이 없다
# 실패는 파일:줄과 번호를 적고 1 로 끝난다. 저장소 뿌리는 이 파일의 위치에서 구한다.
# taskgraph 를 빌드해 부르는 방식은 tools/ci/lib/task-branch.sh 와 같다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/lib/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet

# 테스트는 이미 빌드된 taskgraph 를 WIZARD_TASKGRAPH_DLL 로 준다
if [ -n "${WIZARD_TASKGRAPH_DLL:-}" ] && [ -f "${WIZARD_TASKGRAPH_DLL}" ]; then
    TG="$WIZARD_TASKGRAPH_DLL"
else
    ART="$ROOT/artifacts/ci-runner"
    dotnet build "$ROOT/tools/Wizard.TaskGraph/Wizard.TaskGraph.csproj" -c Release -nologo -v quiet -p:ArtifactsPath="$ART" >/dev/null \
        || ci_die "tools/Wizard.TaskGraph 를 빌드하지 못했다"
    TG="$ART/bin/Wizard.TaskGraph/release/Wizard.TaskGraph.dll"
fi
if ! dotnet "$TG" ledger --repo "$ROOT"; then
    echo "FAIL  taskgraph ledger: 개정 장부의 번호가 겹치거나 충돌 표식이 남았다. 위 줄의 파일:줄을 고친다(번호는 taskgraph next-ids 로 고른다)" >&2
    exit 1
fi
exit 0
