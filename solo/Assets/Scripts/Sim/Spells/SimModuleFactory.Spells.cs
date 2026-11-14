namespace Wizard.Solo.Sim
{
    // 등록 자리(00 문서 2.1절): 주문 모듈 구현을 끼운다.
    public static partial class SimModuleFactory
    {
        static partial void CreateSpells(ref SpellModule m) { m = new Spells.SpellSystem(); }
    }
}
