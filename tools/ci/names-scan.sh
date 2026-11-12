#!/bin/bash
# ci-stage: fast
# 이름 표와 화면 글자의 G0·G5 검사(PIPELINE 7.1, CLAUDE.md 5절, CONTRACTS 2.1·5절, DECISIONS-TECH T-35). 로직은 tools/Wizard.DataBuild 에 있다:
#   1. databuild names --check   data/names/names.ko.json 의 형식(서수 정렬, 중복 없음, 한 줄에 열쇠 하나, 끝 표지 "~end", 정규 띄어쓰기)
#   2. databuild names --scan    제품 코드(src/, server/, unity/Assets/Game/)의 이름 표 밖 한글 문자열 리터럴 0(tests/ 는 보지 않는다),
#                                작업용 이름의 로마자 표기가 src/, server/, data/ 의 식별자·열쇠·파일 이름에 0
#                                (data/presentation/asset_aliases.json 과 art/ 경로는 뺀다)
#   3. databuild check           데이터의 모든 id 에 이름 열쇠(M1 부터 돈다). 스키마·변환·결정성도 같이 본다
# bash 3.2 문법만 쓴다. 저장소 뿌리는 현재 폴더가 아니라 이 파일의 위치에서 구한다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/lib/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet

ART="$ROOT/artifacts/ci-runner"
dotnet build "$ROOT/tools/Wizard.DataBuild/Wizard.DataBuild.csproj" -c Release -nologo -v quiet -p:ArtifactsPath="$ART" >/dev/null \
    || ci_die "tools/Wizard.DataBuild 를 빌드하지 못했다(dotnet build $ROOT/tools/Wizard.DataBuild)"
DB="$ART/bin/Wizard.DataBuild/release/Wizard.DataBuild.dll"
[ -f "$DB" ] || ci_die "databuild 실행 파일이 없다: $DB"

fail=0
# 도구가 현재 폴더에 기대지 않는다는 것을 보이려고 / 에서 돌린다
(cd / && dotnet "$DB" names --check --root "$ROOT") || fail=1
(cd / && dotnet "$DB" names --scan --root "$ROOT") || fail=1
(cd / && dotnet "$DB" check --root "$ROOT") || fail=1
if [ "$fail" != "0" ]; then
    echo "FAIL  names-scan: 위 줄의 문제를 고친다(이름 표 정렬·중복은 dotnet run --project $ROOT/tools/Wizard.DataBuild -- names --fix)" >&2
    exit 1
fi
echo "names-scan: ok"
