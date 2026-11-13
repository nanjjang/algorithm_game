namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 그림 선택 열쇠 (지팡이 종류, 겉모습 id, 단계) (spec 04 12.3절 AppearanceKey, world-characters 3.3절).
    // SkinId 0 = 기본 겉모습(덧씌움 없음). WandKind 는 wand.N 의 N(1~5), Stage 는 0~13
    public readonly struct WandLookKey
    {
        public const int DefaultSkin = 0;

        public readonly int WandKind;
        public readonly int SkinId;
        public readonly int Stage;

        public WandLookKey(int wandKind, int skinId, int stage)
        {
            WandKind = wandKind;
            SkinId = skinId;
            Stage = stage;
        }
    }
}
