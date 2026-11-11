#!/bin/bash
# ci-stage: fast
# .meta 검사(T-34, PIPELINE 4.5): MetaGen 의 자체 테스트, 그리고 src/·unity/Assets/ 의 빠진 .meta, 주인 없는 .meta, 중복 GUID,
# 틀린 꼴, src/·unity/Assets/Game/ 의 GUID 가 경로의 MD5 와 다른 것을 찾는다(metagen --check). pre-commit 훅도 이 스크립트를 부른다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet

ART="$ROOT/artifacts/ci-runner"
dotnet build "$ROOT/tools/Wizard.MetaGen/Wizard.MetaGen.csproj" -c Release -nologo -v quiet -p:ArtifactsPath="$ART" >/dev/null \
    || ci_die "tools/Wizard.MetaGen 을 빌드하지 못했다"
METAGEN="$ART/bin/Wizard.MetaGen/release/Wizard.MetaGen.dll"
dotnet "$METAGEN" --self-test || ci_die "metagen --self-test 실패"
dotnet "$METAGEN" --check --root "$ROOT" || ci_die "metagen --check: .meta 문제가 있다. 'dotnet run --project $ROOT/tools/Wizard.MetaGen' 로 빠진 것을 만들고 나머지는 위 줄대로 고친다"
echo "meta-check: ok"
