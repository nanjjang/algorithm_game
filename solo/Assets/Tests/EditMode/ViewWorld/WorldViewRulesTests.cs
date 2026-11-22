using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.View;
using Wizard.Solo.View.World;

namespace Wizard.Solo.Tests.ViewWorld
{
    /// <summary>G14의 순수 규칙. 기대값은 요약서(09 2·3·7절, 02 1절, 06 4.3절, 08 8.2·12.2·14절, 01 4.1·4.3절)에서 가져온다.</summary>
    public class WorldViewRulesTests
    {
        static WorldTuning T => WorldTuning.From(TestContent.Db);

        // ---------------------------------------------------------------- 방향 (09 1절: 왼쪽은 right를 뒤집는다)

        [TestCase(Dir8.S, Facing3.Down, false)]
        [TestCase(Dir8.N, Facing3.Up, false)]
        [TestCase(Dir8.E, Facing3.Right, false)]
        [TestCase(Dir8.W, Facing3.Right, true)]
        // 대각은 좌우 우선(G14 규칙): NE·SE 오른쪽, NW·SW 오른쪽 뒤집음.
        [TestCase(Dir8.NE, Facing3.Right, false)]
        [TestCase(Dir8.SE, Facing3.Right, false)]
        [TestCase(Dir8.NW, Facing3.Right, true)]
        [TestCase(Dir8.SW, Facing3.Right, true)]
        public void S09_Facing__Dir8ToFacing3AndFlip(Dir8 d, Facing3 expected, bool flip)
        {
            Assert.AreEqual(expected, WorldViewRules.ToFacing3(d, out bool f));
            Assert.AreEqual(flip, f);
        }

        // ---------------------------------------------------------------- 클립 표 (09 2.2절)

        static BodyInput In(Dir8 facing = Dir8.S) => new BodyInput { Facing = facing };

        [Test]
        public void S09_Clip__StateToClipTable()
        {
            var t = T;
            var idle = WorldViewRules.SelectClip(In(Dir8.S), t);
            Assert.AreEqual("idle", idle.Clip); Assert.AreEqual(Facing3.Down, idle.Facing); Assert.IsFalse(idle.FlipX);
            Assert.AreEqual(ClipPlay.Loop, idle.Play); Assert.IsFalse(idle.WandAim);

            var walkLeft = In(Dir8.W); walkLeft.Moving = true;
            var c = WorldViewRules.SelectClip(walkLeft, t);
            Assert.AreEqual("walk", c.Clip); Assert.AreEqual(Facing3.Right, c.Facing); Assert.IsTrue(c.FlipX);

            var stanceMove = In(Dir8.N); stanceMove.InStance = true; stanceMove.Moving = true;
            c = WorldViewRules.SelectClip(stanceMove, t);
            Assert.AreEqual("stance_walk", c.Clip); Assert.AreEqual(Facing3.Up, c.Facing); Assert.AreEqual(ClipPlay.Loop, c.Play);
            Assert.IsTrue(c.WandAim, "자세 중엔 지팡이를 내민다(09 3절)");

            var stanceStill = In(Dir8.E); stanceStill.InStance = true;
            c = WorldViewRules.SelectClip(stanceStill, t);
            Assert.AreEqual("stance_walk", c.Clip); Assert.AreEqual(ClipPlay.Hold, c.Play);

            var fire = In(Dir8.E); fire.FireActive = true; fire.Moving = true;
            c = WorldViewRules.SelectClip(fire, t);
            Assert.AreEqual("fire", c.Clip); Assert.AreEqual(ClipPlay.Once, c.Play); Assert.IsTrue(c.WandAim);

            var special = In(Dir8.S); special.ArtActive = true;
            Assert.AreEqual("special", WorldViewRules.SelectClip(special, t).Clip);
            special.ArtIsSignature = true;
            Assert.AreEqual("signature", WorldViewRules.SelectClip(special, t).Clip);

            var hit = In(Dir8.N); hit.HitActive = true; hit.FireActive = true;
            c = WorldViewRules.SelectClip(hit, t);
            Assert.AreEqual("hit", c.Clip); Assert.AreEqual(Facing3.Up, c.Facing);
        }

