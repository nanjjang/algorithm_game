using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 지팡이 한 종류(wand.N)의 겉모습 자료 전부(data/presentation/wand_looks/wand_N.json, spec 04 12절).
    // 성장 축·+13 발밑 효과·슬롯 확장 표식의 생김새는 식별자만 둔다(그림은 나중에 꽂는다)
    public sealed class WandLookSet
    {
        public WandLookSet(int wandKind, string element, string growthAxis, string footFx13, string slotMarker, WandLookSheet sheet,
            WandDecor decor, IReadOnlyList<WandDetachedRange> detached, int footnoteDotsMax, WandFootStars? footStars,
            IReadOnlyList<WandStageLook> stages, IReadOnlyList<WandSkin> skins)
        {
            if (stages.Count != WandLookSheet.StageCount)
            {
                throw new ArgumentException("a wand look set needs one entry per stage", nameof(stages));
            }
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i].Stage != i)
                {
                    throw new ArgumentException("stages must be ordered 0..13", nameof(stages));
                }
            }
            WandKind = wandKind;
            Element = element;
            GrowthAxis = growthAxis;
            FootFx13 = footFx13;
            SlotMarker = slotMarker;
            Sheet = sheet;
            Decor = decor;
            Detached = detached;
            FootnoteDotsMax = footnoteDotsMax;
            FootStars = footStars;
            Stages = stages;
            Skins = skins;
        }

        public const string WandIdPrefix = "wand.";

        public int WandKind { get; }

        // 콘텐츠 id "wand.N" = 그림 레지스트리의 Wand 묶음 Id(spec 14 5.7절)
        public string WandId => WandIdFor(WandKind);

        // 정식 속성 키(fire, lightning, arcane, ice, dark). 효과 묶음의 접미사이고 강화 의식의 속성별 변주를 고른다(effects.md 5절)
        public string Element { get; }

        public string GrowthAxis { get; }

        public string FootFx13 { get; }

        public string SlotMarker { get; }

        public WandLookSheet Sheet { get; }

        public WandDecor Decor { get; }

        public IReadOnlyList<WandDetachedRange> Detached { get; }

        // 탄의 각주 점 최대 수(0 = 이 지팡이는 각주 점이 없다)
        public int FootnoteDotsMax { get; }

        public WandFootStars? FootStars { get; }

        public IReadOnlyList<WandStageLook> Stages { get; }

        public IReadOnlyList<WandSkin> Skins { get; }

        public WandStageLook Stage(int stage)
        {
            if (stage < 0 || stage >= Stages.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(stage));
            }
            return Stages[stage];
        }

        public WandSkin? Skin(int skinId)
        {
            if (skinId == WandLookKey.DefaultSkin)
            {
                return null;
            }
            foreach (WandSkin s in Skins)
            {
                if (s.SkinId == skinId)
                {
                    return s;
                }
            }
            return null;
        }

        public static string WandIdFor(int wandKind) => WandIdPrefix + wandKind.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
