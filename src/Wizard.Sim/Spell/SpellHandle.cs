namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4, 0.3. 스냅숏이 값 복사가 되게 참조 대신 정수 핸들이다
    public readonly struct SpellHandle
    {
        public readonly int Value;                        // 세계의 SpellTable 색인(추가 순서, 0부터)
        
        public SpellHandle(int value)
        {
        Value = value;
        }
    }
}
