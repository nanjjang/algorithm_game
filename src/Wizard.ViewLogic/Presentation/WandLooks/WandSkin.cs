using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 덧씌움 하나(spec 04 12.3절 SkinOverride, O 7.9 skin_id). 색과 장식만 바꾸고 단계 실루엣·단계 표식은 가리지 못한다(DECISIONS 2.7).
    // 능력치·판정·강화 확률과 무관하다. 겉모습 id 를 읽는 곳은 "어떤 그림 묶음을 불러올지" 한 곳(WandLookTable.Resolve)뿐이다
    public sealed class WandSkin
    {
        public WandSkin(int skinId, int wandKind, WandSkinSource source, bool accountBound, IReadOnlyList<int> palette,
            string? ornament, string? trailOverride, string? enhanceFxOverride, string? soundTimbre, IReadOnlyList<WandStagePatch> perStage)
        {
            SkinId = skinId;
            WandKind = wandKind;
            Source = source;
            AccountBound = accountBound;
            Palette = palette;
            Ornament = ornament;
            TrailOverride = trailOverride;
            EnhanceFxOverride = enhanceFxOverride;
            SoundTimbre = soundTimbre;
            PerStage = perStage;
        }

        public int SkinId { get; }

        public int WandKind { get; }

        public WandSkinSource Source { get; }

        public bool AccountBound { get; }

        public IReadOnlyList<int> Palette { get; }

        public string? Ornament { get; }

        public string? TrailOverride { get; }

        public string? EnhanceFxOverride { get; }

        public string? SoundTimbre { get; }

        public IReadOnlyList<WandStagePatch> PerStage { get; }

        // 없는 단계는 null → 기본 그림으로 되돌아간다(Wm 7.4)
        public WandStagePatch? PatchFor(int stage)
        {
            foreach (WandStagePatch p in PerStage)
            {
                if (p.Stage == stage)
                {
                    return p;
                }
            }
            return null;
        }
    }
}
