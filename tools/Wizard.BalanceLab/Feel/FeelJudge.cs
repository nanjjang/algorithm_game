using Wizard.BalanceLab.Core;

namespace Wizard.BalanceLab.Feel
{
    // 세어 놓은 값을 GDD 16절 1단계 기준으로 판정한다. 교차 곱 정수 비교라 경계(정확히 90%, 정확히 10%)가 뒤집히지 않는다
    internal static class FeelJudge
    {
        // 두 번 누르는 커맨드 성공률 ≥ 90%
        public static Verdict TwoPress(FeelResult r)
        {
            if (r.TwoPressAttempts == 0)
            {
                return Verdict.NoData;
            }
            return IntMath.AtLeastPermille(r.TwoPressSuccesses, r.TwoPressAttempts, FeelTargets.TwoPressMinPermille) ? Verdict.Pass : Verdict.Fail;
        }

        // 연습 뒤 섬 성공률 ≥ 30%
        public static Verdict Just(FeelResult r)
        {
            if (r.JustAttempts == 0)
            {
                return Verdict.NoData;
            }
            return IntMath.AtLeastPermille(r.JustSuccesses, r.JustAttempts, FeelTargets.JustMinPermille) ? Verdict.Pass : Verdict.Fail;
        }

        // 자세 중 오발 < 10%
        public static Verdict Misfire(FeelResult r)
        {
            if (r.MisfireCasts == 0)
            {
                return Verdict.NoData;
            }
            return IntMath.BelowPermille(r.Misfires, r.MisfireCasts, FeelTargets.MisfireBelowPermille) ? Verdict.Pass : Verdict.Fail;
        }
    }
}
