namespace Wizard.ViewLogic.Assets
{
    // MAN-wd detachedEffects 의 열쇠 하나가 가리키는 지팡이와 레지스트리 변형(spec 14 5.4절 DetachedEffect)
    public readonly struct WandDetachedAlias
    {
        public readonly string WandId;
        public readonly string Variant;

        public WandDetachedAlias(string wandId, string variant)
        {
            WandId = wandId;
            Variant = variant;
        }
    }
}
