#!/bin/bash
# Unity 배치 실행의 입구(ARCHITECTURE 4.1·4.2, PIPELINE 4.4·4.5, T-30, T-34, T-44). bash 3.2 문법만 쓴다.
# 이 폴더는 이름이 '~' 로 끝나서 Unity 와 metagen 이 자산으로 보지 않는다.
#
#   unity.sh compile                                    컴파일(-quit. 동기로 끝난다): Wizard.Unity.Editor.Ci.Compile
#   unity.sh test editmode|playmode [결과 XML 절대 경로]   테스트(-quit 없이 -runTests -testPlatform … -testResults …)
#   unity.sh false-pass-check [editmode|playmode]       일부러 실패하는 탐침을 켜고 테스트 실행의 종료 코드가 0이 아닌지 본다
#   unity.sh build mono|il2cpp arm64|x64                플레이어 빌드(-quit): Wizard.Unity.Editor.Ci.BuildPlayer → artifacts/unity/<backend>-<arch>/
#   unity.sh perf mono|il2cpp arm64|x64                 성능 측정용 실행: 그 플레이어를 -batchmode·-nographics 없이 창으로 띄운다
#   unity.sh apply-settings                             Wizard.Unity.Editor.Ci.ApplyProjectSettings. 이 모드만 ProjectSettings/, Assets/Scenes/, Assets/Settings/ 의 변경을 남긴다
#
# 편집기를 띄우는 모드의 순서: metagen(.meta 를 경로에서 계산해 먼저 만든다. T-34) → Unity → Unity 가 스스로 고쳐 쓴 파일 되돌리기.
#   되돌리기 1: unity/Packages/packages-lock.json, unity/ProjectSettings/(ProjectVersion.txt 포함)를 실행 전 내용으로 돌린다.
#               실행 전부터 있던 미커밋 변경은 실행 전 그대로 남는다.
#   되돌리기 2: src/, tests/, unity/ 에서 실행 전에는 없던 변경(새 파일, 고친 파일)을 지우거나 git 의 내용으로 돌린다.
#               예: Unity 가 metagen 범위 밖의 패키지(tests/Wizard.TestKit/Shared)에 만든 무작위 GUID 의 .meta.
# 테스트는 이름이 'Wizard.' 로 시작하는 것만 돈다(-testFilter). testables 의 com.unity.inputsystem 은 InputTestFixture 를 쓰려는 것이고
#   그 패키지 자체의 테스트는 돌리지 않는다. 실행된 테스트가 0개이거나, 실패·건너뜀이 있는데 종료 코드가 0이면 거짓 통과로 보고 실패한다.
# 환경 변수: UNITY_EDITOR(편집기 실행 파일. 없으면 ProjectVersion.txt 의 판으로 Hub 경로를 쓴다),
#            UNITY_TIMEOUT(초. 기본 3600), WIZARD_PERF_SECONDS(초. 기본 20 [임시]).
# 종료 코드: 0 통과, 1 Unity 또는 검사 실패, 2 사용법·환경 오류, 4 거짓 통과 의심.
set -u

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../../../.." && pwd)"
PROJ="$ROOT/unity"
ART="$ROOT/artifacts/unity"
TIMEOUT="${UNITY_TIMEOUT:-3600}"
PERF_SECONDS="${WIZARD_PERF_SECONDS:-20}"
TEST_FILTER='^Wizard\.'
PROBE_VARIABLE=WIZARD_CI_FALSE_PASS_PROBE

die() { echo "unity.sh: $*" >&2; exit 2; }

[ -f "$ROOT/Wizard.sln" ] || die "repo root not found above $HERE"
[ -f "$PROJ/ProjectSettings/ProjectVersion.txt" ] || die "no Unity project at $PROJ"

UNITY_VERSION="$(/usr/bin/sed -n 's/^m_EditorVersion: *//p' "$PROJ/ProjectSettings/ProjectVersion.txt" | /usr/bin/head -1)"
UNITY="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"

SNAP=""
KEEP_SETTINGS=0
EDITOR_RUN=0

# ---- metagen, 스냅숏, 되돌리기 ----

run_metagen() {
    command -v dotnet >/dev/null 2>&1 || die "dotnet is missing (.NET SDK in global.json)"
    echo "unity.sh: metagen"
    dotnet run --project "$ROOT/tools/Wizard.MetaGen" -c Release -- --root "$ROOT" || die "metagen failed"
}

repo_status() {
    command git -C "$ROOT" -c core.quotepath=off status --porcelain=v1 --untracked-files=all -- src tests unity
}

