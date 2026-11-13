using System;
using Wizard.Core;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57), spec 02 6.3. 불변(컴파일러가 채운 뒤 바꾸지 않는다). 원천에서 다시 만들 수 있는 캐시일 뿐 세계 상태가 아니다
    public sealed class CompiledSpell
    {
        public bool IsValid;                                            // 오류가 하나라도 있으면 false(출발 불가)
        public int FireIntervalTicks;                                   // 코어 블록의 간격(G 4)
        public SpellScope[] Scopes = Array.Empty<SpellScope>();          // 유효하면 길이 1~4, Scopes[d].Depth == d
        public int HeatPerShotHu;                                       // 7절. 거울상 +50% 전의 값
        public int FireHeatHu;                                          // J 발사 1회가 더하는 열: 거울상 변이가 있으면 배수(HT-7) 뒤, 없으면 HeatPerShotHu
        public int InstancesTotal, InstancesTotalMirrored;              // 가지치기 전 트리 실행 1회의 투사체 수(거울상 없음 / 거울상)
        public int MaxDepthNormal, MaxDepthMirrored;                    // 상한 가지치기 뒤 실행할 가장 깊은 스코프
        public Fix64 StandardT;                                         // spec 02 14.2(컴파일 시점의 값: 강화 +0, 변이·캐릭터 배율 1)
        public CompileDiagnostic[] Diagnostics = Array.Empty<CompileDiagnostic>();   // 슬롯 오름차순, 같으면 코드 오름차순
        public CompiledSlot[] Slots = Array.Empty<CompiledSlot>();       // 슬롯 0..L(유효하지 않으면 비어 있다)
        public ulong Hash;                                              // 원천의 정규 바이트 해시(15절)

        // 가지치기 뒤 트리 실행 1회가 만드는 투사체 수(spec 02 14.4 "발사 1회 = 투사체 N개", 14.5 count / mirror_count).
        // 할당하지 않는다(실행기가 부른다)
        public int ExecutedCount(bool mirrored)
        {
            if (!IsValid)
            {
                return 0;
            }
            int maxDepth = mirrored ? MaxDepthMirrored : MaxDepthNormal;
            long sum = 0L;
            for (int d = 0; d <= maxDepth; d++)
            {
                sum += Scopes[d].InstancesTotal;
            }
            if (mirrored)
            {
                sum += sum;   // 거울상은 앞 묶음과 뒤 묶음(6.4 InstancesTotalMirrored)
            }
            return FixMath.SaturateToInt(sum);
        }
    }
}
