#!/bin/bash
# ci-stage: fast
# 다시 만드는 파일(T-41)이 도구의 출력과 바이트가 같은지 본다: SinTable·PowTables(TableGen), SimVersion(VersionStamp).
# 그 전에 두 도구의 자체 테스트(--self-test)를 돌리고, 표 생성기가 실수형·실수 라이브러리 함수를 쓰지 않는지 본다(M0-BT-03).
# bash 3.2 문법만 쓴다. 저장소 뿌리는 현재 폴더가 아니라 이 파일의 위치에서 구한다.
set -eu
ROOT="${WIZARD_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
ART="$ROOT/artifacts/ci-runner"

command -v dotnet >/dev/null 2>&1 || { echo "FAIL  dotnet 이 없다. .NET SDK(global.json 의 판)를 설치해야 한다." >&2; exit 1; }

# 표 생성기는 정수만 쓴다(주석을 걷어 낸 뒤 실수형 키워드와 Math./MathF. 호출을 찾는다)
float_hits=""
for f in "$ROOT/tools/Wizard.TableGen/TableMath.cs" "$ROOT/tools/Wizard.TableGen/Program.cs"; do
    [ -f "$f" ] || { echo "FAIL  $f missing" >&2; exit 1; }
    hit="$(/usr/bin/sed 's://.*$::' "$f" | /usr/bin/grep -nE '(^|[^A-Za-z0-9_])(float|double|decimal|Single|Double|Decimal|Half)([^A-Za-z0-9_]|$)|(^|[^A-Za-z0-9_])(Math|MathF)\.' || true)"
    [ -z "$hit" ] || float_hits="$float_hits$f: $hit
"
done
if [ -n "$float_hits" ]; then
    echo "FAIL  table generator must use integers only (no float/double/decimal, no Math.*):" >&2
    printf '%s' "$float_hits" >&2
    exit 1
fi

dotnet build "$ROOT/tools/Wizard.TableGen/Wizard.TableGen.csproj" -c Release -nologo -v quiet -p:ArtifactsPath="$ART" >/dev/null
dotnet build "$ROOT/tools/Wizard.VersionStamp/Wizard.VersionStamp.csproj" -c Release -nologo -v quiet -p:ArtifactsPath="$ART" >/dev/null
TABLEGEN="$ART/bin/Wizard.TableGen/release/Wizard.TableGen.dll"
VERSIONSTAMP="$ART/bin/Wizard.VersionStamp/release/Wizard.VersionStamp.dll"

dotnet "$TABLEGEN" --self-test
dotnet "$VERSIONSTAMP" --self-test

# 도구는 자기 위치에서 저장소 뿌리를 찾는다. 현재 폴더에 기대지 않는다는 것을 보이려고 / 에서 돌린다.
(cd / && dotnet "$TABLEGEN" --check)
(cd / && dotnet "$VERSIONSTAMP" --check)
echo "gen-check: ok"
