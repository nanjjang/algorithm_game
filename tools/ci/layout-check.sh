#!/bin/bash
# ci-stage: fast
# 저장소 뼈대 검사(ARCHITECTURE 2·3.4·4.2, T-43): 프로젝트 수, 공유 패키지의 짝(csproj·asmdef·package.json·csc.rsp),
# asmdef 의 noEngineReferences, Unity 쪽 빌드 설정 끊기, Unity 가 만드는 IDE 파일이 추적되지 않음.
set -u
ROOT="${WIZARD_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
fail=0
err() { echo "FAIL  $*" >&2; fail=1; }

count() { /usr/bin/find "$ROOT/$1" -name '*.csproj' -not -path '*/artifacts/*' | /usr/bin/wc -l | /usr/bin/tr -d ' '; }
[ "$(count src)" = "9" ] || err "src/ must have 9 shared projects (found $(count src))"
[ "$(count server)" = "8" ] || err "server/ must have 8 projects (found $(count server))"
[ "$(count tests)" = "16" ] || err "tests/ must have 16 projects (found $(count tests))"
[ "$(count tools)" = "13" ] || err "tools/ must have 13 projects (found $(count tools))"
[ -f "$ROOT/Wizard.sln" ] || err "Wizard.sln missing (.sln, not .slnx)"
[ ! -e "$ROOT/Wizard.slnx" ] || err "Wizard.slnx must not exist"

for dir in "$ROOT"/src/Wizard.* "$ROOT/tests/Wizard.TestKit/Shared"; do
    name="$(basename "$dir")"
    [ "$name" = "Shared" ] && name="Wizard.TestKit.Shared"
    for f in "$name.asmdef" package.json csc.rsp; do
        [ -f "$dir/$f" ] || err "$dir/$f missing"
    done
    /usr/bin/grep -q '"noEngineReferences": true' "$dir/$name.asmdef" 2>/dev/null || err "$name.asmdef must set noEngineReferences: true"
    /usr/bin/grep -q '<TargetFramework>netstandard2.1</TargetFramework>' "$dir/$name.csproj" 2>/dev/null || err "$name must target netstandard2.1"
    /usr/bin/grep -q '<LangVersion>9.0</LangVersion>' "$dir/$name.csproj" 2>/dev/null || err "$name must use LangVersion 9.0"
done

if [ -f "$ROOT/unity/Directory.Build.props" ]; then
    content="$(/usr/bin/tr -d ' \n\r\t' < "$ROOT/unity/Directory.Build.props")"
    [ "$content" = "<Project/>" ] || err "unity/Directory.Build.props must be an empty <Project/>"
else
    err "unity/Directory.Build.props missing"
fi
/usr/bin/grep -q 'OutputItemType="Analyzer"' "$ROOT/Directory.Build.props" || err "analyzer reference missing from Directory.Build.props"
/usr/bin/grep -q '"rollForward": "latestPatch"' "$ROOT/global.json" || err "global.json must use rollForward latestPatch"
if /usr/bin/grep -q 'Payments.Fakes' "$ROOT/server/Wizard.Server.Api/Wizard.Server.Api.csproj"; then
    err "Wizard.Server.Api must not reference Payments.Fakes"
fi

if command -v git >/dev/null 2>&1 && command git -C "$ROOT" rev-parse --git-dir >/dev/null 2>&1; then
    tracked="$(command git -C "$ROOT" ls-files -- 'unity/*.csproj' 'unity/*.sln' 'unity/*.slnx' 'unity/Library' 'artifacts' 2>/dev/null)"
    [ -z "$tracked" ] || err "generated files are tracked: $tracked"
fi
[ $fail -eq 0 ] && echo "layout-check: ok"
exit $fail
