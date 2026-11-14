#!/bin/bash
# 로컬 CI 의 입구(PIPELINE 4.6, DECISIONS-TECH T-40). bash 3.2 문법만 쓰는 껍데기다.
#
#   tools/ci/ci.sh fast            작업 게이트: 빌드(Debug·Release, 분석기, 계층), 결정론 탐침, 계층·뼈대, .meta, 생성 파일, tasks.json,
#                                  에이전트 설정, 바뀐 프로젝트의 테스트, 벡터 대응
#   tools/ci/ci.sh full            병합 게이트: fast + 전체 테스트(Release) + 결정론·넷코드·서버 통합(그 검사 스크립트) + Unity 배치 컴파일과 EditMode
#                                  (Unity 단계는 바뀐 경로가 src/, unity/, data/, tests/golden/, tests/Wizard.TestKit/Shared/,
#                                   tools/Wizard.MetaGen/ 에 닿지 않을 때만 건너뛰고 그 사실을 결과에 적는다. PIPELINE 3.8)
#   tools/ci/ci.sh integration     통합 빌드: full + 플레이어 빌드, 런타임 × 아키텍처, 2인 헤드리스, 성능, PlayMode
#   tools/ci/ci.sh gate M<N>       마일스톤 게이트: 위 전부(경로로 줄이지 않는다) + docs/dev/gates/M<N>.json
#   tools/ci/ci.sh run <스크립트>   검사 하나만(예: gen-check.sh, tasks-check.sh, lib/meta-check.sh)
#   --all                          fast|full|integration 에서 바뀐 경로로 줄이지 않는다
#
# 단계에 붙는 검사는 tools/ci/*.sh 와 tools/ci/lib/*.sh 가운데 앞 12줄 안에 "# ci-stage: fast|full|integration" 이 있는 것이고
# 파일 이름순으로 돈다("# ci-timeout: <초>" 로 시간 제한을 바꾼다). 새 검사는 이 파일을 고치지 않고 tools/ci/ 에 스크립트를 더한다.
# 단계 실행, 시간 제한, 잠금, 바뀐 경로에 따른 선택은 tools/Wizard.Ci 가 한다.
# 결과: artifacts/ci/<단계>.json(단계 요약), artifacts/ci/<단계>/<검사>.json·.log
# 저장소 뿌리는 현재 폴더가 아니라 이 파일의 위치에서 구한다(다른 폴더에서 절대 경로로 불러도 이 파일이 든 체크아웃을 검사한다).
# 필요한 것(Unity, mac-il2cpp, x64 dotnet, Docker)이 없으면 그 검사는 건너뛰지 않고 무엇이 없는지와 할 일을 적고 실패한다.
set -u

case "$0" in
    */*) CI_DIR="${0%/*}" ;;
    *) CI_DIR="." ;;
esac
CI_DIR="$(cd "$CI_DIR" && pwd -P)" || exit 2
. "$CI_DIR/lib/common.sh"
# 뿌리는 ci_path 를 거친 꼴(Windows 의 Git Bash 에서 pwd 가 낸 /d/… 를 D:/… 로. dotnet 에 그대로 넘긴다. T-67 4번)
ROOT="$(ci_path "$(cd "$CI_DIR/../.." && pwd -P)")" || exit 2
export WIZARD_ROOT="$ROOT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

usage() {
    echo "usage: $0 fast|full|integration [--all] | gate M<N> | run <script>" >&2
    exit 2
}

[ $# -ge 1 ] || set -- fast
MODE="$1"
shift

ci_need_dotnet
# 테스트는 이미 빌드된 실행기를 WIZARD_CI_RUNNER 로 준다(임시 저장소에는 tools/Wizard.Ci 가 없다)
if [ -n "${WIZARD_CI_RUNNER:-}" ] && [ -f "${WIZARD_CI_RUNNER}" ]; then
    RUNNER="$WIZARD_CI_RUNNER"
else
    RUNNER="$(ci_build_runner "$ROOT")" || exit 2
fi
export WIZARD_CI_DLL="$RUNNER"

case "$MODE" in
    fast|full|integration)
        if [ $# -gt 1 ] || { [ $# -eq 1 ] && [ "$1" != "--all" ]; }; then
            usage
        fi
        exec dotnet "$RUNNER" stage "$MODE" --root "$ROOT" "$@"
        ;;
    gate)
        [ $# -eq 1 ] || usage
        exec dotnet "$RUNNER" gate "$1" --root "$ROOT"
        ;;
    run)
        [ $# -eq 1 ] || usage
        exec dotnet "$RUNNER" run "$1" --root "$ROOT"
        ;;
    *)
        usage
        ;;
esac
