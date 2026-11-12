namespace Wizard.ViewLogic.Presentation.Limits
{
    // HitSparkMerger 의 판정. 번호는 더하기만 한다
    public enum HitSparkKind : byte
    {
        Normal = 0,      // 지팡이 단계의 적중 장식을 그대로 요청한다
        MergedBig = 1,   // 큰 불꽃 하나(입자 8)로 바꿔 낸다
        Absorbed = 2,    // 이미 낸 큰 불꽃에 합쳐졌다. 새 입자를 내지 않는다
    }
}
