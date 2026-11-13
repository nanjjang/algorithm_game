using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // Resolve(k) = Base[k.WandKind][k.Stage].With(Skin(k.SkinId)?.PatchFor(k.Stage)) (spec 04 12.3절).
    // 그림 묶음은 실제 단계의 것이고 점(머리·변이·슬롯 표식)은 언제나 기본 자료의 자리다(덧씌움 위에도 같은 자리, 12.3절 1).
    // 겉모습(skin_id)별 시트는 아직 없어 그림 열쇠는 기본 시트를 가리킨다(spec 14 5.4절 [임시])
    public sealed class WandLookResolved
    {
        public const string HandVariant = "hand";
        public const string InspectionVariant = "inspection";
        public const string IconVariant = "icon";

        private static readonly IReadOnlyList<int> NoPalette = new int[0];

        public WandLookResolved(WandLookKey key, WandLookSet set, WandSkin? skin)
        {
            Key = key;
            Set = set;
            Base = set.Stage(key.Stage);
            Skin = skin;
            Patch = skin?.PatchFor(key.Stage);
        }

        public WandLookKey Key { get; }

        public WandLookSet Set { get; }

        public WandStageLook Base { get; }

        public WandSkin? Skin { get; }

        public WandStagePatch? Patch { get; }

        public int Stage => Key.Stage;

        public AssetKey HandSprite => new AssetKey(AssetPack.Wand, Set.WandId, HandVariant);

        public AssetKey InspectionSprite => new AssetKey(AssetPack.Wand, Set.WandId, InspectionVariant);

        public AssetKey IconSprite => new AssetKey(AssetPack.Wand, Set.WandId, IconVariant);

        public WandParticleRequest Particles => Patch?.Particles ?? Base.Particles;

        public IReadOnlyList<int> Palette
        {
            get
            {
                if (Patch != null && Patch.Palette.Count > 0)
                {
                    return Patch.Palette;
                }
                return Skin != null ? Skin.Palette : NoPalette;
            }
        }

        public string? Ornament => Patch?.Ornament ?? Skin?.Ornament;

        // 내 화면의 꼬리 그림. 덧씌움은 꼬리 그림만 바꾸고 탄의 속성색은 못 바꾼다(12.3절 3)
        public string TrailFx => Skin?.TrailOverride ?? Set.Decor.Trail;

        // 이 단계에서 그리는 떨어진 조각 변형
        public IEnumerable<string> DetachedVariants
        {
            get
            {
                foreach (WandDetachedRange d in Set.Detached)
                {
                    if (d.Covers(Key.Stage))
                    {
                        yield return d.Variant;
                    }
                }
            }
        }
    }
}
