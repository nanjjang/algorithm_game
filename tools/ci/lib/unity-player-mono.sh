#!/bin/bash
# ci-stage: integration
# ci-timeout: 7200
# Unity 플레이어 빌드, Mono × arm64·x64(PIPELINE 7.2 1번). 결과: artifacts/unity/mono-<arch>/Wizard.app
# 기본 팩(FIX-3, ARCHITECTURE 8.2): Unity 를 띄우기 전에 databuild build 로 artifacts/content/base.wzpack 을 만든다.
#   Ci.BuildPlayer 가 그 팩을 unity/Assets/StreamingAssets/content/ 에 놓고 빌드한 뒤 지운다.
#   빌드마다 build.json 의 playerContentPackSha256, 앱 안의 팩, databuild 출력의 SHA-256 이 같은지 대조하고,
#   unity/Assets/StreamingAssets 에 실행 전에 없던 것(무시된 파일 포함)이 남았으면 실패한다.
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
BACKEND=mono
ci_need_dotnet
ci_unity_prepare

PACK="$ROOT/artifacts/content/base.wzpack"
sha256_of() {
    /usr/bin/shasum -a 256 "$1" | /usr/bin/awk '{print $1}'
}
streaming_status() {
    command git -C "$ROOT" -c core.quotepath=off status --porcelain=v1 --ignored --untracked-files=all -- unity/Assets/StreamingAssets unity/Assets/StreamingAssets.meta
}
json_field() {
    # build.json(JsonUtility 의 들여쓴 출력)에서 문자열 칸 하나
    /usr/bin/sed -n "s/^ *\"$2\": *\"\\([^\"]*\\)\".*/\\1/p" "$1" | /usr/bin/head -1
}

/bin/rm -f "${PACK:?}"
dotnet run --project "$ROOT/tools/Wizard.DataBuild" -c Release -- build --root "$ROOT" --out "$ROOT/artifacts/content" \
    || ci_die "databuild build 가 기본 팩을 만들지 못했다(dotnet run --project $ROOT/tools/Wizard.DataBuild -c Release -- build). 팩 없는 플레이어는 만들지 않는다"
[ -f "$PACK" ] || ci_die "databuild build 가 끝났지만 $PACK 이 없다"
WANT="$(sha256_of "$PACK")"
echo "ci: 기본 팩 $PACK sha256 $WANT"

# 빌드가 성공이든 실패든 놓은 팩과 .meta 가 지워졌는가. 남았으면 이유를 적고 1
verify_unstaged() {
    local arch="$1" before="$2" after
    after="$(streaming_status)"
    if [ "$after" != "$before" ]; then
        echo "FAIL  $BACKEND $arch: 빌드 뒤 unity/Assets/StreamingAssets 에 실행 전에 없던 것이 남았다:" >&2
        echo "$after" >&2
        return 1
    fi
}

# 빌드 하나의 팩 대조. 실패하면 이유를 적고 1
verify_player_pack() {
    local arch="$1" out json app_pack recorded actual
    out="$ROOT/artifacts/unity/$BACKEND-$arch"
    json="$out/build.json"
    app_pack="$out/Wizard.app/Contents/Resources/Data/StreamingAssets/content/base.wzpack"
    [ -f "$json" ] || { echo "FAIL  $BACKEND $arch: $json 이 없다" >&2; return 1; }
    recorded="$(json_field "$json" playerContentPackSha256)"
    [ -f "$app_pack" ] || { echo "FAIL  $BACKEND $arch: 앱 안에 기본 팩이 없다: $app_pack" >&2; return 1; }
    actual="$(sha256_of "$app_pack")"
    if [ "$recorded" != "$WANT" ] || [ "$actual" != "$WANT" ]; then
        echo "FAIL  $BACKEND $arch: 기본 팩의 SHA-256 이 다르다: databuild $WANT, build.json $recorded, 앱 안 $actual" >&2
        return 1
    fi
    echo "ci: $BACKEND $arch 앱 안의 기본 팩이 databuild 출력과 같다(sha256 $actual)"
}

fail=0
for arch in arm64 x64; do
    before="$(streaming_status)"
    if /bin/bash "$UNITY_SH" build "$BACKEND" "$arch"; then
        verify_player_pack "$arch" || { echo "FAIL  Mono $arch 플레이어의 기본 팩" >&2; fail=1; }
    else
        echo "FAIL  Mono $arch 플레이어 빌드" >&2
        fail=1
    fi
    verify_unstaged "$arch" "$before" || fail=1
done
exit $fail
