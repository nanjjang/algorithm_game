namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 탄 장식 묶음의 효과 열쇠("<묶음>/<열쇠>", spec 14 5.6절). 결투 상대 화면의 가장 단순한 형태(DECISIONS 2.7,
    // effects.md 17절)는 총구 섬광 그림 1장과 최소형 적중만 남기고 꼬리·흘림·바닥 흔적을 쓰지 않는다 → WandLookFx.DuelOpponent
    public sealed class WandDecor
    {
        public WandDecor(string trail, string muzzle, string hit, string foot)
        {
            Trail = trail;
            Muzzle = muzzle;
            Hit = hit;
            Foot = foot;
        }

        public string Trail { get; }

        public string Muzzle { get; }

        public string Hit { get; }

        public string Foot { get; }

        public static AssetKey FxKey(string fx) => new AssetKey(AssetPack.Fx, fx);
    }
}
