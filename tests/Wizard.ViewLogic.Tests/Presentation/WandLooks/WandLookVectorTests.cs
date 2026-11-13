using System;
using System.Linq;
using Wizard.ViewLogic.Presentation.Limits;
using Wizard.ViewLogic.Presentation.WandLooks;
using Wizard.ViewLogic.Tests.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.WandLooks
{
    // spec 04 15.12절 겉모습 자료 벡터 149~156. 기대값은 명세의 then 절에서 옮겼다
    public sealed class WandLookVectorTests
    {
        private static WandLookTable T => WandLooksRepo.Table;

        // HSV(도, %, %) → 0xRRGGBB. 벡터 150 의 색을 명세의 HSV 값으로 만든다
        private static int Hsv(int hDeg, int sPercent, int vPercent)
        {
            double v = vPercent / 100.0, s = sPercent / 100.0;
            double c = v * s, hp = hDeg / 60.0, x = c * (1 - Math.Abs(hp % 2 - 1)), m = v - c;
            (double r, double g, double b) = hp < 1 ? (c, x, 0.0) : hp < 2 ? (x, c, 0.0) : hp < 3 ? (0.0, c, x) : hp < 4 ? (0.0, x, c) : hp < 5 ? (x, 0.0, c) : (c, 0.0, x);
            int R = (int)Math.Round((r + m) * 255), G = (int)Math.Round((g + m) * 255), B = (int)Math.Round((b + m) * 255);
            return (R << 16) | (G << 8) | B;
        }

        private static string Hex(int rgb) => "#" + rgb.ToString("X6");

        [Fact]
        public void S04_N149__EveryStageRequestWithinTierCap()
        {
            // 모든 칸이 구간 상한 이하(예: wand.4 +13 대기 18 ≤ 60, wand.2 +5 흘림 1 ≤ 1)
            ParticleBudgetTable budget = ParticleBudgetJson.Table;
            foreach (WandLookSet s in T.Sets)
            {
                Assert.Empty(WandLookChecks.CheckBase(s, budget));
            }
            Assert.Equal(18, T.Get(4).Stage(13).Particles.Idle);
            Assert.True(18 <= budget.TierCap(13).Idle);
            Assert.Equal(60, budget.TierCap(13).Idle);
            Assert.Equal(1, T.Get(2).Stage(5).Particles.Shed);
            Assert.Equal(1, budget.TierCap(5).ShedPerBullet);

            // 상한을 넘는 요청은 등록 검사가 잡는다(+5 흘림 2 > 1)
            var problems = new System.Collections.Generic.List<string>();
            WandLookChecks.CheckRequest("probe", 5, new WandParticleRequest(0, 0, 2, 0, 0, true), budget, problems);
            Assert.Single(problems);
        }

        [Fact]
        public void S04_N150__SkinWithDangerColorRejected()
        {
            int danger = Hsv(0, 80, 90);
            Assert.True(FlashColor.FromRgb(danger).IsDanger);
            WandLookLoadResult r = WandLooksRepo.LoadWithSkins(1,
                "[{ \"skin_id\": 7, \"source\": \"season\", \"account_bound\": false, \"palette\": [\"#3A4B5C\", \"" + Hex(danger) + "\"], \"per_stage\": [] }]");
            Assert.Contains(r.Problems, p => p.Contains("danger color", StringComparison.Ordinal));
            Assert.Empty(r.Table.Get(1).Skins);   // 거부된 덧씌움은 표에 없다 → 기본 겉모습
            Assert.Null(r.Table.Resolve(new WandLookKey(1, 7, 3)).Skin);

            // 대조: 위험색이 없으면 받아들인다
            WandLookLoadResult ok = WandLooksRepo.LoadWithSkins(1,
                "[{ \"skin_id\": 7, \"source\": \"season\", \"account_bound\": false, \"palette\": [\"#3A4B5C\"], \"per_stage\": [] }]");
            Assert.Empty(ok.Problems);
            Assert.NotNull(ok.Table.Resolve(new WandLookKey(1, 7, 3)).Skin);
        }

        [Fact]
        public void S04_N151__PatchCarryingStage13ArtForStage10Rejected()
        {
            WandLookLoadResult r = WandLooksRepo.LoadWithSkins(2,
                "[{ \"skin_id\": 3, \"source\": \"record\", \"account_bound\": true, \"per_stage\": [{ \"stage\": 10, \"silhouette_stage\": 13 }] }]");
            Assert.Contains(r.Problems, p => p.Contains("silhouette", StringComparison.Ordinal));
            Assert.Empty(r.Table.Get(2).Skins);

            WandLookLoadResult ok = WandLooksRepo.LoadWithSkins(2,
                "[{ \"skin_id\": 3, \"source\": \"record\", \"account_bound\": true, \"per_stage\": [{ \"stage\": 10, \"silhouette_stage\": 10, \"ornament\": \"gold_leaf\" }] }]");
            Assert.Empty(ok.Problems);
        }

        [Fact]
        public void S04_N152__PerStageOnlyPlus5FallsBackToBasePlus6()
        {
            WandLookLoadResult r = WandLooksRepo.LoadWithSkins(5,
                "[{ \"skin_id\": 4, \"source\": \"season\", \"account_bound\": false, \"per_stage\": [{ \"stage\": 5, \"silhouette_stage\": 5, \"ornament\": \"moon_tassel\" }] }]");
            Assert.Empty(r.Problems);
            WandLookTable t = r.Table;

            WandLookResolved six = t.Resolve(new WandLookKey(5, 4, 6));
            Assert.NotNull(six.Skin);
            Assert.Null(six.Patch);
            Assert.Same(t.Get(5).Stage(6), six.Base);
            Assert.Null(six.Ornament);
            WandLookResolved baseSix = t.Resolve(new WandLookKey(5, WandLookKey.DefaultSkin, 6));
            Assert.Equal(baseSix.HandSprite.Id, six.HandSprite.Id);
            Assert.Equal(baseSix.HandSprite.Variant, six.HandSprite.Variant);
            Assert.Equal(t.Get(5).Sheet.HandRect(6, Wizard.ViewLogic.Assets.SpriteFacing.Down, Wizard.ViewLogic.Assets.WandPose.Hold),
                         six.Set.Sheet.HandRect(six.Stage, Wizard.ViewLogic.Assets.SpriteFacing.Down, Wizard.ViewLogic.Assets.WandPose.Hold));

            WandLookResolved five = t.Resolve(new WandLookKey(5, 4, 5));
            Assert.NotNull(five.Patch);
            Assert.Equal("moon_tassel", five.Ornament);
        }

        [Fact]
        public void S04_N153__DuelOpponentSeesSimplestDecor()
        {
            ParticleRequestPolicy policy = WandLooksRepo.Policy;
            // 꼬리 0, 흘림 0, 적중 입자 2, 대기 = 요청량의 절반(wand.2 +13 → 6, wand.1 +13 → 5, wand.3 +13 → 3)
            (int kind, int idle)[] cases = { (2, 6), (1, 5), (3, 3) };
            foreach ((int kind, int idle) in cases)
            {
                WandFxAmounts a = WandLookFx.DuelOpponent(policy, T.Resolve(new WandLookKey(kind, 0, 13)), WandFxLevel.All);
                Assert.Equal(0, a.TrailPx);
                Assert.Null(a.TrailFx);
                Assert.Equal(0, a.ShedPerBullet);
                Assert.Equal(2, a.Hit);
                Assert.Equal(idle, a.Idle);
                Assert.Equal(0, a.Muzzle);
                Assert.Equal(WandLookFx.SimplestMuzzleFrames, a.MuzzleFrames);
                Assert.False(a.GroundMarks);
                Assert.True(a.BodyVisible);
            }

            // 단계마다 같은 가장 단순한 형태(DECISIONS 2.7): 어느 지팡이·단계에서도 꼬리·흘림 0, 적중 2 이하, 총구 섬광 1장
            foreach (WandLookSet s in T.Sets)
            {
                for (int stage = 0; stage < 14; stage++)
                {
                    WandFxAmounts a = WandLookFx.DuelOpponent(policy, T.Resolve(new WandLookKey(s.WandKind, 0, stage)), WandFxLevel.All);
                    Assert.Equal(0, a.TrailPx);
                    Assert.Equal(0, a.ShedPerBullet);
                    Assert.True(a.Hit <= 2);
                    Assert.Equal(s.Decor.Muzzle, a.MuzzleFx);
                    Assert.Equal(1, a.MuzzleFrames);
                }
            }

            // 치장의 꼬리 덧씌움도 상대 화면에서는 쓰지 않는다
            WandLookLoadResult skinned = WandLooksRepo.LoadWithSkins(2,
                "[{ \"skin_id\": 9, \"source\": \"trade\", \"account_bound\": false, \"trail_override\": \"wand/trail_ice\", \"per_stage\": [] }]");
            Assert.Empty(skinned.Problems);
            WandFxAmounts sk = WandLookFx.DuelOpponent(policy, skinned.Table.Resolve(new WandLookKey(2, 9, 13)), WandFxLevel.All);
            Assert.Null(sk.TrailFx);
            Assert.Equal(6, sk.Idle);
        }

        [Fact]
        public void S04_N154__PlazaNinthNearestHasNoIdleButBodyAndBlinkVisible()
        {
            ParticleRequestPolicy policy = WandLooksRepo.Policy;
            WandLookResolved look = T.Resolve(new WandLookKey(2, 0, 13));
            WandFxAmounts ninth = WandLookFx.Plaza(policy, look, 9, WandFxLevel.All, OthersWandFx.All);
            Assert.Equal(0, ninth.Idle);
            Assert.True(ninth.BodyVisible);
            Assert.True(ninth.LightBlinkVisible);
            // 대조: 8번째까지는 절반(12 → 6)
            Assert.Equal(6, WandLookFx.Plaza(policy, look, 8, WandFxLevel.All, OthersWandFx.All).Idle);
        }

        [Fact]
        public void S04_N155__FootnoteDotsFollowScopeDepthEvenAtMinimal()
        {
            WandLookSet galpi = T.Get(3);
            for (int depth = 0; depth <= 3; depth++)
            {
                Assert.Equal(depth, WandProjectileMarks.FootnoteDots(galpi, depth));
            }
            // "지팡이 효과 최소": 입자는 0 이 되지만 각주 점은 설정과 무관하다
            WandFxAmounts minimal = WandLookFx.Self(WandLooksRepo.Policy, T.Resolve(new WandLookKey(3, 0, 13)), WandFxLevel.Minimal, 0);
            Assert.Equal(0, minimal.Idle);
            Assert.Equal(3, WandProjectileMarks.FootnoteDots(galpi, 3));
            // 다른 지팡이는 각주 점이 없다
            Assert.Equal(0, WandProjectileMarks.FootnoteDots(T.Get(1), 2));
        }

        [Fact]
        public void S04_N156__Wand5Plus13FootStarsAndDebtRings()
        {
            WandLookSet s = T.Get(5);
            WandFootStarsView a = WandProjectileMarks.FootStars(s, 13, 5, 0);
            Assert.Equal(5, a.LitStars);
            Assert.Equal(0, a.HollowRings);
            WandFootStarsView b = WandProjectileMarks.FootStars(s, 13, 0, 2);
            Assert.Equal(0, b.LitStars);
            Assert.Equal(2, b.HollowRings);
            // +13 이 아니면 그리지 않는다
            Assert.Equal(0, WandProjectileMarks.FootStars(s, 12, 5, 0).LitStars);
        }

        [Fact]
        public void ShopSkinsAreRejected()
        {
            // 상점 출처는 값만 정의하고 항목 0개(spec 04 12.3절 5, D 1.14)
            WandLookLoadResult r = WandLooksRepo.LoadWithSkins(4,
                "[{ \"skin_id\": 2, \"source\": \"shop\", \"account_bound\": false, \"per_stage\": [] }]");
            Assert.Contains(r.Problems, p => p.Contains("shop", StringComparison.Ordinal));
            Assert.Empty(r.Table.Get(4).Skins);
        }

        [Fact]
        public void DuelOpponentDecorIdentifierExistsForEveryStage()
        {
            // 수락 조건 3: 결투 상대 화면용 "탄 장식 가장 단순한 형태" 식별자가 단계마다 있다(DECISIONS 2.7).
            // 효과 매니페스트에 있는 열쇠이고, 다섯 지팡이·모든 단계에서 같은 꼴(총구 섬광 + 적중, 꼬리 없음)이다
            foreach (WandLookSet s in T.Sets)
            {
                for (int stage = 0; stage < 14; stage++)
                {
                    WandSimplestDecor d = s.Stage(stage).DuelDecor;
                    Assert.True(Wizard.ViewLogic.Tests.Assets.RepoAssets.Registry.Contains(WandDecor.FxKey(d.Muzzle)), d.Muzzle);
                    Assert.True(Wizard.ViewLogic.Tests.Assets.RepoAssets.Registry.Contains(WandDecor.FxKey(d.Hit)), d.Hit);
                    Assert.Equal("wand/muzzle_" + s.Element, d.Muzzle);
                    Assert.Equal("player/hit_" + s.Element, d.Hit);
                    WandFxAmounts a = WandLookFx.DuelOpponent(WandLooksRepo.Policy, T.Resolve(new WandLookKey(s.WandKind, 0, stage)), WandFxLevel.All);
                    Assert.Equal(d.Muzzle, a.MuzzleFx);
                    Assert.Equal(d.Hit, a.HitFx);
                    Assert.Null(a.TrailFx);
                }
            }
        }

        [Fact]
        public void DuelDecorDifferingByStageIsRejected()
        {
            string text = WandLooksRepo.FileText(3);
            int at = text.IndexOf("\"stage\": 7,",StringComparison.Ordinal);
            int muzzle = text.IndexOf("wand/muzzle_arcane", at, StringComparison.Ordinal);
            string broken = text.Substring(0, muzzle) + "wand/muzzle_fire" + text.Substring(muzzle + "wand/muzzle_arcane".Length);
            WandLookLoadResult r = WandLookLoader.Load(new[] { new System.Collections.Generic.KeyValuePair<string, string>("wand_3.json", broken) }, ParticleBudgetJson.Table);
            Assert.Contains(r.Problems, p => p.Contains("duel_decor", StringComparison.Ordinal));
        }
    }
}
