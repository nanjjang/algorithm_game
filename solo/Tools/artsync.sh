#!/usr/bin/env bash
# solo/Tools/artsync.sh — Unity 없이 그림 대응표(Assets/Editor/Art/artsync.json)대로 원본 그림을 Assets/Resources/Art로 복사한다.
# 에디터의 Wizard.Solo.Editor.ArtSync.Run과 같은 대응표·같은 글롭 규칙을 쓴다(00 문서 10절).
#
# 쓰는 법: Tools/artsync.sh [-n]      -n = 바꾸지 않고 무엇을 할지만 보여 준다
# 끝에 복사한 장수, 정리한 파일 수, 그림 장수와 무압축 크기(가로×세로×4바이트 합)를 적는다.
# .meta는 만들지 않는다(Unity가 다음에 열 때 만든다). 지운 파일의 .meta는 같이 지운다.
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TABLE="$ROOT/Assets/Editor/Art/artsync.json"
DRY=0
[ "${1:-}" = "-n" ] && DRY=1

if [ ! -f "$TABLE" ]; then echo "artsync.sh: 대응표가 없다: $TABLE" >&2; exit 2; fi

json_str() {   # json_str <줄> <열쇠> → 글자 값
  echo "$1" | sed -n "s/.*\"$2\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p"
}
json_arr() {   # json_arr <줄> <열쇠> → 배열 항목(한 줄에 하나)
  echo "$1" | sed -n "s/.*\"$2\"[[:space:]]*:[[:space:]]*\[\([^]]*\)\].*/\1/p" | grep -o '"[^"]*"' | tr -d '"'
}
# 글롭 → 확장 정규식(ArtSyncRules.GlobToRegex와 같은 규칙)
glob_re() {
  echo "$1" | sed -e 's/[.+(){}|^$]/\\&/g' \
    -e 's#\*\*/#@@A@@#g' -e 's#\*\*#@@B@@#g' -e 's#\*#[^/]*#g' -e 's#?#[^/]#g' \
    -e 's#@@A@@#(.*/)?#g' -e 's#@@B@@#.*#g' -e 's#.*#^&$#'
}
re_list() {   # 글롭 목록(표준 입력) → 하나로 묶은 정규식(없으면 빈 글)
  local out="" g
  while IFS= read -r g; do
    [ -z "$g" ] && continue
    out="${out:+$out|}$(glob_re "$g")"
  done
  echo "$out"
}

SRC_ROOT_REL="$(json_str "$(grep -E '^[[:space:]]*"source_root"' "$TABLE")" source_root)"
DST_ROOT_REL="$(json_str "$(grep -E '^[[:space:]]*"dest_root"' "$TABLE")" dest_root)"
SRC_ROOT="$(cd "$ROOT/$SRC_ROOT_REL" 2>/dev/null && pwd)"
DST_ROOT="$ROOT/$DST_ROOT_REL"
if [ -z "$SRC_ROOT" ]; then echo "artsync.sh: 원본 폴더가 없다: $ROOT/$SRC_ROOT_REL" >&2; exit 2; fi

# 대응표가 JSON으로 읽히지 않으면(따옴표 빠짐 등) keep이 깨져 교체본을 지울 수 있다 → 아무것도 하지 않고 멈춘다.
PY="$(command -v python || command -v python3 || true)"
if [ -n "$PY" ]; then
  if ! "$PY" -I -c 'import json,sys; d=json.load(open(sys.argv[1],encoding="utf-8")); assert isinstance(d.get("keep"),list) and isinstance(d.get("exclude"),list)' "$TABLE" 2>/dev/null; then
    echo "artsync.sh: 대응표가 올바른 JSON이 아니다(keep·exclude 배열 확인): $TABLE" >&2; exit 2
  fi
fi
KEEP_LINE="$(grep -E '^[[:space:]]*"keep"' "$TABLE" | head -1)"
if [ -n "$KEEP_LINE" ] && [ "$(echo "$KEEP_LINE" | tr -cd '"' | wc -c)" -ne $(( $(echo "$KEEP_LINE" | grep -o '"[^"]*"' | wc -l) * 2 )) ]; then
  echo "artsync.sh: keep 줄의 따옴표 수가 맞지 않는다" >&2; exit 2
fi

