using System;
using Wizard.Core;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4. 불변. 원천에서 다시 만들 수 있는 캐시일 뿐 세계 상태가 아니다
    public sealed class CompiledSpell
    {
        public bool IsValid;
        public int FireIntervalTicks;
        public SpellScope[] Scopes = Array.Empty<SpellScope>();          // 길이 1~4
        public int HeatPerShotHu;                                       // 거울상 +50% 전의 값
        public int InstancesTotal, InstancesTotalMirrored, MaxDepthNormal, MaxDepthMirrored;
        public Fix64 StandardT;                                         // spec 02 14.2
        public CompileDiagnostic[] Diagnostics = Array.Empty<CompileDiagnostic>();
        public ulong Hash;
    }
}
