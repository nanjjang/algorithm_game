namespace Wizard.Solo.Meta
{
    /// <summary>정산 서비스 등록(00 문서 2.1절). 재료 추첨의 난수는 기기 난수다(판 씨앗과 무관).</summary>
    public static partial class MetaFactory
    {
        static partial void CreateSettlement(GameContext ctx, ref ISettlementService s)
        {
            s = new Settlement.SettlementService(ctx);
        }
    }
}