take_snapshot() {
    SNAP="$(/usr/bin/mktemp -d "${TMPDIR:-/tmp}/wizard-unity.XXXXXX")" || die "mktemp failed"
    repo_status > "$SNAP/before.txt" || die "git status failed"
    /bin/mkdir -p "$SNAP/ProjectSettings"
    /bin/cp -Rp "$PROJ/ProjectSettings/." "$SNAP/ProjectSettings/"
    if [ -f "$PROJ/Packages/packages-lock.json" ]; then
        /bin/cp -p "$PROJ/Packages/packages-lock.json" "$SNAP/packages-lock.json"
    fi
}

restore_snapshot() {
    [ -n "$SNAP" ] && [ -d "$SNAP" ] || return 0
    if [ -f "$SNAP/packages-lock.json" ]; then
        /bin/cp -p "$SNAP/packages-lock.json" "$PROJ/Packages/packages-lock.json"
    else
        /bin/rm -f "${PROJ:?}/Packages/packages-lock.json"
    fi
    /bin/cp -p "$SNAP/ProjectSettings/ProjectVersion.txt" "$PROJ/ProjectSettings/ProjectVersion.txt"
    if [ "$KEEP_SETTINGS" -eq 0 ]; then
        local f name
        for f in "$PROJ/ProjectSettings"/* "$PROJ/ProjectSettings"/.[!.]*; do
            [ -e "$f" ] || continue
            name="$(basename "$f")"
            if [ ! -e "$SNAP/ProjectSettings/$name" ]; then
                /bin/rm -rf "${f:?}"
                echo "unity.sh: reverted Unity's change: new unity/ProjectSettings/$name"
            elif ! /usr/bin/cmp -s "$f" "$SNAP/ProjectSettings/$name"; then
                echo "unity.sh: reverted Unity's change: rewrote unity/ProjectSettings/$name"
            fi
        done
        /bin/cp -Rp "$SNAP/ProjectSettings/." "$PROJ/ProjectSettings/"
    fi

    repo_status > "$SNAP/after.txt" || { echo "unity.sh: git status failed; nothing else reverted" >&2; return 0; }
    local line code path
    while IFS= read -r line; do
        [ -n "$line" ] || continue
        if /usr/bin/grep -qxF -- "$line" "$SNAP/before.txt"; then
            continue
        fi
        code="${line:0:2}"
        path="${line:3}"
        case "$path" in
            \"*\") path="${path#\"}"; path="${path%\"}" ;;
        esac
        if [ "$KEEP_SETTINGS" -eq 1 ]; then
            case "$path" in
                unity/ProjectSettings/*|unity/Assets/Scenes/*|unity/Assets/Settings/*) continue ;;
            esac
        fi
        if [ "$code" = "??" ]; then
            /bin/rm -f "${ROOT:?}/${path:?}"
        else
            command git -C "$ROOT" checkout -q -- "$path" 2>/dev/null || echo "unity.sh: could not revert $path" >&2
        fi
        echo "unity.sh: reverted Unity's change: $code $path"
    done < "$SNAP/after.txt"
    /bin/rm -rf "${SNAP:?}"
    SNAP=""
}

on_exit() {
    local rc=$?
    if [ "$EDITOR_RUN" -eq 1 ]; then
        restore_snapshot
    fi
    exit $rc
}
trap on_exit EXIT
trap 'exit 130' INT TERM

begin_editor_run() {
    [ -x "$UNITY" ] || die "Unity $UNITY_VERSION not found at $UNITY (set UNITY_EDITOR)"
    run_metagen
    take_snapshot
    EDITOR_RUN=1
    /bin/mkdir -p "$ART/logs"
}

# 편집기를 띄우고 시간 제한을 건다. 멈추면 끊고 실패로 돌려준다.
run_unity() {
    local log="$1"
    shift
    echo "unity.sh: $UNITY $* -logFile $log"
    "$UNITY" "$@" -logFile "$log" &
    local pid=$!
    (
        trap 'kill $nap 2>/dev/null; exit 0' TERM
        /bin/sleep "$TIMEOUT" &
        nap=$!
        wait $nap
        echo "unity.sh: timeout after ${TIMEOUT}s; stopping Unity" >&2
        kill -TERM $pid 2>/dev/null
        /bin/sleep 15
        kill -KILL $pid 2>/dev/null
    ) &
    local watchdog=$!
    wait $pid
    local code=$?
    kill -TERM $watchdog 2>/dev/null
    wait $watchdog 2>/dev/null
    if [ $code -ne 0 ]; then
        echo "unity.sh: Unity exited with $code; last lines of $log:" >&2
        /usr/bin/tail -n 40 "$log" >&2
    fi
    return $code
}

# ---- 모드 ----

xml_attr() {
    # <test-run …> 의 속성 하나
    /usr/bin/sed -n "s/.*<test-run [^>]* $2=\"\\([^\"]*\\)\".*/\\1/p" "$1" | /usr/bin/head -1
}

