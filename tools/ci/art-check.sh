#!/bin/bash
# ci-stage: integration
# G9 에셋 게이트(PIPELINE 6.4절 ⑤, 7.1절). 로직은 tools/Wizard.AssetCheck 에 있다:
#   1. assetcheck check    반입 검사(spec 14 5.7.1절): 파일과 PNG 크기, 점 필터·밉맵·압축, 플레이어 쪽 위험색 0픽셀(치장 포함),
#                          적 몸의 색 규칙, 큰 배경의 아틀라스 제외, char.5 팔레트와 지팡이 +0 머리. 오류가 하나라도 있으면 실패
#   2. assetcheck report   자리마다 없음/반입됨/검수됨/실패 → artifacts/asset-report.json(없음과 경고는 실패가 아니다)
# 마일스톤 게이트(ci.sh gate)가 integration 단계로 이 검사를 돈다. 그림만 바꿨을 때는 ci.sh run art-check.sh 로 돌린다.
# bash 3.2 문법만 쓴다. 저장소 뿌리는 현재 폴더가 아니라 이 파일의 위치에서 구한다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/lib/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet

ART="$ROOT/artifacts/ci-runner"
dotnet build "$ROOT/tools/Wizard.AssetCheck/Wizard.AssetCheck.csproj" -c Release -nologo -v quiet -p:ArtifactsPath="$ART" >/dev/null \
    || ci_die "tools/Wizard.AssetCheck 를 빌드하지 못했다(dotnet build $ROOT/tools/Wizard.AssetCheck)"
AC="$ART/bin/Wizard.AssetCheck/release/Wizard.AssetCheck.dll"
[ -f "$AC" ] || ci_die "assetcheck 실행 파일이 없다: $AC"

fail=0
# 도구가 현재 폴더에 기대지 않는다는 것을 보이려고 / 에서 돌린다
(cd / && dotnet "$AC" check --root "$ROOT") || fail=1
(cd / && dotnet "$AC" report --root "$ROOT") || fail=1
ci_note "asset report: $ROOT/artifacts/asset-report.json"
if [ "$fail" != "0" ]; then
    echo "FAIL  art-check: 위 줄의 그림을 고친다(규격 spec 14 4절·5.7.1절, 슬롯별 상태는 artifacts/asset-report.json)" >&2
    exit 1
fi
echo "art-check: ok"
