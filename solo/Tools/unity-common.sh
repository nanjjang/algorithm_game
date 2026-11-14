#!/usr/bin/env bash
# Unity 배치 모드 래퍼들이 같이 쓰는 것. 직접 부르지 않고 source 한다.
# - Unity 배치 모드는 이 프로젝트에 한 번에 한 프로세스만 뜬다(Temp/UnityLockfile). 이미 떠 있으면 끝 코드 2로 거절한다.
# - 로그와 결과는 solo/Logs/unity/ 아래(절대 경로)로 간다. Logs/는 git이 무시한다.
# - Unity 자리는 환경 변수 UNITY_EXE로 바꿀 수 있다.

SOLO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EXE="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.3.20f1/Editor/Unity.exe}"
UNITY_LOG_DIR="$SOLO_ROOT/Logs/unity"

winpath() {
  if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi
}

unity_precheck() {
  if [ ! -x "$UNITY_EXE" ] && [ ! -f "$UNITY_EXE" ]; then
    echo "unity: Unity.exe를 찾지 못했다: $UNITY_EXE (UNITY_EXE로 지정)" >&2
    return 2
  fi
  if [ -e "$SOLO_ROOT/Temp/UnityLockfile" ]; then
    # 잠금 파일이 남아 있어도 프로세스가 없으면(비정상 종료) 그냥 간다.
    # 에디터는 "Unity.exe"(대문자 U). Unity Hub의 "unity.exe serve"는 세지 않는다(대소문자를 가린다).
    if tasklist 2>/dev/null | grep -q '^Unity\.exe'; then
      echo "unity: 이 프로젝트를 연 Unity가 이미 떠 있다(Temp/UnityLockfile). 한 번에 한 프로세스만 띄운다." >&2
      return 2
    fi
  fi
  mkdir -p "$UNITY_LOG_DIR"
  return 0
}

# 로그에서 컴파일 오류 줄만 뽑는다(같은 줄이 여러 번 찍히므로 한 번씩만).
unity_compile_errors() {
  grep -E "error CS[0-9]+|error SOLO[0-9]+" "$1" 2>/dev/null | sort -u
}

# (python이 있어야 한다.) 테스트 결과 XML(NUnit3)을 읽어 요약 한 줄과 실패 목록을 찍는다. 끝 코드: 0 = 전부 통과(1개 이상 돎), 1 = 실패 있음, 3 = 결과 없음·0개.
unity_test_summary() {
  local xml="$1"
  if [ ! -s "$xml" ]; then
    echo "unity-test: 결과 파일이 없다: $xml (테스트가 돌지 않았다 — 로그의 컴파일 오류를 본다)" >&2
    return 3
  fi
  python - "$xml" <<'PY'
import sys, xml.etree.ElementTree as ET
p = sys.argv[1]
try:
    root = ET.parse(p).getroot()
except Exception as e:
    print("unity-test: 결과 파일을 읽지 못했다: %s" % e); sys.exit(3)
g = lambda k: int(root.attrib.get(k, "0") or 0)
total, passed, failed, skipped, inconc = g("total"), g("passed"), g("failed"), g("skipped"), g("inconclusive")
out = getattr(sys.stdout, "buffer", None)
def w(s):
    if out: out.write((s + "\n").encode("utf-8", "replace"))
    else: print(s)
for tc in root.iter("test-case"):
    r = tc.attrib.get("result", "")
    if r == "Failed":
        msg = tc.findtext("failure/message") or ""
        st = tc.findtext("failure/stack-trace") or ""
        w("FAILED %s" % tc.attrib.get("fullname", tc.attrib.get("name", "?")))
        for line in msg.strip().splitlines()[:12]: w("    " + line)
        for line in st.strip().splitlines()[:4]: w("      " + line.strip())
    elif r in ("Skipped", "Inconclusive"):
        w("%s %s — %s" % (r.upper(), tc.attrib.get("fullname", "?"), (tc.findtext("reason/message") or "").strip()[:200]))
w("unity-test: total=%d passed=%d failed=%d skipped=%d inconclusive=%d result=%s  (%s)" %
  (total, passed, failed, skipped, inconc, root.attrib.get("result", "?"), p))
if total == 0: sys.exit(3)
sys.exit(1 if failed > 0 else 0)
PY
}
