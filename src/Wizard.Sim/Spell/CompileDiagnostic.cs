namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57), spec 02 6.3. 진단 하나. 문구는 이름 표에서 읽는다(코드 → warn.* 열쇠는 ViewLogic)
    public readonly struct CompileDiagnostic
    {
        public readonly DiagCode Code;
        public readonly DiagSeverity Severity;
        public readonly int Slot;          // 슬롯 번호(0 = 코어, 1..L = 연결 슬롯)
        public readonly int ScopeDepth;    // 0..3

        public CompileDiagnostic(DiagCode code, DiagSeverity severity, int slot, int scopeDepth)
        {
            Code = code;
            Severity = severity;
            Slot = slot;
            ScopeDepth = scopeDepth;
        }
    }
}
