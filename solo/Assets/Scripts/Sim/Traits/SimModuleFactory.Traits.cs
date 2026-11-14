namespace Wizard.Solo.Sim
{
    /// <summary>G04 등록 자리(00 문서 2.1절): 캐릭터 패시브·지팡이 특성·변이.</summary>
    public static partial class SimModuleFactory
    {
        static partial void CreateTraits(ref TraitModule m) { m = new Traits.TraitSystem(); }
    }
}