test_platform_name() {
    case "$1" in
        editmode|EditMode) echo EditMode ;;
        playmode|PlayMode) echo PlayMode ;;
        *) die "test platform must be editmode or playmode (got '$1')" ;;
    esac
}

# 결과: 0 통과, 1 실패, 4 거짓 통과 의심. 마지막 실행의 수치를 전역에 남긴다.
LAST_TOTAL=""
LAST_FAILED=""
run_tests() {
    local platform="$1" results="$2" lower
    lower="$(echo "$platform" | /usr/bin/tr 'A-Z' 'a-z')"
    /bin/mkdir -p "$(dirname "$results")"
    /bin/rm -f "${results:?}"
    run_unity "$ART/logs/test-$lower.log" -batchmode -nographics -projectPath "$PROJ" -buildTarget OSXUniversal \
        -runTests -testPlatform "$platform" -testFilter "$TEST_FILTER" -testResults "$results"
    local code=$?
    if [ ! -f "$results" ]; then
        echo "unity.sh: no test results at $results (Unity exit $code)" >&2
        [ $code -ne 0 ] && return 1
        return 4
    fi
    LAST_TOTAL="$(xml_attr "$results" total)"
    LAST_FAILED="$(xml_attr "$results" failed)"
    local passed skipped inconclusive result
    passed="$(xml_attr "$results" passed)"
    skipped="$(xml_attr "$results" skipped)"
    inconclusive="$(xml_attr "$results" inconclusive)"
    result="$(xml_attr "$results" result)"
    echo "unity.sh: $platform result=$result total=$LAST_TOTAL passed=$passed failed=$LAST_FAILED skipped=$skipped inconclusive=$inconclusive exit=$code"
    echo "unity.sh: results $results"
    if [ $code -ne 0 ]; then
        return 1
    fi
    if [ -z "$LAST_TOTAL" ] || [ "$LAST_TOTAL" = "0" ]; then
        echo "unity.sh: exit 0 but no test ran: treating as a false pass" >&2
        return 4
    fi
    if [ "${LAST_FAILED:-0}" != "0" ] || [ "${skipped:-0}" != "0" ] || [ "${inconclusive:-0}" != "0" ] || [ "$result" != "Passed" ]; then
        echo "unity.sh: exit 0 but results are not a clean pass: treating as a false pass" >&2
        return 4
    fi
    return 0
}

mode_compile() {
    begin_editor_run
    run_unity "$ART/logs/compile.log" -batchmode -nographics -quit -projectPath "$PROJ" -buildTarget OSXUniversal \
        -executeMethod Wizard.Unity.Editor.Ci.Compile || return 1
    /usr/bin/grep -q "ci: compile ok" "$ART/logs/compile.log" || { echo "unity.sh: Ci.Compile did not report success" >&2; return 1; }
    echo "unity.sh: compile ok"
}

