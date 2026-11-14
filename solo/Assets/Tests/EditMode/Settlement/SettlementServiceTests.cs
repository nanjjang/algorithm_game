using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Settlement;
using Wizard.Solo.Save;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Settlement
{
    /// <summary>
    /// 정산(01 요약서 4.6·10절, 06 요약서 5절, 05 요약서 10.3·10.4절, 07 요약서 12.9절, 10 요약서 6절, 00 문서 D-22~D-28·D-53~D-55·D-68).
    /// 임시 폴더의 진짜 ProfileStore와 실제 데이터(TestContent)로 돌린다. 기대값은 요약서에서 가져왔다.
    /// </summary>
    public class SettlementServiceTests
    {
        sealed class TestClock : IClock
        {
            public DateTime Value = new DateTime(2026, 11, 18, 15, 30, 0, DateTimeKind.Local);
            public DateTime Now => Value;
            public DateTime UtcNow => Value.ToUniversalTime();
        }

        string _root;
        TestClock _clock;
        ProfileStore _store;
        GameContext _ctx;
        SettlementService _svc;
        int _saves;

        static IContentDb Db => TestContent.Db;
        ProfileData P => _store.Current;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "wizard-solo-settle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _clock = new TestClock();
            _store = NewStore();
            _store.LoadOrCreate(out _, out _);
            _saves = 0;
            _store.Saved += () => _saves++;
            _ctx = new GameContext { Content = Db, Profiles = _store, Clock = _clock };
            _svc = new SettlementService(_ctx, new Pcg32(20261118UL));
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { }
        }

        ProfileStore NewStore() =>
            new ProfileStore(SavePaths.ProfileDirectory(_root), () => NewProfile.Create(Db, _clock), Db.DataVersion);

        /// <summary>디스크에서 다시 읽은 저장소와 그 위의 새 서비스(다음 실행을 흉내 낸다).</summary>
        SettlementService Relaunch(out ProfileStore store)
        {
            store = NewStore();
            store.LoadOrCreate(out bool created, out _);
            Assert.IsFalse(created, "profile should have been saved");
            return new SettlementService(new GameContext { Content = Db, Profiles = store, Clock = _clock }, new Pcg32(7UL));
        }

        static RunSummary Run(string id, int cleared = 0, int startWave = 1)
        {
            int last = cleared > 0 ? startWave - 1 + cleared : 0;
            return new RunSummary
            {
                RunId = id, CharId = Ids.Char.C1, WandId = 1, Mode = RunMode.Endless, WandStage = 0,
                StartWave = startWave, ClearedDirect = cleared, LastClearedWave = last, ReachedWave = startWave + cleared,
                EndReason = RunEndReason.HpZero
            };
        }

        static WildBlock Wild(int uid, string blockId, string rarity, bool borrowed = false) =>
            new WildBlock { Uid = uid, BlockId = blockId, Rarity = rarity, Borrowed = borrowed, AcquiredWave = 1 };

        static DropChance Chance(DropChanceSource source, int wave, int count = 1) =>
            new DropChance { Source = source, Wave = wave, Count = count };

        static WaveResult Result(int wave, bool noHit, int total = 0)
        {
            var r = new WaveResult { Wave = wave, Stats = new WaveStats { Wave = wave, NoHit = noHit } };
            r.Score.Total = total;
            r.Score.Clear = total;
            return r;
        }

        int Qty(string item) => P.Items.TryGetValue(item, out ItemStack s) ? s.Qty : 0;
        int Bound(string item) => P.Items.TryGetValue(item, out ItemStack s) ? s.QtyBound : 0;

        static int Gained(SettlementResult r, string item, bool? guaranteed = null)
        {
            int n = 0;
            foreach (MaterialGain g in r.Materials)
                if (g.ItemId == item && (!guaranteed.HasValue || g.Guaranteed == guaranteed.Value)) n += g.Qty;
            return n;
        }

        SettlementResult CommitOk(RunSummary s, params int[] bag)
        {
            Result<SettlementResult> r = _svc.Commit(s, bag);
            Assert.IsTrue(r.Ok, "commit failed: " + r.Error);
            return r.Value;
        }

        // ------------------------------------------------------------ 귀환 가방 (01 요약서 10.2절, S09 T090~T100)

        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 0)]
        [TestCase(2, 0, 0)]
        [TestCase(3, 1, 0)]
        [TestCase(8, 2, 0)]
        [TestCase(10, 3, 1)]
        [TestCase(14, 4, 1)]
        [TestCase(20, 6, 2)]
        [TestCase(30, 10, 3)]
        [TestCase(36, 12, 3)]
        [TestCase(40, 12, 4)]
        [TestCase(60, 12, 6)]
        public void S09_T090_T099__BagSlotsAndHeroCap(int cleared, int slots, int heroCap)
        {
            BagLimits b = _svc.BagLimitsFor(cleared);
            Assert.AreEqual(slots, b.Slots);
            Assert.AreEqual(heroCap, b.HeroCap);
        }

        [Test]
        public void S09_T100__StartAt21Clear21To29GivesThreeSlotsNoHero()
        {
            // 건너뛴 20웨이브는 세지 않는다: 직접 넘긴 9웨이브 → 3칸, 영웅 0.
            RunSummary s = Run("r-skip", cleared: 9, startWave: 21);
            SettlementView v = _svc.Prepare(s);
            Assert.AreEqual(3, v.Bag.Slots);
            Assert.AreEqual(0, v.Bag.HeroCap);
        }

        [Test]
        public void S07_TV_236__AutoPickHeroCapThenRare()
        {
            // cleared 20(6칸, 영웅 2), 영웅 4·희귀 5·일반 3 → 영웅 2 + 희귀 4
            RunSummary s = Run("r-auto", cleared: 20);
            int uid = 1;
            string[] heroes = { Ids.Block.Corrosion, Ids.Block.Homing, Ids.Block.ChainLightning, Ids.Block.TripleHeat };
            foreach (string h in heroes) s.WildBlocks.Add(Wild(uid++, h, Ids.Rarity.Hero));
            for (int i = 0; i < 5; i++) s.WildBlocks.Add(Wild(uid++, Ids.Block.Fireball, Ids.Rarity.Rare));
            for (int i = 0; i < 3; i++) s.WildBlocks.Add(Wild(uid++, Ids.Block.Pierce, Ids.Rarity.Common));

            SettlementView v = _svc.Prepare(s);
            List<int> picks = _svc.AutoPick(v);
            Assert.AreEqual(6, picks.Count);
            int hero = 0, rare = 0, common = 0;
            foreach (int id in picks)
            {
                string rarity = s.WildBlocks.Find(b => b.Uid == id).Rarity;
                if (rarity == Ids.Rarity.Hero) hero++; else if (rarity == Ids.Rarity.Rare) rare++; else common++;
            }
            Assert.AreEqual(2, hero);
            Assert.AreEqual(4, rare);
            Assert.AreEqual(0, common);
            // 영웅 안에서는 블록 표 순서(연쇄 번개, 유도 …): uid 3(연쇄 번개), uid 2(유도)
            Assert.AreEqual(3, picks[0]);
            Assert.AreEqual(2, picks[1]);
            // 같은 블록이면 uid 오름차순
            CollectionAssert.AreEqual(new[] { 5, 6, 7, 8 }, picks.GetRange(2, 4));
            // 자동 가방은 검증을 통과한다
            Assert.IsTrue(_svc.Commit(s, picks).Ok);
        }

        [Test]
        public void S07_U17__AutoPickSkipsBorrowedAndStopsWhenFull()
        {
            RunSummary s = Run("r-auto2", cleared: 3);   // 1칸, 영웅 0
            s.WildBlocks.Add(Wild(1, Ids.Block.Homing, Ids.Rarity.Hero, borrowed: true));
            s.WildBlocks.Add(Wild(2, Ids.Block.ChainLightning, Ids.Rarity.Hero));
            s.WildBlocks.Add(Wild(3, Ids.Block.Frost, Ids.Rarity.Common));
            s.WildBlocks.Add(Wild(4, Ids.Block.MagicBolt, Ids.Rarity.Common));
            SettlementView v = _svc.Prepare(s);
            Assert.AreEqual(3, v.BagCandidates.Count, "borrowed block is not a candidate");
            // 영웅 한도 0 → 영웅을 건너뛰고 일반 중 표 순서가 앞선 마력탄
            CollectionAssert.AreEqual(new[] { 4 }, _svc.AutoPick(v));
        }

        [Test]
        public void S07_TV_241__BagValidationRejects()
        {
            RunSummary s = Run("r-bad", cleared: 10);   // 3칸, 영웅 1
            s.Purse = 100;
            s.WildBlocks.Add(Wild(1, Ids.Block.Homing, Ids.Rarity.Hero));
            s.WildBlocks.Add(Wild(2, Ids.Block.ChainLightning, Ids.Rarity.Hero));
            s.WildBlocks.Add(Wild(3, Ids.Block.Fireball, Ids.Rarity.Rare));
            s.WildBlocks.Add(Wild(4, Ids.Block.Frost, Ids.Rarity.Common));
            s.WildBlocks.Add(Wild(5, Ids.Block.Pierce, Ids.Rarity.Common));
            s.WildBlocks.Add(Wild(6, Ids.Block.TripleHeat, Ids.Rarity.Hero, borrowed: true));

            Assert.AreEqual(ErrorCode.InvalidArgument, _svc.Commit(s, new[] { 1, 3, 4, 5 }).Error, "slot overflow");
            Assert.AreEqual(ErrorCode.InvalidArgument, _svc.Commit(s, new[] { 1, 2 }).Error, "hero overflow");
            Assert.AreEqual(ErrorCode.InvalidArgument, _svc.Commit(s, new[] { 6 }).Error, "borrowed");
            Assert.AreEqual(ErrorCode.InvalidArgument, _svc.Commit(s, new[] { 99 }).Error, "unknown uid");
            Assert.AreEqual(ErrorCode.InvalidArgument, _svc.Commit(s, new[] { 3, 3 }).Error, "same block twice");

            // 거절은 프로필을 바꾸지 않는다
            Assert.AreEqual(0, P.Currencies.Gold);
            Assert.IsNull(P.LastSettledRunId);
            Assert.AreEqual(0, P.RunHistory.Count);
            Assert.AreEqual(0, _saves);

            // 한도 안이면 된다
            SettlementResult ok = CommitOk(s, 1, 3, 4);
            Assert.AreEqual(3, ok.Bagged.Count);
        }

        [Test]
        public void S08_TV_G_07__LeftoverBlocksBecomeGold()
        {
            // 가져가지 못한 블록 일반 2, 희귀 1, 영웅 1(빌린 블록) → 10 + 15 + 50 = 75
            RunSummary s = Run("r-left", cleared: 2);
            s.WildBlocks.Add(Wild(1, Ids.Block.Frost, Ids.Rarity.Common));
            s.WildBlocks.Add(Wild(2, Ids.Block.Pierce, Ids.Rarity.Common));
            s.WildBlocks.Add(Wild(3, Ids.Block.Fireball, Ids.Rarity.Rare));
            s.WildBlocks.Add(Wild(4, Ids.Block.Homing, Ids.Rarity.Hero, borrowed: true));
            SettlementResult r = CommitOk(s);
            Assert.AreEqual(75, r.GoldFromLeftovers);
            Assert.AreEqual(75, r.GoldGained);
            Assert.AreEqual(75, P.Currencies.Gold);
        }

        [Test]
        public void S07_LeftoverGold__HeroNotBorrowedIsFifty()
        {
            RunSummary s = Run("r-left2", cleared: 0);
            s.WildBlocks.Add(Wild(1, Ids.Block.Homing, Ids.Rarity.Hero));
            Assert.AreEqual(50, CommitOk(s).GoldFromLeftovers);
        }

        [Test]
        public void S08_TV_G_08__PurseGoesToProfileGold()
        {
            // 원정 지갑 1,000에서 회복 150을 쓴 잔액 850 → 프로필 골드 +850
            P.Currencies.Gold = 40;
            RunSummary s = Run("r-purse", cleared: 4);
            s.Purse = 1000 - 150;
            SettlementResult r = CommitOk(s);
            Assert.AreEqual(850, r.GoldGained);
            Assert.AreEqual(0, r.GoldFromLeftovers);
            Assert.AreEqual(890, P.Currencies.Gold);
        }

        [Test]
        public void S08_TV_G_09__SkippedWavesGiveNothing()
        {
            // 웨이브 21에서 출발해 하나도 못 넘김: 가방 0칸, 재료 없음, 골드는 지갑뿐(0)
            RunSummary s = Run("r-skip0", cleared: 0, startWave: 21);
            s.SkippedScore = 28859;
            SettlementView v = _svc.Prepare(s);
            Assert.AreEqual(0, v.Bag.Slots);
            SettlementResult r = CommitOk(s);
            Assert.AreEqual(0, r.GoldGained);
            Assert.AreEqual(0, r.Materials.Count);
            Assert.AreEqual(0, P.Character(1).TotalWaves);
            Assert.AreEqual(0, P.Character(1).BestWave);
            Assert.AreEqual(28859, r.Record.Score, "skipped score still counts as run score");
        }

        [Test]
        public void S07_TV_239__LibraryFullTurnsBagIntoGold()
        {
            // 서고 100개(가득) → 가방에 담은 블록은 골드로
            P.Item(Ids.Block.MagicBolt).Qty = Db.Economy.LibraryBaseCap;
            RunSummary s = Run("r-full", cleared: 6);   // 2칸
            s.WildBlocks.Add(Wild(1, Ids.Block.Fireball, Ids.Rarity.Rare));
            s.WildBlocks.Add(Wild(2, Ids.Block.Frost, Ids.Rarity.Common));
            Assert.AreEqual(0, _svc.Prepare(s).LibraryRoom);
            SettlementResult r = CommitOk(s, 1, 2);
            Assert.AreEqual(0, r.Bagged.Count);
            Assert.AreEqual(15 + 5, r.GoldFromLeftovers);
            Assert.AreEqual(0, Qty(Ids.Block.Fireball));
        }

        [Test]
        public void S07_TV_239__LibraryRoomForOneKeepsFirstPick()
        {
            P.Item(Ids.Block.MagicBolt).Qty = Db.Economy.LibraryBaseCap - 1;
            RunSummary s = Run("r-room1", cleared: 6);
            s.WildBlocks.Add(Wild(1, Ids.Block.Fireball, Ids.Rarity.Rare));
            s.WildBlocks.Add(Wild(2, Ids.Block.Frost, Ids.Rarity.Common));
            Assert.AreEqual(1, _svc.Prepare(s).LibraryRoom);
            SettlementResult r = CommitOk(s, 1, 2);
            Assert.AreEqual(1, r.Bagged.Count);
            Assert.AreEqual(1, r.Bagged[0].Uid);
            Assert.AreEqual(1, Qty(Ids.Block.Fireball));
            Assert.AreEqual(0, Qty(Ids.Block.Frost));
            Assert.AreEqual(5, r.GoldFromLeftovers);
        }

        [Test]
        public void S07_LibraryExpansion__AddsRoom()
        {
            // 확장 한 번 = 50칸
            P.Item(Ids.Block.MagicBolt).Qty = Db.Economy.LibraryBaseCap;
            P.LibraryExpansions = 1;
            Assert.AreEqual(50, _svc.Prepare(Run("r-exp", cleared: 3)).LibraryRoom);
        }

        [Test]
        public void S09_Bag__BaggedBlocksEnterLibrary()
        {
            RunSummary s = Run("r-bag", cleared: 10);
            s.WildBlocks.Add(Wild(1, Ids.Block.Homing, Ids.Rarity.Hero));
            s.WildBlocks.Add(Wild(2, Ids.Block.Fireball, Ids.Rarity.Rare));
            s.WildBlocks.Add(Wild(3, Ids.Block.Fireball, Ids.Rarity.Rare));
            s.WildBlocks.Add(Wild(4, Ids.Block.Frost, Ids.Rarity.Common));
            SettlementResult r = CommitOk(s, 1, 2, 3);
            Assert.AreEqual(1, Qty(Ids.Block.Homing));
            Assert.AreEqual(2, Qty(Ids.Block.Fireball));
            Assert.AreEqual(0, Qty(Ids.Block.Frost));
            Assert.AreEqual(5, r.GoldFromLeftovers);
            Assert.AreEqual(5, Qty(Ids.Block.MagicBolt), "starter library blocks untouched");
        }

        // ------------------------------------------------------------ 시작 웨이브 (01 요약서 4.6절, S09 T080~T085)

        [Test]
        public void S09_T080__Best34()
        {
            CollectionAssert.AreEqual(new[] { 0, 10, 20 }, _svc.StartSkipOptions(34));
        }

        [Test]
        public void S09_T081__Best14OnlyZero()
        {
            CollectionAssert.AreEqual(new[] { 0 }, _svc.StartSkipOptions(14));
        }

        [Test]
        public void S09_T082__Best15()
        {
            CollectionAssert.AreEqual(new[] { 0, 10 }, _svc.StartSkipOptions(15));
        }

        [Test]
        public void S09_T083__Best29()
        {
            CollectionAssert.AreEqual(new[] { 0, 10, 20 }, _svc.StartSkipOptions(29));
        }

        [Test]
        public void S09_T084__NewProfileOnlyZero()
        {
            CollectionAssert.AreEqual(new[] { 0 }, _svc.StartSkipOptions(0));
        }

        [Test]
        public void S09_T085__Skip15Rejected()
        {
            Assert.IsFalse(_svc.IsValidStartSkip(34, 15));
            Assert.IsTrue(_svc.IsValidStartSkip(34, 20));
            Assert.IsFalse(_svc.IsValidStartSkip(34, 30));
            Assert.IsFalse(_svc.IsValidStartSkip(14, 10));
            Assert.IsTrue(_svc.IsValidStartSkip(14, 0));
        }

        [TestCase(28, 10)]
        [TestCase(29, 20)]
        [TestCase(43, 30)]
        [TestCase(100, 70)]
        public void S07_TV_123__MaxSkip(int best, int maxSkip)
        {
            List<int> o = _svc.StartSkipOptions(best);
            Assert.AreEqual(maxSkip, o[o.Count - 1]);
            Assert.AreEqual(maxSkip / 10 + 1, o.Count);
        }

        // ------------------------------------------------------------ 재료 표 (06 요약서 5.2절, S07 TV-250~261)

        sealed class Rolls
        {
            public readonly List<int> Bps = new List<int>();
            public bool Answer;
            public bool Roll(int bp) { Bps.Add(bp); return Answer; }
        }

        static List<MaterialGain> Draw(Rolls rolls, bool crowAllowed, params DropChance[] chances) =>
            SettlementService.DrawMaterials(Db.Drops.MaterialTable, chances, crowAllowed, rolls.Roll);

        static int Count(List<MaterialGain> gains, string item, bool guaranteed)
        {
            int n = 0;
            foreach (MaterialGain g in gains) if (g.ItemId == item && g.Guaranteed == guaranteed) n += g.Qty;
            return n;
        }

        static int Total(List<MaterialGain> gains)
        {
            int n = 0;
            foreach (MaterialGain g in gains) n += g.Qty;
            return n;
        }

        [Test]
        public void S07_TV_250__CrowBundleThreeLowStonesNoRoll()
        {
            var rolls = new Rolls { Answer = true };
            List<MaterialGain> g = Draw(rolls, true, Chance(DropChanceSource.CrowBundle, 3));
            Assert.AreEqual(3, Count(g, Ids.Item.StoneLow, true));
            Assert.AreEqual(3, Total(g));
            Assert.IsEmpty(rolls.Bps);
        }

        [Test]
        public void S07_TV_251__CrowBundleAlreadyGivenIsNothing()
        {
            var rolls = new Rolls { Answer = true };
            Assert.AreEqual(0, Total(Draw(rolls, false, Chance(DropChanceSource.CrowBundle, 3))));
            // 목록에 두 번 있어도 한 번만
            Assert.AreEqual(3, Total(Draw(rolls, true, Chance(DropChanceSource.CrowBundle, 3), Chance(DropChanceSource.CrowBundle, 3))));
        }

        [Test]
        public void S07_TV_252__FifthWaveByDay()
        {
            var rolls = new Rolls { Answer = false };
            List<MaterialGain> w5 = Draw(rolls, true, Chance(DropChanceSource.FifthWave, 5));
            Assert.AreEqual(1, Count(w5, Ids.Item.StoneLow, true));
            Assert.AreEqual(1, Total(w5));

            List<MaterialGain> w15 = Draw(rolls, true, Chance(DropChanceSource.FifthWave, 15));
            Assert.AreEqual(1, Count(w15, Ids.Item.StoneLow, true));
            Assert.AreEqual(1, Count(w15, Ids.Item.StoneMid, true));
            Assert.AreEqual(2, Total(w15));
            Assert.IsEmpty(rolls.Bps, "days 1-2 have no rolls");

            int[] waves = { 25, 35, 55, 65, 95 };
            int[] bps = { 500, 1000, 2000, 2500, 2500 };
            for (int i = 0; i < waves.Length; i++)
            {
                rolls.Bps.Clear();
                List<MaterialGain> g = Draw(rolls, true, Chance(DropChanceSource.FifthWave, waves[i]));
                Assert.AreEqual(1, Count(g, Ids.Item.StoneMid, true), "wave " + waves[i]);
                Assert.AreEqual(1, Total(g), "wave " + waves[i]);
                CollectionAssert.AreEqual(new[] { bps[i] }, rolls.Bps, "wave " + waves[i]);
            }

            // 맞으면 상급 1개
            var hit = new Rolls { Answer = true };
            List<MaterialGain> w25 = Draw(hit, true, Chance(DropChanceSource.FifthWave, 25));
            Assert.AreEqual(1, Count(w25, Ids.Item.StoneHigh, false));
            Assert.AreEqual(2, Total(w25));
        }

        [Test]
        public void S07_TV_253__BossWave10()
        {
            // 하급 1 확정, 중급 5000bp, 부적 200bp (순서: 중급 → 부적)
            var rolls = new Rolls { Answer = true };
            List<MaterialGain> g = Draw(rolls, true, Chance(DropChanceSource.BossKill, 10));
            CollectionAssert.AreEqual(new[] { 5000, 200 }, rolls.Bps);
            Assert.AreEqual(1, Count(g, Ids.Item.StoneLow, true));
            Assert.AreEqual(1, Count(g, Ids.Item.StoneMid, false));
            Assert.AreEqual(1, Count(g, Ids.Item.Charm, false));
            Assert.AreEqual(3, Total(g));

            var miss = new Rolls { Answer = false };
            Assert.AreEqual(1, Total(Draw(miss, true, Chance(DropChanceSource.BossKill, 10))));
        }

        [Test]
        public void S07_TV_254__BossWave20()
        {
            var rolls = new Rolls { Answer = false };
            List<MaterialGain> g = Draw(rolls, true, Chance(DropChanceSource.BossKill, 20));
            CollectionAssert.AreEqual(new[] { 2000, 400 }, rolls.Bps);
            Assert.AreEqual(1, Count(g, Ids.Item.StoneMid, true));
            Assert.AreEqual(1, Total(g));
        }

        [Test]
        public void S07_TV_255__BossWave30ExtraHighIsNotRolled()
        {
            // 중급 1, 상급 1, 추가 0bp(추첨 없음), 부적 600bp
            var rolls = new Rolls { Answer = true };
            List<MaterialGain> g = Draw(rolls, true, Chance(DropChanceSource.BossKill, 30));
            CollectionAssert.AreEqual(new[] { 600 }, rolls.Bps);
            Assert.AreEqual(1, Count(g, Ids.Item.StoneMid, true));
            Assert.AreEqual(1, Count(g, Ids.Item.StoneHigh, true));
            Assert.AreEqual(0, Count(g, Ids.Item.StoneHigh, false));
            Assert.AreEqual(1, Count(g, Ids.Item.Charm, false));
        }

        [TestCase(40, 1000, 800)]
        [TestCase(50, 2000, 1000)]
        [TestCase(60, 3000, 1000)]
        [TestCase(70, 4000, 1000)]
        [TestCase(80, 5000, 1000)]
        [TestCase(100, 5000, 1000)]
        public void S07_TV_256__BossExtraHighAndCharm(int wave, int extraBp, int charmBp)
        {
            var rolls = new Rolls { Answer = true };
            List<MaterialGain> g = Draw(rolls, true, Chance(DropChanceSource.BossKill, wave));
            CollectionAssert.AreEqual(new[] { extraBp, charmBp }, rolls.Bps);
            Assert.AreEqual(1, Count(g, Ids.Item.StoneHigh, true));
            Assert.AreEqual(1, Count(g, Ids.Item.StoneHigh, false));
            Assert.AreEqual(1, Count(g, Ids.Item.StoneMid, true));
            Assert.AreEqual(1, Count(g, Ids.Item.Charm, false));
        }

        [Test]
        public void S07_TV_257__PhantomFirstOneLowStoneNoRoll()
        {
            var rolls = new Rolls { Answer = true };
            List<MaterialGain> g = Draw(rolls, true, Chance(DropChanceSource.PhantomFirst, 10));
            Assert.AreEqual(1, Count(g, Ids.Item.StoneLow, true));
            Assert.AreEqual(1, Total(g));
            Assert.IsEmpty(rolls.Bps);
        }

        [Test]
        public void S07_TV_258__EliteDay2()
        {
            foreach (int wave in new[] { 12, 19 })
            {
                var rolls = new Rolls { Answer = true };
                List<MaterialGain> g = Draw(rolls, true, Chance(DropChanceSource.EliteKill, wave));
                CollectionAssert.AreEqual(new[] { 1000, 800 }, rolls.Bps, "wave " + wave);
                Assert.AreEqual(1, Count(g, Ids.Item.StoneLow, false));
                Assert.AreEqual(1, Count(g, Ids.Item.StoneMid, false));
            }
            // 웨이브 12 전의 엘리트는 표가 없다
            var early = new Rolls { Answer = true };
            Assert.AreEqual(0, Total(Draw(early, true, Chance(DropChanceSource.EliteKill, 11))));
            Assert.IsEmpty(early.Bps);
        }

        [TestCase(25, 200)]
        [TestCase(45, 300)]
        [TestCase(75, 450)]
        [TestCase(85, 500)]
        [TestCase(125, 500)]
        public void S07_TV_259__EliteHighStoneBp(int wave, int highBp)
        {
            // 중급 500, 상급 bp, 부적 50 (줄 순서 그대로)
            var rolls = new Rolls { Answer = false };
            Assert.AreEqual(0, Total(Draw(rolls, true, Chance(DropChanceSource.EliteKill, wave))));
            CollectionAssert.AreEqual(new[] { 500, highBp, 50 }, rolls.Bps);
        }

        [Test]
        public void S07_TV_260__ThreeElitesRollEachRowThreeTimes()
        {
            var rolls = new Rolls { Answer = true };
            List<MaterialGain> g = Draw(rolls, true, Chance(DropChanceSource.EliteKill, 23, 3));
            CollectionAssert.AreEqual(new[] { 500, 200, 50, 500, 200, 50, 500, 200, 50 }, rolls.Bps);
            Assert.AreEqual(3, Count(g, Ids.Item.StoneMid, false));
            Assert.AreEqual(3, Count(g, Ids.Item.StoneHigh, false));
            Assert.AreEqual(3, Count(g, Ids.Item.Charm, false));
        }

        [Test]
        public void S07_TV_261__NoLowStoneFromWave21()
        {
            var rolls = new Rolls { Answer = true };
            for (int w = 21; w <= 130; w++)
            {
                var chances = new List<DropChance> { Chance(DropChanceSource.EliteKill, w, 2) };
                if (DayCycle.Slot(w) == 5) chances.Add(Chance(DropChanceSource.FifthWave, w));
                if (DayCycle.IsBossWave(w)) chances.Add(Chance(DropChanceSource.BossKill, w));
                List<MaterialGain> g = Draw(rolls, true, chances.ToArray());
                Assert.AreEqual(0, Count(g, Ids.Item.StoneLow, true) + Count(g, Ids.Item.StoneLow, false), "wave " + w);
            }
        }

        [Test]
        public void S07_Materials__CommitAddsFixedAndRolledToProfile()
        {
            // 웨이브 5·10(가마등)·15: 확정 하급 1 + 하급 1 + (하급 1·중급 1). 추첨분은 주입한 난수에 달렸다.
            RunSummary s = Run("r-mat", cleared: 15);
            s.DropChances.Add(Chance(DropChanceSource.FifthWave, 5));
            s.DropChances.Add(Chance(DropChanceSource.BossKill, 10));
            s.DropChances.Add(Chance(DropChanceSource.FifthWave, 15));
            SettlementResult r = CommitOk(s);
            Assert.AreEqual(3, Gained(r, Ids.Item.StoneLow, true));
            Assert.AreEqual(1, Gained(r, Ids.Item.StoneMid, true));
            Assert.AreEqual(Gained(r, Ids.Item.StoneLow), Qty(Ids.Item.StoneLow));
            Assert.AreEqual(Gained(r, Ids.Item.StoneMid), Qty(Ids.Item.StoneMid));
            Assert.AreEqual(Gained(r, Ids.Item.Charm), Qty(Ids.Item.Charm));
            Assert.That(Gained(r, Ids.Item.StoneMid, false), Is.InRange(0, 1));
            Assert.That(Gained(r, Ids.Item.Charm, false), Is.InRange(0, 1));
            Assert.AreEqual(0, Gained(r, Ids.Item.StoneHigh));
        }

        [Test]
        public void S07_Materials__SameSeedSameDraw()
        {
            // 난수기를 주입하면 추첨이 되풀이된다(테스트 가능성). 엘리트 40마리 × 3줄.
            var chances = new[] { Chance(DropChanceSource.EliteKill, 45, 40) };
            var a = SettlementService.DrawMaterials(Db.Drops.MaterialTable, chances, true, new Pcg32(99UL).RollBp);
            var b = SettlementService.DrawMaterials(Db.Drops.MaterialTable, chances, true, new Pcg32(99UL).RollBp);
            Assert.AreEqual(Total(a), Total(b));
            Assert.AreEqual(Count(a, Ids.Item.StoneMid, false), Count(b, Ids.Item.StoneMid, false));
            Assert.That(Total(a), Is.LessThanOrEqualTo(120));
        }

        // ------------------------------------------------------------ 까마귀 보따리·강화 안내 (06 요약서 5.2-A·5.4절, 10 요약서 6절)

        [Test]
        public void S07_TV_250__CrowBundleOncePerProfileAndTutorialHint()
        {
            RunSummary first = Run("r-crow1", cleared: 4);
            first.CrowBundleEarned = true;
            first.DropChances.Add(Chance(DropChanceSource.CrowBundle, 3));
            SettlementResult r1 = CommitOk(first);
            Assert.IsTrue(r1.CrowBundle);
            Assert.IsTrue(P.Flags.CrowBundleGiven);
            Assert.AreEqual(3, Qty(Ids.Item.StoneLow));
            Assert.IsTrue(r1.ShowEnhanceTutorial, "first low stones → enhance guide");

            // TV-251: 받은 프로필은 다시 받지 못한다
            RunSummary second = Run("r-crow2", cleared: 5);
            second.DropChances.Add(Chance(DropChanceSource.CrowBundle, 3));
            second.DropChances.Add(Chance(DropChanceSource.FifthWave, 5));
            SettlementResult r2 = CommitOk(second);
            Assert.IsFalse(r2.CrowBundle);
            Assert.AreEqual(4, Qty(Ids.Item.StoneLow));
            Assert.IsFalse(r2.ShowEnhanceTutorial, "guide only on the first settlement that gives low stones");
        }

        [Test]
        public void S07_CrowBundle__EarnedFlagWithoutChanceEntryStillPays()
        {
            RunSummary s = Run("r-crow3", cleared: 3);
            s.CrowBundleEarned = true;
            SettlementResult r = CommitOk(s);
            Assert.IsTrue(r.CrowBundle);
            Assert.AreEqual(3, Qty(Ids.Item.StoneLow));
        }

        [Test]
        public void S07_EnhanceTutorial__NotShownWhenAlreadySeenOrNoLowStone()
        {
            P.Flags.EnhanceTutorialShown = true;
            RunSummary s = Run("r-tut", cleared: 5);
            s.DropChances.Add(Chance(DropChanceSource.FifthWave, 5));
            Assert.IsFalse(CommitOk(s).ShowEnhanceTutorial);

            P.Flags.EnhanceTutorialShown = false;
            Assert.IsFalse(CommitOk(Run("r-tut2", cleared: 2)).ShowEnhanceTutorial, "no low stone gained");
        }

        [Test]
        public void S10_6__AbnormalExitAfterWave3BeforeCheckpointGivesNothing()
        {
            // 웨이브 3을 넘긴 뒤 중간 지점(5) 전에 비정상 종료: 정산할 것이 없다 → 보따리 못 받음, 표시 그대로
            Assert.IsNull(_svc.SettlePendingCheckpoint());
            Assert.IsFalse(P.Flags.CrowBundleGiven);
            Assert.AreEqual(0, Qty(Ids.Item.StoneLow));
            Assert.AreEqual(0, P.RunHistory.Count);
            Assert.AreEqual(0, _saves);
        }

        // ------------------------------------------------------------ 첫 만남·복수 (06 요약서 5.7절, 05 요약서 10.3·10.4절)

        RunSummary FirstMeetRun(string id, int cleared)
        {
            RunSummary s = Run(id, cleared);
            s.PhantomFirst = true;
            s.PhantomFirstDone = cleared >= 10;
            s.RevengeActive = cleared >= 10;
            if (cleared >= 10) s.DropChances.Add(Chance(DropChanceSource.PhantomFirst, 10));
            return s;
        }

        [Test]
        public void S07_FirstMeet__Wave10ClearedSetsPhantomMet()
        {
            SettlementResult r = CommitOk(FirstMeetRun("r-meet", 10));
            Assert.IsTrue(P.Flags.PhantomMet);
            Assert.IsTrue(r.PhantomMetNow);
            Assert.IsFalse(r.RevengeReward);
            // 재료는 하급 1 확정만
            Assert.AreEqual(1, Gained(r, Ids.Item.StoneLow, true));
            Assert.AreEqual(1, Qty(Ids.Item.StoneLow));
            Assert.AreEqual(0, Qty(Ids.Item.StoneMid));
            // 웨이브 10을 직접 넘긴 것으로 센다: 최고 웨이브, 칭호 "첫 밤을 넘긴"
            Assert.AreEqual(10, P.Character(1).BestWave);
            CollectionAssert.Contains(r.NewTitles, Ids.Title.FirstNight);
            CollectionAssert.Contains(P.Titles.Owned, Ids.Title.FirstNight);
            Assert.AreEqual(RevengeState.Pursuing, P.RevengeState);
            Assert.AreEqual(1, P.Revenge.RunsSinceFirstMeet);
        }

        [Test]
        public void S07_TV_305__GiveUpDuringFirstMeetKeepsFlag()
        {
            // 첫 만남 도중 판 포기 → 웨이브 10 미클리어, 표시 유지, 웨이브 1~9 정산
            RunSummary s = FirstMeetRun("r-quit", 9);
            s.EndReason = RunEndReason.Abandon;
            s.Purse = 300;
            SettlementResult r = CommitOk(s);
            Assert.IsFalse(P.Flags.PhantomMet);
            Assert.IsFalse(r.PhantomMetNow);
            Assert.AreEqual(300, P.Currencies.Gold);
            Assert.AreEqual(9, P.Character(1).BestWave);
            Assert.AreEqual(0, P.Revenge.RunsSinceFirstMeet);
            CollectionAssert.DoesNotContain(P.Titles.Owned, Ids.Title.FirstNight);
            Assert.AreEqual("Abandon", r.Record.EndReason);
        }

        RunSummary RevengeKillRun(string id)
        {
            RunSummary s = Run(id, cleared: 22);
            s.RevengePending = true;
            s.RevengeActive = true;
            s.PhantomAttempted = true;
            s.KilledPhantomAtWave20 = true;
            s.PhantomDentPercent = 100;
            return s;
        }

        [Test]
        public void S07_TV_309__RevengeRewardOnce()
        {
            // 웨이브 20 허상 첫 처치 뒤 웨이브 23에서 사망 → 복수 보상(칭호, 머리 장식, 귀속 중급 3·상급 1·부적 1)
            P.Flags.PhantomMet = true;
            P.Revenge.RunsSinceFirstMeet = 3;
            SettlementResult r = CommitOk(RevengeKillRun("r-rev"));
            Assert.IsTrue(r.RevengeReward);
            CollectionAssert.Contains(r.NewTitles, Ids.Title.MirrorBreaker);
            CollectionAssert.Contains(P.Titles.Owned, Ids.Title.MirrorBreaker);
            CollectionAssert.Contains(P.Cosmetics.Owned, Ids.Cosmetic.CrackedMirrorShard);
            Assert.AreEqual(3, Qty(Ids.Item.StoneMid));
            Assert.AreEqual(3, Bound(Ids.Item.StoneMid));
            Assert.AreEqual(1, Qty(Ids.Item.StoneHigh));
            Assert.AreEqual(1, Bound(Ids.Item.StoneHigh));
            Assert.AreEqual(1, Qty(Ids.Item.Charm));
            Assert.AreEqual(1, Bound(Ids.Item.Charm));
            foreach (MaterialGain g in r.Materials) Assert.IsTrue(g.Bound && g.Guaranteed);
            Assert.IsNotNull(P.Flags.RevengeDoneAt);
            Assert.AreEqual(RevengeState.Done, P.RevengeState);
            Assert.AreEqual(4, P.Revenge.RevengeRuns, "runs from first meet through the revenge run");
            Assert.AreEqual(1, P.Revenge.PhantomTries);
            Assert.AreEqual(0, P.Revenge.PhantomBestDent, "dent is only from lost fights");

            RevengeTrackerView tracker = RevengeTracker.Build(P, Db, completedThisRun: r.RevengeReward);
            Assert.IsTrue(tracker.JustCompleted);
            Assert.AreEqual(MirrorMark.Broken, tracker.Mirror);
            Assert.AreEqual(4, tracker.RunsToComplete);
        }

        [Test]
        public void S07_TV_309__BossKillMaterialsAreSeparateFromRevengeReward()
        {
            // 보스 처치 보상(표 C 2일째: 중급 1 확정)은 복수 보상과 따로 받는다
            P.Flags.PhantomMet = true;
            RunSummary s = RevengeKillRun("r-rev-boss");
            s.DropChances.Add(Chance(DropChanceSource.BossKill, 20));
            SettlementResult r = CommitOk(s);
            int boundMid = 0, freeFixedMid = 0;
            foreach (MaterialGain g in r.Materials)
                if (g.ItemId == Ids.Item.StoneMid)
                {
                    if (g.Bound) boundMid += g.Qty; else if (g.Guaranteed) freeFixedMid += g.Qty;
                }
            Assert.AreEqual(3, boundMid);
            Assert.AreEqual(1, freeFixedMid);
            Assert.AreEqual(4, Qty(Ids.Item.StoneMid));
            Assert.AreEqual(3, Bound(Ids.Item.StoneMid));
        }

        [Test]
        public void S07_TV_310__RevengeDoneProfileGetsNoSecondReward()
        {
            P.Flags.PhantomMet = true;
            CommitOk(RevengeKillRun("r-rev1"));
            string doneAt = P.Flags.RevengeDoneAt;
            int runs = P.Revenge.RevengeRuns, tries = P.Revenge.PhantomTries;

            _clock.Value = _clock.Value.AddDays(1);
            SettlementResult again = CommitOk(RevengeKillRun("r-rev2"));
            Assert.IsFalse(again.RevengeReward);
            Assert.AreEqual(0, again.Materials.Count);
            CollectionAssert.DoesNotContain(again.NewTitles, Ids.Title.MirrorBreaker);
            Assert.AreEqual(3, Qty(Ids.Item.StoneMid));
            Assert.AreEqual(doneAt, P.Flags.RevengeDoneAt);
            Assert.AreEqual(runs, P.Revenge.RevengeRuns);
            Assert.AreEqual(tries, P.Revenge.PhantomTries, "tracker stops after revenge is done");
        }

        [Test]
        public void S06_T267__FirstMeetAndWave20KillInSameRun()
        {
            // 계약: 같은 판에서 첫 만남을 치르고 웨이브 20 허상까지 잡음(RevengePending false, RevengeActive true) → 복수 보상 1회
            RunSummary s = FirstMeetRun("r-same", 20);
            s.RevengePending = false;
            s.RevengeActive = true;
            s.PhantomAttempted = true;
            s.KilledPhantomAtWave20 = true;
            SettlementResult r = CommitOk(s);
            Assert.IsTrue(r.PhantomMetNow);
            Assert.IsTrue(P.Flags.PhantomMet);
            Assert.IsTrue(r.RevengeReward);
            Assert.AreEqual(3, Bound(Ids.Item.StoneMid));
            Assert.AreEqual(1, P.Revenge.RevengeRuns);
            Assert.AreEqual(RevengeState.Done, P.RevengeState);
            CollectionAssert.Contains(P.Titles.Owned, Ids.Title.MirrorBreaker);
            CollectionAssert.Contains(P.Titles.Owned, Ids.Title.FirstNight);
        }

        [Test]
        public void Contract__RevengeActiveFalseGivesNoReward()
        {
            // 복수 중이 아닌 판(RevengeActive false)의 웨이브 20 처치는 복수 보상이 아니다
            RunSummary s = Run("r-noactive", cleared: 20);
            s.KilledPhantomAtWave20 = true;
            s.RevengePending = false;
            s.RevengeActive = false;
            SettlementResult r = CommitOk(s);
            Assert.IsFalse(r.RevengeReward);
            Assert.IsNull(P.Flags.RevengeDoneAt);
            Assert.AreEqual(0, Qty(Ids.Item.StoneMid));
            CollectionAssert.DoesNotContain(P.Titles.Owned, Ids.Title.MirrorBreaker);
            CollectionAssert.DoesNotContain(P.Cosmetics.Owned, Ids.Cosmetic.CrackedMirrorShard);
        }

        [Test]
        public void S06_T280__DentPercent62()
        {
            Assert.AreEqual(62, SettlementService.DentPercent(7111, 2702));
            Assert.AreEqual(0, SettlementService.DentPercent(7111, 7111));
            Assert.AreEqual(100, SettlementService.DentPercent(7111, 0));
        }

        [Test]
        public void S06_Tracker__TriesAndBestDentFromLostFights()
        {
            P.Flags.PhantomMet = true;
            RunSummary lost = Run("r-dent1", cleared: 19);
            lost.RevengePending = true;
            lost.RevengeActive = true;
            lost.PhantomAttempted = true;
            lost.PhantomDentPercent = SettlementService.DentPercent(7111, 2702);
            CommitOk(lost);
            Assert.AreEqual(1, P.Revenge.PhantomTries);
            Assert.AreEqual(62, P.Revenge.PhantomBestDent);
            // 흠집 50% 이상이면 "금 간 거울"
            RevengeTrackerView t = RevengeTracker.Build(P, Db);
            Assert.IsTrue(t.Visible);
            Assert.IsTrue(t.HasDent);
            Assert.AreEqual(MirrorMark.Cracked, t.Mirror);
            Assert.AreEqual(19, t.Progress);

            RunSummary worse = Run("r-dent2", cleared: 19);
            worse.RevengePending = true;
            worse.RevengeActive = true;
            worse.PhantomAttempted = true;
            worse.PhantomDentPercent = 40;
            CommitOk(worse);
            Assert.AreEqual(2, P.Revenge.PhantomTries);
            Assert.AreEqual(62, P.Revenge.PhantomBestDent, "max is kept");

            // 웨이브 20에 닿지 못한 판은 도전으로 세지 않는다
            RunSummary early = Run("r-dent3", cleared: 12);
            early.RevengePending = true;
            early.RevengeActive = true;
            CommitOk(early);
            Assert.AreEqual(2, P.Revenge.PhantomTries);
            Assert.AreEqual(3, P.Revenge.RunsSinceFirstMeet);
        }

        [Test]
        public void S06_Tracker__NothingCountedBeforeFirstMeet()
        {
            CommitOk(Run("r-before", cleared: 6));
            Assert.AreEqual(0, P.Revenge.RunsSinceFirstMeet);
            Assert.AreEqual(0, P.Revenge.PhantomTries);
            Assert.IsFalse(RevengeTracker.Build(P, Db).Visible);
        }

        // ------------------------------------------------------------ 기록·칭호·야생 물 (01 요약서 10.5절, D-25)

        [Test]
        public void S01_Records__BestScoreBestWaveTotalWaves()
        {
            RunSummary a = Run("r-rec1", cleared: 7);
            a.Score = 5000;
            SettlementView view = _svc.Prepare(a);
            Assert.IsTrue(view.NewBestScore);
            Assert.IsTrue(view.NewBestWave);
            SettlementResult ra = CommitOk(a);
            Assert.IsTrue(ra.NewBestScore);
            Assert.IsTrue(ra.NewBestWave);
            CharacterRecord rec = P.Character(1);
            Assert.AreEqual(5000, rec.BestScore);
            Assert.AreEqual(7, rec.BestWave);
            Assert.AreEqual(7, rec.TotalWaves);

            RunSummary b = Run("r-rec2", cleared: 4);
            b.Score = 5000;
            SettlementResult rb = CommitOk(b);
            Assert.IsFalse(rb.NewBestScore, "equal score is not a new best");
            Assert.IsFalse(rb.NewBestWave);
            Assert.AreEqual(7, rec.BestWave);
            Assert.AreEqual(11, rec.TotalWaves);

            // 캐릭터별로 따로
            RunSummary c = Run("r-rec3", cleared: 2);
            c.CharId = Ids.Char.C3;
            c.Score = 10;
            CommitOk(c);
            Assert.AreEqual(2, P.Character(3).BestWave);
            Assert.AreEqual(10, P.Character(3).BestScore);
            Assert.AreEqual(7, P.Character(1).BestWave);
        }

        [Test]
        public void S01_Records__BestWaveIsLastClearedNotReached()
        {
            // 웨이브 21에서 출발해 21~25를 넘김: 넘긴 최고 웨이브 25, 누적은 직접 넘긴 5
            P.Character(1).BestWave = 29;
            RunSummary s = Run("r-rec4", cleared: 5, startWave: 21);
            SettlementResult r = CommitOk(s);
            Assert.IsFalse(r.NewBestWave);
            Assert.AreEqual(29, P.Character(1).BestWave);
            Assert.AreEqual(5, P.Character(1).TotalWaves);
            Assert.AreEqual(26, r.Record.ReachedWave);
            Assert.AreEqual(21, r.Record.StartWave);
        }

        [Test]
        public void S01_Records__RunScoreIsWaveSumPlusSkipped()
        {
            RunSummary s = Run("r-score", cleared: 2, startWave: 11);
            s.SkippedScore = 8398;
            s.WaveResults.Add(Result(11, false, 700));
            s.WaveResults.Add(Result(12, false, 900));
            SettlementView v = _svc.Prepare(s);
            Assert.AreEqual(8398, v.Score.Skipped);
            Assert.AreEqual(1600, v.Score.Clear);
            Assert.AreEqual(8398 + 1600, v.Score.Total);
            SettlementResult r = CommitOk(s);
            Assert.AreEqual(8398 + 1600, r.Record.Score);
            Assert.AreEqual(8398 + 1600, P.Character(1).BestScore);
        }

        [TestCase(99, 1)]
        [TestCase(100, 2)]
        [TestCase(500, 3)]
        [TestCase(1999, 3)]
        [TestCase(2000, 4)]
        public void S01_WildStain__StageByTotalWaves(int totalAfter, int stage)
        {
            // 누적 0/100/500/2000에서 4단계
            P.Character(1).TotalWaves = totalAfter - 5;
            CommitOk(Run("r-stain", cleared: 5));
            Assert.AreEqual(totalAfter, P.Character(1).TotalWaves);
            Assert.AreEqual(stage, P.Character(1).WildStainStageMax);
        }

        [Test]
        public void S01_WildStain__NeverGoesBack()
        {
            P.Character(1).WildStainStageMax = 3;
            CommitOk(Run("r-stain2", cleared: 5));
            Assert.AreEqual(3, P.Character(1).WildStainStageMax);
        }

        [TestCase(9, false, false, false)]
        [TestCase(10, true, false, false)]
        [TestCase(29, true, false, false)]
        [TestCase(30, true, true, false)]
        [TestCase(100, true, true, true)]
        public void S01_RecordTitles__Wave10_30_100(int cleared, bool firstNight, bool threeNights, bool tenDays)
        {
            RunSummary s = Run("r-title", cleared);
            SettlementView v = _svc.Prepare(s);
            SettlementResult r = CommitOk(s);
            Assert.AreEqual(firstNight, P.Titles.Owned.Contains(Ids.Title.FirstNight));
            Assert.AreEqual(threeNights, P.Titles.Owned.Contains(Ids.Title.ThreeNights));
            Assert.AreEqual(tenDays, P.Titles.Owned.Contains(Ids.Title.TenDays));
            CollectionAssert.AreEqual(v.NewTitles, r.NewTitles, "Prepare predicts what Commit grants");
        }

        [Test]
        public void S01_RecordTitles__GrantedOnlyOnce()
        {
            CommitOk(Run("r-t1", cleared: 10));
            SettlementResult r = CommitOk(Run("r-t2", cleared: 12));
            Assert.IsEmpty(r.NewTitles);
            Assert.AreEqual(1, P.Titles.Owned.FindAll(t => t == Ids.Title.FirstNight).Count);
        }

        [Test]
        public void D25_Bloom100__CumulativeBloomCasts()
        {
            P.Feats.BloomCount = 90;
            RunSummary a = Run("r-bloom1", cleared: 1);
            a.BloomCasts = 9;
            Assert.IsEmpty(CommitOk(a).NewTitles);
            Assert.AreEqual(99, P.Feats.BloomCount);

            RunSummary b = Run("r-bloom2", cleared: 1);
            b.BloomCasts = 1;
            SettlementResult r = CommitOk(b);
            Assert.AreEqual(100, P.Feats.BloomCount);
            CollectionAssert.Contains(r.NewTitles, Ids.Title.Bloom100);
            CollectionAssert.Contains(P.Titles.Owned, Ids.Title.Bloom100);
        }

        [Test]
        public void D25_FlawlessNight__Waves8And9OfSameDayNoHit()
        {
            RunSummary hit9 = Run("r-fl1", cleared: 9);
            hit9.WaveResults.Add(Result(8, true));
            hit9.WaveResults.Add(Result(9, false));
            CollectionAssert.DoesNotContain(CommitOk(hit9).NewTitles, Ids.Title.FlawlessNight);

            // 다른 날의 8과 9(웨이브 8과 19)는 아니다
            RunSummary split = Run("r-fl2", cleared: 19);
            split.WaveResults.Add(Result(8, true));
            split.WaveResults.Add(Result(9, false));
            split.WaveResults.Add(Result(18, false));
            split.WaveResults.Add(Result(19, true));
            CollectionAssert.DoesNotContain(CommitOk(split).NewTitles, Ids.Title.FlawlessNight);

            // 2일째의 웨이브 18·19 무피격
            RunSummary ok = Run("r-fl3", cleared: 19);
            ok.WaveResults.Add(Result(8, false));
            ok.WaveResults.Add(Result(9, true));
            ok.WaveResults.Add(Result(18, true));
            ok.WaveResults.Add(Result(19, true));
            SettlementResult r = CommitOk(ok);
            CollectionAssert.Contains(r.NewTitles, Ids.Title.FlawlessNight);
            CollectionAssert.Contains(P.Titles.Owned, Ids.Title.FlawlessNight);
        }

        [Test]
        public void D25_FlawlessNight__Wave8WithoutWave9IsNot()
        {
            // 웨이브 9에서 죽은 판: 9는 넘긴 웨이브가 아니다
            RunSummary s = Run("r-fl4", cleared: 8);
            s.WaveResults.Add(Result(8, true));
            CollectionAssert.DoesNotContain(CommitOk(s).NewTitles, Ids.Title.FlawlessNight);
        }

        [Test]
        public void S04_NewArts__RecordedOnSettlementAndImmediately()
        {
            RunSummary s = Run("r-art", cleared: 4);
            s.NewArts.Add(Ids.Art.Fan);
            SettlementResult r = CommitOk(s);
            CollectionAssert.AreEqual(new[] { Ids.Art.Fan }, r.NewArts);
            CollectionAssert.Contains(P.Character(1).UnlockedArts, Ids.Art.Fan);
            Assert.AreEqual(1, P.Character(1).UnlockedArts.FindAll(a => a == Ids.Art.Fan).Count);

            // 판 중 해금은 바로 저장된다(다음 실행에도 남는다)
            _saves = 0;
            _svc.RecordArtUnlock(2, Ids.Art.Wings);
            Assert.AreEqual(1, _saves);
            _svc.RecordArtUnlock(2, Ids.Art.Wings);
            Assert.AreEqual(1, _saves, "already recorded → no second save");
            _svc.RecordArtUnlock(9, Ids.Art.Wings);
            Assert.AreEqual(1, _saves, "unknown character is ignored");
            Relaunch(out ProfileStore reloaded);
            CollectionAssert.Contains(reloaded.Current.Character(2).UnlockedArts, Ids.Art.Wings);
            CollectionAssert.DoesNotContain(reloaded.Current.Character(1).UnlockedArts, Ids.Art.Wings);
        }

        // ------------------------------------------------------------ 계약 벡터: 두 번 정산, 판 목록, 알아낸 약점, 연습 판

        [Test]
        public void Contract__SameRunCommittedTwiceIsRejected()
        {
            RunSummary s = Run("r-twice", cleared: 5);
            s.Purse = 200;
            s.DropChances.Add(Chance(DropChanceSource.FifthWave, 5));
            CommitOk(s);
            Assert.AreEqual("r-twice", P.LastSettledRunId);
            long gold = P.Currencies.Gold;
            int low = Qty(Ids.Item.StoneLow);

            Result<SettlementResult> again = _svc.Commit(s, new int[0]);
            Assert.IsFalse(again.Ok);
            Assert.AreEqual(ErrorCode.NotEligible, again.Error);
            Assert.AreEqual(gold, P.Currencies.Gold);
            Assert.AreEqual(low, Qty(Ids.Item.StoneLow));
            Assert.AreEqual(1, P.RunHistory.Count);
            Assert.AreEqual(5, P.Character(1).TotalWaves);

            // 다음 실행에서도 막힌다(프로필에 적혀 있다)
            SettlementService next = Relaunch(out ProfileStore store);
            Assert.AreEqual("r-twice", store.Current.LastSettledRunId);
            Assert.IsFalse(next.Commit(s, new int[0]).Ok);
        }

        [Test]
        public void D55_RunHistory__OneRowPerSettlement()
        {
            RunSummary s = Run("r-hist", cleared: 12);
            s.CharId = Ids.Char.C4;
            s.WandStage = 7;
            s.Score = 12345;
            s.EndReason = RunEndReason.HpZero;
            s.LoadoutIds.Add(Ids.Block.Fireball);
            s.LoadoutIds.Add(null);
            s.LoadoutIds.Add(Ids.Block.Pierce);
            SettlementResult r = CommitOk(s);

            Assert.AreEqual(1, P.RunHistory.Count);
            RunRecordData row = P.RunHistory[0];
            Assert.AreSame(row, r.Record);
            Assert.AreEqual("r-hist", row.RunId);
            Assert.AreEqual(4, row.Character);
            Assert.AreEqual(13, row.ReachedWave);
            Assert.AreEqual(1, row.StartWave);
            Assert.AreEqual(12345, row.Score);
            Assert.AreEqual(7, row.WandStage);
            CollectionAssert.AreEqual(new[] { Ids.Block.Fireball, null, Ids.Block.Pierce }, row.Loadout);
            Assert.AreEqual("HpZero", row.EndReason);
            Assert.AreEqual(_clock.Value, DateTime.Parse(row.Date, System.Globalization.CultureInfo.InvariantCulture));
            StringAssert.StartsWith("2026-11-18T15:30:00", row.Date);
            Assert.IsFalse(r.FromCheckpoint);
        }

        [Test]
        public void D55_RunHistory__OldestDroppedOverCap()
        {
            // 솔로 임시: 최근 100판
            for (int i = 1; i <= 100; i++) P.RunHistory.Add(new RunRecordData { RunId = "old-" + i });
            CommitOk(Run("r-new1", cleared: 1));
            Assert.AreEqual(100, P.RunHistory.Count);
            Assert.AreEqual("old-2", P.RunHistory[0].RunId);
            Assert.AreEqual("r-new1", P.RunHistory[99].RunId);
            CommitOk(Run("r-new2", cleared: 1));
            Assert.AreEqual(100, P.RunHistory.Count);
            Assert.AreEqual("old-3", P.RunHistory[0].RunId);
            Assert.AreEqual("r-new2", P.RunHistory[99].RunId);
        }

        [Test]
        public void D54_KnownAffinities__MergedWithoutDuplicates()
        {
            string a = AffinityKey.Of("enemy.slime_moss", Affinity.Weak);
            string b = AffinityKey.Of("enemy.slime_moss", Affinity.Resist);
            string c = AffinityKey.Of("enemy.wolf_ash", Affinity.Weak);
            P.KnownAffinities.Add(a);
            RunSummary s = Run("r-aff", cleared: 2);
            s.AffinityKnown.Add(a);
            s.AffinityKnown.Add(b);
            s.AffinityKnown.Add(c);
            s.AffinityKnown.Add(c);
            CommitOk(s);
            CollectionAssert.AreEqual(new[] { a, b, c }, P.KnownAffinities);
        }

        [TestCase(RunMode.Practice)]
        [TestCase(RunMode.Tutorial)]
        public void Contract__PracticeSummaryIsNotSettledOrCheckpointed(RunMode mode)
        {
            RunSummary s = Run("r-practice", cleared: 5);
            s.Mode = mode;
            s.Purse = 999;
            s.DropChances.Add(Chance(DropChanceSource.FifthWave, 5));
            Result<SettlementResult> r = _svc.Commit(s, new int[0]);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(ErrorCode.NotEligible, r.Error);
            _svc.SaveCheckpoint(s);
            Assert.IsNull(P.RunCheckpoint);
            Assert.AreEqual(0, P.Currencies.Gold);
            Assert.AreEqual(0, P.RunHistory.Count);
            Assert.IsNull(P.LastSettledRunId);
            Assert.AreEqual(0, _saves);
            Assert.IsEmpty(_svc.Prepare(s).BagCandidates);
        }

        [Test]
        public void Contract__EmptyRunIdIsNotTreatedAsAlreadySettled()
        {
            // RunId가 빈 판끼리는 같은 판으로 보지 않는다(막을 근거가 없다)
            RunSummary s = Run(null, cleared: 1);
            Assert.IsTrue(_svc.Commit(s, null).Ok);
            Assert.IsTrue(_svc.Commit(Run(null, cleared: 1), null).Ok);
        }

        [Test]
        public void Commit__SavesProfileOnce()
        {
            CommitOk(Run("r-save", cleared: 3));
            Assert.AreEqual(1, _saves);
            Relaunch(out ProfileStore reloaded);
            Assert.AreEqual("r-save", reloaded.Current.LastSettledRunId);
            Assert.AreEqual(1, reloaded.Current.RunHistory.Count);
        }

        // ------------------------------------------------------------ 중간 지점과 자동 정산 (D-23·D-68, 06 요약서 5.5절)

        RunSummary CheckpointRun(string id)
        {
            RunSummary s = Run(id, cleared: 5);
            s.EndReason = RunEndReason.None;
            s.Purse = 380;
            s.Score = 4321;
            s.CrowBundleEarned = true;
            s.DropChances.Add(Chance(DropChanceSource.CrowBundle, 3));
            s.DropChances.Add(Chance(DropChanceSource.FifthWave, 5));
            s.WildBlocks.Add(Wild(1, Ids.Block.Fireball, Ids.Rarity.Rare));
            s.WildBlocks.Add(Wild(2, Ids.Block.Frost, Ids.Rarity.Common));
            s.WildBlocks.Add(Wild(3, Ids.Block.Homing, Ids.Rarity.Hero, borrowed: true));
            s.NewArts.Add(Ids.Art.Fan);
            s.AffinityKnown.Add(AffinityKey.Of("enemy.slime_moss", Affinity.Weak));
            s.LoadoutIds.Add(Ids.Block.MagicBolt);
            var wr = Result(5, true, 900);
            wr.Stats.Whims.Add(Ids.Whim.Crowding);
            s.WaveResults.Add(wr);
            return s;
        }

        [Test]
        public void D23_Checkpoint__SavedToProfile()
        {
            _svc.SaveCheckpoint(CheckpointRun("r-cp"));
            Assert.AreEqual(1, _saves);
            Assert.IsNotNull(P.RunCheckpoint);
            Assert.AreEqual("r-cp", P.RunCheckpoint.RunId);
            Assert.AreEqual(5, P.RunCheckpoint.Wave);
            Assert.IsNotNull(P.RunCheckpoint.Summary);
            // 중간 지점은 아직 정산이 아니다
            Assert.AreEqual(0, P.Currencies.Gold);
            Assert.IsNull(P.LastSettledRunId);
        }

        [Test]
        public void D23_Checkpoint__CodecRoundTrip()
        {
            RunSummary s = CheckpointRun("r-codec");
            RunSummary back = CheckpointCodec.FromJson(CheckpointCodec.ToJson(s));
            Assert.AreEqual(s.RunId, back.RunId);
            Assert.AreEqual(s.CharId, back.CharId);
            Assert.AreEqual(RunMode.Endless, back.Mode);
            Assert.AreEqual(5, back.ClearedDirect);
            Assert.AreEqual(5, back.LastClearedWave);
            Assert.AreEqual(380, back.Purse);
            Assert.AreEqual(4321, back.Score);
            Assert.IsTrue(back.CrowBundleEarned);
            Assert.AreEqual(2, back.DropChances.Count);
            Assert.AreEqual(DropChanceSource.FifthWave, back.DropChances[1].Source);
            Assert.AreEqual(5, back.DropChances[1].Wave);
            Assert.AreEqual(3, back.WildBlocks.Count);
            Assert.IsTrue(back.WildBlocks[2].Borrowed);
            Assert.AreEqual(Ids.Rarity.Rare, back.WildBlocks[0].Rarity);
            CollectionAssert.AreEqual(s.NewArts, back.NewArts);
            CollectionAssert.AreEqual(s.AffinityKnown, back.AffinityKnown);
            CollectionAssert.AreEqual(s.LoadoutIds, back.LoadoutIds);
            Assert.AreEqual(1, back.WaveResults.Count);
            Assert.AreEqual(900, back.WaveResults[0].Score.Total);
            Assert.IsTrue(back.WaveResults[0].Stats.NoHit);
            CollectionAssert.AreEqual(new[] { Ids.Whim.Crowding }, back.WaveResults[0].Stats.Whims);
            Assert.IsNull(CheckpointCodec.FromJson(null));
        }

        [Test]
        public void D68_SettlePendingCheckpoint__AutoSettlesOnNextLaunch()
        {
            _svc.SaveCheckpoint(CheckpointRun("r-crash"));

            // 다음 실행: 디스크의 프로필에서 중간 지점까지 자동 정산(가방 자동 담기)
            SettlementService next = Relaunch(out ProfileStore store);
            ProfileData p = store.Current;
            Assert.IsNotNull(p.RunCheckpoint);
            SettlementResult r = next.SettlePendingCheckpoint();
            Assert.IsNotNull(r);
            Assert.IsTrue(r.FromCheckpoint);
            Assert.AreEqual(SettlementService.EndReasonCheckpoint, r.Record.EndReason);
            Assert.AreEqual("Checkpoint", r.Record.EndReason);
            Assert.AreEqual("r-crash", r.Record.RunId);
            Assert.AreEqual(4321, r.Record.Score);
            // cleared 5 → 1칸, 영웅 0: 희귀(거대 화염구)를 담고 일반 5 + 빌린 50은 골드
            Assert.AreEqual(1, r.Bagged.Count);
            Assert.AreEqual(Ids.Block.Fireball, r.Bagged[0].BlockId);
            Assert.AreEqual(55, r.GoldFromLeftovers);
            Assert.AreEqual(380 + 55, p.Currencies.Gold);
            // 까마귀 보따리 3 + 다섯 번째 웨이브 1
            Assert.IsTrue(r.CrowBundle);
            Assert.AreEqual(4, p.Items[Ids.Item.StoneLow].Qty);
            Assert.IsNull(p.RunCheckpoint);
            Assert.AreEqual("r-crash", p.LastSettledRunId);
            Assert.AreEqual(1, p.RunHistory.Count);
            Assert.AreEqual(5, p.Character(1).BestWave);
            CollectionAssert.Contains(p.Character(1).UnlockedArts, Ids.Art.Fan);
            Assert.AreEqual(1, p.KnownAffinities.Count);

            // 한 번만
            Assert.IsNull(next.SettlePendingCheckpoint());
            Relaunch(out ProfileStore third);
            Assert.IsNull(third.Current.RunCheckpoint);
            Assert.AreEqual(380 + 55, third.Current.Currencies.Gold);
        }

        [Test]
        public void D68_SettlePendingCheckpoint__NullWhenNothingPending()
        {
            Assert.IsNull(_svc.SettlePendingCheckpoint());
            Assert.AreEqual(0, _saves);
        }

        [Test]
        public void D23_Checkpoint__NormalCommitClearsIt()
        {
            RunSummary cp = CheckpointRun("r-normal");
            _svc.SaveCheckpoint(cp);
            RunSummary end = CheckpointRun("r-normal");
            end.EndReason = RunEndReason.HpZero;
            end.ClearedDirect = 7;
            end.LastClearedWave = 7;
            CommitOk(end);
            Assert.IsNull(P.RunCheckpoint);
            Assert.IsNull(_svc.SettlePendingCheckpoint());
            Assert.AreEqual(1, P.RunHistory.Count);
            Assert.AreEqual("HpZero", P.RunHistory[0].EndReason);
        }

        [Test]
        public void D68_SettlePendingCheckpoint__StaleCheckpointOfSettledRunIsDropped()
        {
            // 이미 정산한 판의 중간 지점이 남아 있어도 두 번 정산하지 않는다(last_settled_run_id)
            RunSummary s = CheckpointRun("r-stale");
            CommitOk(s);
            long gold = P.Currencies.Gold;
            P.RunCheckpoint = new RunCheckpointData { RunId = "r-stale", Wave = 5, Summary = CheckpointCodec.ToJson(s) };
            Assert.IsNull(_svc.SettlePendingCheckpoint());
            Assert.IsNull(P.RunCheckpoint);
            Assert.AreEqual(gold, P.Currencies.Gold);
            Assert.AreEqual(1, P.RunHistory.Count);
        }

        [Test]
        public void S07_TV_306__FirstMeetRunCrashWithCheckpoint5KeepsFlag()
        {
            // 첫 만남 판이 비정상 종료, 마지막 중간 지점 5 → 표시 유지, 웨이브 1~5 정산
            RunSummary s = FirstMeetRun("r-306", 5);
            s.EndReason = RunEndReason.None;
            s.Purse = 380;
            _svc.SaveCheckpoint(s);
            SettlementResult r = _svc.SettlePendingCheckpoint();
            Assert.IsNotNull(r);
            Assert.IsFalse(P.Flags.PhantomMet);
            Assert.AreEqual(380, P.Currencies.Gold);
            Assert.AreEqual(5, P.Character(1).BestWave);
        }

        // ------------------------------------------------------------ 보기

        [Test]
        public void Prepare__DoesNotChangeProfile()
        {
            RunSummary s = CheckpointRun("r-view");
            s.BloomCasts = 500;
            SettlementView v = _svc.Prepare(s);
            Assert.AreSame(s, v.Summary);
            Assert.AreEqual(2, v.BagCandidates.Count);
            Assert.AreEqual(1, v.Bag.Slots);
            Assert.AreEqual(Db.Economy.LibraryBaseCap - 5, v.LibraryRoom);
            CollectionAssert.Contains(v.NewTitles, Ids.Title.Bloom100);
            Assert.AreEqual(0, _saves);
            Assert.AreEqual(0, P.Feats.BloomCount);
            Assert.IsEmpty(P.Titles.Owned);
            Assert.IsNull(P.LastSettledRunId);
        }
    }
}
