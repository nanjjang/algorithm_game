#!/bin/bash
# ci-stage: integration
# ci-timeout: 7200
# Unity 플레이어 빌드, IL2CPP × arm64·x64(PIPELINE 7.2 1번). mac-il2cpp 모듈이 없으면 건너뛰지 않고, 없다는 것과 설치 명령을 적고 실패한다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet
ci_unity_prepare
ci_require il2cpp
fail=0
for arch in arm64 x64; do
    /bin/bash "$UNITY_SH" build il2cpp "$arch" || { echo "FAIL  IL2CPP $arch 플레이어 빌드" >&2; fail=1; }
done
exit $fail
