#!/bin/bash
# ci-stage: fast
# 복원, Debug·Release 빌드(경고 0, 오류 0). 빌드 뒤 src/·server/·tests/·tools/ 안에 bin/·obj/ 가 없어야 한다(UseArtifactsOutput).
set -eu
ROOT="${WIZARD_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
dotnet restore "$ROOT/Wizard.sln" -nologo -v quiet
dotnet build "$ROOT/Wizard.sln" -c Debug --no-restore -nologo -v quiet -warnaserror
dotnet build "$ROOT/Wizard.sln" -c Release --no-restore -nologo -v quiet -warnaserror
stray="$(/usr/bin/find "$ROOT/src" "$ROOT/server" "$ROOT/tests" "$ROOT/tools" -type d \( -name bin -o -name obj \) -not -path '*/node_modules/*' 2>/dev/null || true)"
if [ -n "$stray" ]; then
    echo "FAIL  bin/ or obj/ inside source folders (output must go to artifacts/):" >&2
    echo "$stray" >&2
    exit 1
fi
echo "build: ok"
