#!/bin/bash
# 로컬 CI 의 입구 (PIPELINE 4.6, T-40). bash 3.2 문법만 쓴다.
#   tools/ci/ci.sh [fast|full|integration]   그 단계까지의 검사를 전부 돈다(기본 fast)
#   tools/ci/ci.sh run <스크립트 이름>          검사 하나만
# 단계에 붙는 검사는 tools/ci/ 의 다른 스크립트 가운데 머리말에 "# ci-stage: <단계>" 가 있는 것이다(이름순).
# 새 검사는 이 파일을 고치지 않고 스크립트 파일을 더해서 넣는다.
# 시간 제한과 결과 기록은 tools/Wizard.Ci(C#)가 한다. 결과: artifacts/ci/<단계>/<스크립트>.json
# 저장소 뿌리는 현재 폴더가 아니라 이 파일의 위치에서 구한다.
set -u

CI_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$CI_DIR/../.." && pwd)"
export WIZARD_ROOT="$ROOT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

STEP_TIMEOUT="${CI_STEP_TIMEOUT:-1800}"
RUNNER_DLL="$ROOT/artifacts/ci-runner/bin/Wizard.Ci/release/Wizard.Ci.dll"

die() { echo "ci: $*" >&2; exit 2; }

command -v dotnet >/dev/null 2>&1 || die "dotnet 이 없다. .NET SDK 10.0.201 을 설치해야 한다(global.json)."

# CI 실행기는 솔루션 빌드와 출력이 섞이지 않게 따로 만든다(실행 중인 DLL 을 덮어쓰지 않는다).
build_runner() {
    dotnet build "$ROOT/tools/Wizard.Ci/Wizard.Ci.csproj" -c Release -nologo -v quiet \
        -p:ArtifactsPath="$ROOT/artifacts/ci-runner" >/dev/null || die "tools/Wizard.Ci 를 빌드하지 못했다"
}

stage_rank() {
    case "$1" in
        fast) echo 1 ;;
        full) echo 2 ;;
        integration) echo 3 ;;
        *) echo 0 ;;
    esac
}

script_stage() {
    /usr/bin/sed -n 's/^# ci-stage: *\([a-z]*\).*/\1/p' "$1" | /usr/bin/head -1
}

run_one() {
    local script="$1" stage="$2"
    local name
    name="$(basename "$script" .sh)"
    local out="$ROOT/artifacts/ci/$stage/$name.json"
    echo "==> [$stage] $name"
    dotnet "$RUNNER_DLL" exec --name "$name" --timeout "$STEP_TIMEOUT" --out "$out" -- /bin/bash "$script"
    local code=$?
    if [ $code -eq 0 ]; then
        echo "    PASS  $name"
    else
        echo "    FAIL  $name (exit $code)"
    fi
    return $code
}

MODE="${1:-fast}"
build_runner

if [ "$MODE" = "run" ]; then
    [ $# -ge 2 ] || die "usage: ci.sh run <script>"
    target="$CI_DIR/$2"
    [ -f "$target" ] || target="$CI_DIR/$2.sh"
    [ -f "$target" ] || die "no such script: $2"
    st="$(script_stage "$target")"
    run_one "$target" "${st:-adhoc}"
    exit $?
fi

want="$(stage_rank "$MODE")"
[ "$want" -gt 0 ] || die "unknown stage '$MODE' (fast|full|integration|run)"

failed=0
passed=0
# 단계 순서(fast → full → integration), 단계 안에서는 이름순
for current in fast full integration; do
    [ "$(stage_rank "$current")" -le "$want" ] || break
    for script in "$CI_DIR"/*.sh; do
        [ "$script" = "$CI_DIR/ci.sh" ] && continue
        [ "$(script_stage "$script")" = "$current" ] || continue
        if run_one "$script" "$current"; then
            passed=$((passed + 1))
        else
            failed=$((failed + 1))
        fi
    done
done

echo "ci $MODE: $passed passed, $failed failed"
[ $failed -eq 0 ]
