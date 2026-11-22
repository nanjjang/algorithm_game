using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Screens.Story;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Story
{
    /// <summary>
    /// 첫 만남·복수 연출 시간표. 기대값의 출처: 05 요약서 10.2절(되비침 만개 24/54/84틱, +114틱, 쓰러짐 240틱, 72틱에 대사),
    /// 10.3절(복수 처치 기준 0~60 느린 연출·거울 금 / 60 흰 번쩍 / 84~144 머리 장식 / 144~264 칭호 띠 + 한마디, 84틱 이후 넘기기,
    /// 등장 시 복수 꼬리표 + 첫 만남 한마디 1.2초), 08 요약서 6.3·13.2절(처음 쪽지 4초 = 240틱, TV-236), 14.1절(번쩍 한도·관리기),
    /// 00 문서 5.5절(복수 중 여부 = RunState.RevengeActive, WaveClearing 길이의 주인 = Run), D-27(칭호 띠 이름 = 캐릭터 표시 이름).
    /// </summary>
    public sealed class StoryTimelineTests
    {
        static SimWorld NewWorld()
        {
            var w = new SimWorld(TestContent.Db, SimModuleSet.Stubs());
            w.Players[0].Active = true;
            w.Players[0].CharId = Ids.Char.C2;
            return w;
        }

        static SimEvent Ev(SimEventType t, int tick, int a = 0, string id = null) =>
            new SimEvent { Type = t, Tick = tick, A = a, Id = id, Slot = -1, Pos = new Vector2(12f, 7f) };

        // ------------------------------------------------------------ 조각 수치

        [Test]
        public void S05_10_3__TuningMatchesSpecTicks()
        {
            var t = StoryTuning.From(TestContent.Db);
            Assert.AreEqual(60, t.RevengeSlowEnd);
            Assert.AreEqual(60, t.RevengeFlashTick);
            Assert.AreEqual(84, t.RevengeOrnamentStart);
            Assert.AreEqual(144, t.RevengeOrnamentEnd);
            Assert.AreEqual(144, t.RevengeBandStart);
            Assert.AreEqual(264, t.RevengeEnd);
            Assert.AreEqual(84, t.RevengeSkipHint);
            Assert.AreEqual(240, t.FirstNoteTicks, "4초");
            Assert.AreEqual(72, t.IntroTicks, "1.2초");
            Assert.AreEqual(72, t.IntroRevengeLineTicks, "1.2초");
            Assert.AreEqual(114, t.ReflectionHpZeroTicks);
            Assert.AreEqual(240, t.CollapseTicks);
            CollectionAssert.AreEqual(new[] { 24, 54, 84 }, t.ReflectionBurstTicks);
            Assert.AreEqual(24, t.MirrorCrackTicks, "0.4초");
            Assert.AreEqual(5, t.TutorialStepCount);
            Assert.AreEqual(560f, t.Balloon.X); Assert.AreEqual(200f, t.Balloon.Y);
            Assert.AreEqual(800f, t.Balloon.W); Assert.AreEqual(120f, t.Balloon.H);
            Assert.AreEqual(480f, t.Dialog.X); Assert.AreEqual(920f, t.Dialog.Y);
            Assert.AreEqual(960f, t.Dialog.W); Assert.AreEqual(96f, t.Dialog.H);
            Assert.AreEqual(8f, t.FinalRageEdgePx);
        }

        // ------------------------------------------------------------ 복수 연출 틱 경계 (05 10.3절)

        [Test]
        public void S05_10_3__RevengeTickBoundaries()
        {
            var t = StoryTuning.Defaults();
            var f0 = StoryTimeline.Revenge(t, 0, true);
            Assert.IsTrue(f0.Visible); Assert.IsTrue(f0.SlowMotion); Assert.IsFalse(f0.Shards); Assert.IsFalse(f0.SkipHint);

            var f59 = StoryTimeline.Revenge(t, 59, true);
            Assert.IsTrue(f59.SlowMotion);
            Assert.IsFalse(StoryTimeline.CrossedRevengeFlash(t, 58, 59));

            var f60 = StoryTimeline.Revenge(t, 60, true);
            Assert.IsFalse(f60.SlowMotion, "0~60 느린 연출");
            Assert.IsTrue(f60.Shards, "60 거울 조각");
            Assert.IsTrue(StoryTimeline.CrossedRevengeFlash(t, 59, 60), "60 흰 번쩍");

            var f83 = StoryTimeline.Revenge(t, 83, true);
            Assert.IsFalse(f83.SkipHint); Assert.IsFalse(f83.Ornament);

            var f84 = StoryTimeline.Revenge(t, 84, true);
            Assert.IsTrue(f84.SkipHint, "넘기기 안내는 84틱부터");
            Assert.IsTrue(f84.Ornament, "84~144 머리 장식");
            Assert.IsFalse(f84.Shards);
            Assert.AreEqual(0f, f84.OrnamentT, 1e-6f);

            var f143 = StoryTimeline.Revenge(t, 143, true);
            Assert.IsFalse(f143.TitleBand); Assert.IsFalse(f143.MasterLine);

            var f144 = StoryTimeline.Revenge(t, 144, true);
            Assert.IsTrue(f144.TitleBand, "144~264 칭호 띠");
            Assert.IsTrue(f144.MasterLine);
            Assert.AreEqual(1f, f144.OrnamentT, 1e-6f, "144에 붙음");

            Assert.IsTrue(StoryTimeline.Revenge(t, 263, true).Visible);
            Assert.IsFalse(StoryTimeline.Revenge(t, 264, true).Visible, "264틱에 끝");
        }

        [Test]
        public void S05_10_3__RevengeFoldsWhenPhaseLeavesWaveClearing()
        {
            var t = StoryTuning.Defaults();
            Assert.IsFalse(StoryTimeline.Revenge(t, 100, false).Visible);

            var w = NewWorld();
            var d = new StoryDirector(t);
            w.Run.Phase = RunPhase.WaveClearing;
            w.Tick = 1000;
            d.OnEvent(w, Ev(SimEventType.RevengeKill, 1000));
            w.Tick = 1100;
            var f = d.Compute(w, 0, 1f);
            Assert.IsTrue(f.Revenge.Visible);
            Assert.IsTrue(f.Revenge.SkipHint);

            // J로 넘겼다(Run이 LootPick으로) → 남은 연출을 바로 접는다.
            w.Run.Phase = RunPhase.LootPick;
            w.Tick = 1101;
            f = d.Compute(w, 0, 1f);
            Assert.IsFalse(f.Revenge.Visible);
            Assert.IsNull(f.DialogKey);
            Assert.AreEqual(-1, d.RevengeBaseTick);
            // 다시 WaveClearing이 와도 다시 틀지 않는다.
            w.Run.Phase = RunPhase.WaveClearing;
            Assert.IsFalse(d.Compute(w, 0, 1f).Revenge.Visible);
        }

        [Test]
        public void S05_10_3__RevengeDialogAndTitleBandFrom144()
        {
            var w = NewWorld();
            var d = new StoryDirector(StoryTuning.Defaults());
            w.Run.Phase = RunPhase.WaveClearing;
            d.OnEvent(w, Ev(SimEventType.RevengeKill, 0));
            w.Tick = 143;
            Assert.IsNull(d.Compute(w, 0, 1f).DialogKey);
            w.Tick = 144;
            var f = d.Compute(w, 0, 1f);
            Assert.IsTrue(f.Revenge.TitleBand);
            Assert.AreEqual("revenge.master_line", f.DialogKey);
            w.Tick = 264;
            Assert.IsFalse(d.Compute(w, 0, 1f).Revenge.Visible, "264틱이 차면 접힌다");
        }

        [Test]
        public void S05_10_3__RevengeFlashOnceAt60ThroughGate()
        {
            var w = NewWorld();
            var d = new StoryDirector(StoryTuning.Defaults());
            w.Run.Phase = RunPhase.WaveClearing;
            d.OnEvent(w, Ev(SimEventType.RevengeKill, 0));
            for (int tick = 1; tick <= 59; tick++) { w.Tick = tick; d.Compute(w, 0, 1f); }
            Assert.AreEqual(StoryFlashKind.None, d.LastFlash.Kind);
            Assert.AreEqual(0, d.PendingShakes);
            w.Tick = 60;
            var f = d.Compute(w, 0, 1f);
            Assert.AreEqual(StoryFlashKind.Full, d.LastFlash.Kind);
            Assert.AreEqual(1, d.PendingShakes, "흔들림 대 1회");
            Assert.Greater(f.FlashAlpha, 0f);
            Assert.LessOrEqual(f.FlashAlpha, 0.35f, "순백 100% 금지, Full 상한 35%");
            d.PendingShakes = 0;
            w.Tick = 61; d.Compute(w, 0, 1f);
            Assert.AreEqual(0, d.PendingShakes, "한 번만");
        }

        [Test]
        public void S05_10_3__TitleBandUsesCharacterDisplayName()
        {
            // D-27: revenge.title_band = "{title.revenge.mirror_breaker} {nickname}", nickname = 캐릭터 표시 이름.
            string s = TestContent.Names.Format("revenge.title_band", "nickname", TestContent.Names.Get(Ids.Char.C2));
            StringAssert.Contains(TestContent.Names.Get("title.revenge.mirror_breaker"), s);
            StringAssert.Contains(TestContent.Names.Get(Ids.Char.C2), s);
        }

        // ------------------------------------------------------------ 복수 꼬리표는 BossIntroBegin의 A로만

        [Test]
        public void S05_10_3__RevengeTagComesOnlyFromIntroEventA()
        {
            var w = NewWorld();
            w.Run.RevengeActive = true;
            var d = new StoryDirector(StoryTuning.Defaults());
            d.OnEvent(w, Ev(SimEventType.BossIntroBegin, 0, 0, Ids.Boss.MirrorPhantom));
            w.Tick = 1;
            var f = d.Compute(w, 0, 1f);
            Assert.IsTrue(f.IntroVisible);
            Assert.IsFalse(f.IntroRevengeTag, "판이 복수 중이어도 A = 0이면 꼬리표 없음");
            Assert.IsNull(f.DialogKey);

            w.Run.RevengeActive = false;
            d.OnEvent(w, Ev(SimEventType.BossIntroBegin, 10, 1, Ids.Boss.MirrorPhantom));
            w.Tick = 11;
            f = d.Compute(w, 0, 1f);
            Assert.IsTrue(f.IntroRevengeTag, "A = 1이면 꼬리표");
            Assert.AreEqual("phantom_first.master_line", f.DialogKey, "첫 만남 한마디 1.2초");
            Assert.AreEqual(Ids.Boss.MirrorPhantom, f.IntroBossId);
            w.Tick = 10 + 71; Assert.IsTrue(d.Compute(w, 0, 1f).IntroVisible);
            w.Tick = 10 + 72;
            f = d.Compute(w, 0, 1f);
            Assert.IsFalse(f.IntroVisible, "1.2초 = 72틱");
            Assert.IsNull(f.DialogKey);
        }

        // ------------------------------------------------------------ 첫 만남 (05 10.2절)

        [Test]
        public void S05_10_2__MasterLineShowsOnEventNotOwnTimer()
        {
            var w = NewWorld();
            var d = new StoryDirector(StoryTuning.Defaults());
            w.Run.Phase = RunPhase.PhantomDowned;
            d.OnEvent(w, Ev(SimEventType.PhantomCollapse, 500));
            w.Tick = 500 + 72;
            Assert.IsNull(d.Compute(w, 0, 1f).DialogKey, "층이 72틱을 따로 세지 않는다 — PhantomMasterLine 이벤트(쓰러짐 +72틱)를 기다린다");
            d.OnEvent(w, Ev(SimEventType.PhantomMasterLine, 572));
            w.Tick = 573;
            var f = d.Compute(w, 0, 1f);
            Assert.AreEqual("phantom_first.master_line", f.DialogKey);
            CollectionAssert.Contains(CueIds(d), "ui.hud.phantom_line");
            w.Tick = 600;
            Assert.AreEqual("phantom_first.master_line", d.Compute(w, 0, 1f).DialogKey);

            // 일어남 → 대사 판 걷힘.
            w.Run.Phase = RunPhase.WaveClearing;
            d.OnEvent(w, new SimEvent { Type = SimEventType.PlayerRose, Tick = 740, Slot = 0 });
            w.Tick = 741;
            Assert.IsNull(d.Compute(w, 0, 1f).DialogKey);
        }

        [Test]
        public void S05_10_2__ReflectionBurstsAt24_54_84AndFlashAt114()
        {
            var w = NewWorld();
            var d = new StoryDirector(StoryTuning.Defaults());
            w.Run.Phase = RunPhase.PhantomFinale;
            d.OnEvent(w, Ev(SimEventType.PhantomReflection, 0));
            w.Tick = 23; var f = d.Compute(w, 0, 1f);
            Assert.Less(f.Reflection.Burst0, 0f);
            w.Tick = 24; f = d.Compute(w, 0, 1f);
            Assert.GreaterOrEqual(f.Reflection.Burst0, 0f, "24틱 첫 만개");
            w.Tick = 54; f = d.Compute(w, 0, 1f);
            Assert.GreaterOrEqual(f.Reflection.Burst1, 0f, "54틱 둘째");
            Assert.Less(f.Reflection.Burst0, 0f, "0.5초 간격");
            w.Tick = 84; f = d.Compute(w, 0, 1f);
            Assert.GreaterOrEqual(f.Reflection.Burst2, 0f, "84틱 셋째");
            w.Tick = 113; d.Compute(w, 0, 1f);
            Assert.AreEqual(StoryFlashKind.None, d.LastFlash.Kind);
            w.Tick = 114; f = d.Compute(w, 0, 1f);
            Assert.AreEqual(StoryFlashKind.Full, d.LastFlash.Kind, "+114틱 전체 번쩍 1회");
            Assert.AreEqual(0.25f, d.LastFlash.Opacity, 1e-6f, "흰 25%");
            Assert.AreEqual(1, d.PendingShakes);
        }

        [Test]
        public void S08_TV236__FirstNoteDropsOnRiseAndLiftsAfter240Ticks()
        {
            var t = StoryTuning.Defaults();
            var w = NewWorld();
            var d = new StoryDirector(t);
            w.Run.Phase = RunPhase.WaveClearing;
            w.Run.RevengeActive = true;   // 판 안의 값(프로필 phantom_met은 아직 거짓이어도)
            d.OnEvent(w, new SimEvent { Type = SimEventType.PlayerRose, Tick = 1000, Slot = 0 });
            w.Tick = 1000;
            var f = d.Compute(w, 0, 1f);
            Assert.IsTrue(f.Note.Visible);
            CollectionAssert.Contains(CueIds(d), "ui.revenge.note_drop");
            d.Cues.Clear();
            w.Tick = 1000 + 239;
            f = d.Compute(w, 0, 1f);
            Assert.IsTrue(f.Note.Visible); Assert.IsFalse(f.Note.Lifting);
            Assert.AreEqual(1f, f.Note.Drop01, 1e-6f);
            w.Tick = 1000 + 240;
            f = d.Compute(w, 0, 1f);
            Assert.IsTrue(f.Note.Lifting, "4초(240틱) 뒤 올라간다");
            CollectionAssert.Contains(CueIds(d), "ui.revenge.note_lift");
            d.Cues.Clear();
            w.Tick = 1000 + 240 + t.FirstNoteSlideTicks;
            f = d.Compute(w, 0, 1f);
            Assert.IsFalse(f.Note.Visible);
            Assert.AreEqual(0, d.Cues.Count, "올라감 소리는 한 번");
        }

        [Test]
        public void S08_TV236__FirstNoteOnlyWhenRevengeActive()
        {
            var w = NewWorld();
            var d = new StoryDirector(StoryTuning.Defaults());
            w.Run.RevengeActive = false;
            d.OnEvent(w, new SimEvent { Type = SimEventType.PlayerRose, Tick = 10, Slot = 0 });
            w.Tick = 11;
            Assert.IsFalse(d.Compute(w, 0, 1f).Note.Visible);
            Assert.AreEqual(0, d.Cues.Count);
        }

        [Test]
        public void S05_10_2__MirrorCrackLasts24Ticks()
        {
            var w = NewWorld();
            var d = new StoryDirector(StoryTuning.Defaults());
            d.OnEvent(w, Ev(SimEventType.PhantomMirrorCrack, 0));
            w.Tick = 23; Assert.GreaterOrEqual(d.Compute(w, 0, 1f).MirrorCrackT, 0f);
            w.Tick = 24; Assert.Less(d.Compute(w, 0, 1f).MirrorCrackT, 0f, "0.4초");
        }

        [Test]
        public void S05_10_2__CollapseOverlayEndsAt240AndDissolveAt156()
        {
            var t = StoryTuning.Defaults();
            var c = StoryTimeline.Collapse(t, 100);
            Assert.IsTrue(c.Visible);
            Assert.AreEqual(t.CollapseOverlayAlpha, c.OverlayAlpha, 1e-6f);
            Assert.Less(StoryTimeline.Collapse(t, 155).DissolveT, 0f);
            Assert.GreaterOrEqual(StoryTimeline.Collapse(t, 156).DissolveT, 0f, "2.60초 은색 가루");
            Assert.Less(StoryTimeline.Collapse(t, 200).OverlayAlpha, t.CollapseOverlayAlpha, "3.20초부터 색이 돌아온다");
            Assert.IsFalse(StoryTimeline.Collapse(t, 240).Visible);
        }

        // ------------------------------------------------------------ 최종 격노·Build

        [Test]
        public void S05_8_3__FinalRageEdgeUntilBossGone()
        {
            var w = NewWorld();
            var d = new StoryDirector(StoryTuning.Defaults());
            w.Boss = new BossState();
            Assert.IsFalse(d.Compute(w, 0, 1f).FinalRageEdge);
            d.OnEvent(w, Ev(SimEventType.BossFinalRage, 0));
            Assert.IsTrue(d.Compute(w, 0, 1f).FinalRageEdge);
            w.Boss = null;
            Assert.IsFalse(d.Compute(w, 0, 1f).FinalRageEdge, "w.Boss가 null이 되면 끝");
            w.Boss = new BossState { FinalRage = true };
            Assert.IsTrue(d.Compute(w, 0, 1f).FinalRageEdge, "상태로도 그린다(Build 뒤)");
        }

        [Test]
        public void S00_7__BuildDoesNotReplayOneShots()
        {
            var w = NewWorld();
            var d = new StoryDirector(StoryTuning.Defaults());
            w.Run.Phase = RunPhase.WaveClearing;
            w.Run.RevengeActive = true;
            d.OnEvent(w, Ev(SimEventType.RevengeKill, 0));
            d.OnEvent(w, new SimEvent { Type = SimEventType.PlayerRose, Tick = 0, Slot = 0 });
            w.Tick = 100;
            Assert.IsTrue(d.Compute(w, 0, 1f).Revenge.Visible);
            d.Reset();
            w.Run.RevengeKillTick = 0;
            var f = d.Compute(w, 0, 1f);
            Assert.IsFalse(f.Revenge.Visible, "다시 세운 뒤 일회 연출을 다시 틀지 않는다");
            Assert.IsFalse(f.Note.Visible);
        }

        // ------------------------------------------------------------ 번쩍 관리기 (08 14.1절)

        [Test]
        public void S08_14_1__FlashGateCapsAndBorder()
        {
            var t = StoryTuning.Defaults();
            var g = new StoryFlashGate(t);
            var r = g.Request(0.5f, 0, 0, 1f);
            Assert.AreEqual(StoryFlashKind.Full, r.Kind); Assert.AreEqual(0.35f, r.Opacity, 1e-6f, "Full 0.35");
            g.Reset();
            r = g.Request(0.5f, 0, 1, 1f);
            Assert.AreEqual(0.15f, r.Opacity, 1e-6f, "Reduced 0.15");
            g.Reset();
            Assert.AreEqual(StoryFlashKind.Border, g.Request(0.5f, 0, 2, 1f).Kind, "Minimal = 테두리 강조");
            g.Reset();
            Assert.AreEqual(0.175f, g.Request(0.5f, 0, 0, 0.5f).Opacity, 1e-6f, "Full + flash_scale 50 → 0.175");
        }

        [Test]
        public void S08_14_1__FlashGateRateLimit()
        {
            var g = new StoryFlashGate(StoryTuning.Defaults());
            Assert.AreEqual(StoryFlashKind.Full, g.Request(0.25f, 0, 0, 1f).Kind);
            Assert.AreEqual(StoryFlashKind.Border, g.Request(0.25f, 23, 0, 1f).Kind, "간격 24틱 미만 → 테두리");
            Assert.AreEqual(StoryFlashKind.Full, g.Request(0.25f, 24, 0, 1f).Kind);
            Assert.AreEqual(StoryFlashKind.Border, g.Request(0.25f, 50, 0, 1f).Kind, "1초에 2회 넘음 → 테두리");
            Assert.AreEqual(StoryFlashKind.Full, g.Request(0.25f, 84, 0, 1f).Kind, "창이 지나면 다시");
        }

        [Test]
        public void S08_14_1__FlashShapeOnWithin3OffOver6Plus()
        {
            var t = StoryTuning.Defaults();
            var f = new StoryFlash { Kind = StoryFlashKind.Full, Opacity = 0.25f, StartTick = 0 };
            Assert.AreEqual(0.25f, StoryFlashGate.AlphaAt(f, t.FlashOnTicks - 1, t.FlashOnTicks, t.FlashOffTicks), 1e-6f, "≤ 3틱에 켜짐");
            Assert.Greater(StoryFlashGate.AlphaAt(f, t.FlashOnTicks + 5, t.FlashOnTicks, t.FlashOffTicks), 0f, "6틱 이상에 걸쳐 꺼짐");
            Assert.AreEqual(0f, StoryFlashGate.AlphaAt(f, t.FlashOnTicks + t.FlashOffTicks, t.FlashOnTicks, t.FlashOffTicks), 1e-6f);
            Assert.LessOrEqual(t.FlashOnTicks, 3);
            Assert.GreaterOrEqual(t.FlashOffTicks, 6);
        }

        static System.Collections.Generic.List<string> CueIds(StoryDirector d)
        {
            var l = new System.Collections.Generic.List<string>();
            for (int i = 0; i < d.Cues.Count; i++) l.Add(d.Cues[i].Id);
            return l;
        }
    }
}
