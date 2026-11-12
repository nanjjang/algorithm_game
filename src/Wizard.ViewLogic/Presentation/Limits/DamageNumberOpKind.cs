namespace Wizard.ViewLogic.Presentation.Limits
{
    // DamageNumberLimiter.Submit 의 결과 종류. 번호는 더하기만 한다
    public enum DamageNumberOpKind : byte
    {
        New = 0,      // 새 숫자를 띄운다
        Merged = 1,   // 같은 적의 0.2초 안 숫자에 더했다(그 숫자를 Amount 로 고쳐 그린다)
    }
}
