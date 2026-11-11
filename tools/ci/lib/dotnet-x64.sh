#!/bin/bash
# ci-stage: integration
# ci-timeout: 3600
# dotnet 의 x64 열(PIPELINE 7.2 2번, 7.3 "런타임 × 아키텍처"): 테스트 프로젝트를 Rosetta 2 의 x64 런타임으로 돌린다(dotnet test -a x64).
# 골든과 산술 경계 벡터가 arm64 와 같아야 한다. x64 dotnet 런타임이 없으면 건너뛰지 않고, 없다는 것과 설치 방법을 적고 실패한다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet
ci_require x64-dotnet
fail=0
for proj in $(ci_tool affected-tests --all --root "$ROOT" | /usr/bin/grep -v '^#'); do
    echo "==> dotnet test -a x64 $proj"
    dotnet test "$ROOT/$proj" -c Release -a x64 -nologo || fail=1
done
exit $fail
