using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Settlement;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.UI;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensSettlement
{
    /// <summary>
    /// 정산 화면 모델(G22). 벡터: 08 요약서 9절·13.2절·19절(S13 TV-154~164, TV-234·235), 01 요약서 10.2절(S09 T090~T100).
    /// 기대값은 요약서에서 가져왔다. 정산 서비스는 가짜(FakeSettlementService)이고 진짜 서비스의 규칙 시험은 Settlement 폴더에 있다.
    /// </summary>
    public class SettlementModelTests
    {
        FakeSettlementService _svc;
        FakeSound _sound;
        ProfileData _profile;
        int _endRuns;

        [SetUp]
        public void SetUp()
        {
            _svc = new FakeSettlementService();
            _sound = new FakeSound();
            _profile = SettlementFixtures.Profile();
            _endRuns = 0;
        }

        SettlementModel NewModel(ISettlementService svc = null, bool noService = false)
        {
            return new SettlementModel(noService ? null : (svc ?? _svc), TestContent.Db, () => _profile, _sound, () => _endRuns++);
        }

        SettlementModel Begin(RunSummary s, bool noService = false)
        {
            var m = NewModel(null, noService);
            m.Begin(s);
            return m;
        }

        int CountSound(string id)
        {
            int n = 0;
            foreach (var p in _sound.Played) if (p == id + "@" + SoundBus.Ui) n++;
            return n;
        }

        // ================================================================ 귀환 가방 칸수·영웅 한도

        // 08 9.2절 표 TV-154(칸) · TV-155(영웅 한도): 직접 넘긴 웨이브 → 가방 카운터와 영웅 한도.
        [TestCase(0, 0, 0)]
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
        public void S13_TV_154_155__BagSlotsAndHeroCapShown(int passed, int slots, int heroCap)
        {
            var m = Begin(SettlementFixtures.Summary(passed));
            Assert.AreEqual(slots, m.Limits.Slots);
            Assert.AreEqual(heroCap, m.Limits.HeroCap);
            m.Next();
            Assert.AreEqual(SettlementPage.ReturnBag, m.Page);
            var names = TestContent.Names;
            Assert.AreEqual("가방 0 / " + slots, names.Format(SettlementNameKeys.BagSlots, "used", m.Picked.Count, "max", m.Limits.Slots));
        }

        // 01 10.2절 (S09 T090~T100): 0~2 → 0칸/영웅 0, 1도 0 / 0.
        [TestCase(1, 0, 0)]
        [TestCase(2, 0, 0)]
        public void S09_T090__FewWavesNoSlots(int passed, int slots, int heroCap)
        {
            var m = Begin(SettlementFixtures.Summary(passed));
            Assert.AreEqual(slots, m.Limits.Slots);
            Assert.AreEqual(heroCap, m.Limits.HeroCap);
            Assert.AreEqual(BagToggleResult.NotFound, m.Toggle(999));
        }

        // 01 10.2절 (S09 T100): 시작 21에서 21~29 넘김(직접 9) → 3칸 / 영웅 0. 건너뛴 웨이브는 세지 않는다.
        [Test]
        public void S09_T100__StartAt21Cleared21To29()
        {
            var s = SettlementFixtures.Summary(9, startWave: 21);
            var m = Begin(s);
            Assert.AreEqual(3, m.Limits.Slots);
            Assert.AreEqual(0, m.Limits.HeroCap);
        }

        // TV-156: 12칸은 한 줄 8칸 → 8 + 4 두 줄.
        [Test]
        public void S13_TV_156__TwelveSlotsTwoRows()
        {
            CollectionAssert.AreEqual(new[] { 8, 4 }, SettlementRules.BagRows(12));
            CollectionAssert.AreEqual(new[] { 3 }, SettlementRules.BagRows(3));
            CollectionAssert.AreEqual(new[] { 8 }, SettlementRules.BagRows(8));
            Assert.AreEqual(0, SettlementRules.BagRows(0).Count);
        }

        // TV-157: 칸 2, 영웅 한도 0, 후보 [영웅, 희귀, 일반, 빌린 영웅] → 자동 담기 [희귀, 일반].
        // 서비스가 등급 순서(영웅 먼저)를 주어도 화면 검증이 영웅 한도·빌림을 거른다.
        [Test]
        public void S13_TV_157__AutoFillRareAndCommon()
        {
            var s = SettlementFixtures.Summary(6); // 칸 2, 영웅 0
            s.WildBlocks.Add(SettlementFixtures.Block(1, Ids.Rarity.Hero));
            s.WildBlocks.Add(SettlementFixtures.Block(2, Ids.Rarity.Rare));
            s.WildBlocks.Add(SettlementFixtures.Block(3, Ids.Rarity.Common));
            s.WildBlocks.Add(SettlementFixtures.Block(4, Ids.Rarity.Hero, borrowed: true));
            _svc.AutoPickOrder = new List<int> { 1, 2, 3, 4 };
            var m = Begin(s);
            Assert.AreEqual(2, m.Limits.Slots);
            Assert.AreEqual(0, m.Limits.HeroCap);
            int added = m.AutoPick();
            Assert.AreEqual(2, added);
            CollectionAssert.AreEqual(new[] { 2, 3 }, m.Picked);
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundBagPut));
        }

        // 06 5.3절 (S07 TV-236): cleared 20, 영웅 4 · 희귀 5 · 일반 3, 자동 → 영웅 2, 희귀 4 (6칸).
        [Test]
        public void S07_TV_236__AutoPickHeroCapThenRare()
        {
            var s = SettlementFixtures.Summary(20);
            int uid = 1;
            var order = new List<int>();
            for (int i = 0; i < 4; i++) { s.WildBlocks.Add(SettlementFixtures.Block(uid, Ids.Rarity.Hero)); order.Add(uid++); }
            for (int i = 0; i < 5; i++) { s.WildBlocks.Add(SettlementFixtures.Block(uid, Ids.Rarity.Rare)); order.Add(uid++); }
            for (int i = 0; i < 3; i++) { s.WildBlocks.Add(SettlementFixtures.Block(uid, Ids.Rarity.Common)); order.Add(uid++); }
            _svc.AutoPickOrder = order;
            var m = Begin(s);
            m.AutoPick();
            Assert.AreEqual(6, m.Picked.Count);
            Assert.AreEqual(2, m.HeroPicked);
            int rare = 0;
            foreach (int u in m.Picked) if (m.Entry(u).Rarity == Ids.Rarity.Rare) rare++;
            Assert.AreEqual(4, rare);
        }

        // TV-158: 칸 3, 영웅 한도 1, 영웅 1개 담음 → 영웅 하나 더 고르기 = 거부("영웅 한도").
        [Test]
        public void S13_TV_158__SecondHeroRejected()
        {
            var s = SettlementFixtures.Summary(10); // 칸 3, 영웅 1
            s.WildBlocks.Add(SettlementFixtures.Block(1, Ids.Rarity.Hero));
            s.WildBlocks.Add(SettlementFixtures.Block(2, Ids.Rarity.Hero));
            var m = Begin(s);
            Assert.AreEqual(BagToggleResult.Added, m.Toggle(1));
            Assert.AreEqual(BagToggleResult.HeroLimit, m.Toggle(2));
            Assert.AreEqual(BagToggleResult.HeroLimit, m.LastReject);
            CollectionAssert.AreEqual(new[] { 1 }, m.Picked);
            Assert.AreEqual("영웅 한도", TestContent.Names.Get(SettlementNameKeys.BagHeroLimit));
        }

        // TV-159: 빌린 블록 → 가방에 담기 거부, 판 끝 50골드.
        [Test]
        public void S13_TV_159__BorrowedRejectedAndFiftyGold()
        {
            var s = SettlementFixtures.Summary(10);
            s.WildBlocks.Add(SettlementFixtures.Block(7, Ids.Rarity.Hero, borrowed: true));
            var m = Begin(s);
            Assert.AreEqual(BagToggleResult.Borrowed, m.Toggle(7));
            Assert.AreEqual(0, m.Picked.Count);
            Assert.IsTrue(m.HasBorrowed);
            Assert.AreEqual(50, m.LeftoverGold);
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundDisabled));
        }

        // TV-160: 담지 못한 일반 2, 희귀 1, 영웅 1 → 2×5 + 15 + 50 = 75. 담은 블록은 골드에 들지 않는다.
        [Test]
        public void S13_TV_160__LeftoverGold75()
        {
            var s = SettlementFixtures.Summary(10);
            s.WildBlocks.Add(SettlementFixtures.Block(1, Ids.Rarity.Common));
            s.WildBlocks.Add(SettlementFixtures.Block(2, Ids.Rarity.Common));
            s.WildBlocks.Add(SettlementFixtures.Block(3, Ids.Rarity.Rare));
            s.WildBlocks.Add(SettlementFixtures.Block(4, Ids.Rarity.Hero));
            s.WildBlocks.Add(SettlementFixtures.Block(5, Ids.Rarity.Rare));
            s.WildBlocks.Add(new WildBlock { Uid = 0, BlockId = "block.magic_bolt", Rarity = Ids.Rarity.Hero, FromLibrary = true });
            var m = Begin(s);
            Assert.AreEqual(BagToggleResult.Added, m.Toggle(5));
            Assert.AreEqual(75, m.LeftoverGold);
            Assert.AreEqual("담지 못한 블록은 75 골드로 바뀐다",
                TestContent.Names.Format(SettlementNameKeys.BagLeftoverGold, "gold", TestContent.Names.Number(m.LeftoverGold)));
        }

        // TV-164: 서고 99 / 100, 가방 2개 고름 → [임시] 1개만 담김, 나머지 골드 + "서고가 가득 찼다".
        [Test]
        public void S13_TV_164__LibraryRoomOneOfTwo()
        {
            var s = SettlementFixtures.Summary(10);
            s.WildBlocks.Add(SettlementFixtures.Block(1, Ids.Rarity.Rare));
            s.WildBlocks.Add(SettlementFixtures.Block(2, Ids.Rarity.Common));
            _svc.LibraryRoom = 1;
            var m = Begin(s);
            Assert.AreEqual(BagToggleResult.Added, m.Toggle(1));
            Assert.AreEqual(BagToggleResult.Added, m.Toggle(2));
            Assert.IsTrue(m.LibraryOverflow);
            Assert.AreEqual(1, m.WillBag);
            Assert.AreEqual(5, m.LeftoverGold); // 두 번째(일반)는 골드 5로
            StringAssert.StartsWith("서고가 가득 찼다", TestContent.Names.Get(SettlementNameKeys.BagLibraryFull));
            m.Next(); m.Next();
            CollectionAssert.AreEqual(new[] { 1, 2 }, _svc.LastCommitUids);
        }

        // 가방이 찼다: 칸 1에 둘째 블록 → 거부 + ui.settlement.bag_full.
        [Test]
        public void BagFull__RejectedWithSound()
        {
            var s = SettlementFixtures.Summary(3);
            s.WildBlocks.Add(SettlementFixtures.Block(1, Ids.Rarity.Common));
            s.WildBlocks.Add(SettlementFixtures.Block(2, Ids.Rarity.Common));
            var m = Begin(s);
            Assert.AreEqual(BagToggleResult.Added, m.Toggle(1));
            Assert.AreEqual(BagToggleResult.Full, m.Toggle(2));
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundBagFull));
            Assert.AreEqual(BagToggleResult.Removed, m.Toggle(1));
            Assert.AreEqual(BagToggleResult.Added, m.Toggle(2));
            CollectionAssert.AreEqual(new[] { 2 }, m.Picked);
        }

        // ================================================================ 1쪽 기록

        // TV-161: 웨이브 21 출발, 23 도달 → "3일째 웨이브 23" + "웨이브 21에서 출발".
        [Test]
        public void S13_TV_161__RecordLineWithStartWave()
        {
            var s = SettlementFixtures.Summary(2, startWave: 21, reached: 23);
            var m = Begin(s);
            var names = TestContent.Names;
            Assert.AreEqual("3일째 웨이브 23", names.Format(SettlementNameKeys.FormatDayWave, "day", m.Day, "wave", m.DisplayWave));
            Assert.IsTrue(m.ShowStartWave);
            Assert.AreEqual("웨이브 21에서 출발", names.Format(SettlementNameKeys.FormatStartWave, "start", m.StartWave));

            var one = Begin(SettlementFixtures.Summary(4, startWave: 1, reached: 5));
            Assert.IsFalse(one.ShowStartWave);
        }

        // TV-162: 첫 만남이 든 판 → 점수 내역에 "허상과의 첫 만남 3,648" 줄, 복수 추적기 처음 등장.
        [Test]
        public void S13_TV_162__PhantomFirstRowAndTrackerAppears()
        {
            var s = SettlementFixtures.Summary(10);
            s.PhantomFirst = true;
            s.PhantomFirstDone = true;
            s.RevengeActive = true;
            s.WaveResults.Add(new WaveResult { Wave = 10, Score = new ScoreBreakdown { PhantomFirst = 3648, Total = 3648 } });
            _profile = SettlementFixtures.Profile(phantomMet: false, bestWave: 9);
            var m = Begin(s, noService: true);

            ScoreRow row = default;
            bool found = false;
            foreach (var r in m.ScoreRows) if (r.Key == SettlementNameKeys.ScorePhantomFirst) { row = r; found = true; }
            Assert.IsTrue(found);
            var names = TestContent.Names;
            Assert.AreEqual("허상과의 첫 만남 3,648", names.Get(row.Key) + " " + names.Number(row.Value));
            Assert.IsTrue(m.Tracker.Visible);
            Assert.AreEqual(RevengeState.Pursuing, m.Tracker.State);

            m.OnPageShown();
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundTrackerRow));
        }

        // 점수 내역 줄이 0인 항목: 처치~변덕 배율은 0이어도 그린다, 첫 만남·건너뛴 줄은 없는 판이면 줄 자체가 없다. 합계는 맨 끝.
        [Test]
        public void ScoreRows__ZeroItemsShownOptionalRowsHidden()
        {
            var m = Begin(SettlementFixtures.Summary(0, startWave: 1, reached: 1));
            var keys = new List<string>();
            foreach (var r in m.ScoreRows) { keys.Add(r.Key); Assert.AreEqual(0, r.Value); }
            CollectionAssert.AreEqual(new[]
            {
                SettlementNameKeys.ScoreKills, SettlementNameKeys.ScoreBoss, SettlementNameKeys.ScoreClear, SettlementNameKeys.ScoreFast,
                SettlementNameKeys.ScoreNoHit, SettlementNameKeys.ScoreGraze, SettlementNameKeys.ScoreVariance, SettlementNameKeys.ScoreTotal
            }, keys);
            Assert.IsTrue(m.ScoreRows[m.ScoreRows.Count - 1].Total);

            var skip = SettlementRules.ScoreRows(new ScoreBreakdown { Skipped = 28859, Total = 28859 }, false, 21);
            Assert.AreEqual(SettlementNameKeys.ScoreSkipped, skip[skip.Count - 2].Key);
            Assert.AreEqual(28859, skip[skip.Count - 2].Value);
        }

        // 최고 기록 갱신: 딱지 + ui.settlement.new_best 한 번.
        [Test]
        public void NewBest__BadgeAndSoundOnce()
        {
            _svc.NewBestWave = true;
            var m = Begin(SettlementFixtures.Summary(5));
            Assert.IsTrue(m.NewBest);
            m.OnPageShown();
            m.OnPageShown();
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundNewBest));
            Assert.AreEqual("최고 기록 갱신", TestContent.Names.Get(SettlementNameKeys.BestRecord));
        }

        // ================================================================ 복수 추적기 (08 13.2절)

        // TV-234: 복수 중, 흠집 기록 없음 → 큰 꼴 3줄째 없음.
        [Test]
        public void S13_TV_234__NoDentLine()
        {
            _profile = SettlementFixtures.Profile(phantomMet: true, bestWave: 14);
            var m = Begin(SettlementFixtures.Summary(5));
            Assert.IsTrue(m.Tracker.Visible);
            Assert.IsFalse(m.Tracker.HasDent);
            Assert.AreEqual("넘긴 최고 웨이브 14 / 19", TestContent.Names.Format(SettlementNameKeys.TrackerProgress, "n", m.Tracker.Progress));
        }

        // TV-235: 복수 중, 흠집 62%, 도전 3 → "허상에게 낸 가장 큰 흠집 62% · 도전 3번" / Cracked.
        [Test]
        public void S13_TV_235__DentLineAndCrackedMirror()
        {
            _profile = SettlementFixtures.Profile(phantomMet: true, bestWave: 19);
            _profile.Revenge.PhantomTries = 3;
            _profile.Revenge.PhantomBestDent = 62;
            var m = Begin(SettlementFixtures.Summary(5));
            Assert.IsTrue(m.Tracker.HasDent);
            Assert.AreEqual(MirrorMark.Cracked, m.Tracker.Mirror);
            Assert.AreEqual("허상에게 낸 가장 큰 흠집 62% · 도전 3번",
                TestContent.Names.Format(SettlementNameKeys.TrackerDent, "percent", m.Tracker.DentPercent, "tries", m.Tracker.Tries));
        }

        // 계약: 만나기 전 프로필 + summary.RevengeActive → 추적기가 처음 나타난다.
        [Test]
        public void Contract__BeforeMeetingButMetThisRun_TrackerAppears()
        {
            _profile = SettlementFixtures.Profile(phantomMet: false);
            var s = SettlementFixtures.Summary(10);
            s.RevengeActive = true;
            var m = Begin(s);
            Assert.IsTrue(m.Tracker.Visible);
        }

        // 계약: RevengeActive가 거짓이고 만나기 전이면 추적기 없음(첫 만남을 미리 알리지 않는다).
        [Test]
        public void Contract__BeforeMeetingNotMet_NoTracker()
        {
            _profile = SettlementFixtures.Profile(phantomMet: false);
            var s = SettlementFixtures.Summary(9);
            s.RevengeActive = false;
            var m = Begin(s);
            Assert.IsFalse(m.Tracker.Visible);
            m.OnPageShown();
            Assert.AreEqual(0, CountSound(SettlementNameKeys.SoundTrackerRow));
        }

        // 계약 + TV-163: 복수 보상 결과 → 완료 꼴 한 번(ui.revenge.tracker_complete 한 번), 3쪽 맨 위 복수 보상 줄:
        // 중급 3, 상급 1, 부적 1이 "계정에 묶임"으로, 칭호는 복수 줄에서만(새 칭호 줄에 겹치지 않는다).
        [Test]
        public void S13_TV_163__RevengeRewardRowAndDoneFormOnce()
        {
            _profile = SettlementFixtures.Profile(phantomMet: true, bestWave: 19);
            var s = SettlementFixtures.Summary(20);
            s.RevengeActive = true;
            s.KilledPhantomAtWave20 = true;
            var result = new SettlementResult { RevengeReward = true, GoldGained = 580 };
            result.NewTitles.Add(Ids.Title.MirrorBreaker);
            result.NewTitles.Add(Ids.Title.FirstNight);
            result.Materials.Add(new MaterialGain { ItemId = Ids.Item.StoneMid, Qty = 3, Guaranteed = true, Bound = true });
            result.Materials.Add(new MaterialGain { ItemId = Ids.Item.StoneHigh, Qty = 1, Guaranteed = true, Bound = true });
            result.Materials.Add(new MaterialGain { ItemId = Ids.Item.Charm, Qty = 1, Guaranteed = true, Bound = true });
            result.Materials.Add(new MaterialGain { ItemId = Ids.Item.StoneMid, Qty = 1, Guaranteed = true, Source = DropChanceSource.BossKill });
            _svc.CommitResult = result;
            _svc.OnCommit = _ => _profile.Flags.RevengeDoneAt = "2026-11-14T00:00:00+09:00";

            var m = Begin(s);
            Assert.IsFalse(m.Tracker.JustCompleted);
            m.OnPageShown();
            m.Next(); m.OnPageShown();
            m.Next(); m.OnPageShown();
            Assert.IsTrue(m.RevengeReward);
            Assert.IsTrue(m.Tracker.Visible);
            Assert.IsTrue(m.Tracker.JustCompleted);
            Assert.AreEqual(MirrorMark.Broken, m.Tracker.Mirror);

            Assert.AreEqual(3, m.RevengeMaterials.Count);
            Assert.AreEqual(Ids.Item.StoneMid, m.RevengeMaterials[0].ItemId); Assert.AreEqual(3, m.RevengeMaterials[0].Qty);
            Assert.AreEqual(Ids.Item.StoneHigh, m.RevengeMaterials[1].ItemId); Assert.AreEqual(1, m.RevengeMaterials[1].Qty);
            Assert.AreEqual(Ids.Item.Charm, m.RevengeMaterials[2].ItemId); Assert.AreEqual(1, m.RevengeMaterials[2].Qty);
            foreach (var g in m.RevengeMaterials) Assert.IsTrue(g.Bound);
            Assert.AreEqual(1, m.ConfirmedMaterials.Count);
            CollectionAssert.AreEqual(new[] { Ids.Title.FirstNight }, m.NewTitles);
            var names = TestContent.Names;
            Assert.AreEqual("계정에 묶임", names.Get(SettlementNameKeys.TakeBound));
            Assert.AreEqual("거울을 깬", names.Get(Ids.Title.MirrorBreaker));

            // 쪽을 오가도 완료 꼴 소리는 한 번.
            m.Prev(); m.OnPageShown(); m.Prev(); m.OnPageShown(); m.Next(); m.Next(); m.OnPageShown();
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundTrackerComplete));
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundRevengeReward));
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundRevengeComplete));
        }

        // ================================================================ Commit은 정확히 한 번

        [Test]
        public void Contract__CommitExactlyOnceEvenWhenGoingBack()
        {
            var s = SettlementFixtures.Summary(9);
            s.WildBlocks.Add(SettlementFixtures.Block(1, Ids.Rarity.Rare));
            var m = Begin(s);
            Assert.AreEqual(1, _svc.PrepareCalls);
            m.Toggle(1);
            Assert.IsTrue(m.Next());   // 1 → 2
            Assert.AreEqual(0, _svc.CommitCalls);
            Assert.IsTrue(m.Next());   // 2 → 3, Commit
            Assert.AreEqual(1, _svc.CommitCalls);
            Assert.AreEqual(1, _endRuns);
            Assert.AreSame(s, _svc.LastCommitSummary);
            CollectionAssert.AreEqual(new[] { 1 }, _svc.LastCommitUids);

            Assert.IsTrue(m.Prev());   // 3 → 2
            Assert.AreEqual(BagToggleResult.Locked, m.Toggle(1));
            Assert.AreEqual(0, m.AutoPick());
            Assert.IsTrue(m.Prev());   // 2 → 1
            Assert.IsFalse(m.Prev());
            m.Next(); m.Next();
            Assert.IsFalse(m.Next());
            Assert.AreEqual(SettlementPage.Takeaways, m.Page);
            Assert.AreEqual(1, _svc.CommitCalls);
            Assert.AreEqual(1, m.CommitCalls);
            Assert.AreEqual(1, _endRuns);
        }

        // Commit이 거절돼도(같은 판 두 번 등) 다시 부르지 않고 3쪽으로 간다. 끝 버튼은 된다.
        [Test]
        public void Commit__FailureStillOnceAndEndButtonsWork()
        {
            _svc.CommitError = ErrorCode.NotEligible;
            var m = Begin(SettlementFixtures.Summary(3));
            m.Next(); m.Next();
            Assert.IsNull(m.Result);
            Assert.AreEqual(ErrorCode.NotEligible, m.CommitError);
            m.Prev(); m.Next();
            Assert.AreEqual(1, _svc.CommitCalls);
            Assert.IsNotNull(m.ToMenu());
        }

        // Esc로 정산을 건너뛸 수 없다: 확정 전 Esc는 전이가 없다. 확정 뒤 3쪽에서는 메인 메뉴.
        [Test]
        public void Esc__CannotSkipBeforeCommit()
        {
            var m = Begin(SettlementFixtures.Summary(3));
            Assert.IsNull(m.Back());
            Assert.AreEqual(SettlementPage.Record, m.Page);
            m.Next();
            Assert.IsNull(m.Back());
            Assert.AreEqual(0, _svc.CommitCalls);
            Assert.IsNull(m.Again());
            Assert.IsNull(m.ToEnhance());
            Assert.IsNull(m.ToMenu());
            m.Next();
            SettlementNav nav = m.Back();
            Assert.IsNotNull(nav);
            Assert.AreEqual(SettlementNavKind.ToMenu, nav.Kind);
        }

        // 쪽 넘김 소리와 가방 열기 소리.
        [Test]
        public void Sounds__PageAndBagOpen()
        {
            var m = Begin(SettlementFixtures.Summary(3));
            m.OnPageShown();
            m.Next(); m.OnPageShown();
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundPage));
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundBagOpen));
            m.Prev(); m.Next(); m.OnPageShown();
            Assert.AreEqual(3, CountSound(SettlementNameKeys.SoundPage));
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundBagOpen));
        }

        // ================================================================ 끝 버튼

        // 계약: 끝 버튼들이 넘기는 인자(ReturnTo = MainMenu, Character는 int), 강화 첫 안내를 직접 열지 않는다.
        [Test]
        public void Contract__EndButtonArgs()
        {
            var result = new SettlementResult { ShowEnhanceTutorial = true };
            _svc.CommitResult = result;
            var m = Begin(SettlementFixtures.Summary(3));
            m.Next(); m.Next();
            Assert.IsTrue(m.ShowEnhanceHint);

            SettlementNav enh = m.ToEnhance();
            Assert.AreEqual(ScreenId.WandPicker, enh.Target);
            Assert.AreEqual(ScreenId.WandPicker, enh.PushId);
            Assert.AreEqual(ScreenId.MainMenu, enh.Args.Get(ScreenArgKeys.ReturnTo, ScreenId.None));
            Assert.IsInstanceOf<int>(enh.Args.Values[ScreenArgKeys.Character]);
            Assert.AreEqual(3, enh.Args.Get(ScreenArgKeys.Character, 0));
            Assert.IsInstanceOf<ScreenId>(enh.Args.Values[ScreenArgKeys.ReturnTo]);

            var sm = new FakeScreenManager();
            enh.Apply(sm);
            CollectionAssert.AreEqual(new[] { "popto MainMenu", "push WandPicker" }, sm.Calls);

            SettlementNav again = m.Again();
            Assert.AreEqual(ScreenId.ExpeditionPrep, again.Target);
            Assert.AreEqual(ScreenId.Loading, again.PushId);
            Assert.AreEqual(ScreenId.ExpeditionPrep, again.Args.Get(ScreenArgKeys.NextScreen, ScreenId.None));
            Assert.IsNotNull(again.Args.Get<ScreenArgs>(ScreenArgKeys.NextArgs));
            var sm2 = new FakeScreenManager();
            again.Apply(sm2);
            CollectionAssert.AreEqual(new[] { "popto MainMenu", "push Loading" }, sm2.Calls);

            SettlementNav menu = m.ToMenu();
            var sm3 = new FakeScreenManager();
            menu.Apply(sm3);
            CollectionAssert.AreEqual(new[] { "popto MainMenu" }, sm3.Calls);

            // ShowEnhanceTutorial이어도 이 화면은 EnhanceTutorial을 열지 않는다(D-67).
            foreach (var c in sm.Calls) StringAssert.DoesNotContain(ScreenId.EnhanceTutorial.ToString(), c);
            foreach (var c in sm2.Calls) StringAssert.DoesNotContain(ScreenId.EnhanceTutorial.ToString(), c);
            Assert.AreNotEqual(ScreenId.EnhanceTutorial, enh.Target);
        }

        // ================================================================ 3쪽 가져가는 것

        [Test]
        public void Takeaways__MaterialSplitAndBundleOpen()
        {
            var result = new SettlementResult { CrowBundle = true, GoldGained = 120 };
            result.Materials.Add(new MaterialGain { ItemId = Ids.Item.StoneLow, Qty = 3, Guaranteed = true, Source = DropChanceSource.CrowBundle });
            result.Materials.Add(new MaterialGain { ItemId = Ids.Item.StoneLow, Qty = 1, Guaranteed = true, Source = DropChanceSource.FifthWave });
            result.Materials.Add(new MaterialGain { ItemId = Ids.Item.StoneMid, Qty = 1, Guaranteed = false, Source = DropChanceSource.BossKill });
            result.NewArts.Add("art.fan");
            _svc.CommitResult = result;
            var m = Begin(SettlementFixtures.Summary(5));
            Assert.IsFalse(m.OpenBundle()); // 확정 전에는 없다
            m.Next(); m.Next();
            Assert.AreEqual(1, m.CrowMaterials.Count);
            Assert.AreEqual(1, m.ConfirmedMaterials.Count);
            Assert.AreEqual(1, m.BundleMaterials.Count);
            Assert.AreEqual(0, m.RevengeMaterials.Count);
            Assert.IsFalse(m.BundleOpened);
            Assert.IsTrue(m.OpenBundle());
            Assert.IsFalse(m.OpenBundle());
            Assert.AreEqual(1, CountSound(SettlementNameKeys.SoundBundleOpen));
            Assert.AreEqual("중급 강화석 ×1",
                TestContent.Names.Format(SettlementNameKeys.TakeItemCount, "name", TestContent.Names.Get(Ids.Item.StoneMid), "n", 1));
        }

        // ctx.Settlement가 null이면 요약만 보여 주고 죽지 않는다(Commit 없음, 끝 버튼은 된다).
        [Test]
        public void NoService__SummaryOnlyNoCrash()
        {
            var s = SettlementFixtures.Summary(4);
            s.WaveResults.Add(new WaveResult { Wave = 1, Score = new ScoreBreakdown { Kills = 100, Clear = 200, Total = 300 } });
            s.WaveResults.Add(new WaveResult { Wave = 2, Score = new ScoreBreakdown { Kills = 50, Clear = 220, Total = 270 } });
            s.WildBlocks.Add(SettlementFixtures.Block(1, Ids.Rarity.Rare));
            var m = Begin(s, noService: true);
            Assert.IsNull(m.View);
            Assert.AreEqual(570, m.Score.Total);
            Assert.AreEqual(150, m.Score.Kills);
            Assert.AreEqual(0, m.Limits.Slots);
            Assert.AreEqual(BagToggleResult.NotFound, m.Toggle(1));
            Assert.AreEqual(0, m.AutoPick());
            m.Next(); m.Next();
            Assert.IsTrue(m.Committed);
            Assert.IsNull(m.Result);
            Assert.AreEqual(1, _endRuns);
            Assert.IsNotNull(m.ToMenu());
        }

        // 요약이 없어도(인자 없음) 죽지 않는다.
        [Test]
        public void NullSummary__NoCrash()
        {
            var m = Begin(null);
            Assert.AreEqual(1, m.DisplayWave);
            Assert.AreEqual(0, _svc.PrepareCalls);
            m.Next(); m.Next();
            Assert.AreEqual(0, _svc.CommitCalls);
            Assert.IsNotNull(m.ToMenu());
        }
    }
}