        [Test]
        public void S09_Clip__DownedAndRiseUseDeathDownOnly()
        {
            var t = T;
            var down = In(Dir8.W); down.Downed = true; down.HitActive = true; down.Moving = true;
            var c = WorldViewRules.SelectClip(down, t);
            Assert.AreEqual("death", c.Clip);
            Assert.AreEqual(Facing3.Down, c.Facing, "death는 아래 방향만 있다(09 2.2절 death_down)");
            Assert.IsFalse(c.FlipX);
            Assert.AreEqual(ClipPlay.Once, c.Play);
            Assert.IsTrue(c.HideWand, "마지막 장 = 지팡이만 남는 빈 몸");

            var rise = In(Dir8.E); rise.Rising = true;
            c = WorldViewRules.SelectClip(rise, t);
            Assert.AreEqual("death", c.Clip);
            Assert.AreEqual(ClipPlay.Reverse, c.Play);
        }

        [Test]
        public void S09_Clip__FrameIndexModes()
        {
            // fire: 3장 14fps 반복 아님(09 2.2절) → 1초 뒤 마지막 장에 머문다.
            Assert.AreEqual(0, WorldViewRules.FrameIndex(ClipPlay.Once, 3, 14f, false, 0f));
            Assert.AreEqual(2, WorldViewRules.FrameIndex(ClipPlay.Once, 3, 14f, false, 1f));
            // idle: 4장 7fps 반복 → 4/7초 뒤 다시 0.
            Assert.AreEqual(0, WorldViewRules.FrameIndex(ClipPlay.Loop, 4, 7f, true, 4f / 7f + 0.001f));
            // 일어남 = 쓰러짐(6장)을 거꾸로: 처음은 마지막 장, 끝나면 첫 장.
            Assert.AreEqual(5, WorldViewRules.FrameIndex(ClipPlay.Reverse, 6, 10f, false, 0f));
            Assert.AreEqual(0, WorldViewRules.FrameIndex(ClipPlay.Reverse, 6, 10f, false, 5f));
            Assert.AreEqual(0, WorldViewRules.FrameIndex(ClipPlay.Hold, 4, 9f, true, 3f));
        }

        // ---------------------------------------------------------------- 발 위치 (02 1절: 판정 중심 = 발밑 위 0.4칸)

        [Test]
        public void S02_Feet__CenterToViewFeet()
        {
            var t = T;
            Assert.AreEqual(0.4f, t.FeetBelowCenterCells, 1e-6f, "G14.json feet_below_center_cells = 02 1절 0.4칸");
            var v = WorldViewRules.FeetView(new Vector2(12f, 7f), t.FeetBelowCenterCells, 14f);
            Assert.AreEqual(12f, v.x, 1e-5f);
            Assert.AreEqual(6.6f, v.y, 1e-5f, "발 = (12, 7.4) → Unity y = 14 − 7.4");
        }

        [Test]
        public void S10_Q17__InterpolationBreaksOverOneCell()
        {
            var p = new PlayerState { PrevPos = new Vector2(10, 7), Pos = new Vector2(10.5f, 7) };
            Assert.AreEqual(10.25f, WorldViewRules.InterpolatedCenter(p, 0.5f).x, 1e-5f);
            p.PrevPos = new Vector2(10, 7); p.Pos = new Vector2(12, 7); // 넉백 2칸
            Assert.AreEqual(12f, WorldViewRules.InterpolatedCenter(p, 0.5f).x, 1e-5f);
        }

        [Test]
        public void S09_TV208__WandAttachAndPointMirror()
        {
            // 손 (34,44)·손잡이 (16,48) → 지팡이 칸 왼쪽 위 (18,−4)(TV-208). 손잡이 기준이면 발에서 손까지의 차이만 쓴다.
            var feet = new PixelPoint(30, 60);
            var d = WorldViewRules.PixelDelta(feet, new PixelPoint(34, 44), false);
            Assert.AreEqual(4f / 64f, d.x, 1e-6f);
            Assert.AreEqual(16f / 64f, d.y, 1e-6f);
            var m = WorldViewRules.PixelDelta(feet, new PixelPoint(34, 44), true);
            Assert.AreEqual(-4f / 64f, m.x, 1e-6f, "반전은 x 차이 부호만(09 1절)");
            // 지팡이 점: 반전 x' = 31 − x (U-04)와 손잡이 둘레 반전이 같다.
            var grip = new PixelPoint(16, 48);
            var head = new PixelPoint(20, 34);
            var a = WorldViewRules.WandPointOffset(head, grip, false);
            var b = WorldViewRules.WandPointOffset(head, grip, true);
            Assert.AreEqual(-a.x, b.x, 1e-6f);
            Assert.AreEqual(a.y, b.y, 1e-6f);
            Assert.AreEqual((WandSheet.MirrorX(20) + 0.5f - 16f) / 64f, b.x, 1e-6f);
        }

