namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 발밑 별 그리기: 켜진 별 수와 속 빈 고리 수(spec 04 15.12절 156)
    public readonly struct WandFootStarsView
    {
        public readonly int LitStars;
        public readonly int HollowRings;

        public WandFootStarsView(int litStars, int hollowRings)
        {
            LitStars = litStars;
            HollowRings = hollowRings;
        }
    }
}
