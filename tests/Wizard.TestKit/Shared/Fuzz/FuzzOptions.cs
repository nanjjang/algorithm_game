using System;

namespace Wizard.TestKit.Shared.Fuzz
{
    // 퍼저 실행 설정. 씨앗은 언제나 코드에 고정된 값에서 시작한다(시각·환경 변수에서 얻지 않는다. 흔들리는 테스트 금지).
    // 실패 보고에 나온 사례 씨앗을 Replay 로 넘기면 그 사례 하나만 다시 만들어 돌린다.
    public sealed class FuzzOptions
    {
        public const int DefaultMaxShrinkSteps = 2000;

        public FuzzOptions(ulong seed, int cases)
        {
            if (cases < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cases), "at least one case");
            }
            Seed = seed;
            Cases = cases;
            MaxShrinkSteps = DefaultMaxShrinkSteps;
        }

        private FuzzOptions(ulong caseSeed)
        {
            Seed = caseSeed;
            Cases = 1;
            MaxShrinkSteps = DefaultMaxShrinkSteps;
            ReplayCaseSeed = caseSeed;
        }

        // 사례 씨앗을 뽑는 주 씨앗
        public ulong Seed { get; }

        public int Cases { get; }

        // 줄이기에서 성질을 다시 검사하는 횟수의 상한
        public int MaxShrinkSteps { get; set; }

        // 값이 있으면 그 사례 하나만 돌린다
        public ulong? ReplayCaseSeed { get; }

        public static FuzzOptions Replay(ulong caseSeed) => new FuzzOptions(caseSeed);
    }
}