        // ---------------------------------------------------------------- 무적·피격

        [Test]
        public void S08_14_2__InvulnBlinkByEffectsLevel()
        {
            var t = T;
            // 전체: 초당 8회(한 주기 0.125초 = 앞 절반 보임, 뒤 절반 반투명), 테두리 없음.
            Assert.AreEqual(1f, WorldViewRules.InvulnAlpha(0, 0.01f, t, out float o0), 1e-6f);
            Assert.AreEqual(0f, o0);
            Assert.Less(WorldViewRules.InvulnAlpha(0, 0.07f, t, out _), 1f);
            Assert.AreEqual(1f, WorldViewRules.InvulnAlpha(0, 0.13f, t, out _), 1e-6f);
            // 줄임·최소: 깜빡임 없이 60% 투명 + 흰 테두리.
            Assert.AreEqual(0.4f, WorldViewRules.InvulnAlpha(1, 0.07f, t, out float o1), 1e-6f);
            Assert.Greater(o1, 0f);
            Assert.AreEqual(0.4f, WorldViewRules.InvulnAlpha(2, 0.5f, t, out _), 1e-6f);
        }

        [Test]
        public void S10_3_7__HitFlashLasts012Seconds()
        {
            var t = T;
            Assert.AreEqual(0.12f, t.HitFlashSeconds, 1e-6f);
            Assert.AreEqual(1f, WorldViewRules.HitFlashAmount(0f, t), 1e-6f);
            Assert.Greater(WorldViewRules.HitFlashAmount(0.1f, t), 0f);
            Assert.AreEqual(0f, WorldViewRules.HitFlashAmount(0.12f, t));
            Assert.AreEqual(0f, WorldViewRules.HitFlashAmount(-1f, t));
        }

        // ---------------------------------------------------------------- 지팡이 효과 단계 (08 12.2절, effects 9.3·18절)

        [Test]
        public void S08_12_2__WandFxLevelThreeSteps()
        {
            Assert.AreEqual(2, WorldViewRules.WandParticleBudget(OptionValues.All, 2));
            Assert.AreEqual(3, WorldViewRules.WandParticleBudget(OptionValues.All, 3));
            Assert.AreEqual(1, WorldViewRules.WandParticleBudget(OptionValues.Reduced, 2), "줄임 = 절반");
            Assert.AreEqual(2, WorldViewRules.WandParticleBudget(OptionValues.Reduced, 3), "줄임 = 절반(올림)");
            Assert.AreEqual(1, WorldViewRules.WandParticleBudget(OptionValues.Reduced, 1));
            Assert.AreEqual(0, WorldViewRules.WandParticleBudget(OptionValues.Minimal, 2), "최소 = 대기 입자 0");
            Assert.AreEqual(0, WorldViewRules.WandParticleBudget(OptionValues.All, 0));

            var o = new ViewOptions { WandFxOpacity = 0.2f };
            Assert.AreEqual(0.16f, WorldViewRules.WandFxAlpha(o, 0.8f), 1e-6f, "지팡이 효과 투명도를 곱한다");
        }

        [Test]
        public void S02_8_3__WandHeatLookAndLockRebrighten()
        {
            var p = new PlayerState();
            p.Derived.CritStartHu = SimConst.HeatToHu(85f);
            p.HeatHu = SimConst.HeatToHu(84f);
            Assert.AreEqual(WandHeatLook.Normal, WorldViewRules.HeatLook(p));
            p.HeatHu = SimConst.HeatToHu(85f);
            Assert.AreEqual(WandHeatLook.Critical, WorldViewRules.HeatLook(p), "정확히 임계 시작(85)부터 임계");
            p.Locked = true;
            Assert.AreEqual(WandHeatLook.Locked, WorldViewRules.HeatLook(p));

            // 잠금 3초(180틱): 0.5초(30틱)마다 한 칸 → 6칸.
            Assert.AreEqual(0f, WorldViewRules.LockBrightness(100, 100, 280, 0.5f), 1e-6f);
            Assert.AreEqual(0f, WorldViewRules.LockBrightness(129, 100, 280, 0.5f), 1e-6f);
            Assert.AreEqual(1f / 6f, WorldViewRules.LockBrightness(130, 100, 280, 0.5f), 1e-6f);
            Assert.AreEqual(1f, WorldViewRules.LockBrightness(280, 100, 280, 0.5f), 1e-6f);
        }

