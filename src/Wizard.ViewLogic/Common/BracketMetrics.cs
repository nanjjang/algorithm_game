namespace Wizard.ViewLogic.Common
{
    // 선택 괄호 한 크기의 치수(spec 14 4.3절 표). ArmPx = ㄱ자 팔 길이, SpritePx = K-40 모서리 그림 한 변
    public readonly struct BracketMetrics
    {
        public readonly BracketSize Size;
        public readonly int ArmPx;
        public readonly int SpritePx;

        public BracketMetrics(BracketSize size, int armPx, int spritePx)
        {
            Size = size;
            ArmPx = armPx;
            SpritePx = spritePx;
        }
    }
}
