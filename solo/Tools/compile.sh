#!/usr/bin/env bash
# solo/Tools/compile.sh — Unity를 띄우지 않고 Roslyn으로 스크립트를 컴파일해 오류만 보여 준다.
#
# 쓰는 법:
#   Tools/compile.sh [-o 출력폴더] [-f 경로필터] [-w] [출력폴더] [경로필터]
#     -o DIR   컴파일 산출물(dll, rsp, log)을 둘 폴더. 없으면 임시 폴더를 새로 만든다(동시에 여럿 돌려도 안 겹친다).
#     -f PATH  이 경로(폴더 또는 파일) 아래의 진단만 보여 준다. 여러 개는 쉼표로. 폴더 경계로 맞춘다
#              (Assets/Tests/EditMode/Art 는 .../Arts/ 를 잡지 않는다. 끝의 / 는 있어도 없어도 된다).
#     -w       경고도 보여 준다(기본은 오류만).
#
# 끝 코드:
#   0  필터 안에 오류가 없고, 필터에 걸리는 단위가 전부 실제로 컴파일됐다.
#   1  필터 안에 확실한 오류가 있다.
#   2  도구 문제(Unity 설치본·Library를 못 찾음).
#   3  미확인: 필터에 걸리는 단위가 컴파일되지 못했거나(앞 단위가 깨짐), 깨진 남의 파일을 빼고 다시 컴파일했더니
#      필터 안에 진단이 나왔다(빠진 파일 때문일 수 있다). **3은 통과가 아니다.** 잠시 뒤 다시 돌린다.
#
# 컴파일 단위(asmdef와 같다):
#   Assets/Scripts        -> Wizard.Solo.dll                   (런타임. UNITY_EDITOR 없음, UnityEditor 참조 없음)
#   Assets/Editor         -> Wizard.Solo.Editor.dll            (Wizard.Solo 참조, UNITY_EDITOR)
#   Assets/Tests/EditMode -> Wizard.Solo.Tests.EditMode.dll    (Wizard.Solo, Wizard.Solo.Editor 참조, UNITY_EDITOR)
#   Assets/Tests/PlayMode -> Wizard.Solo.Tests.PlayMode.dll    (Wizard.Solo 참조. UNITY_EDITOR·UnityEditor 없음 — 플레이어에서도 도는 코드만)
#
# 남의 파일이 깨져 있을 때: 한 단위가 오류로 dll을 못 만들면 오류 난 파일을 빼고 다시 컴파일한다(최대 4번).
# 그렇게 줄여 만든 dll로 뒤 단위(테스트)를 계속 컴파일한다. 1차 진단은 "확실", 줄인 뒤의 진단은 "불확실"로 따로 보여 준다.
#
# 덧검사(컴파일러가 못 잡는 프로젝트 규칙. error SOLOnnnn으로 나온다):
#   SOLO0001  Assets/Scripts에 #if UNITY_EDITOR  (런타임 코드는 에디터 전용 갈래를 두지 않는다)
#   SOLO0002  Assets/Scripts(Art 폴더 제외)에 Shader.Find  (머티리얼은 ctx.Art.Material(kind)에서)
#   SOLO0003  Assets/Scripts에 옛 입력(UnityEngine.Input.GetKey 등, StandaloneInputModule)  (활성 입력은 Input System 전용)
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

OUT=""
FILTER=""
SHOW_WARN=0
POS=()
while [ $# -gt 0 ]; do
  case "$1" in
    -o) OUT="$2"; shift 2 ;;
    -f) FILTER="$2"; shift 2 ;;
    -w) SHOW_WARN=1; shift ;;
    -h|--help) sed -n '2,31p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) POS+=("$1"); shift ;;
  esac