mode_test() {
    local platform
    platform="$(test_platform_name "${1:-}")" || exit 2
    local lower
    lower="$(echo "$platform" | /usr/bin/tr 'A-Z' 'a-z')"
    local results="${2:-$ART/tests/$lower.xml}"
    case "$results" in /*) ;; *) die "results path must be absolute: $results" ;; esac
    begin_editor_run
    run_tests "$platform" "$results"
}

mode_false_pass_check() {
    local platform
    platform="$(test_platform_name "${1:-editmode}")" || exit 2
    local lower
    lower="$(echo "$platform" | /usr/bin/tr 'A-Z' 'a-z')"
    begin_editor_run
    export WIZARD_CI_FALSE_PASS_PROBE=1
    run_tests "$platform" "$ART/tests/false-pass-$lower.xml"
    local code=$?
    unset WIZARD_CI_FALSE_PASS_PROBE
    if [ $code -eq 1 ] && [ "${LAST_FAILED:-0}" != "0" ] && /usr/bin/grep -q 'FalsePassProbe_FailsOnlyWhenRequested' "$ART/tests/false-pass-$lower.xml"; then
        echo "unity.sh: false-pass check ok: the intentional failure made the $platform run exit non-zero"
        return 0
    fi
    echo "unity.sh: false-pass check FAILED: $PROBE_VARIABLE=1 should fail the $platform run (got code $code, failed=${LAST_FAILED:-?})" >&2
    return 1
}

check_build_args() {
    case "${1:-}" in mono|il2cpp) ;; *) die "backend must be mono or il2cpp (got '${1:-}')" ;; esac
    case "${2:-}" in arm64|x64) ;; *) die "arch must be arm64 or x64 (got '${2:-}')" ;; esac
}

mode_build() {
    check_build_args "${1:-}" "${2:-}"
    begin_editor_run
    run_unity "$ART/logs/build-$1-$2.log" -batchmode -nographics -quit -projectPath "$PROJ" -buildTarget OSXUniversal \
        -executeMethod Wizard.Unity.Editor.Ci.BuildPlayer -wizardBackend "$1" -wizardArch "$2" || return 1
    [ -d "$ART/$1-$2/Wizard.app" ] || { echo "unity.sh: build reported success but $ART/$1-$2/Wizard.app is missing" >&2; return 1; }
    echo "unity.sh: player at $ART/$1-$2/Wizard.app"
}

# 성능 측정용 실행. 프레임 시간은 그래픽 장치가 있어야 재므로 -batchmode·-nographics 를 쓰지 않는다(ARCHITECTURE 4.1).
# 플레이어가 스스로 끝나면 그 종료 코드가 결과다. WIZARD_PERF_SECONDS 안에 끝나지 않으면 끄고, 창과 그래픽 장치로 돌았는지만 본다.
mode_perf() {
    check_build_args "${1:-}" "${2:-}"
    local app="$ART/$1-$2/Wizard.app"
    if [ ! -d "$app" ]; then
        mode_build "$1" "$2" || return 1
    fi
    local exe_name
    exe_name="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app/Contents/Info.plist")" || die "cannot read $app/Contents/Info.plist"
    local exe="$app/Contents/MacOS/$exe_name"
    local arch_flag="-arm64"
    [ "$2" = "x64" ] && arch_flag="-x86_64"
    local log="$ART/perf/$1-$2/player.log"
    /bin/mkdir -p "$(dirname "$log")"
    /bin/rm -f "${log:?}"
    echo "unity.sh: perf run /usr/bin/arch $arch_flag $exe (no -batchmode, no -nographics) for up to ${PERF_SECONDS}s"
    /usr/bin/arch "$arch_flag" "$exe" -logFile "$log" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -wizard-perf &
    local pid=$!
    local waited=0
    while [ $waited -lt "$PERF_SECONDS" ]; do
        kill -0 $pid 2>/dev/null || break
        /bin/sleep 1
        waited=$((waited + 1))
    done
    if ! kill -0 $pid 2>/dev/null; then
        wait $pid
        local code=$?
        echo "unity.sh: player exited by itself with $code; log $log"
        return $code
    fi
    kill -TERM $pid 2>/dev/null
    /bin/sleep 3
    kill -KILL $pid 2>/dev/null
    wait $pid 2>/dev/null
    [ -f "$log" ] || { echo "unity.sh: player wrote no log" >&2; return 1; }
    if /usr/bin/grep -qi -e 'NullGfxDevice' -e 'nographics' "$log"; then
        echo "unity.sh: player ran without a graphics device; frame time is meaningless" >&2
        return 1
    fi
    if ! /usr/bin/grep -q 'GfxDevice' "$log"; then
        echo "unity.sh: no graphics device line in $log" >&2
        return 1
    fi
    /usr/bin/grep -m1 'GfxDevice' "$log"
    echo "unity.sh: perf run stayed up ${PERF_SECONDS}s with a graphics device; no measurement code yet in the player. log $log"
}

mode_apply_settings() {
    begin_editor_run
    KEEP_SETTINGS=1
    run_unity "$ART/logs/apply-settings.log" -batchmode -nographics -quit -projectPath "$PROJ" -buildTarget OSXUniversal \
        -executeMethod Wizard.Unity.Editor.Ci.ApplyProjectSettings || return 1
    echo "unity.sh: settings applied; review git diff of unity/ProjectSettings, unity/Assets/Scenes, unity/Assets/Settings before committing"
}

MODE="${1:-}"
[ $# -gt 0 ] && shift
case "$MODE" in
    compile) mode_compile ;;
    test) mode_test "$@" ;;
    false-pass-check) mode_false_pass_check "$@" ;;
    build) mode_build "$@" ;;
    perf) mode_perf "$@" ;;
    apply-settings) mode_apply_settings ;;
    *) die "usage: unity.sh compile | test editmode|playmode [results.xml] | false-pass-check [editmode|playmode] | build mono|il2cpp arm64|x64 | perf mono|il2cpp arm64|x64 | apply-settings" ;;
esac
