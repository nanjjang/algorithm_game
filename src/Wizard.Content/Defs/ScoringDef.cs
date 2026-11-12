namespace Wizard.Content
{
    // CONTRACTS 2.2. spec 07 12.1~12.2(웨이브 점수의 항목과 정수 원점수). 팩에서 읽은 뒤 바뀌지 않는다. 원본 data/sim/scoring.json.
    // 웨이브 계수 S(w) = 1 + 0.1 × (w − 1) 은 10배 정수 S10(w) = WaveCoefTenthsAtWave1 + WaveCoefTenthsPerWave × (w − 1) (= 9 + w)로 든다.
    // 걸린 시간은 틱이다(빠른 처리 = max(0, 목표 틱 − 걸린 틱) × FastPerSecond ÷ 60).
    // 여기 없는 것과 그 주인: 지체(120초) 뒤 처치의 절반 → WaveCurveDef.StallKillScorePct, 변덕 점수 배율 → ModifierDef,
    // 서버 즉시 검사 상한(spec 07 12.3) → 규칙 쪽 RunLimitsTable(data/rules/limits.json).
    public sealed class ScoringDef
    {
        public int WaveCoefTenthsAtWave1;                   // 10 (S(1) = 1.0)
        public int WaveCoefTenthsPerWave;                   // 1 (웨이브마다 +0.1)
        public int KillPerCost;                             // 10. 처치 원점수 = 비용 × 10 (엘리트는 비용이 이미 ×3)
        public int BossKill;                                // 3000
        public int WaveClear;                               // 200
        public int FastPerSecond;                           // 2. 빠른 처리 = 남은 초 × 2
        public int FastTargetTicks, FastTargetTicksBoss;    // 3600(60초), 7200(120초)
        public int NoHit;                                   // 100
        public int GrazePer;                                // 5. 스침 1회당
        public int GrazeCap, GrazeCapBoss;                  // 20, 40 (한 웨이브에서 세는 스침 수의 상한)
        public int PhantomFirst;                            // 3648. 허상과의 첫 만남 웨이브의 고정 점수(다른 항목 없음, = 3200 × S(10) × 0.6)
    }
}
