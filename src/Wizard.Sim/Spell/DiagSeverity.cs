namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57), spec 02 6.3. 오류가 하나라도 있으면 CompiledSpell.IsValid == false(출발 불가)
    public enum DiagSeverity : byte
    {
        Warning = 1,
        Error = 2,
    }
}
