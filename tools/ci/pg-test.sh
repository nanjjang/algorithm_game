#!/bin/bash
# ci-stage: full
# ci-timeout: 1800
# 서버 통합 테스트의 PostgreSQL(DECISIONS-TECH T-38). Docker 의 postgres:17 고정 이미지(판 번호와 다이제스트로 고정)를
# 작업 트리마다 컨테이너 하나로, 127.0.0.1 의 빈 포트에 띄운다. 테스트는 테스트마다 임시 데이터베이스를 만들고 지운다
# (tests/Wizard.Server.Tests/Infra/PgTestDatabase.cs).
#
#   tools/ci/pg-test.sh               ci 단계(full): Docker 확인 → 이 작업 트리의 DB → tests/Wizard.Server.Tests 를 Release 로 돌린다
#   tools/ci/pg-test.sh up [--print]  컨테이너를 띄우거나 떠 있는 것을 쓴다. --print 면 관리 연결 문자열 한 줄만 stdout 에 쓴다
#                                     (테스트가 WIZARD_PG_ADMIN 없이 돌면 이것을 부른다)
#   tools/ci/pg-test.sh down          이 작업 트리의 컨테이너를 지운다
#   tools/ci/pg-test.sh status        이 작업 트리의 컨테이너 상태와 포트
#
# Docker 가 없거나 돌고 있지 않으면 건너뛰지 않고 무엇이 없는지와 사용자가 할 일을 적고 1 로 끝난다.
# 데이터는 컨테이너 안의 tmpfs 에만 있다(지워도 되는 시험용). 비밀번호는 컨테이너를 만들 때 무작위로 정하고 저장소에 남기지 않는다.
# 지워진 작업 트리의 컨테이너는 up 이 치운다. bash 3.2 문법만 쓴다(T-40).
#
# 바꿀 수 있는 것: WIZARD_DOCKER(docker 실행 파일), WIZARD_PG_IMAGE(이미지. 기본은 아래 고정 값)
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/lib/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2

# postgres:17-alpine 의 17.11 판. 태그가 움직여도 다이제스트가 판을 고정한다(T-38. 배치용과 주 판 번호 17 을 맞춘다)
PG_IMAGE="${WIZARD_PG_IMAGE:-postgres:17-alpine@sha256:18cfe3ef5e6815560c98237d6216d1e5119702fb0f3894c8785dd58b8bbe5d73}"
PG_LABEL="wizard.pg-test"
PG_NAME="wizard-pg-$(printf '%s' "$ROOT" | /usr/bin/shasum -a 256 | /usr/bin/cut -c1-12)"

say() { echo "pg-test: $*" >&2; }

find_docker() {
    if [ -n "${WIZARD_DOCKER:-}" ]; then
        [ -x "$WIZARD_DOCKER" ] && { echo "$WIZARD_DOCKER"; return 0; }
        return 1
    fi
    local c
    c="$(command -v docker 2>/dev/null)"
    if [ -n "$c" ] && [ -x "$c" ]; then
        echo "$c"
        return 0
    fi
    for c in /usr/local/bin/docker /opt/homebrew/bin/docker /Applications/Docker.app/Contents/Resources/bin/docker; do
        [ -x "$c" ] && { echo "$c"; return 0; }
    done
    return 1
}

# Docker 확인. ci 안에서는 Wizard.Ci 의 공통 확인(ci_require)을 쓰고, 테스트가 부를 때는 같은 내용을 여기서 직접 본다
require_docker() {
    if [ -n "${WIZARD_CI_DLL:-}" ] && [ -f "${WIZARD_CI_DLL}" ]; then
        ci_require docker
    fi
    DOCKER="$(find_docker)" || {
        echo "FAIL  Docker 가 없다(WIZARD_DOCKER, PATH, /usr/local/bin, /opt/homebrew/bin, /Applications/Docker.app 에서 docker 를 찾지 못했다). 사용자가 할 일: Docker Desktop 을 설치하고 띄운다(서버 통합 테스트의 PostgreSQL 이 Docker 에서 돈다. T-38)" >&2
        exit 1
    }
    if ! "$DOCKER" info >/dev/null 2>&1; then
        echo "FAIL  Docker 가 돌고 있지 않다($DOCKER info 실패). 사용자가 할 일: Docker Desktop 을 띄운다(open -a Docker) 뒤 다시 돌린다" >&2
        exit 1
    fi
}

# 작업 트리가 지워진 컨테이너를 치운다
gc_stale() {
    local line name tree
    "$DOCKER" ps -a --filter "label=$PG_LABEL=1" --format '{{.Names}}|{{.Label "wizard.worktree"}}' 2>/dev/null | while IFS= read -r line; do
        name="${line%%|*}"
        tree="${line#*|}"
        if [ -n "$name" ] && [ -n "$tree" ] && [ ! -d "$tree" ]; then
            say "지워진 작업 트리($tree)의 컨테이너 $name 를 치운다"
            "$DOCKER" rm -f "$name" >/dev/null 2>&1 || true
        fi
    done
}

