#!/bin/bash
# tools/ci 의 공통 함수. 검사 스크립트와 훅이 source 한다(단계 머리말이 없으므로 ci.sh 가 검사로 부르지 않는다).
# bash 3.2 문법만 쓴다(연관 배열, mapfile, ${x,,}, timeout, flock 없음. T-40). 로직은 tools/Wizard.Ci 에 있다.
#
#   ci_path <경로>            경로를 실행기가 넘기는 꼴(T-67 4번)로 바꾼다: cygpath 가 있으면(Git for Windows) cygpath -m(/d/x → D:/x), 없으면(맥) 그대로
#   ci_root_from <파일>      그 파일이 놓인 저장소의 뿌리(tools/ci/… 또는 tools/ci/lib/… 의 위치에서 구한다. 현재 폴더에 기대지 않는다). ci_path 를 거친 꼴
#   ci_os                    macos | windows | other (uname -s 로)
#   ci_sha256                표준 입력의 SHA-256(16진 소문자 한 줄). shasum -a 256 > sha256sum > openssl dgst -sha256 가운데 있는 것(T-67 8번)
#   ci_is_exe <경로>          실행 파일인가(-x. Windows 에서는 .exe·.cmd·.bat 도 실행 파일로 친다)
#   ci_tool <명령> …          Wizard.Ci 를 부른다. ci.sh 안에서는 WIZARD_CI_DLL(이미 빌드된 것), 밖에서는 artifacts/ci-runner 에 빌드해서
#   ci_note <말>              검사 결과(artifacts/ci/<단계>/<이름>.json 의 notes)에 한 줄을 남긴다
#   ci_skip <이유>            건너뜀으로 기록하고 0 으로 끝난다. 바뀐 경로가 그 검사에 닿지 않을 때만 쓴다.
#                             필요한 것이 기기에 없을 때는 쓰지 않는다(ci_require 로 실패한다)
#   ci_require <무엇> …       unity|il2cpp|x64-dotnet|docker 가 없으면 무엇이 없는지와 사용자가 할 일을 적고 1 로 끝난다

ci_path() {
    if command -v /usr/bin/cygpath >/dev/null 2>&1; then
        /usr/bin/cygpath -m "$1"
    else
        printf '%s\n' "$1"
    fi
}

ci_os() {
    case "$(/usr/bin/uname -s 2>/dev/null)" in
        Darwin) echo macos ;;
        MINGW*|MSYS*|CYGWIN*) echo windows ;;
        *) echo other ;;
    esac
}

ci_sha256() {
    if command -v /usr/bin/shasum >/dev/null 2>&1; then
        /usr/bin/shasum -a 256 | /usr/bin/cut -d' ' -f1
    elif command -v /usr/bin/sha256sum >/dev/null 2>&1; then
        /usr/bin/sha256sum | /usr/bin/cut -d' ' -f1
    elif command -v /usr/bin/openssl >/dev/null 2>&1; then
        /usr/bin/openssl dgst -sha256 -r | /usr/bin/cut -d' ' -f1
    else
        echo "ci: SHA-256 도구가 없다(/usr/bin/shasum, /usr/bin/sha256sum, /usr/bin/openssl)" >&2
        return 1
    fi
}

ci_is_exe() {
    [ -x "$1" ] && return 0
    if [ -f "$1" ] && [ "$(ci_os)" = "windows" ]; then
        case "$1" in
            *.exe|*.cmd|*.bat|*.EXE|*.CMD|*.BAT) return 0 ;;
        esac
    fi
    return 1
}

