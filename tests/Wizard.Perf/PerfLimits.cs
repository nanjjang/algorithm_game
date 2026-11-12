using System;
using System.Collections.Generic;

namespace Wizard.Perf
{
    // PIPELINE 7.5 의 한도(이 벤치가 재는 줄). 값은 그 표의 것이고 여기서 고치지 않는다. 표가 바뀌면 같이 바뀐다.
    //
    //   결투 틱(두 사람, 탄 300개 이하)         평균 0.2ms 이하(spec 10 14.13, spec 12 4절의 가정), p99 0.5ms 이하([총괄 제안])
    //   무한 웨이브 틱(탄과 적이 상한까지 찬 최악) p99 1.0ms 이하([총괄 제안])
    //   Step 의 관리 힙 할당                     0바이트(CONTRACTS 3.2)
    //   스냅숏 저장 + 복원                        합쳐서 0.1ms 이하, 할당 0바이트([총괄 제안])
    //   재실행 속도                               실시간의 100배 이상(spec 12 4절) = 초당 6,000틱
    //   회귀                                      어느 항목이든 tests/perf/baseline.json 보다 15% 넘게 나빠지면 실패([총괄 제안])
    //
    // M1 에서 이 줄들이 무엇을 재는가(결과 보고에도 적는다):
    //   - 결투 세계에는 아직 탄이 없다(주문 실행기 M2, 결투 모듈 M14). duel_2p 는 두 사람의 이동·자세·열·스침만 있는 결투 세계다.
    //   - 무한 웨이브 세계에는 아직 웨이브·적이 없다(M3). 최악 사례의 줄은 M1 에서 가장 무거운 세계인 시제품 연습 세계(practice_proto)와
    //     빈 무한 웨이브 세계(endless_1p) 둘에 건다. 웨이브 모듈이 들어오면 그 작업이 최악 사례 시나리오를 더한다.
    //   - 스냅숏 0.1ms 는 합(저장 + 복원) 한 쌍의 p99 에 건다(평균보다 엄격하다. 되감기 9틱이 한 프레임 안에 들어야 하는 것은 최악의 프레임이다).
    internal static class PerfLimits
    {
        public const double RegressionPermille = 150;      // 15%
        public const double PermilleScale = 1000;

        public const double DuelTickMeanMicros = 200;      // 0.2ms
        public const double DuelTickP99Micros = 500;       // 0.5ms
        public const double WorstTickP99Micros = 1000;     // 1.0ms
        public const double SnapshotPairMicros = 100;      // 0.1ms
        public const double ZeroBytes = 0;
        public const double ReplayRealtimeMultiple = 100;
        public const double TickRate = Wizard.Sim.SimVersion.TickRate;

        private const string SrcDuelMean = "spec 10 §14.13, spec 12 §4 (0.2ms)";
        private const string SrcDuelP99 = "PIPELINE 7.5 [총괄 제안] (0.5ms)";
        private const string SrcWorst = "PIPELINE 7.5 [총괄 제안] (1.0ms, 최악 사례 자리)";
        private const string SrcAlloc = "CONTRACTS §3.2 (할당 없음)";
        private const string SrcSnapshot = "PIPELINE 7.5 [총괄 제안] (합 0.1ms)";
        private const string SrcSnapshotAlloc = "CONTRACTS §3.2, PIPELINE 7.5 (할당 0)";
        private const string SrcReplay = "spec 12 §4 (실시간 × 100)";
        private const string SrcNone = "한도 없음, 기준선 대비 회귀만";

        private const string Us = "us";
        private const string Bytes = "bytes";
        private const string TicksPerSec = "ticks/s";

        public static readonly IReadOnlyList<MetricDef> All = Array.AsReadOnly(new[]
        {
            new MetricDef("tick.duel_2p.mean_us", Us, Better.Lower, DuelTickMeanMicros, SrcDuelMean),
            new MetricDef("tick.duel_2p.p99_us", Us, Better.Lower, DuelTickP99Micros, SrcDuelP99),
            new MetricDef("alloc.duel_2p.step_bytes", Bytes, Better.Lower, ZeroBytes, SrcAlloc),
            new MetricDef("tick.practice_proto.mean_us", Us, Better.Lower, null, SrcNone),
            new MetricDef("tick.practice_proto.p99_us", Us, Better.Lower, WorstTickP99Micros, SrcWorst),
            new MetricDef("alloc.practice_proto.step_bytes", Bytes, Better.Lower, ZeroBytes, SrcAlloc),
            new MetricDef("tick.endless_1p.mean_us", Us, Better.Lower, null, SrcNone),
            new MetricDef("tick.endless_1p.p99_us", Us, Better.Lower, WorstTickP99Micros, SrcWorst),
            new MetricDef("alloc.endless_1p.step_bytes", Bytes, Better.Lower, ZeroBytes, SrcAlloc),
            new MetricDef("snapshot.duel_2p.save_load_mean_us", Us, Better.Lower, null, SrcNone),
            new MetricDef("snapshot.duel_2p.save_load_p99_us", Us, Better.Lower, SnapshotPairMicros, SrcSnapshot),
            new MetricDef("snapshot.duel_2p.alloc_bytes", Bytes, Better.Lower, ZeroBytes, SrcSnapshotAlloc),
            new MetricDef("snapshot.practice_proto.save_load_mean_us", Us, Better.Lower, null, SrcNone),
            new MetricDef("snapshot.practice_proto.save_load_p99_us", Us, Better.Lower, SnapshotPairMicros, SrcSnapshot),
            new MetricDef("snapshot.practice_proto.alloc_bytes", Bytes, Better.Lower, ZeroBytes, SrcSnapshotAlloc),
            new MetricDef("replay.endless_1p.ticks_per_sec", TicksPerSec, Better.Higher, ReplayRealtimeMultiple * TickRate, SrcReplay),
        });

        public static MetricDef? Find(string id)
        {
            foreach (MetricDef d in All)
            {
                if (string.Equals(d.Id, id, StringComparison.Ordinal))
                {
                    return d;
                }
            }
            return null;
        }
    }
}
