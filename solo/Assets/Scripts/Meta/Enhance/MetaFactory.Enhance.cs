namespace Wizard.Solo.Meta
{
    /// <summary>G12 등록(00 문서 2.1절): 강화 서비스.</summary>
    public static partial class MetaFactory
    {
        static partial void CreateEnhance(GameContext ctx, ref IEnhanceService s)
        {
            s = new global::Wizard.Solo.Meta.Enhance.EnhanceService(ctx);
        }
    }
}
