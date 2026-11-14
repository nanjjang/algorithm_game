using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.View;
using Wizard.Solo.View.Hud;

namespace Wizard.Solo.Tests.Hud
{
    /// <summary>
    /// HudLayer를 그림 창고 없이 세워(대체 도형) 계약 벡터를 화면 쪽에서 확인한다: Build는 이벤트 없이 지금 상태로 그린다,
    /// w.Boss == null이면 보스 표시 없음, 콤보 자리 두 곳, 출발 안내는 반경 안에서만, Practice에서 웨이브·지갑 숨김.
    /// </summary>
    public class HudLayerTests
    {
        GameObject _canvas;
        HudFakePool _pool;
        HudLayer _layer;
        HudFakeSound _sound;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("TestHudRoot", typeof(RectTransform));
            var root = (RectTransform)_canvas.transform;
            root.sizeDelta = new Vector2(1920, 1080);
            _pool = new HudFakePool();
            _sound = new HudFakeSound();
            _layer = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_layer != null) _layer.Dispose();
            _pool.DestroyAll();
            Object.DestroyImmediate(_canvas);
        }

        ViewContext Context(SimWorld w, ViewOptions options = null, ProfileData profile = null)
        {
            var hudRoot = (RectTransform)_canvas.transform;
            var wt = new GameObject("WorldTextRoot", typeof(RectTransform));
            var wtr = (RectTransform)wt.transform;
            wtr.SetParent(hudRoot, false);
            var ctx = new GameContext { Names = new HudFakeNames(), Sound = _sound };
            if (profile != null) ctx.Profiles = new FixedProfiles(profile);
            return new ViewContext
            {
                Game = ctx, World = w, HudRoot = hudRoot, WorldTextRoot = wtr, Pool = _pool,
                Options = options ?? new ViewOptions()
            };
        }

        HudLayer Build(SimWorld w, ViewOptions options = null, ProfileData profile = null)
        {
            _layer = new HudLayer();
            _layer.Build(Context(w, options, profile), w);
            return _layer;
        }

        [Test]
        public void S13_Contract__BuildDrawsFromStateWithoutEvents()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 14);
            w.Run.RevengeActive = true;
            w.Run.Purse = 1234;
            w.P0.Combo = 7;
            w.P0.Crystals = 2;
            w.P0.Hp = 100;
            var l = Build(w, null, HudTestWorlds.Profile(false));
            Assert.IsTrue(l.Top.DayWaveVisible);
            Assert.AreEqual("str.battle_hud.day_wave{day=2,wave=14}", l.Top.DayWaveText);
            Assert.IsTrue(l.Top.RevengeVisible);
            Assert.AreEqual("revenge.tracker.hud{n=6}", l.Top.RevengeText);
            Assert.AreEqual("str.battle_hud.wallet 1,234", l.Status.WalletRowText(0));
            Assert.AreEqual(6, l.Status.HpCellsShown);
            Assert.IsTrue(l.Status.LastCellCornerVisible, "체력 1칸 남음");
            Assert.AreEqual(7, l.Marks[0].Combo.ShownValue);
            Assert.AreEqual(5, l.Marks[0].CrystalCellsShown);
            Assert.AreEqual(1, _pool.RentedMeshes > 0 ? 1 : 0, "열 링은 전장 층 메시");
        }

        [Test]
        public void S13_Contract__BossNullNoBossPanel()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 10);
            var l = Build(w);
            Assert.IsFalse(l.Boss.Visible);
            Assert.IsFalse(l.Top.ProgressVisible, "보스 웨이브에 진행 바 숨김");

            var def = new BossDef { Id = Ids.Boss.KilnBoar, Weak = Ids.Element.Ice, Resist = Ids.Element.Fire };
            def.Phases.Add(new BossPhaseDef { EnterAtHpPercent = 100 });
            def.Phases.Add(new BossPhaseDef { EnterAtHpPercent = 60 });
            var e = new EnemyState { Id = 5, Hp = 50, MaxHp = 100, IsBoss = true };
            w.Enemies.Add(e);
            w.Boss = new BossState { EnemyId = 5, Def = def, Wave = 10 };
            l.Render(w, 1f, 0.016f);
            Assert.IsTrue(l.Boss.Visible);
            Assert.AreEqual(Ids.Boss.KilnBoar, l.Boss.NameText);
            Assert.AreEqual(1, l.Boss.TickCount);
            Assert.AreEqual(480f, l.Boss.FillWidth, 1e-3f);
            Assert.IsFalse(l.Boss.FinalRageFrameVisible);
            w.Boss.FinalRage = true;
            l.Render(w, 1f, 0.016f);
            Assert.IsTrue(l.Boss.FinalRageFrameVisible, "최종 격노 = 테만 빨강");

            w.Boss = null;
            l.Render(w, 1f, 0.016f);
            Assert.IsFalse(l.Boss.Visible);
        }

        [Test]
        public void S13_TV_047__FirstEncounterNoTicksOnLayer()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 10);
            var def = new BossDef { Id = Ids.Boss.MirrorPhantom };
            def.Phases.Add(new BossPhaseDef { EnterAtHpPercent = 100 });
            def.Phases.Add(new BossPhaseDef { EnterAtHpPercent = 60 });
            def.Phases.Add(new BossPhaseDef { EnterAtHpPercent = 25 });
            w.Enemies.Add(new EnemyState { Id = 9, Hp = 100, MaxHp = 100, IsBoss = true });
            w.Boss = new BossState { EnemyId = 9, Def = def, Wave = 10, FirstEncounter = true };
            var l = Build(w);
            Assert.IsTrue(l.Boss.Visible);
            Assert.AreEqual(0, l.Boss.TickCount);
            Assert.IsFalse(l.Boss.RevengeTagVisible);
        }

        [Test]
        public void S13_Contract__RevengeTagFromIntroEvent()
        {
            var w = HudTestWorlds.World(RunPhase.BossIntro, 20);
            w.Enemies.Add(new EnemyState { Id = 3, Hp = 10, MaxHp = 10, IsBoss = true });
            w.Boss = new BossState { EnemyId = 3, Def = new BossDef { Id = Ids.Boss.MirrorPhantom }, Wave = 20 };
            var l = Build(w);
            Assert.IsFalse(l.Boss.RevengeTagVisible, "복수 중이 아니다");
            l.OnEvent(w, new SimEvent { Type = SimEventType.BossIntroBegin, EntityId = 3, Id = Ids.Boss.MirrorPhantom, A = 1, Slot = -1 });
            l.Render(w, 1f, 0.016f);
            Assert.IsTrue(l.Boss.RevengeTagVisible);
            CollectionAssert.Contains(_sound.Played, "ui.hud.revenge_tag");
        }

        [Test]
        public void S13_Contract__ComboPositionNearCharacterOrHud()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 3);
            w.P0.Combo = 4;
            var o = new ViewOptions();
            var l = Build(w, o);
            Assert.IsFalse(l.Marks[0].ComboInHud);
            Assert.AreEqual(l.Marks[0].Root, l.Marks[0].Combo.Root.parent, "캐릭터 옆");
            o.ComboPosition = OptionValues.Hud;
            l.Render(w, 1f, 0.016f);
            Assert.IsTrue(l.Marks[0].ComboInHud);
            Assert.AreEqual(l.Status.ComboHudAnchor, l.Marks[0].Combo.Root.parent, "HUD(초상 옆)");
            o.ComboPosition = OptionValues.NearCharacter;
            l.Render(w, 1f, 0.016f);
            Assert.AreEqual(l.Marks[0].Root, l.Marks[0].Combo.Root.parent);
        }

        [Test]
        public void S13_Contract__DepartHintLineOnlyInsideRadius()
        {
            var w = HudTestWorlds.World(RunPhase.Camp, 4);
            w.Run.CampSub = CampSubState.FireIdle;
            w.Run.DepartMarkerPos = new Vector2(16, 7);
            w.Run.DepartMarkerRadius = 1f;
            w.P0.Pos = new Vector2(12, 7);
            var l = Build(w);
            Assert.IsFalse(l.Notice.Visible);
            w.P0.Pos = new Vector2(16.4f, 7.2f);
            l.Render(w, 1f, 0.016f);
            Assert.IsTrue(l.Notice.Visible);
            Assert.AreEqual(HudModel.KeyDepartHint, l.Notice.Text);
            w.P0.Pos = new Vector2(18f, 7f);
            l.Render(w, 1f, 0.016f);
            Assert.IsFalse(l.Notice.Visible);
        }

        [Test]
        public void S13_Contract__PracticeHidesWaveAndWalletOnLayer()
        {
            var w = HudTestWorlds.World(RunPhase.Practice, 1);
            var l = Build(w);
            Assert.IsFalse(l.Top.ProgressVisible);
            Assert.IsFalse(l.Top.DayWaveVisible);
            Assert.IsFalse(l.Top.ElapsedVisible);
            Assert.IsFalse(l.Status.WalletVisible);
            Assert.AreEqual(6, l.Status.HpCellsShown, "체력·초상·회로는 남는다");
        }

        [Test]
        public void S13_HeatLockNoticeAndRingAlways()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 2);
            w.Tick = 300;
            w.P0.Locked = true;
            w.P0.LockStartTick = 200;
            w.P0.UnlockTick = 414;
            var o = new ViewOptions { EffectsLevel = 2 };
            var l = Build(w, o);
            Assert.AreEqual("str.battle_hud.notice.heat_lock{n=2}", l.Notice.Text);
            Assert.IsTrue(l.Notice.BadgeVisible);
            Assert.AreEqual(HeatRingState.Locked, l.Rings[0].LastState, "연출 최소에서도 열 링");
            Assert.Greater(l.Rings[0].LastTriangleCount, 0);
            w.P0.Stance = StanceState.Reverse;
            l.Render(w, 1f, 0.016f);
            Assert.AreEqual(FacingArrowMode.Reverse, l.Rings[0].LastArrowMode);
            Assert.AreEqual(new Color(0.56f, 0.70f, 1.0f, 1f), l.Rings[0].LastArrowColor);
        }

        [Test]
        public void S13_WaveClearNoticeFromEvent()
        {
            var w = HudTestWorlds.World(RunPhase.WaveClearing, 2);
            var l = Build(w);
            Assert.IsFalse(l.Notice.Visible);
            l.OnEvent(w, new SimEvent { Type = SimEventType.WaveCleared, A = 2, Slot = -1 });
            l.Render(w, 1f, 0.016f);
            Assert.AreEqual(HudModel.KeyNoticeWaveClear, l.Notice.Text);
            l.Render(w, 1f, l.Tuning.NoticeSeconds + 0.1f);
            Assert.IsFalse(l.Notice.Visible);
        }

        [Test]
        public void S03_NowKLineAndRecentInputsOnLayer()
        {
            var mods = SimModuleSet.Stubs();
            var arts = new HudFakeArts();
            mods.Arts = arts;
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 2, mods);
            arts.NextPreview = new ArtPreview { ArtId = Ids.Art.Spear, Cost = 2, HasJustLine = true, JustArtId = Ids.Art.ThunderSpear, JustCost = 3, CooldownRatio = 1f };
            arts.Recent.Add(new RecentInput { Dir = CmdDir.Forward });
            arts.Recent.Add(new RecentInput { Dir = CmdDir.Back, Chained = true });
            var l = Build(w);
            Assert.IsTrue(l.Marks[0].NowKVisible);
            Assert.AreEqual("str.battle_hud.now_k{art=art.spear,n=2}", l.Marks[0].LastNowK.Main);
            Assert.IsNotNull(l.Marks[0].LastNowK.Just);
            Assert.AreEqual(2, l.Marks[0].RecentShown);
            for (int i = 0; i < 6; i++) arts.Recent.Add(new RecentInput { Dir = CmdDir.Left });
            l.Render(w, 1f, 0.016f);
            Assert.AreEqual(5, l.Marks[0].RecentShown, "최근 5개");

            l.OnEvent(w, new SimEvent { Type = SimEventType.ArtFizzled, Slot = 0, Id = Ids.Art.Bloom, A = (int)ArtFizzleReason.FullBloomLate, B = 3, F = 614 / 15360f });
            l.Render(w, 1f, 0.016f);
            Assert.IsTrue(l.Marks[0].LateVisible);
            Assert.AreEqual("str.dummy_practice.fail.late_input{n=3,sec=0.04}", l.Marks[0].LateText);

            w.Run.Phase = RunPhase.Camp;
            l.Render(w, 1f, 0.016f);
            Assert.IsFalse(l.Marks[0].NowKVisible, "야영에는 지금 K 없음");
        }

        [Test]
        public void S13_CircuitLightsInOrderOnShot()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 2);
            w.P0.Loadout.Core = new SlotItem { BlockId = Ids.Block.MagicBolt };
            w.P0.Loadout.Links = new SlotItem[4];
            w.P0.Loadout.Links[2] = new SlotItem { BlockId = Ids.Block.Pierce };
            var l = Build(w);
            Assert.AreEqual(5, l.Circuit.CellCount);
            Assert.AreEqual(-1, l.Circuit.LitCell);
            l.OnEvent(w, new SimEvent { Type = SimEventType.ShotFired, Slot = 0, A = 1 });
            l.Render(w, 1f, 0f);
            Assert.AreEqual(0, l.Circuit.LitCell, "코어부터");
            l.Render(w, 1f, l.Tuning.CircuitStepSeconds);
            Assert.AreEqual(3, l.Circuit.LitCell, "빈 칸을 건너뛴다");
            l.Render(w, 1f, l.Tuning.CircuitStepSeconds);
            Assert.AreEqual(-1, l.Circuit.LitCell);
        }

        [Test]
        public void S13_DisposeReturnsPoolAndDestroysHud()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 2);
            var l = Build(w);
            var root = l.Root;
            Assert.IsNotNull(root);
            l.Dispose();
            _layer = null;
            Assert.AreEqual(0, _pool.RentedMeshes);
            Assert.AreEqual(0, _pool.RentedSprites);
            Assert.IsTrue(root == null, "HUD 루트를 지웠다");
        }

        /// <summary>프로필 하나만 돌려주는 저장소(HUD는 Current만 읽는다).</summary>
        sealed class FixedProfiles : IProfileStore
        {
            readonly ProfileData _p;
            public FixedProfiles(ProfileData p) { _p = p; }
            public ProfileData Current => _p;
            public void LoadOrCreate(out bool created, out bool restored) { created = false; restored = false; }
            public void Save() { }
            public string Directory => string.Empty;
            public bool RestorePrevious() => false;
            public event System.Action Saved { add { } remove { } }
        }
    }
}
