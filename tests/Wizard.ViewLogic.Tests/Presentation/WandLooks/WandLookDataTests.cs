using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;
using Wizard.ViewLogic.Presentation.WandLooks;
using Wizard.ViewLogic.Tests.Assets;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.WandLooks
{
    // 수락 조건 1: (지팡이 종류, 겉모습 id, 단계) → 그림 묶음 열쇠와 점(머리, 변이 표식, 슬롯 표식)이 다섯 지팡이 × 14단계에 있다.
    // 기대값은 명세 표(spec 04 12.1·12.5절, effects.md 4절, spec 04 18.3절)와 그림 매니페스트의 현물에서 옮긴다
    public sealed class WandLookDataTests
    {
        private static WandLookTable T => WandLooksRepo.Table;

        // spec 04 12.5절 "지팡이별 단계 요청량(대기 / 총구 / 흘림 / 적중 / 꼬리px)" 표를 그대로 붙였다
        private const string Spec04Table12_5 = @"
| +0 | 0/1/0/0/0 | 0/0/0/2/0 | 0/1/0/2/0 | 1/0/0/2/0 | 0/0/0/0/0 |
| +1 | 1/1/0/0/4 | 0/0/0/2/0 | 1/1/0/2/잔상 | 2/0/0/3/6 | 1/0/0/0/0 |
| +2 | 2/2/0/1/6 | 1/0/0/2/0 | 0/1/0/3/6 | 2/0/0/3/6 | 1/0/0/0/4 |
| +3 | 2/2/0/2/6 | 1/0/0/3/6 | 1/2/0/3/8 | 3/0/0/4/6 | 2/0/0/0/4 |
| +4 | 3/3/0/3/8 | 2/0/0/4/12 | 1/2/0/4/8 | 4/0/0/5/8 | 3/0/0/1/6 |
| +5 | 4/3/0/3/8 | 2/0/**1**/4/12 | 1/3/0/4/10 | 6/0/0/5/12 | 4/0/0/1/6 |
| +6 | 4/3/0/4/10 | 3/0/0/4/12 | 1/3/0/4/10 | 7/0/0/5/12 | 5/0/0/2/8 |
| +7 | 5/4/0/5/10 | 4/0/0/5/18 | 2/4/0/5/16 | 6/0/0/6/12 | 6/0/0/2/8 |
| +8 | 5/4/0/5/10 | 5/0/0/5/18 | 3/4/0/6/16 | 7/0/0/6/16 | 7/0/0/3/8 |
| +9 | 6/4/0/6/12 | 6/0/0/6/18 | 3/4/0/6/16 | 8/0/0/6/16 | 8/0/0/3/8 |
| +10 | 7/5/0/6/12 | 8/0/0/6/24 | 4/5/0/6/16 | 8/0/0/7/16 | 10/0/0/3/8 |
| +11 | 8/5/0/7/14 | 8/0/0/6/24 | 4/5/0/7/20 | 12/**2**/0/7/16 | 10/0/0/4/— |
| +12 | 9/5/0/7/14 | 10/0/0/7/24 | 5/5/0/7/20 | 14/2/0/7/20 | 12/0/0/4/— |
| +13 | 10/6/0/8/16 | 12/0/0/8/32 | 6/6/0/8/24 | 18/2/0/8/20 | 16/0/0/6/— |";

        public static IEnumerable<object[]> Kinds() => Enumerable.Range(1, 5).Select(k => new object[] { k });

        [Fact]
        public void LoadsFiveWandsWithoutProblems()
        {
            Assert.Empty(WandLooksRepo.Result.Problems);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, T.Sets.Select(s => s.WandKind).ToArray());
            foreach (WandLookSet s in T.Sets)
            {
                Assert.Equal(14, s.Stages.Count);
                Assert.Empty(s.Skins);   // 겉모습 = 기본 하나 [임시] (spec 14 5.4절)
            }
        }

        [Fact]
        public void ParticleRequestsMatchSpec04Table12_5RowByRow()
        {
            string[] rows = Spec04Table12_5.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(14, rows.Length);
            for (int stage = 0; stage < 14; stage++)
            {
                string[] cells = rows[stage].Split('|', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToArray();
                Assert.Equal("+" + stage, cells[0]);
                for (int kind = 1; kind <= 5; kind++)
                {
                    string[] v = cells[kind].Replace("**", "").Split('/');
                    WandParticleRequest p = T.Get(kind).Stage(stage).Particles;
                    string at = "wand." + kind + " +" + stage;
                    Assert.True(int.Parse(v[0]) == p.Idle, at + " idle");
                    Assert.True(int.Parse(v[1]) == p.Muzzle, at + " muzzle");
                    Assert.True(int.Parse(v[2]) == p.Shed, at + " shed");
                    Assert.True(int.Parse(v[3]) == p.Hit, at + " hit");
                    if (int.TryParse(v[4], out int trail))
                    {
                        Assert.True(p.TrailSpecified, at + " trail specified");
                        Assert.True(trail == p.TrailPx, at + " trail");
                    }
                    else
                    {
                        // "잔상"(띠 길이 아님), "—"(미기재, Q-22): 아래 단계의 값을 물려 쓴다 [임시]
                        Assert.False(p.TrailSpecified, at + " trail unspecified");
                        Assert.Equal(T.Get(kind).Stage(stage - 1).Particles.TrailPx, p.TrailPx);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void StagePointsMatchArtManifest(int kind)
        {
            // spec 14 5.4절 Stages: 단계마다 머리 1점, 변이 2점(wand.2 3점), 슬롯 확장 1점. 현물은 art/production/wands/manifest.json
            WandLookSet set = T.Get(kind);
            Assert.True(RepoAssets.Registry.TryGetWand(set.WandId, out WandSheet sheet));
            for (int stage = 0; stage < 14; stage++)
            {
                WandStageLook look = set.Stage(stage);
                WandStagePoints art = sheet.PointsOf(stage);
                Assert.Equal(art.Head, look.Head);
                Assert.Equal(art.Expansion, look.Expansion);
                Assert.Equal(art.Mutations.ToArray(), look.Mutations.ToArray());
                Assert.Equal(kind == 2 ? 3 : 2, look.Mutations.Count);
            }
            Assert.Equal(sheet.Grip, set.Sheet.Grip);
            Assert.Equal(new PixelPoint(16, 48), set.Sheet.Grip);
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void EveryKeyResolvesToRegisteredSprites(int kind)
        {
            for (int stage = 0; stage < 14; stage++)
            {
                WandLookResolved r = T.Resolve(new WandLookKey(kind, WandLookKey.DefaultSkin, stage));
                Assert.Equal(stage, r.Stage);
                Assert.Null(r.Skin);
                foreach (AssetKey k in new[] { r.HandSprite, r.InspectionSprite, r.IconSprite })
                {
                    Assert.Equal(AssetPack.Wand, k.Pack);
                    Assert.Equal("wand." + kind, k.Id);
                    Assert.True(RepoAssets.Registry.Contains(k), k.Id + "/" + k.Variant);
                }
                foreach (string v in r.DetachedVariants)
                {
                    Assert.True(RepoAssets.Registry.Contains(new AssetKey(AssetPack.Wand, "wand." + kind, v)), v);
                }
            }
        }

        [Fact]
        public void DetachedPiecesOnlyWhereSpecAllows()
        {
            // spec 04 12.1절: 뜨는 조각은 wand.5 전용, 도는 조각은 wand.4 전용. wand.2 는 +13 선 벼락(12.4절)
            Assert.Empty(T.Get(1).Detached);
            Assert.Empty(T.Get(3).Detached);
            Assert.Equal(new[] { "detached_max_lightning" }, T.Resolve(new WandLookKey(2, 0, 13)).DetachedVariants.ToArray());
            Assert.Empty(T.Resolve(new WandLookKey(2, 0, 12)).DetachedVariants);
            Assert.Equal(new[] { "detached_orbit" }, T.Resolve(new WandLookKey(4, 0, 7)).DetachedVariants.ToArray());
            Assert.Empty(T.Resolve(new WandLookKey(4, 0, 6)).DetachedVariants);
            Assert.Equal(new[] { "detached_orb" }, T.Resolve(new WandLookKey(5, 0, 10)).DetachedVariants.ToArray());
            Assert.Empty(T.Resolve(new WandLookKey(5, 0, 9)).DetachedVariants);
        }

        [Fact]
        public void GrowthAxesAndFootFxMatchSpec04Table12_1()
        {
            string[,] expected =
            {
                { "growth.weight_melt", "footfx.scorched_footprints" },
                { "growth.count_upward", "footfx.rotating_beam" },
                { "growth.unfold_sideways", "footfx.straight_underline" },
                { "growth.length_facets_orbit", "footfx.hex_frost" },
                { "growth.release_darken", "footfx.round_night" },
            };
            for (int kind = 1; kind <= 5; kind++)
            {
                Assert.Equal(expected[kind - 1, 0], T.Get(kind).GrowthAxis);
                Assert.Equal(expected[kind - 1, 1], T.Get(kind).FootFx13);
            }
        }

        [Fact]
        public void ElementsAndDecorKeysExistInEffectsManifest()
        {
            // 다섯 지팡이의 속성(world-characters 3.4, spec 14 3.2절)과 효과 묶음 wand/·player/ 의 속성 접미사(spec 14 5.6절)
            string[] elements = { "fire", "lightning", "arcane", "ice", "dark" };
            for (int kind = 1; kind <= 5; kind++)
            {
                WandLookSet s = T.Get(kind);
                Assert.Equal(elements[kind - 1], s.Element);
                foreach (string fx in new[] { s.Decor.Trail, s.Decor.Muzzle, s.Decor.Hit, s.Decor.Foot })
                {
                    Assert.True(RepoAssets.Registry.Contains(WandDecor.FxKey(fx)), fx);
                    Assert.EndsWith("_" + s.Element, fx, StringComparison.Ordinal);
                }
            }
        }

        [Fact]
        public void SlotMarkersMatchSpec04_18_3()
        {
            // SlotExtended: 구리 고리 / 민트 고리 / 색인 꼬리표 / 금 방울 / 금 가락지
            Assert.Equal(new[] { "copper_ring", "mint_ring", "index_tab", "gold_bell", "gold_band" }, T.Sets.Select(s => s.SlotMarker).ToArray());
        }

        [Fact]
        public void EnhanceVariantsMatchEffects4AndSpec04_18_3()
        {
            // effects.md 4절: 1단 +2,+3 / 2단 +4~+6 / 3단 +7~+9 / 4단 +10~+12 / 5단 +13. +1 은 2절의 예외(0), +0 은 도전 대상이 아니다
            int?[] intensity = { null, 0, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5 };
            foreach (WandLookSet s in T.Sets)
            {
                for (int stage = 0; stage < 14; stage++)
                {
                    WandEnhanceVariant e = s.Stage(stage).Enhance;
                    Assert.Equal(intensity[stage], e.Intensity);
                    WandEnhanceScene scene = stage == 4 || stage == 7 || stage == 10 ? WandEnhanceScene.Milestone
                        : stage == 13 ? WandEnhanceScene.Stage13 : WandEnhanceScene.None;
                    Assert.Equal(scene, e.Scene);
                    // StageTransition frames 6, +12→+13 은 12
                    Assert.Equal(stage == 0 ? (int?)null : stage == 13 ? 12 : 6, e.TransitionFrames);
                }
            }
        }

        [Fact]
        public void RitualElementVariantsExistInEffectsManifest()
        {
            // spec 14 5.6절 강화 의식: 속성별 seal_<el>, outer_glyph_<el>, charge_<el>, … stage_hold_<el>
            foreach (WandLookSet s in T.Sets)
            {
                foreach (string part in WandRitualFx.ElementParts)
                {
                    AssetKey k = WandRitualFx.Key(s, part);
                    Assert.True(RepoAssets.Registry.Contains(k), k.Id);
                }
            }
            Assert.Equal("ritual/seal_fire", WandRitualFx.FxId(T.Get(1), "seal"));
        }

        [Fact]
        public void LoaderRejectsBrokenFiles()
        {
            var files = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("bad.json", "{ \"wand\": \"wand.x\" }"),
                new KeyValuePair<string, string>("notjson.json", "{"),
            };
            WandLookLoadResult r = WandLookLoader.Load(files, null);
            Assert.False(r.Ok);
            Assert.Empty(r.Table.Sets);
        }
    }
}
