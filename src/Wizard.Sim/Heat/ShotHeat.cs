using System;
using Wizard.Core;

namespace Wizard.Sim.Heat
{
    // spec 01 7.2 ShotHeatHu, spec 02 7절(HT-1~HT-7). 발사 1회의 열(HU). 순수 함수이고 할당하지 않는다.
    // 컴파일러(spec 02 6절)가 실행 트리를 이 입력으로 펴서 부르고, 야영 화면의 "발사당 열"(GDD 13)도 같은 값이다.
    //   발사 1회의 열 = 스코프별 (분열을 뺀 블록 열 합 × 그 스코프의 첫 번째 분열 배수)의 합   (HT-1, HT-3)
    //   두 번째 이후 분열은 무시한다(GDD 4 → 열에도 첫 번째 분열의 배수만, [해석])
    //   트리거 스코프 안의 블록도 발사할 때 미리 더한다(적중 여부와 무관, [해석])
    //   밑줄(갈피): 지팡이의 마지막 연결 슬롯 칸에 있는 블록의 열이 절반(올림)만큼 깎인다. 최대 깎는 양은 패시브 수치.
    //               분열 배수는 깎은 뒤에 곱한다(HT-5). 그 칸이 비어 있으면 아무것도 깎지 않는다
    //   거울상: 발사당 열 × 거울상 배수(천분율, 1500)                                        (HT-7)
    public static class ShotHeat
    {
        public const int NoUnderline = -1;       // underlineSlot: 밑줄 없음
        public const int NoMirrorPermille = 1000; // mirrorPermille: 거울상 없음(×1.0)

        private const long PermilleOne = 1000L;  // 천분율 1.0(단위 환산)

        // underlineSlot = 밑줄이 깎는 칸(지팡이의 마지막 연결 슬롯 번호), 밑줄이 없으면 NoUnderline.
        // underlineMaxCutHu = 깎는 양의 상한(HU, 원문 5 → 600). mirrorPermille = 1000(없음) 또는 거울상 배수
        public static int ShotHeatHu(ReadOnlySpan<ShotHeatBlock> blocks, int underlineSlot, int underlineMaxCutHu, int mirrorPermille)
        {
            if (mirrorPermille < 0 || underlineMaxCutHu < 0)
            {
                throw new SimInvariantException("ShotHeat: negative multiplier or cut");
            }
            int maxScope = -1;
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i].Scope < 0 || blocks[i].HeatHu < 0 || blocks[i].SplitMulPermille < 0)
                {
                    throw new SimInvariantException("ShotHeat: negative scope, heat or split multiplier");
                }
                if (blocks[i].Scope > maxScope)
                {
                    maxScope = blocks[i].Scope;
                }
            }
            long total = 0L;
            for (int scope = 0; scope <= maxScope; scope++)
            {
                long sum = 0L;
                long mul = PermilleOne;
                bool splitSeen = false;
                for (int i = 0; i < blocks.Length; i++)
                {
                    ref readonly ShotHeatBlock b = ref blocks[i];
                    if (b.Scope != scope)
                    {
                        continue;
                    }
                    if (b.SplitMulPermille > 0)
                    {
                        if (!splitSeen)
                        {
                            splitSeen = true;
                            mul = b.SplitMulPermille;
                        }
                        continue;
                    }
                    int h = b.HeatHu;
                    if (underlineSlot != NoUnderline && b.Slot == underlineSlot)
                    {
                        h -= UnderlineCutHu(h, underlineMaxCutHu);
                    }
                    sum += h;
                }
                total += sum * mul / PermilleOne;
            }
            total = total * mirrorPermille / PermilleOne;
            return FixMath.SaturateToInt(total);
        }

        // 밑줄이 블록 하나에서 깎는 양(spec 01 7.2, wc 4.3): 그 블록 열의 절반(열 단위로 올림), 상한 maxCutHu
        public static int UnderlineCutHu(int heatHu, int maxCutHu)
        {
            if (heatHu <= 0)
            {
                return 0;
            }
            const int HalfUnitHu = HeatRules.HuPerHeat * 2;
            int cut = (heatHu + HalfUnitHu - 1) / HalfUnitHu * HeatRules.HuPerHeat;
            return cut < maxCutHu ? cut : maxCutHu;
        }
    }
}
