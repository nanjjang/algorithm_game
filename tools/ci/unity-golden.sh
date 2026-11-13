#!/bin/bash
# ci-stage: integration
# ci-timeout: 14400
# 런타임 × 아키텍처 골든(T-30, T-39, ARCHITECTURE 11절, PIPELINE 7.2 2번·7.3). bash 3.2 문법만 쓴다(T-40).
#
#   tools/ci/unity-golden.sh [run] [--no-build] [--extra <결과 파일>]…   모든 열을 돌리고 한 표로 비교한다
#   tools/ci/unity-golden.sh compare [--extra <결과 파일>]…               이미 있는 결과 파일만 비교한다(돌리지 않는다)
#
# 열(조합 하나 = 결과 파일 하나 = artifacts/golden/golden-result.<조합>.json. 형식은 tests/golden/README.md):
#   macos-dotnet-test-arm64      dotnet 의 골든 재실행(ReplayCli GoldenRunner)과 산술 표(TestKit.Shared CoreArithOps)를 CoreCLR arm64 로
#   macos-dotnet-test-x64        같은 것을 x64 dotnet 런타임으로(Rosetta 2). DOTNET_ROOT_X64(기본 /usr/local/share/dotnet/x64)
#   macos-mono-editor-<arch>     Unity EditMode(unity.sh test editmode. 시험 GoldenEditModeTests 가 파일을 쓴다)
#   macos-mono-player-arm64/x64  Mono 플레이어를 -batchmode -nographics -golden 으로(x64 는 /usr/bin/arch -x86_64, Rosetta 2)
#   macos-il2cpp-player-arm64/x64  IL2CPP 플레이어를 같은 방식으로(mac-il2cpp 모듈 필요)
#   그 밖의 기기(Windows)에서 돌린 결과 파일: --extra 로 주거나 artifacts/golden/external/ 에 넣는다. 같은 표의 열이 된다(T-39)
# 통과: 위 일곱 열이 전부 있고, 모든 열의 모든 골든 항목이 기대 해시(expected.json)와 같고 모든 산술 벡터가 기대를 만족한다.
#   각 열이 기대와 같으면 열끼리도 같다(GoldenComparison).
# 필요한 것(Unity 편집기, mac-il2cpp 모듈, x64 dotnet 런타임)이 없으면 건너뛰지 않는다: 돌 수 있는 열은 다 돌리고,
#   무엇이 없는지와 설치 명령(tools/Wizard.Ci 의 require)을 적고 1 로 끝난다.
# 팩: databuild build 로 저장소 data/ 의 기본 팩을 artifacts/golden/pack/base.wzpack 에 만든다. dotnet 열은 같은 팩을 메모리에서 만든다.
# 결과와 로그: artifacts/golden/(golden-result.*.json, table.txt, logs/). 커밋하지 않는다.
# 종료 코드: 0 통과, 1 실패(불일치, 빠진 열, 없는 것), 2 사용법·준비 오류.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/lib/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

OUT="$ROOT/artifacts/golden"
LOGS="$OUT/logs"
PACK_DIR="$OUT/pack"
PACK="$PACK_DIR/base.wzpack"
HOST_DIR="$OUT/dotnet-host"
HOST_BIN="$HOST_DIR/bin"
HOST_DLL="$HOST_BIN/Wizard.GoldenDotnetHost.dll"
PLAYER_TIMEOUT="${WIZARD_GOLDEN_PLAYER_TIMEOUT:-1800}"

usage() {
    echo "usage: $0 [run] [--no-build] [--extra <golden-result.json>]... | compare [--extra <golden-result.json>]..." >&2
    exit 2
}

