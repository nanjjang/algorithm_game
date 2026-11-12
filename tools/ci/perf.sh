#!/bin/bash
# ci-stage: integration
# ci-timeout: 1800
# 성능 게이트(PIPELINE 7.1 G8, 7.5. M1-PERF-01): Release 로 빌드한 tests/Wizard.Perf 가 틱 시간(평균, p99), Step 할당, 스냅숏 저장·복원
# (시간, 할당), 재실행 속도를 재서 7.5절의 한도와 tests/perf/baseline.json(15% 넘게 나빠지면 실패)에 대 본다.
#
#   tools/ci/perf.sh                                      재서 견준다. 결과: artifacts/perf/results.json
#   tools/ci/perf.sh compare <결과 파일>                   재지 않고 결과 파일을 기준선에 대 본다
#   tools/ci/perf.sh baseline [--allow-slower --reason <글>]  기준선을 다시 잰다(마일스톤 게이트를 통과할 때. 한도를 넘으면 쓰지 않고,
#                                                         있던 기준선보다 느린 항목이 있으면 tech-lead 의 승인과 이유가 있어야 쓴다)
#
# 재는 기기는 이 맥이다(PIPELINE 7.5). 다른 일이 많이 도는 동안(부하 평균이 높을 때) 시간 값은 흔들린다. 부하 평균을 결과에 남긴다.
# 기준선 파일이 없거나 Release 빌드가 아니면 건너뛰지 않고 실패한다. 끝 코드는 Wizard.Perf 의 것(0 통과, 1 한도·회귀, 2 사용법·환경).
set -u
case "$0" in */*) _here="${0%/*}" ;; *) _here="." ;; esac
. "$_here/lib/common.sh"
ROOT="$(ci_root_from "$0")" || exit 2
ci_need_dotnet

BASELINE="$ROOT/tests/perf/baseline.json"
OUT_DIR="$ROOT/artifacts/perf"
DLL="$ROOT/artifacts/bin/Wizard.Perf/release/Wizard.Perf.dll"

echo "==> dotnet build tests/Wizard.Perf -c Release"
dotnet build "$ROOT/tests/Wizard.Perf/Wizard.Perf.csproj" -c Release -nologo -v quiet \
    || ci_die "tests/Wizard.Perf 를 Release 로 빌드하지 못했다"
[ -f "$DLL" ] || ci_die "빌드 출력이 없다: $DLL"
/bin/mkdir -p "$OUT_DIR"

# 재기 전에 부하 평균을 남긴다(재지 않는 compare 에는 쓰지 않는다)
note_load() {
    local load ncpu busy
    load="$(/usr/sbin/sysctl -n vm.loadavg 2>/dev/null || echo '?')"
    ncpu="$(/usr/sbin/sysctl -n hw.ncpu 2>/dev/null || echo '?')"
    ci_note "load average $load on $ncpu logical cpus (a busy machine makes the timing noisy)"
    busy="$(echo "$load $ncpu" | /usr/bin/awk '{ gsub(/[{}]/, ""); if ($1 + 0 > $NF + 0) print "yes" }')"
    if [ "$busy" = "yes" ]; then
        # 판정은 그대로 한다(건너뛰지 않는다). 회귀로 실패하면 조용한 기기에서 다시 돌려 확인하라는 뜻을 남긴다
        ci_note "WARNING: the 1-minute load average is above the logical cpu count. A timing regression here may be noise: rerun on an idle machine"
    fi
}

mode="${1:-check}"
[ $# -gt 0 ] && shift
case "$mode" in
    check)
        [ $# -eq 0 ] || ci_die "usage: $0 [compare <results.json> | baseline [--allow-slower --reason <why>]]"
        [ -f "$BASELINE" ] || ci_die "기준선이 없다: $BASELINE (tools/ci/perf.sh baseline 으로 잰다)"
        note_load
        dotnet "$DLL" check --baseline "$BASELINE" --out "$OUT_DIR/results.json"
        ;;
    compare)
        [ $# -eq 1 ] || ci_die "usage: $0 compare <results.json>"
        [ -f "$BASELINE" ] || ci_die "기준선이 없다: $BASELINE"
        dotnet "$DLL" compare --baseline "$BASELINE" --results "$1"
        ;;
    baseline)
        note_load
        dotnet "$DLL" baseline --out "$BASELINE" "$@"
        ;;
    *)
        ci_die "usage: $0 [compare <results.json> | baseline [--allow-slower --reason <why>]]"
        ;;
esac