ci_root_from() {
    local f="$1" d
    case "$f" in
        */*) d="${f%/*}" ;;
        *) d="." ;;
    esac
    d="$(cd "$d" && pwd -P)" || return 1
    while [ "$d" != "/" ]; do
        if [ -f "$d/Wizard.sln" ] && [ -f "$d/tools/ci/ci.sh" ]; then
            ci_path "$d"
            return 0
        fi
        d="${d%/*}"
        [ -n "$d" ] || d="/"
    done
    echo "ci: 저장소 뿌리(Wizard.sln + tools/ci/ci.sh)를 $1 위에서 찾지 못했다" >&2
    return 1
}

ci_die() {
    echo "FAIL  $*" >&2
    exit 1
}

ci_need_dotnet() {
    command -v dotnet >/dev/null 2>&1 || ci_die "dotnet 이 없다. 사용자가 할 일: .NET SDK(global.json 의 판)를 설치한다"
}

# Wizard.Ci 를 artifacts/ci-runner 에 빌드한다(솔루션 빌드의 출력과 섞이지 않게)
ci_build_runner() {
    local root="$1"
    ci_need_dotnet
    dotnet build "$root/tools/Wizard.Ci/Wizard.Ci.csproj" -c Release -nologo -v quiet \
        -p:ArtifactsPath="$root/artifacts/ci-runner" >/dev/null || ci_die "tools/Wizard.Ci 를 빌드하지 못했다(dotnet build $root/tools/Wizard.Ci)"
    echo "$root/artifacts/ci-runner/bin/Wizard.Ci/release/Wizard.Ci.dll"
}

ci_tool() {
    local dll="${WIZARD_CI_DLL:-}"
    if [ -z "$dll" ] || [ ! -f "$dll" ]; then
        dll="$(ci_build_runner "$ROOT")" || exit 1
    fi
    dotnet "$dll" "$@"
}

ci_note() {
    if [ -n "${WIZARD_CI_NOTE:-}" ]; then
        echo "$*" >> "$WIZARD_CI_NOTE"
    fi
    echo "note: $*"
}

ci_skip() {
    if [ -n "${WIZARD_CI_NOTE:-}" ]; then
        echo "skipped: $*" >> "$WIZARD_CI_NOTE"
    fi
    echo "skipped: $*"
    exit 0
}

ci_require() {
    ci_tool require "$@" --root "$ROOT" || exit 1
}

# Unity 를 띄우는 검사의 공통 앞부분: 바뀐 경로가 Unity 에 닿지 않으면 건너뛰고(full 만. 게이트는 건너뛰지 않는다), 편집기가 없으면 실패한다.
# 작업 트리에 unity/Library 가 없고 메인 체크아웃에 있으면 APFS 복제(cp -c)로 가져온다(PIPELINE 4.3. 첫 가져오기를 줄인다. 커밋하지 않는다).
# APFS 복제는 macOS 에서만 한다. 다른 OS 에서는 가져오지 않고 그 사실을 적는다(통째 복사는 작업 트리마다 수 GB 다. T-67 8번).
ci_unity_prepare() {
    local scope
    if [ "${1:-}" = "scoped" ]; then
        scope="$(ci_tool unity-scope --root "$ROOT")"
        case $? in
            0) ci_note "Unity 단계를 돈다: ${scope#needed: }" ;;
            3) ci_skip "${scope#skip: }" ;;
            *) ci_die "unity-scope 실패: $scope" ;;
        esac
    fi
    ci_require unity
    if [ ! -d "$ROOT/unity/Library" ]; then
        local common main
        common="$(command git -C "$ROOT" rev-parse --path-format=absolute --git-common-dir 2>/dev/null)" || common=""
        main="${common%/.git}"
        [ -n "$common" ] && main="$(ci_path "$main")"
        if [ -n "$common" ] && [ "$main" != "$ROOT" ] && [ -d "$main/unity/Library" ]; then
            if [ "$(ci_os)" = "macos" ]; then
                echo "ci: $main/unity/Library 를 APFS 복제로 가져온다"
                /bin/cp -c -R "$main/unity/Library" "$ROOT/unity/Library" || echo "ci: Library 복제 실패. Unity 가 처음부터 가져온다" >&2
            else
                ci_note "unity/Library 를 가져오지 않았다(APFS 복제는 macOS 에서만 한다. 통째 복사는 작업 트리마다 수 GB 다): Unity 가 처음부터 가져온다"
            fi
        fi
    fi
    UNITY_SH="$ROOT/unity/Assets/Editor/Ci/Launcher~/unity.sh"
    [ -f "$UNITY_SH" ] || ci_die "Unity 실행 스크립트가 없다: $UNITY_SH"
}
