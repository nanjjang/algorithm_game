namespace Wizard.Perf
{
    // 항목의 좋은 쪽. 회귀 판정(15%)과 한도의 방향이 이것을 따른다
    internal enum Better
    {
        Lower,     // 시간, 할당: 한도는 상한, 기준선 × 1.15 를 넘으면 회귀
        Higher,    // 속도: 한도는 하한, 기준선 × 0.85 보다 작으면 회귀
    }
}
