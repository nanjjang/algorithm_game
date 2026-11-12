using Wizard.BalanceLab.Core;
using Wizard.Content;

namespace Wizard.BalanceLab.Feel
{
    // 화력 비 한 번의 측정과 판정
    internal sealed class FireRatioReport
    {
        public FireRatioReport(BotRun hold, BotRun crit, BlockId core, bool splitThree)
        {
            Hold = hold;
            Crit = crit;
            Core = core;
            SplitThree = splitThree;
        }

        public BotRun Hold { get; }

        public BotRun Crit { get; }

        public BlockId Core { get; }

        public bool SplitThree { get; }

        public bool Measurable => Hold.Cycles > 0 && Crit.Cycles > 0 && Hold.WindowDamageRaw > 0 && Crit.WindowDamageRaw > 0;

        // 임계 구간 DPS ÷ 꾹 누르기 DPS, 천분율(내림). 재지 못했으면 0
        public long RatioPermille => Measurable
            ? IntMath.RatePermilleFloor(Crit.WindowDamageRaw, Crit.WindowTicks, Hold.WindowDamageRaw, Hold.WindowTicks)
            : 0;

        public Verdict Verdict
        {
            get
            {
                if (!Measurable)
                {
                    return Verdict.NoData;
                }
                return IntMath.RateAtLeastPermille(Crit.WindowDamageRaw, Crit.WindowTicks, Hold.WindowDamageRaw, Hold.WindowTicks,
                                                    FeelTargets.FireRatioMinPermille) ? Verdict.Pass : Verdict.Fail;
            }
        }
    }
}
