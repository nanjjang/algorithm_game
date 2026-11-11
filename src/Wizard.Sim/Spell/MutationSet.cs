namespace Wizard.Sim.Spell
{
    // spec 04 11절. 지팡이가 가진 변이의 집합
    public readonly struct MutationSet
    {
        public readonly byte Bits;                        // 비트 번호 = (int)MutationId
        
        public MutationSet(byte bits)
        {
        Bits = bits;
        }
    }
}
