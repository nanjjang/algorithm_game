using System.Collections.Generic;
using Wizard.ViewLogic.Common;

namespace Wizard.ViewLogic.Assets
{
    // 지팡이 단계 하나의 점(손 그림 32×64 칸 안 px). spec 14 5.4절 Stages:
    // 머리 1점, 변이 표식 2점(wand.2 는 3점, TV-207), 슬롯 확장 표식 1점.
    // 단계마다 한 벌이고 방향·자세별로 따로 없다(C-21) → [임시] 모든 방향·자세에 같은 점, 왼쪽은 WandSheet.MirrorX
    public sealed class WandStagePoints
    {
        public WandStagePoints(int stage, PixelPoint head, IReadOnlyList<PixelPoint> mutations, PixelPoint expansion)
        {
            Stage = stage;
            Head = head;
            Mutations = mutations;
            Expansion = expansion;
        }

        public int Stage { get; }

        public PixelPoint Head { get; }

        public IReadOnlyList<PixelPoint> Mutations { get; }

        public PixelPoint Expansion { get; }
    }
}
