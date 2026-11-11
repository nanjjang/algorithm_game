#!/bin/bash
# ci-stage: full
# Release 구성에서도 같은 테스트가 통과한다(디버그 빌드도 결과가 같다. ARCHITECTURE 11 연산 규칙).
set -eu
ROOT="${WIZARD_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
dotnet test "$ROOT/Wizard.sln" -c Release -nologo