MODE=run
NO_BUILD=0
EXTRAS=()
first=1
while [ $# -gt 0 ]; do
    case "$1" in
        run|compare)
            [ $first -eq 1 ] || usage
            MODE="$1"
            ;;
        --no-build) NO_BUILD=1 ;;
        --extra)
            shift
            [ $# -gt 0 ] || usage
            case "$1" in /*) f="$1" ;; *) f="$PWD/$1" ;; esac
            [ -f "$f" ] || { echo "unity-golden: --extra $1: no such file" >&2; exit 2; }
            EXTRAS+=("$f")
            ;;
        *) usage ;;
    esac
    first=0
    shift
done
[ "$MODE" = "compare" ] && [ $NO_BUILD -eq 1 ] && usage

/bin/mkdir -p "$OUT" "$LOGS" || exit 2

MISSING=""
FAILED=""
add_missing() { MISSING="$MISSING $1"; }
step_fail() {
    FAILED="$FAILED
  $1"
    echo "FAIL  $1" >&2
}

# 이 기기의 편집기 열 이름(편집기 프로세스의 아키텍처. Apple 실리콘에서는 arm64)
EDITOR_ARCH="$(/usr/bin/uname -m)"
[ "$EDITOR_ARCH" = "x86_64" ] && EDITOR_ARCH=x64
REQUIRED="macos-dotnet-test-arm64 macos-dotnet-test-x64 macos-mono-editor-$EDITOR_ARCH macos-mono-player-arm64 macos-mono-player-x64 macos-il2cpp-player-arm64 macos-il2cpp-player-x64"

# ---- 준비물 ----
HAVE_UNITY=0
HAVE_IL2CPP=0
HAVE_X64=0
if [ "$MODE" = "run" ]; then
    echo "==> requirements"
    if ci_tool require unity --root "$ROOT"; then HAVE_UNITY=1; else add_missing unity; fi
    if [ $HAVE_UNITY -eq 1 ]; then
        if ci_tool require il2cpp --root "$ROOT"; then HAVE_IL2CPP=1; else add_missing il2cpp; fi
    else
        add_missing il2cpp
    fi
    if ci_tool require x64-dotnet --root "$ROOT"; then HAVE_X64=1; else add_missing x64-dotnet; fi
fi

# ---- dotnet 쪽 실행기(dotnet 열과 표 비교). 프로젝트 파일은 여기서 만든다(unity/ 아래 csproj 는 추적하지 않는다) ----
build_host() {
    local tfm
    tfm="$(/usr/bin/sed -n 's:.*<TargetFramework>\(.*\)</TargetFramework>.*:\1:p' "$ROOT/tools/Wizard.ReplayCli/Wizard.ReplayCli.csproj" | /usr/bin/head -1)"
    [ -n "$tfm" ] || { echo "unity-golden: cannot read the target framework of tools/Wizard.ReplayCli" >&2; return 1; }
    /bin/mkdir -p "$HOST_DIR" || return 1
    /bin/cat > "$HOST_DIR/Wizard.GoldenDotnetHost.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <!-- tools/ci/unity-golden.sh 가 만든다. 원본: unity/Assets/Game/App/Headless/DotnetHost~/ -->
  <PropertyGroup>
    <TargetFramework>$tfm</TargetFramework>
    <OutputType>Exe</OutputType>
    <AssemblyName>Wizard.GoldenDotnetHost</AssemblyName>
    <RootNamespace>Wizard.Unity.App.Headless.DotnetHost</RootNamespace>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="\$(WizardRoot)unity/Assets/Game/App/Headless/DotnetHost~/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="\$(WizardRoot)tools/Wizard.ReplayCli/Wizard.ReplayCli.csproj" />
    <ProjectReference Include="\$(WizardRoot)tests/Wizard.TestKit/Shared/Wizard.TestKit.Shared.csproj" />
  </ItemGroup>
</Project>
EOF
    echo "==> dotnet build golden host"
    dotnet build "$HOST_DIR/Wizard.GoldenDotnetHost.csproj" -c Release -nologo -v quiet -o "$HOST_BIN" > "$LOGS/dotnet-host-build.log" 2>&1 || {
        /usr/bin/tail -n 40 "$LOGS/dotnet-host-build.log" >&2
        return 1
    }
    [ -f "$HOST_DLL" ] || { echo "unity-golden: $HOST_DLL was not built" >&2; return 1; }
}

build_host || { echo "unity-golden: cannot build the dotnet golden host (log $LOGS/dotnet-host-build.log)" >&2; exit 2; }

# 앞선 실행이 남긴 이 기기의 결과는 지운다(이번 실행이 내지 않은 열이 옛 파일로 통과하지 않게). 다른 기기의 결과는 external/ 에 둔다
if [ "$MODE" = "run" ]; then
    /bin/rm -f "$OUT"/golden-result.macos-*.json
fi

# 시간 제한을 걸고 돌린다. 멈추면 끊고 실패로 돌려준다
run_limited() {
    local secs="$1"
    shift
    "$@" &
    local pid=$!
    (
        trap 'kill $nap 2>/dev/null; exit 0' TERM
        /bin/sleep "$secs" &
        nap=$!
        wait $nap
        echo "unity-golden: ${secs}s limit reached; stopping $1" >&2
        kill -TERM $pid 2>/dev/null
        /bin/sleep 10
        kill -KILL $pid 2>/dev/null
    ) &
    local watchdog=$!
    wait $pid
    local code=$?
    kill -TERM $watchdog 2>/dev/null
    wait $watchdog 2>/dev/null
    return $code
}

if [ "$MODE" = "run" ]; then
    # ---- 팩 ----
    echo "==> databuild build --out $PACK_DIR"
    dotnet run --project "$ROOT/tools/Wizard.DataBuild" -c Release -- build --out "$PACK_DIR" --root "$ROOT" > "$LOGS/databuild.log" 2>&1 || {
        /usr/bin/tail -n 40 "$LOGS/databuild.log" >&2
        echo "unity-golden: databuild build failed (log $LOGS/databuild.log)" >&2
        exit 2
    }
    [ -f "$PACK" ] || { echo "unity-golden: databuild did not write $PACK" >&2; exit 2; }

    # ---- dotnet arm64, x64 ----
    echo "==> dotnet (arm64)"
    /usr/bin/arch -arm64 "$(command -v dotnet)" "$HOST_DLL" run --root "$ROOT" --out "$OUT" > "$LOGS/dotnet-arm64.log" 2>&1
    code=$?
    /usr/bin/grep -E '^(golden-host|FAIL)' "$LOGS/dotnet-arm64.log"
    [ $code -eq 0 ] || step_fail "dotnet arm64 column exited $code (log $LOGS/dotnet-arm64.log)"

    if [ $HAVE_X64 -eq 1 ]; then
        X64_DOTNET="${DOTNET_ROOT_X64:-/usr/local/share/dotnet/x64}/dotnet"
        echo "==> dotnet (x64, Rosetta 2): $X64_DOTNET"
        DOTNET_ROOT="${DOTNET_ROOT_X64:-/usr/local/share/dotnet/x64}" /usr/bin/arch -x86_64 "$X64_DOTNET" "$HOST_DLL" run --root "$ROOT" --out "$OUT" \
            > "$LOGS/dotnet-x64.log" 2>&1
        code=$?
        /usr/bin/grep -E '^(golden-host|FAIL)' "$LOGS/dotnet-x64.log"
        [ $code -eq 0 ] || step_fail "dotnet x64 column exited $code (log $LOGS/dotnet-x64.log)"
    fi

    if [ $HAVE_UNITY -eq 1 ]; then
        ci_unity_prepare                       # Library 복제, UNITY_SH
        # ---- Unity EditMode(Mono 편집기). 시험이 결과 파일을 쓴다 ----
        echo "==> Unity EditMode (macos-mono-editor-$EDITOR_ARCH)"
        WIZARD_GOLDEN_PACK="$PACK" /bin/bash "$UNITY_SH" test editmode "$OUT/editmode.xml"
        code=$?
        [ $code -eq 0 ] || step_fail "Unity EditMode run exited $code (results $OUT/editmode.xml, log $ROOT/artifacts/unity/logs/test-editmode.log)"
        [ -f "$OUT/golden-result.macos-mono-editor-$EDITOR_ARCH.json" ] || step_fail "EditMode wrote no golden-result.macos-mono-editor-$EDITOR_ARCH.json"

        # ---- 플레이어: Mono(arm64, x64), IL2CPP(arm64, x64) ----
        # Ci.BuildPlayer 는 앱에 넣을 팩을 <저장소>/artifacts/content/base.wzpack(databuild build 의 기본 출력)에서 읽고,
        # 그 팩은 빌드를 띄우는 쪽이 먼저 만들어 둔다(FIX-3). 위에서 만든 같은 팩을 그 자리에 놓는다.
        # 앞선 실행이 남긴 파일에 기대면 artifacts/ 를 지운 뒤 빌드가 "content pack not found" 로 실패한다.
        CONTENT_DIR="$ROOT/artifacts/content"
        /bin/mkdir -p "$CONTENT_DIR" && /bin/cp -f "$PACK" "$CONTENT_DIR/base.wzpack" \
            || { echo "unity-golden: cannot place $PACK at $CONTENT_DIR/base.wzpack" >&2; exit 2; }
        backends="mono"
        [ $HAVE_IL2CPP -eq 1 ] && backends="mono il2cpp"
        for backend in $backends; do
            for arch in arm64 x64; do
                combo="macos-$backend-player-$arch"
                app="$ROOT/artifacts/unity/$backend-$arch/Wizard.app"
                if [ $NO_BUILD -eq 0 ] || [ ! -d "$app" ]; then
                    echo "==> build $backend $arch"
                    /bin/bash "$UNITY_SH" build "$backend" "$arch" || { step_fail "$combo: player build failed"; continue; }
                fi
                exe_name="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app/Contents/Info.plist" 2>/dev/null)"
                exe="$app/Contents/MacOS/$exe_name"
                [ -n "$exe_name" ] && [ -x "$exe" ] || { step_fail "$combo: no executable in $app"; continue; }
                want_arch="$arch"
                arch_flag="-arm64"
                if [ "$arch" = "x64" ]; then
                    want_arch="x86_64"
                    arch_flag="-x86_64"
                fi
                archs="$(/usr/bin/lipo -archs "$exe" 2>/dev/null)"
                case " $archs " in
                    *" $want_arch "*) ;;
                    *) step_fail "$combo: $exe has architectures '$archs', not $want_arch"; continue ;;
                esac
                log="$LOGS/player-$backend-$arch.log"
                /bin/rm -f "$log"
                echo "==> $combo: /usr/bin/arch $arch_flag $exe -batchmode -nographics -golden …"
                run_limited "$PLAYER_TIMEOUT" /usr/bin/arch "$arch_flag" "$exe" -batchmode -nographics \
                    -golden "$ROOT/tests/golden" -goldenPack "$PACK" -goldenOut "$OUT" -goldenCombo "$combo" -logFile "$log"
                code=$?
                [ -f "$log" ] && /usr/bin/grep -E '^(golden:|FAIL  )' "$log"
                [ $code -eq 0 ] || step_fail "$combo: player exited $code (log $log)"
                [ -f "$OUT/golden-result.$combo.json" ] || step_fail "$combo: the player wrote no result file"
            done
        done
    fi
fi

# ---- 한 표로 비교 ----
FILES=()
for f in "$OUT"/golden-result.*.json "$OUT"/external/golden-result.*.json; do
    [ -f "$f" ] && FILES+=("$f")
done
for f in ${EXTRAS[@]+"${EXTRAS[@]}"}; do
    FILES+=("$f")
done
REQ_ARGS=()
for c in $REQUIRED; do
    REQ_ARGS+=(--require "$c")
done
echo "==> compare"
dotnet "$HOST_DLL" compare --root "$ROOT" "${REQ_ARGS[@]}" ${FILES[@]+"${FILES[@]}"} | /usr/bin/tee "$OUT/table.txt"
code=${PIPESTATUS[0]}
[ $code -eq 0 ] || step_fail "the comparison did not pass (table $OUT/table.txt)"

if [ -n "$MISSING" ]; then
    echo "" >&2
    echo "unity-golden: missing on this machine (nothing was skipped silently; these columns are absent and the gate fails):" >&2
    for what in $MISSING; do
        ci_tool require "$what" --root "$ROOT" >/dev/null
    done
fi
if [ -n "$FAILED" ] || [ -n "$MISSING" ]; then
    echo "unity-golden: FAILED$FAILED" >&2
    [ -n "$MISSING" ] && echo "  missing:$MISSING" >&2
    exit 1
fi
echo "unity-golden: ok (${#FILES[@]} result files, every required column present)"
exit 0
