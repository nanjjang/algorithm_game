using Wizard.Content;

namespace Wizard.Sim.Arts
{
    // CONTRACTS 3.5. 필드는 spec 03 7.4절 그대로이고 ArtId 만 2.1절의 정본을 쓴다
    public readonly struct ArtDecision
    {
        public readonly ArtId Art;                        // 나머지 필드는 spec 03 7.4절 그대로(M1-SIM-08)
    }
}
