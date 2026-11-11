namespace Wizard.Rules.Enhance
{
    // CONTRACTS 4.2. +10 이상 성공과 +13 달성은 서버 전체에 알린다(DECISIONS 2.4)
    public enum NoticeKind : byte
    {
        None,
        Plus10to12,
        Plus13,
    }
}