container_state() {
    "$DOCKER" inspect -f '{{.State.Running}}' "$PG_NAME" 2>/dev/null
}

start_container() {
    local state pw
    state="$(container_state)"
    if [ "$state" = "true" ]; then
        return 0
    fi
    if [ -n "$state" ]; then
        "$DOCKER" start "$PG_NAME" >/dev/null || { echo "FAIL  컨테이너 $PG_NAME 를 다시 띄우지 못했다" >&2; exit 1; }
        return 0
    fi
    pw="$(/usr/bin/openssl rand -hex 16)" || { echo "FAIL  무작위 비밀번호를 만들지 못했다(/usr/bin/openssl)" >&2; exit 1; }
    say "컨테이너 $PG_NAME 를 띄운다($PG_IMAGE)"
    if ! "$DOCKER" run -d --name "$PG_NAME" \
            --label "$PG_LABEL=1" --label "wizard.worktree=$ROOT" \
            -e "POSTGRES_PASSWORD=$pw" \
            -p 127.0.0.1::5432 \
            --tmpfs /var/lib/postgresql/data:rw \
            "$PG_IMAGE" \
            -c fsync=off -c synchronous_commit=off -c full_page_writes=off -c max_connections=400 >/dev/null; then
        # 같은 작업 트리의 다른 테스트 실행이 막 만들었으면 그것을 쓴다
        state="$(container_state)"
        [ -n "$state" ] || { echo "FAIL  컨테이너 $PG_NAME 를 만들지 못했다($DOCKER run 실패. 이미지 $PG_IMAGE)" >&2; exit 1; }
        [ "$state" = "true" ] || "$DOCKER" start "$PG_NAME" >/dev/null || { echo "FAIL  컨테이너 $PG_NAME 를 띄우지 못했다" >&2; exit 1; }
    fi
}

# 초기화 중의 임시 서버는 TCP 를 듣지 않는다. 127.0.0.1 로 응답하면 진짜 서버가 뜬 것이다
wait_ready() {
    local i=0
    while [ $i -lt 120 ]; do
        if "$DOCKER" exec "$PG_NAME" pg_isready -h 127.0.0.1 -p 5432 -U postgres -q >/dev/null 2>&1; then
            return 0
        fi
        i=$((i + 1))
        sleep 0.5
    done
    echo "FAIL  컨테이너 $PG_NAME 의 PostgreSQL 이 60초 안에 뜨지 않았다($DOCKER logs $PG_NAME 를 본다)" >&2
    exit 1
}

conn_string() {
    local port pw
    port="$("$DOCKER" port "$PG_NAME" 5432/tcp 2>/dev/null | /usr/bin/head -1 | /usr/bin/sed 's/.*://')"
    pw="$("$DOCKER" inspect -f '{{range .Config.Env}}{{println .}}{{end}}' "$PG_NAME" | /usr/bin/sed -n 's/^POSTGRES_PASSWORD=//p')"
    [ -n "$port" ] && [ -n "$pw" ] || { echo "FAIL  컨테이너 $PG_NAME 의 포트나 비밀번호를 읽지 못했다" >&2; exit 1; }
    echo "Host=127.0.0.1;Port=$port;Username=postgres;Password=$pw;Database=postgres"
}

up() {
    require_docker
    gc_stale
    start_container
    wait_ready
}

MODE="${1:-stage}"
case "$MODE" in
    up)
        up
        if [ "${2:-}" = "--print" ]; then
            conn_string
        else
            say "떠 있다: $PG_NAME ($("$DOCKER" port "$PG_NAME" 5432/tcp | /usr/bin/head -1))"
        fi
        ;;
    down)
        require_docker
        "$DOCKER" rm -f "$PG_NAME" >/dev/null 2>&1 && say "컨테이너 $PG_NAME 를 지웠다" || say "컨테이너 $PG_NAME 가 없다"
        ;;
    status)
        require_docker
        state="$(container_state)"
        if [ -z "$state" ]; then
            echo "$PG_NAME: 없음"
        else
            echo "$PG_NAME: running=$state port=$("$DOCKER" port "$PG_NAME" 5432/tcp 2>/dev/null | /usr/bin/head -1)"
        fi
        ;;
    stage)
        ci_need_dotnet
        up
        WIZARD_PG_ADMIN="$(conn_string)" || exit 1
        export WIZARD_PG_ADMIN
        /bin/mkdir -p "$ROOT/artifacts/ci"
        LOG="$ROOT/artifacts/ci/pg-test.log"
        dotnet test "$ROOT/tests/Wizard.Server.Tests/Wizard.Server.Tests.csproj" -c Release -nologo 2>&1 | /usr/bin/tee "$LOG"
        code=${PIPESTATUS[0]}
        [ "$code" -eq 0 ] || exit "$code"
        if /usr/bin/grep -E 'Skipped: +[1-9]' "$LOG" >/dev/null; then
            echo "FAIL  skipped tests found" >&2
            exit 1
        fi
        ;;
    *)
        echo "usage: $0 [up [--print] | down | status]" >&2
        exit 2
        ;;
esac
