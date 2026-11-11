#!/bin/bash
# ci-stage: fast
# docs/dev/tasks.json 검사(JSON, 위상정렬, 묶음 안 경로 겹침, 벡터 배정). node 표준 모듈만 쓴다.
set -eu
ROOT="${WIZARD_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
command -v node >/dev/null 2>&1 || { echo "FAIL  node 가 없다. Node.js 를 설치해야 한다(tools/check-tasks.mjs)." >&2; exit 1; }
node "$ROOT/tools/check-tasks.mjs"
