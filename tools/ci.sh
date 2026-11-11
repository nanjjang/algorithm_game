#!/bin/bash
# 로컬 CI 의 짧은 입구. 기본은 full(fast 의 모든 검사 + Release 테스트). 단계는 tools/ci/ci.sh 를 본다.
#   tools/ci.sh                 → tools/ci/ci.sh full
#   tools/ci.sh fast|full|integration|run <스크립트>
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
if [ $# -eq 0 ]; then
    exec /bin/bash "$HERE/ci/ci.sh" full
fi
exec /bin/bash "$HERE/ci/ci.sh" "$@"