GLOBAL_EX="$(json_arr "$(grep -E '^[[:space:]]*"exclude"' "$TABLE" | head -1)" exclude | re_list)"
# 대응표와 상관없이 절대 지우지 않는 것(사용자 교체본 _v2, 솔로 덮어쓰기 표·보충 폴더). Editor ArtSyncRules.AlwaysKeep과 같은 목록.
ALWAYS_KEEP='**/*_v2.*
**/*_v2/**
**/solo_*.json
supplement/solo/**
common/**
ui/solo_kit/**
wands/*/animation/**
backgrounds/title_solo_*.png'
KEEP_RE="$( { echo "$ALWAYS_KEEP"; json_arr "$KEEP_LINE" keep; } | re_list)"

WORK="$(mktemp -d "${TMPDIR:-/tmp}/artsync.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT
PLAN="$WORK/plan.tsv"     # 원본 절대 경로 \t 대상 상대 경로
: > "$PLAN"
MISSING=0

while IFS= read -r line; do
  src="$(json_str "$line" src)"; dst="$(json_str "$line" dst)"
  [ -z "$src" ] && continue
  inc="$(json_arr "$line" include | re_list)"
  exc="$(json_arr "$line" exclude | re_list)"
  base="$SRC_ROOT/$src"
  if [ ! -d "$base" ]; then echo "artsync.sh: 원본 폴더 없음: $base" >&2; MISSING=$((MISSING + 1)); continue; fi
  ( cd "$base" && find . -type f | sed 's#^\./##' | sort ) > "$WORK/files"
  grep -E "$inc" "$WORK/files" > "$WORK/sel" || true
  if [ -n "$exc" ]; then grep -vE "$exc" "$WORK/sel" > "$WORK/sel2" || true; mv "$WORK/sel2" "$WORK/sel"; fi
  sed "s#^#$dst/#" "$WORK/sel" > "$WORK/dsel"
  if [ -n "$GLOBAL_EX" ]; then grep -vE "$GLOBAL_EX" "$WORK/dsel" > "$WORK/dsel2" || true; mv "$WORK/dsel2" "$WORK/dsel"; fi
  awk -v b="$base/" -v p="$dst/" '{ r = substr($0, length(p) + 1); print b r "\t" $0 }' "$WORK/dsel" >> "$PLAN"
done < <(grep -E '^[[:space:]]*\{[[:space:]]*"src"' "$TABLE")

if [ "$MISSING" -ne 0 ]; then echo "artsync.sh: 원본 폴더 $MISSING개가 없어 멈춘다." >&2; exit 1; fi

DUP="$(cut -f2 "$PLAN" | sed 's/\.[^./]*$//' | sort | uniq -d)"
if [ -n "$DUP" ]; then echo "artsync.sh: Resources 경로(확장자 뺀)가 겹친다:" >&2; echo "$DUP" >&2; exit 1; fi

COPIED=0; SAME=0; REMOVED=0; PNG=0; RAW=0
mkdir -p "$DST_ROOT"
while IFS=$'\t' read -r s d; do
  t="$DST_ROOT/$d"
  if [ -f "$t" ] && cmp -s "$s" "$t"; then SAME=$((SAME + 1)); else
    COPIED=$((COPIED + 1))
    if [ "$DRY" = "0" ]; then mkdir -p "$(dirname "$t")"; cp "$s" "$t"; else echo "copy $d"; fi
  fi
done < "$PLAN"
# 그림 장수와 무압축 크기: PNG 머리(IHDR의 가로·세로, 16바이트 뒤 8바이트)를 읽는다.
while IFS=$'\t' read -r s d; do
  PNG=$((PNG + 1))
  read -r a b c d2 e f g h <<< "$(od -An -tu1 -j16 -N8 "$s")"
  RAW=$((RAW + ((a << 24) + (b << 16) + (c << 8) + d2) * ((e << 24) + (f << 16) + (g << 8) + h) * 4))
done < <(grep -E '\.png$' "$PLAN")

# 대응표에 없는 파일 정리(.meta와 keep 제외). 지우는 파일의 .meta도 같이.
cut -f2 "$PLAN" | sort > "$WORK/want"
( cd "$DST_ROOT" && find . -type f ! -name '*.meta' | sed 's#^\./##' | sort ) > "$WORK/have"
comm -23 "$WORK/have" "$WORK/want" > "$WORK/extra"
while IFS= read -r x; do
  [ -z "$x" ] && continue
  if [ -n "$KEEP_RE" ] && echo "$x" | grep -qE "$KEEP_RE"; then continue; fi
  REMOVED=$((REMOVED + 1))
  if [ "$DRY" = "0" ]; then rm -f "$DST_ROOT/$x" "$DST_ROOT/$x.meta"; else echo "remove $x"; fi
done < "$WORK/extra"
# 남은 .meta 중 짝 없는 것, 빈 폴더(와 그 .meta) 정리
if [ "$DRY" = "0" ]; then
  find "$DST_ROOT" -type f -name '*.meta' | while IFS= read -r m; do
    [ -e "${m%.meta}" ] || rm -f "$m"
  done
  find "$DST_ROOT" -mindepth 1 -depth -type d -empty | while IFS= read -r e; do rmdir "$e"; rm -f "$e.meta"; done
fi

TOTAL="$(wc -l < "$PLAN" | tr -d ' ')"
echo "artsync.sh: 대응 파일 $TOTAL개(그림 $PNG장, 무압축 $((RAW / 1048576))MB = ${RAW}바이트), 복사 $COPIED, 같음 $SAME, 정리 $REMOVED. 대상 $DST_ROOT"
