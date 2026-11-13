namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 결투 상대 화면에서 내 탄 장식의 가장 단순한 형태(DECISIONS 2.7, effects.md 17절): 총구 섬광 그림 1장(Muzzle 의 첫 장)과
    // 최소형 적중(Hit, 입자 2). 꼬리 띠·흘리는 입자·총구 입자·바닥 흔적은 없다. 상대의 단계나 치장과 무관하게 같다
    public sealed class WandSimplestDecor
    {
        public WandSimplestDecor(string muzzle, string hit)
        {
            Muzzle = muzzle;
            Hit = hit;
        }

        public string Muzzle { get; }

        public string Hit { get; }
    }
}
