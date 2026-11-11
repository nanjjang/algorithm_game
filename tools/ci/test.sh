#!/bin/bash
# ci-stage: fast
# 전체 단위 테스트(Debug). 건너뛴 테스트가 있으면 실패다(테스트를 건너뛰게 표시하지 않는다).
set -eu
ROOT="${WIZARD_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
LOG="$ROOT/artifacts/ci/test.log"
/bin/mkdir -p "$ROOT/artifacts/ci"
set +e
dotnet test "$ROOT/Wizard.sln" -c Debug -nologo 2>&1 | /usr/bin/tee "$LOG"
code=${PIPESTATUS[0]}
set -e
[ $code -eq 0 ] || exit $code
if /usr/bin/grep -E 'Skipped: +[1-9]' "$LOG" >/dev/null; then
    echo "FAIL  skipped tests found" >&2
    exit 1
fi
total=$(/usr/bin/sed -n 's/.*Total: *\([0-9][0-9]*\).*/\1/p' "$LOG" | /usr/bin/awk '{s+=$1} END {print s+0}')
echo "tests: $total passed"
