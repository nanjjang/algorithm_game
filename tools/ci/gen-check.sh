#!/bin/bash
# ci-stage: fast
# 다시 만드는 파일(T-41)이 도구의 출력과 바이트가 같은지 본다: SinTable·PowTables(TableGen), SimVersion(VersionStamp).
set -eu
ROOT="${WIZARD_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
ART="$ROOT/artifacts/ci-runner"
dotnet build "$ROOT/tools/Wizard.TableGen/Wizard.TableGen.csproj" -c Release -nologo -v quiet -p:ArtifactsPath="$ART" >/dev/null
dotnet build "$ROOT/tools/Wizard.VersionStamp/Wizard.VersionStamp.csproj" -c Release -nologo -v quiet -p:ArtifactsPath="$ART" >/dev/null
# 도구는 자기 위치에서 저장소 뿌리를 찾는다. ci-runner 출력도 저장소 안에 있다.
dotnet "$ART/bin/Wizard.TableGen/release/Wizard.TableGen.dll" --check
dotnet "$ART/bin/Wizard.VersionStamp/release/Wizard.VersionStamp.dll" --check
echo "gen-check: ok"