done
if [ -z "$OUT" ] && [ ${#POS[@]} -ge 1 ]; then OUT="${POS[0]}"; fi
if [ -z "$FILTER" ] && [ ${#POS[@]} -ge 2 ]; then FILTER="${POS[1]}"; fi
if [ -z "$OUT" ]; then OUT="$(mktemp -d "${TMPDIR:-/tmp}/solo-compile.XXXXXX")"; fi
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"

# --- Unity 설치본 찾기 ---
UNITY_DATA="${UNITY_EDITOR_DATA:-}"
if [ -z "$UNITY_DATA" ]; then
  VER="$(sed -n 's/^m_EditorVersion: *//p' "$ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
  for base in "/c/Program Files/Unity/Hub/Editor" "/d/Program Files/Unity/Hub/Editor" "/c/Program Files/Unity"; do
    if [ -d "$base/$VER/Editor/Data" ]; then UNITY_DATA="$base/$VER/Editor/Data"; break; fi
  done
  # macOS: /Applications/Unity/Hub/Editor/<버전>/Unity.app/Contents (Data 폴더에 해당)
  if [ -z "$UNITY_DATA" ] && [ -d "/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents" ]; then
    UNITY_DATA="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents"
  fi
fi
if [ -z "$UNITY_DATA" ] || [ ! -d "$UNITY_DATA" ]; then
  echo "compile.sh: Unity Editor/Data 폴더를 찾지 못했다. UNITY_EDITOR_DATA 환경 변수로 알려 달라." >&2
  exit 2
fi

CSC="$UNITY_DATA/DotNetSdkRoslyn/csc.dll"
DOTNET="$UNITY_DATA/NetCoreRuntime/dotnet.exe"
if [ ! -f "$DOTNET" ] && [ -f "$UNITY_DATA/NetCoreRuntime/dotnet" ]; then DOTNET="$UNITY_DATA/NetCoreRuntime/dotnet"; fi
if [ ! -f "$DOTNET" ]; then DOTNET="$(command -v dotnet || true)"; fi
if [ ! -f "$CSC" ] || [ -z "$DOTNET" ]; then
  echo "compile.sh: csc.dll 또는 dotnet을 찾지 못했다 ($CSC)." >&2
  exit 2
fi

w() { cygpath -m "$1"; }   # 컴파일러에 넘길 경로(C:/... 꼴)

SA="$ROOT/Library/ScriptAssemblies"
NEWTONSOFT="$(ls -d "$ROOT"/Library/PackageCache/com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll 2>/dev/null | head -1)"
NUNIT="$(ls -d "$ROOT"/Library/PackageCache/com.unity.ext.nunit@*/net40/unity-custom/nunit.framework.dll 2>/dev/null | head -1)"
if [ ! -d "$SA" ] || [ -z "$NEWTONSOFT" ]; then
  echo "compile.sh: Library가 없다(ScriptAssemblies 또는 Newtonsoft). Unity로 프로젝트를 한 번 열어야 한다." >&2
  exit 2
fi

# --- 공통 참조 ---
BASE_DEFINES="UNITY_6000_3_OR_NEWER;UNITY_6000_0_OR_NEWER;UNITY_2022_3_OR_NEWER;UNITY_2021_3_OR_NEWER;UNITY_5_3_OR_NEWER;UNITY_64;UNITY_STANDALONE;UNITY_STANDALONE_WIN;ENABLE_INPUT_SYSTEM;UNITY_INCLUDE_TESTS;UNITY_ASSERTIONS;DEBUG;TRACE;NET_STANDARD;NET_STANDARD_2_1;NETSTANDARD;NETSTANDARD2_1;CSHARP_7_3_OR_NEWER"
COMMON="$OUT/common.rsp"
{
  echo "-nologo"
  echo "-utf8output"
  echo "-nostdlib+"
  echo "-target:library"
  echo "-langversion:9.0"
  echo "-deterministic"
  echo "-debug-"
  echo "-nowarn:0169,0649,0282,1701,1702,1591"
  echo "-r:\"$(w "$UNITY_DATA/NetStandard/ref/2.1.0/netstandard.dll")\""
  for d in "$UNITY_DATA/NetStandard/compat/2.1.0/shims/netstandard" "$UNITY_DATA/NetStandard/compat/2.1.0/shims/netfx"; do
    for f in "$d"/*.dll; do [ -f "$f" ] && echo "-r:\"$(w "$f")\""; done
  done
  for f in "$UNITY_DATA/Managed/UnityEngine"/UnityEngine*.dll; do echo "-r:\"$(w "$f")\""; done
  # asmdef의 references와 같이 고친다(URP 어셈블리는 넣지 않는다: 00 문서 7절 "평범한 Camera만").
  for n in UnityEngine.UI Unity.InputSystem; do
    [ -f "$SA/$n.dll" ] && echo "-r:\"$(w "$SA/$n.dll")\""
  done
  echo "-r:\"$(w "$NEWTONSOFT")\""
  for f in "$ROOT"/Assets/Plugins/Server/*.dll; do [ -f "$f" ] && echo "-r:\"$(w "$f")\""; done
} > "$COMMON"

EDITOR_REFS="$OUT/editor.rsp"
{
  for f in "$UNITY_DATA/Managed/UnityEngine"/UnityEditor*.dll; do echo "-r:\"$(w "$f")\""; done
  for n in UnityEditor.UI; do
    [ -f "$SA/$n.dll" ] && echo "-r:\"$(w "$SA/$n.dll")\""
  done
} > "$EDITOR_REFS"

TEST_REFS="$OUT/test.rsp"
{
  [ -n "$NUNIT" ] && echo "-r:\"$(w "$NUNIT")\""
  [ -f "$SA/UnityEngine.TestRunner.dll" ] && echo "-r:\"$(w "$SA/UnityEngine.TestRunner.dll")\""
} > "$TEST_REFS"

EDITOR_TEST_REFS="$OUT/editortest.rsp"
{
  [ -f "$SA/UnityEditor.TestRunner.dll" ] && echo "-r:\"$(w "$SA/UnityEditor.TestRunner.dll")\""
} > "$EDITOR_TEST_REFS"

SURE="$OUT/diagnostics.sure.log"       # 확실한 진단(모든 파일로 컴파일한 1차)
UNSURE="$OUT/diagnostics.unsure.log"   # 불확실한 진단(깨진 파일을 뺀 뒤, 또는 줄인 dll을 참조한 단위)
: > "$SURE"; : > "$UNSURE"
MAX_PASS=4
NOTES=""
UNIT_STATES=""

# unit <이름> <소스 폴더(ROOT 기준)> <taint: 0|1> <추가 인자...>
#   taint = 1이면 앞 단위가 줄여 만든 dll이라 이 단위의 진단을 전부 "불확실"로 보낸다.
#   결과: UNIT_RESULT = none(소스 없음) | ok | reduced(파일을 빼고 만듦) | failed(dll 없음)
unit() {
  local name="$1" dir="$2" taint="$3"; shift 3
  UNIT_RESULT="none"
  if [ ! -d "$ROOT/$dir" ]; then return 0; fi
  local files="$OUT/$name.files" src="$OUT/$name.sources.rsp"
  ( cd "$ROOT" && find "$dir" -name '*.cs' -type f | sort ) > "$files"
  if [ ! -s "$files" ]; then return 0; fi
  rm -f "$OUT/$name.dll" "$OUT/$name.excluded"
  local pass=1 plog
  while :; do
    sed 's/.*/"&"/' "$files" > "$src"
    plog="$OUT/$name.pass$pass.log"
    ( cd "$ROOT" && "$DOTNET" "$(w "$CSC")" -noconfig "@$(w "$COMMON")" "$@" "-out:$(w "$OUT/$name.dll")" "@$(w "$src")" ) 2>&1 \
      | tr -d '\r' | sed 's#\\#/#g' > "$plog"
    if [ "$pass" -eq 1 ] && [ "$taint" = "0" ]; then cat "$plog" >> "$SURE"; else cat "$plog" >> "$UNSURE"; fi
    if [ -f "$OUT/$name.dll" ]; then break; fi
    if [ "$pass" -ge "$MAX_PASS" ]; then break; fi
    grep -E 'error CS[0-9]+' "$plog" | sed -E 's/^([^(]+)\([0-9]+,[0-9]+\).*/\1/' | sort -u > "$OUT/$name.bad"
    grep -vxFf "$OUT/$name.bad" "$files" > "$files.next" || true
    if cmp -s "$files" "$files.next" || [ ! -s "$files.next" ]; then break; fi
    grep -xFf "$OUT/$name.bad" "$files" >> "$OUT/$name.excluded" || true
    mv "$files.next" "$files"
    pass=$((pass + 1))
  done
  if [ -f "$OUT/$name.dll" ]; then
    if [ "$pass" -eq 1 ]; then UNIT_RESULT="ok"; else
      UNIT_RESULT="reduced"
      NOTES="$NOTES\n  - $dir: 오류 난 파일 $(sort -u "$OUT/$name.excluded" | wc -l | tr -d ' ')개를 빼고 dll을 만들었다(목록: $(w "$OUT/$name.excluded"))."
    fi
  else
    UNIT_RESULT="failed"
  fi
  return 0
}
have() { [ -f "$OUT/$1.dll" ]; }
ref() { echo "-r:$(w "$OUT/$1.dll")"; }
state() { UNIT_STATES="$UNIT_STATES $1=$2"; }
has_sources() { [ -d "$ROOT/$1" ] && [ -n "$(find "$ROOT/$1" -name '*.cs' -type f | head -1)" ]; }

# 필터가 그 단위 폴더에 걸리는가(필터 없음 = 전부 걸림).
relevant() {
  local dir="$1" item
  if [ -z "$FILTER" ]; then return 0; fi
  local IFS=','
  for item in $FILTER; do
    item="$(echo "$item" | sed 's#\\#/#g; s#/*$##')"
    case "$item/" in "$dir/"*) return 0 ;; esac
    case "$dir/" in "$item/"*) return 0 ;; esac
  done
  return 1
}

UNVERIFIED=""
skip() {   # skip <폴더> <까닭>
  state "$1" "skipped"
  if has_sources "$1" && relevant "$1"; then UNVERIFIED="$UNVERIFIED\n  - $1: 컴파일하지 못했다($2)."; fi
}

RUNTIME_DEF="-define:$BASE_DEFINES"
EDITOR_DEF="-define:$BASE_DEFINES;UNITY_EDITOR;UNITY_EDITOR_64;UNITY_EDITOR_WIN"

unit Wizard.Solo Assets/Scripts 0 "$RUNTIME_DEF"
state Assets/Scripts "$UNIT_RESULT"
RT="$UNIT_RESULT"

if have Wizard.Solo; then
  T=0; [ "$RT" = "reduced" ] && T=1

  unit Wizard.Solo.Editor Assets/Editor "$T" "$EDITOR_DEF" "@$(w "$EDITOR_REFS")" "$(ref Wizard.Solo)"
  ED="$UNIT_RESULT"; state Assets/Editor "$ED"

  unit Wizard.Solo.Tests.PlayMode Assets/Tests/PlayMode "$T" "$RUNTIME_DEF" "@$(w "$TEST_REFS")" "$(ref Wizard.Solo)"
  state Assets/Tests/PlayMode "$UNIT_RESULT"
  if [ "$UNIT_RESULT" = "failed" ] && relevant Assets/Tests/PlayMode && [ "$T" = "1" ]; then
    UNVERIFIED="$UNVERIFIED\n  - Assets/Tests/PlayMode: 줄인 Wizard.Solo.dll로는 컴파일되지 않았다."
  fi

  if [ "$ED" = "failed" ]; then
    skip Assets/Tests/EditMode "Assets/Editor가 깨져서"
  else
    EDITMODE_EXTRA=()
    TE="$T"
    if have Wizard.Solo.Editor; then EDITMODE_EXTRA+=("$(ref Wizard.Solo.Editor)"); fi
    [ "$ED" = "reduced" ] && TE=1
    unit Wizard.Solo.Tests.EditMode Assets/Tests/EditMode "$TE" "$EDITOR_DEF" "@$(w "$EDITOR_REFS")" "@$(w "$TEST_REFS")" "@$(w "$EDITOR_TEST_REFS")" "$(ref Wizard.Solo)" "${EDITMODE_EXTRA[@]}"
    state Assets/Tests/EditMode "$UNIT_RESULT"
  fi
else
  skip Assets/Editor "Assets/Scripts가 깨져서"
  skip Assets/Tests/EditMode "Assets/Scripts가 깨져서"
  skip Assets/Tests/PlayMode "Assets/Scripts가 깨져서"
fi

# --- 덧검사(프로젝트 규칙) ---
lint() {   # lint <코드> <정규식> <글> <제외할 경로 정규식 또는 빈 글> <폴더>
  local code="$1" re="$2" msg="$3" excl="$4" dir="$5"
  [ -d "$ROOT/$dir" ] || return 0
  ( cd "$ROOT" && grep -rnE --include='*.cs' "$re" "$dir" 2>/dev/null ) \
    | { if [ -n "$excl" ]; then grep -vE "$excl"; else cat; fi; } \
    | awk -F: -v code="$code" -v msg="$msg" '{ print $1 "(" $2 ",1): error " code ": " msg }' >> "$SURE" || true
}
lint SOLO0001 '^[[:space:]]*#[[:space:]]*(if|elif)[^/]*UNITY_EDITOR' '런타임 코드에 #if UNITY_EDITOR를 쓰지 않는다(에디터 전용 코드는 Assets/Editor에).' '' Assets/Scripts
lint SOLO0002 'Shader\.Find[[:space:]]*\(' 'Shader.Find 금지: 머티리얼은 ctx.Art.Material(SpriteMaterialKind)에서 받는다.' '^Assets/Scripts/Art/' Assets/Scripts
lint SOLO0003 '(^|[^A-Za-z0-9_.])Input\.(GetKey|GetKeyDown|GetKeyUp|GetButton|GetButtonDown|GetButtonUp|GetAxis|GetAxisRaw|GetMouseButton|GetMouseButtonDown|GetMouseButtonUp|mousePosition|anyKey)|StandaloneInputModule' '옛 입력(UnityEngine.Input, StandaloneInputModule) 금지: Input System만 쓴다(Keyboard.current, Mouse.current, InputSystemUIInputModule).' '' Assets/Scripts

# --- 결과 ---
sort -u "$SURE" -o "$SURE"
sort -u "$UNSURE" -o "$UNSURE"

ERR_RE='error (CS|SOLO)[0-9]+'
SHOW_RE="$ERR_RE"
if [ "$SHOW_WARN" = "1" ]; then SHOW_RE='(error|warning) (CS|SOLO)[0-9]+'; fi

FPAT=""
if [ -n "$FILTER" ]; then
  # 폴더 경계로 맞춘다: (^|/)<경로>(/|\(|$)
  FPAT="$(echo "$FILTER" | sed 's#\\#/#g' | tr ',' '\n' | sed 's#/*$##; s/[.[\*^$+?(){}|]/\\&/g' | grep -v '^$' | sed 's#.*#(^|/)&(/|\\(|$)#' | paste -sd'|' -)"
fi
infilter() { if [ -n "$FPAT" ]; then grep -E "$FPAT" || true; else cat; fi; }

TOTAL_ERR="$(grep -cE "$ERR_RE" "$SURE" || true)"
grep -E "$SHOW_RE" "$SURE" | infilter
MY_ERR="$(grep -E "$ERR_RE" "$SURE" | infilter | wc -l | tr -d ' ')"

# csc 자체가 죽은 경우(진단 형식이 아닌 줄)도 놓치지 않는다.
FATAL="$(grep -vE '(error|warning) (CS|SOLO)[0-9]+' "$SURE" | grep -iE 'error|exception|could not' || true)"
if [ -n "$FATAL" ]; then echo "$FATAL"; MY_ERR=$((MY_ERR + 1)); fi

# 불확실한 진단 중 필터 안의 것(확실한 쪽에 이미 나온 줄은 뺀다).
UNSURE_MINE="$(grep -E "$ERR_RE" "$UNSURE" | grep -vxFf "$SURE" | infilter)"
if [ -n "$UNSURE_MINE" ]; then
  echo "---- 불확실한 진단(깨진 파일을 빼고 다시 컴파일했을 때 나온 것. 빠진 파일 때문일 수 있다) ----"
  echo "$UNSURE_MINE"
  UNVERIFIED="$UNVERIFIED\n  - 필터 안에 불확실한 진단 $(echo "$UNSURE_MINE" | wc -l | tr -d ' ')개."
fi

echo "compile.sh: 오류 $MY_ERR개(전체 $TOTAL_ERR개)${FILTER:+, 필터 '$FILTER'}, 단위:$UNIT_STATES, 출력 $(w "$OUT")"
if [ -n "$NOTES" ]; then echo -e "compile.sh: 참고$NOTES"; fi
if [ "$MY_ERR" -ne 0 ]; then exit 1; fi
if [ -n "$UNVERIFIED" ]; then
  echo "compile.sh: ******** 미확인 — 통과가 아니다 ********"
  echo -e "compile.sh: 까닭$UNVERIFIED"
  echo "compile.sh: 남의 폴더가 고쳐진 뒤 다시 돌린다. 결과 보고에는 '확인하지 못함'으로 적는다."
  exit 3
fi
exit 0
