using System;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 코어 값을 읽어 그리는 지팡이 표시(spec 04 12.7절). 입자가 아니라서 "지팡이 효과" 설정과 무관하게 그린다
    public static class WandProjectileMarks
    {
        // 탄의 각주 점 = ScopeDepth(1~3). 각주 점이 없는 지팡이는 0. "지팡이 효과 최소"에서도 남는다(15.12절 155)
        public static int FootnoteDots(WandLookSet set, int scopeDepth)
        {
            if (set.FootnoteDotsMax <= 0 || scopeDepth <= 0)
            {
                return 0;
            }
            return Math.Min(scopeDepth, set.FootnoteDotsMax);
        }

        // 발밑 별 = 결정 수(최대 7), 빚만큼 속 빈 고리(최대 2). 그 단계가 아니면 그리지 않는다(15.12절 156)
        public static WandFootStarsView FootStars(WandLookSet set, int stage, int crystals, int debt)
        {
            if (!set.FootStars.HasValue || set.FootStars.Value.Stage != stage)
            {
                return default;
            }
            WandFootStars f = set.FootStars.Value;
            return new WandFootStarsView(Clamp(crystals, f.MaxStars), Clamp(debt, f.MaxDebtRings));
        }

        private static int Clamp(int n, int max) => Math.Max(0, Math.Min(n, max));
    }
}
