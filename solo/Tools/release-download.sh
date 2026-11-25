#!/usr/bin/env bash
# 다운로드 페이지(solo/Web/download)가 가리키는 GitHub Release에 게임 zip을 올린다.
# 사용: solo/Tools/release-download.sh v1.0.1 ["바뀐 점 메모"]
# 전제: Unity로 solo/Builds/Windows, solo/Builds/macOS/Wizard.app 빌드가 끝나 있고, gh 로그인이 돼 있다.
set -euo pipefail

TAG="${1:?태그를 주세요. 예: v1.0.1}"
NOTES="${2:-}"
REPO="nanjjang/algorithm_game"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILDS="$ROOT/Builds"
OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT

[ -f "$BUILDS/Windows/Wizard.exe" ] || { echo "Windows 빌드가 없습니다: $BUILDS/Windows"; exit 1; }
[ -d "$BUILDS/macOS/Wizard.app" ]   || { echo "macOS 빌드가 없습니다: $BUILDS/macOS/Wizard.app"; exit 1; }

echo "== Windows zip"
( cd "$BUILDS" && zip -qr "$OUT/Wizard-Windows.zip" Windows \
    -x '*.DS_Store' -x '*_BurstDebugInformation_DoNotShip*' )

echo "== macOS zip (ditto: 실행 권한·서명 유지)"
ditto -c -k --keepParent "$BUILDS/macOS/Wizard.app" "$OUT/Wizard-macOS.zip"

ls -lh "$OUT"

echo "== GitHub Release $TAG 만들기"
gh release create "$TAG" -R "$REPO" \
  --title "$TAG" --notes "${NOTES:-$TAG}" --latest \
  "$OUT/Wizard-Windows.zip" "$OUT/Wizard-macOS.zip"

echo "완료: https://github.com/$REPO/releases/tag/$TAG"
