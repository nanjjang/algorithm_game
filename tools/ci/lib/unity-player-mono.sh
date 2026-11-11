#!/bin/bash
# ci-stage: integration
# ci-timeout: 7200
# Unity 플레이어 빌드, Mono × arm64·x64(PIPELINE 7.2 1번). 결과: artifacts/unity/mono-<arch>/Wizard.app
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet
ci_unity_prepare
fail=0
for arch in arm64 x64; do
    /bin/bash "$UNITY_SH" build mono "$arch" || { echo "FAIL  Mono $arch 플레이어 빌드" >&2; fail=1; }
done
exit $fail