        [Test]
        public void S07_10__MarksCounts()
        {
            // 흔적 = min(2, record_peak − stage)(07 4.9절), 흰 금 = min(3, 임시 하락 수)(07 10절).
            Assert.AreEqual(0, WorldViewRules.ScarCount(7, 7, 2));
            Assert.AreEqual(1, WorldViewRules.ScarCount(6, 7, 2));
            Assert.AreEqual(2, WorldViewRules.ScarCount(3, 9, 2));
            Assert.AreEqual(0, WorldViewRules.CrackCount(0, 3));
            Assert.AreEqual(2, WorldViewRules.CrackCount(2, 3));
            Assert.AreEqual(3, WorldViewRules.CrackCount(5, 3));
            Assert.AreEqual(2, WorldViewRules.ExpansionCount(6, 4));
            Assert.AreEqual(0, WorldViewRules.ExpansionCount(5, 5));
        }

        [Test]
        public void S09_2_4__WildStainInheritance()
        {
            var t = T;
            CollectionAssert.IsEmpty(WorldViewRules.WildStainStages(1, t));
            CollectionAssert.AreEqual(new[] { 2 }, WorldViewRules.WildStainStages(2, t));
            CollectionAssert.AreEqual(new[] { 2, 3 }, WorldViewRules.WildStainStages(3, t));
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, WorldViewRules.WildStainStages(4, t));
            Assert.AreEqual("wild-stain/char.1/stage-2/soot", WorldViewRules.WildStainKey(Ids.Char.C1, 2, "soot"));
        }

        [Test]
        public void S03_WandDetached__StageRanges()
        {
            var t = T;
            Assert.IsFalse(WorldViewRules.DetachedVisible(Ids.Char.C2, 12, t));
            Assert.IsTrue(WorldViewRules.DetachedVisible(Ids.Char.C2, 13, t));
            Assert.IsTrue(WorldViewRules.DetachedVisible(Ids.Char.C4, 7, t));
            Assert.IsFalse(WorldViewRules.DetachedVisible(Ids.Char.C4, 10, t), "+10에서 조각이 왕관으로 내려앉는다");
            Assert.IsTrue(WorldViewRules.DetachedVisible(Ids.Char.C5, 10, t));
            Assert.IsFalse(WorldViewRules.DetachedVisible(Ids.Char.C1, 13, t), "달구는 뜨는 부품이 없다");
        }

        [Test]
        public void S09_WandUpSplit__Char2Only()
        {
            var t = T;
            Assert.IsTrue(t.WandUpSplit.TryGetValue(Ids.Char.C2, out var s));
            Assert.AreEqual(28, s.x, "asset_aliases.json wand_up_split.split_y");
            Assert.AreEqual(6, s.y, "offset_x");
            Assert.IsFalse(t.WandUpSplit.ContainsKey(Ids.Char.C1));
        }

        [Test]
        public void S10_3_9__ForbiddenColorsNotUsedForPlayerSide()
        {
            var t = T;
            // 위험색(색상각 330~15°, 채도·명도 70%↑)은 플레이어 쪽에 쓰지 않는다. 임계에 빨강·금색 금지.
            foreach (var c in new[] { t.WandCritGlowColor, t.WandLockColor, t.InteractRingColor, t.DepartMarkerColor, t.DepartHoldRingColor,
                         t.RimColor, t.HitFlashColor, t.MarkMutationColor, t.MarkCrackColor, t.WandDebtRingColor, t.WandKilnLineColor })
                Assert.IsFalse(Wizard.Solo.UI.UiTheme.IsDangerColor(c), c.ToString());
            foreach (var kv in t.WandParticleColors) Assert.IsFalse(Wizard.Solo.UI.UiTheme.IsDangerColor(kv.Value), kv.Key);
            Color.RGBToHSV(t.WandCritGlowColor, out float h, out float s, out _);
            Assert.IsFalse(h * 360f >= 35f && h * 360f <= 65f && s >= 0.35f, "임계 빛은 금색이 아니다(Q-16)");
        }
    }
}
