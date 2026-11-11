#!/bin/bash
# ci-stage: fast
# 결정론 가드(ARCHITECTURE 11, T-42): 금지 API 를 끼워 넣은 탐침 빌드가 기대한 WZ 진단으로 **실패하는지** 본다.
# 분석기가 조용히 꺼져 있으면(연결 줄이 빠지거나, 심각도를 낮췄거나) 여기서 잡힌다.
# 진단을 끄는 설정(.editorconfig 의 dotnet_diagnostic.WZ*, NoWarn·WarningsNotAsErrors 의 WZ)도 거절한다.
set -u
ROOT="${WIZARD_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
PROBES="$ROOT/tools/ci/probes"
OUT="$ROOT/artifacts/ci/probe"
/bin/mkdir -p "$OUT"
fail=0

probe() {   # 프로젝트 이름, 탐침 파일, 기대 진단…
    local project="$1" file="$2"
    shift 2
    local csproj="$ROOT/src/$project/$project.csproj"
    local log="$OUT/$project-$(basename "$file" .cs).log"
    dotnet build "$csproj" -c Debug -nologo -p:WizardAnalyzerProbe="$PROBES/$file" -p:WizardAnalyzerProbeProject="$project" \
        -p:ArtifactsPath="$OUT/artifacts" >"$log" 2>&1
    local code=$?
    if [ $code -eq 0 ]; then
        echo "FAIL  $project + $file built successfully; the analyzer did not stop it" >&2
        fail=1
        return
    fi
    local id missing=0
    for id in "$@"; do
        if ! /usr/bin/grep -q "error $id:" "$log"; then
            echo "FAIL  $project + $file: expected error $id (see $log)" >&2
            missing=1
        fi
    done
    if [ $missing -eq 0 ]; then
        echo "ok    $project rejects $file ($*)"
    else
        fail=1
    fi
}

CORE_IDS="WZ0001 WZ0002 WZ0003 WZ0004 WZ0005 WZ0006 WZ0007 WZ0008 WZ0009 WZ0010 WZ0013 WZ0014 WZ0015"
probe Wizard.Core CoreProbe.cs $CORE_IDS
probe Wizard.Content CoreProbe.cs $CORE_IDS
probe Wizard.Sim CoreProbe.cs $CORE_IDS
probe Wizard.Sim SimProbe.cs WZ0017 WZ0020
probe Wizard.Rules RulesProbe.cs WZ0001 WZ0020
probe Wizard.Netcode NetcodeProbe.cs WZ0003 WZ0019
probe Wizard.Protocol SharedProbe.cs WZ0021
probe Wizard.Client SharedProbe.cs WZ0021

# 진단 끄기 금지(T-42)
hits="$(/usr/bin/grep -rIn --include='.editorconfig' --include='*.globalconfig' -E 'dotnet_diagnostic\.WZ' \
    "$ROOT/src" "$ROOT/server" "$ROOT/tests" "$ROOT/tools" "$ROOT/.editorconfig" 2>/dev/null || true)"
hits2="$(/usr/bin/grep -rIn --include='*.csproj' --include='*.props' --include='*.targets' -E '<(NoWarn|WarningsNotAsErrors)>[^<]*WZ' \
    "$ROOT/src" "$ROOT/server" "$ROOT/tests" "$ROOT/tools" "$ROOT/Directory.Build.props" "$ROOT/Directory.Build.targets" 2>/dev/null || true)"
hits3="$(/usr/bin/grep -rIn --include='*.rsp' -E 'nowarn|warnaserror-' "$ROOT/src" "$ROOT/tests/Wizard.TestKit/Shared" 2>/dev/null || true)"
if [ -n "$hits$hits2$hits3" ]; then
    echo "FAIL  WZ diagnostics must not be suppressed:" >&2
    printf '%s\n%s\n%s\n' "$hits" "$hits2" "$hits3" >&2
    fail=1
fi
# 분석기 연결 줄이 최상위 빌드 설정에 있어야 한다(T-43)
if ! /usr/bin/grep -q 'OutputItemType="Analyzer"' "$ROOT/Directory.Build.props"; then
    echo "FAIL  Directory.Build.props lost the analyzer reference" >&2
    fail=1
fi
[ $fail -eq 0 ] && echo "determinism-guard: ok"
exit $fail
