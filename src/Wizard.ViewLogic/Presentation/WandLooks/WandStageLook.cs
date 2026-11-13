using System.Collections.Generic;
using Wizard.ViewLogic.Common;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // (종류, 단계)마다 하나(spec 04 12.3절 StageAppearance). 코드에 단계별 분기를 쓰지 않는다(Wc 8, Wm 7.4).
    // 점은 손 그림 칸(32×64) 안의 px: 머리 1점, 변이 표식 2점(wand.2 는 3점), 슬롯 확장 표식 1점(spec 14 5.4절).
    // 단계마다 한 벌이고 방향·자세별로 따로 없다(C-21) → [임시] 모든 방향·자세에 같은 점, 왼쪽은 WandLookSheet.MirrorX
    public sealed class WandStageLook
    {
        public WandStageLook(int stage, PixelPoint head, IReadOnlyList<PixelPoint> mutations, PixelPoint expansion,
            WandParticleRequest particles, WandSimplestDecor duelDecor, WandEnhanceVariant enhance)
        {
            Stage = stage;
            Head = head;
            Mutations = mutations;
            Expansion = expansion;
            Particles = particles;
            DuelDecor = duelDecor;
            Enhance = enhance;
        }

        public int Stage { get; }

        public PixelPoint Head { get; }

        public IReadOnlyList<PixelPoint> Mutations { get; }

        public PixelPoint Expansion { get; }

        public WandParticleRequest Particles { get; }

        // 결투 상대 화면용 탄 장식 가장 단순한 형태(단계마다 하나, DECISIONS 2.7)
        public WandSimplestDecor DuelDecor { get; }

        public WandEnhanceVariant Enhance { get; }
    }
}
