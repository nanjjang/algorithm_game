using Wizard.Core;

namespace Wizard.Sim.Arts
{
    // spec 03 1.2의 판정기 상수(입력 시각 단위, 1단위 = 1/15,360초)와 9.2의 빚 상한. 기획 수치라 코드에 적지 않고
    // 데이터에서 받는다(CLAUDE.md 5절). 비기·트라이 데이터 절(M5-DAT-01)이 생기기 전에는 부르는 쪽이 값을 넣는다.
    // 비교 규칙은 "경과 > 한도이면 실패, 경과 ≤ 한도이면 통과"다(spec 03 1.2).
    public readonly struct ParserTimings
    {
        public readonly long JustWindowUnits;       // 섬 창(0.07초)
        public readonly long GapFullBloomUnits;     // 만개의 각 간격(0.18초)
        public readonly long GapShortUnits;         // 앞→뒤, 옆→앞, 앞→옆, 좌→우, char.1 의 뒤→뒤(0.2초)
        public readonly long GapSameSideUnits;      // 같은 쪽 옆 두 번(0.25초)
        public readonly long GapDefaultUnits;       // 기본 간격 = 커맨드 초기화 시간(0.3초)
        public readonly long ArtCooldownUnits;      // 비기 공통 재사용 대기(0.4초)
        public readonly long KBufferWindowUnits;    // 대기 끝 전 K 보관 창(0.1초)
        public readonly int MaxDebt;                // 외상 빚 상한(결정 수)

        public ParserTimings(long justWindowUnits, long gapFullBloomUnits, long gapShortUnits, long gapSameSideUnits,
                             long gapDefaultUnits, long artCooldownUnits, long kBufferWindowUnits, int maxDebt)
        {
            if (justWindowUnits < 0 || gapFullBloomUnits < 0 || gapShortUnits < 0 || gapSameSideUnits < 0
                || gapDefaultUnits < 0 || artCooldownUnits < 0 || kBufferWindowUnits < 0 || maxDebt < 0)
            {
                throw new SimInvariantException("arts: parser timings must not be negative");
            }
            if (kBufferWindowUnits > artCooldownUnits)
            {
                throw new SimInvariantException("arts: K buffer window longer than the cooldown");
            }
            JustWindowUnits = justWindowUnits;
            GapFullBloomUnits = gapFullBloomUnits;
            GapShortUnits = gapShortUnits;
            GapSameSideUnits = gapSameSideUnits;
            GapDefaultUnits = gapDefaultUnits;
            ArtCooldownUnits = artCooldownUnits;
            KBufferWindowUnits = kBufferWindowUnits;
            MaxDebt = maxDebt;
        }
    }
}
