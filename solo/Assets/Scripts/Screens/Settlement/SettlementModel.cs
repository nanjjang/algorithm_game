using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Settlement
{
    /// <summary>정산의 세 쪽(08 요약서 9절, S13 10절).</summary>
    public enum SettlementPage { Record = 1, ReturnBag = 2, Takeaways = 3 }

    /// <summary>가방 칸 누름의 결과(08 요약서 9.2절 Toggle).</summary>
    public enum BagToggleResult
    {
        Added = 0, Removed = 1,
        /// <summary>빌린 블록은 담을 수 없다(str.settlement.bag.borrowed_note, TV-159).</summary>
        Borrowed = 2,
        /// <summary>가방이 찼다(str.settlement.bag.full).</summary>
        Full = 3,
        /// <summary>영웅 한도(str.settlement.bag.hero_limit, TV-158).</summary>
        HeroLimit = 4,
        /// <summary>가방을 이미 확정했다(Commit 뒤).</summary>
        Locked = 5,
        NotFound = 6
    }

    /// <summary>2쪽의 야생 블록 한 칸.</summary>
    public sealed class BagEntry
    {
        public WildBlock Block;
        public string Rarity;
        public bool Borrowed;
        /// <summary>정산 서비스의 후보에 들어 있다(빌린 블록·서고 블록이 아니다).</summary>
        public bool Baggable;
        public bool Picked;
        public int Uid => Block != null ? Block.Uid : 0;
        public bool Hero => Rarity == Ids.Rarity.Hero;
    }

    /// <summary>
    /// 정산 화면의 규칙과 상태(엔진 없음). 화면(SettlementScreen)은 이것을 그리고 버튼을 이리로 넘기기만 한다.
    /// - Begin: ISettlementService.Prepare 한 번. 서비스가 없으면(ctx.Settlement == null) 요약으로 1쪽만 채우고 죽지 않는다.
    /// - 2쪽 → 3쪽으로 넘길 때 Commit(summary, 고른 uid)을 **정확히 한 번** 부른다(쪽을 되돌아가도 다시 부르지 않는다). 그 뒤 세션 EndRun.
    /// - 복수 추적기는 Meta.RevengeTracker.Build 하나로만 만든다(직접 계산하지 않는다, 00 문서 5.5절).
    /// - 강화 첫 안내 화면은 열지 않는다. result.ShowEnhanceTutorial이면 [강화 화면으로]를 한 번 강조할 뿐이다(D-67).
    /// - 소리는 요약서의 시점에 ui.* 사건으로 낸다(SoundBus.Ui).
    /// </summary>
    public sealed class SettlementModel
    {
        readonly ISettlementService _svc;
        readonly IContentDb _db;
        readonly Func<ProfileData> _profile;
        readonly ISoundPlayer _sound;
        readonly Action _endRun;

        bool _recordShown, _bagShown, _takeShown, _trackerCompleteShown;

        public SettlementModel(ISettlementService service, IContentDb db, Func<ProfileData> profile, ISoundPlayer sound, Action endRun)
        {
            _svc = service;
            _db = db;
            _profile = profile;
            _sound = sound;
            _endRun = endRun;
        }

        public RunSummary Summary { get; private set; }
        /// <summary>Prepare의 결과(서비스나 요약이 없으면 null).</summary>
        public SettlementView View { get; private set; }
        public bool HasService => _svc != null && View != null;
        public SettlementPage Page { get; private set; } = SettlementPage.Record;

        // ---------------------------------------------------------------- 1쪽 기록
        public ScoreBreakdown Score { get; private set; }
        public List<ScoreRow> ScoreRows { get; private set; } = new List<ScoreRow>();
        /// <summary>기록 줄의 웨이브와 날("N일째 웨이브 M").</summary>
        public int DisplayWave { get; private set; }
        public int Day { get; private set; }
        public int StartWave { get; private set; }
        public bool ShowStartWave { get; private set; }
        /// <summary>최고 기록 갱신 딱지(점수 또는 넘긴 웨이브).</summary>
        public bool NewBest { get; private set; }
        public RevengeTrackerView Tracker { get; private set; }
        public int CharacterNumber { get; private set; }
        /// <summary>판 끝 요약("이번 판 돌아보기"). BuildRecap 전에는 null — 화면은 그때 1쪽의 캐릭터 그림을 그린다.</summary>
        public RunRecap Recap { get; private set; }
        /// <summary>그 판의 표시 통계(없으면 null).</summary>
        public Wizard.Solo.View.Stats.RunStats Stats { get; private set; }
        /// <summary>가방을 확정한(Commit) 바로 뒤, 세션을 닫기 전에 한 번 부른다(화면이 판 끝 요약의 개인 최고를 적는다).</summary>
        public Action AfterCommit;

        // ---------------------------------------------------------------- 2쪽 귀환 가방
        public readonly List<BagEntry> Candidates = new List<BagEntry>();
        /// <summary>고른 블록 uid(고른 순서 — 서고 남은 자리가 모자라면 앞에서부터 담긴다).</summary>
        public readonly List<int> Picked = new List<int>();
        public BagLimits Limits { get; private set; }
        public int LibraryRoom { get; private set; }
        public int HeroPicked { get; private set; }
        /// <summary>마지막으로 거절된 까닭(없으면 null). 화면이 안내 한 줄로 그린다.</summary>
        public BagToggleResult? LastReject { get; private set; }
        public bool BagLocked => Committed;
        public bool HasBorrowed { get; private set; }
        /// <summary>고른 수가 서고 남은 자리보다 많다 → 남은 자리까지만 담기고 나머지는 골드(TV-164).</summary>
        public bool LibraryOverflow => Picked.Count > LibraryRoom;

        // ---------------------------------------------------------------- 3쪽 가져가는 것
        public bool Committed { get; private set; }
        /// <summary>Commit을 부른 횟수(0 또는 1).</summary>
        public int CommitCalls { get; private set; }
        public SettlementResult Result { get; private set; }
        public ErrorCode CommitError { get; private set; }
        public readonly List<MaterialGain> RevengeMaterials = new List<MaterialGain>();
        public readonly List<MaterialGain> CrowMaterials = new List<MaterialGain>();
        public readonly List<MaterialGain> ConfirmedMaterials = new List<MaterialGain>();
        public readonly List<MaterialGain> BundleMaterials = new List<MaterialGain>();
        public readonly List<string> NewTitles = new List<string>();
        public bool BundleOpened { get; private set; }
        public bool RevengeReward => Result != null && Result.RevengeReward;
        /// <summary>[강화 화면으로]를 한 번 강조해 안내한다(강화 첫 안내 자체는 강화 쪽이 연다 — D-67).</summary>
        public bool ShowEnhanceHint => Result != null && Result.ShowEnhanceTutorial;
        /// <summary>서고가 가득 차 고른 블록이 다 담기지 못했다(결과 기준).</summary>
        public bool LibraryFullOnCommit => Result != null && Result.Bagged != null && Result.Bagged.Count < Picked.Count;

        // ================================================================ 시작

        public void Begin(RunSummary summary)
        {
            Summary = summary;
            View = null;
            Result = null;
            CommitError = ErrorCode.None;
            Committed = false;
            CommitCalls = 0;
            Page = SettlementPage.Record;
            Candidates.Clear();
            Picked.Clear();
            HeroPicked = 0;
            LastReject = null;
            HasBorrowed = false;
            RevengeMaterials.Clear(); CrowMaterials.Clear(); ConfirmedMaterials.Clear(); BundleMaterials.Clear(); NewTitles.Clear();
            BundleOpened = false;
            _recordShown = _bagShown = _takeShown = _trackerCompleteShown = false;

            if (summary != null && _svc != null) View = _svc.Prepare(summary);

            Score = View != null ? View.Score : SettlementRules.SumScore(summary);
            StartWave = summary != null && summary.StartWave > 0 ? summary.StartWave : 1;
            ScoreRows = SettlementRules.ScoreRows(Score, summary != null && summary.PhantomFirstDone, StartWave);
            DisplayWave = SettlementRules.DisplayWave(summary);
            Day = DayCycle.Day(DisplayWave);
            ShowStartWave = SettlementRules.ShowStartWave(summary);
            NewBest = View != null && (View.NewBestScore || View.NewBestWave);
            CharacterNumber = summary != null ? Ids.Char.NumberOf(summary.CharId) : 0;
            RebuildTracker();

            Limits = View != null ? View.Bag : default(BagLimits);
            LibraryRoom = View != null ? View.LibraryRoom : 0;
            if (summary != null && summary.WildBlocks != null)
                for (int i = 0; i < summary.WildBlocks.Count; i++)
                {
                    WildBlock b = summary.WildBlocks[i];
                    if (b == null || b.FromLibrary) continue;
                    var e = new BagEntry
                    {
                        Block = b,
                        Rarity = SettlementRules.RarityOf(_db, b),
                        Borrowed = b.Borrowed,
                        Baggable = !b.Borrowed && View != null && ContainsUid(View.BagCandidates, b.Uid)
                    };
                    if (e.Borrowed) HasBorrowed = true;
                    Candidates.Add(e);
                }
        }

        static bool ContainsUid(List<WildBlock> list, int uid)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].Uid == uid) return true;
            return false;
        }

        void RebuildTracker()
        {
            bool met = Summary != null && Summary.RevengeActive;
            Tracker = RevengeTracker.Build(_profile != null ? _profile() : null, _db, 0, met, RevengeReward);
        }

        /// <summary>화면이 지금 쪽을 처음 그릴 때 부른다(그 쪽에 처음 들어갈 때 한 번씩 나는 소리).</summary>
        public void OnPageShown()
        {
            switch (Page)
            {
                case SettlementPage.Record:
                    if (_recordShown) return;
                    _recordShown = true;
                    if (NewBest) Play(SettlementNameKeys.SoundNewBest);
                    if (Tracker.Visible) Play(Tracker.JustCompleted ? SettlementNameKeys.SoundTrackerComplete : SettlementNameKeys.SoundTrackerRow);
                    if (Tracker.JustCompleted) _trackerCompleteShown = true;
                    break;
                case SettlementPage.ReturnBag:
                    if (_bagShown) return;
                    _bagShown = true;
                    Play(SettlementNameKeys.SoundBagOpen);
                    break;
                case SettlementPage.Takeaways:
                    if (_takeShown) return;
                    _takeShown = true;
                    if (RevengeReward)
                    {
                        Play(SettlementNameKeys.SoundRevengeReward);
                        Play(SettlementNameKeys.SoundRevengeComplete);
                        if (!_trackerCompleteShown) { _trackerCompleteShown = true; Play(SettlementNameKeys.SoundTrackerComplete); }
                    }
                    break;
            }
        }

        /// <summary>판 끝 요약을 만든다(Begin 뒤). 개인 최고는 정산 전 캐릭터 기록과 견준다.</summary>
        public void BuildRecap(Wizard.Solo.View.Stats.RunStats stats, RecapTuning tuning)
        {
            Stats = stats;
            ProfileData p = _profile != null ? _profile() : null;
            CharacterRecord rec = p != null && CharacterNumber > 0 ? p.Character(CharacterNumber) : null;
            Recap = RunRecap.Build(Summary, stats, rec, NewBest, tuning);
        }

        /// <summary>이번 정산으로 이룬 도전 과제(확정 전이면 빈 목록).</summary>
        public List<string> NewChallenges => Result != null && Result.NewChallenges != null ? Result.NewChallenges : new List<string>();

        /// <summary>3쪽 축하: 새 도전 과제·칭호·비기, 복수 보상.</summary>
        public bool CelebrateTakeaways => Result != null && (NewChallenges.Count > 0 || NewTitles.Count > 0 || RevengeReward
            || (Result.NewArts != null && Result.NewArts.Count > 0));

        /// <summary>1쪽 축하: 점수·웨이브 최고 기록, 판 끝 요약의 개인 최고.</summary>
        public bool CelebrateRecord => NewBest || (Recap != null && Recap.AnyNewBest);

        // ================================================================ 쪽 넘김

        /// <summary>[다음]. 2쪽 → 3쪽이면 가방을 확정한다(Commit 한 번). 쪽이 바뀌었으면 true.</summary>
        public bool Next()
        {
            if (Page == SettlementPage.Record) { SetPage(SettlementPage.ReturnBag); return true; }
            if (Page == SettlementPage.ReturnBag)
            {
                CommitOnce();
                SetPage(SettlementPage.Takeaways);
                return true;
            }
            return false;
        }

        /// <summary>[이전]·K. 확정한 뒤에도 앞 쪽을 다시 볼 수 있다(가방은 잠긴다). 쪽이 바뀌었으면 true.</summary>
        public bool Prev()
        {
            if (Page == SettlementPage.Takeaways) { SetPage(SettlementPage.ReturnBag); return true; }
            if (Page == SettlementPage.ReturnBag) { SetPage(SettlementPage.Record); return true; }
            return false;
        }

        void SetPage(SettlementPage p)
        {
            if (Page == p) return;
            Page = p;
            LastReject = null;
            Play(SettlementNameKeys.SoundPage);
        }

        void CommitOnce()
        {
            if (Committed) return;
            Committed = true;
            if (_svc != null && Summary != null)
            {
                CommitCalls++;
                Result<SettlementResult> r = _svc.Commit(Summary, new List<int>(Picked));
                Result = r.Ok ? r.Value : null;
                CommitError = r.Error;
            }
            if (Result != null)
            {
                SettlementRules.SplitMaterials(Result.Materials, RevengeMaterials, CrowMaterials, ConfirmedMaterials, BundleMaterials);
                NewTitles.AddRange(SettlementRules.OtherTitles(Result));
                // 꾸러미(추첨분)가 없으면 열 것도 없다.
                if (BundleMaterials.Count == 0) BundleOpened = true;
            }
            else BundleOpened = true;
            RebuildTracker();
            if (Result != null) AfterCommit?.Invoke();
            _endRun?.Invoke();
        }

        // ================================================================ 귀환 가방

        public BagEntry Entry(int uid)
        {
            for (int i = 0; i < Candidates.Count; i++) if (Candidates[i].Uid == uid) return Candidates[i];
            return null;
        }

        /// <summary>블록 하나를 담거나 뺀다(08 요약서 9.2절 Toggle). 거절이면 LastReject에 적고 소리를 낸다.</summary>
        public BagToggleResult Toggle(int uid) => ToggleCore(uid, true);

        BagToggleResult ToggleCore(int uid, bool sound)
        {
            BagToggleResult r = Evaluate(uid);
            BagEntry e = Entry(uid);
            switch (r)
            {
                case BagToggleResult.Added:
                    e.Picked = true;
                    Picked.Add(uid);
                    if (e.Hero) HeroPicked++;
                    LastReject = null;
                    if (sound) Play(SettlementNameKeys.SoundBagPut);
                    break;
                case BagToggleResult.Removed:
                    e.Picked = false;
                    Picked.Remove(uid);
                    if (e.Hero) HeroPicked--;
                    LastReject = null;
                    break;
                case BagToggleResult.Full:
                    LastReject = r;
                    if (sound) Play(SettlementNameKeys.SoundBagFull);
                    break;
                default:
                    LastReject = r;
                    if (sound) Play(SettlementNameKeys.SoundDisabled);
                    break;
            }
            return r;
        }

        /// <summary>지금 누르면 어떻게 되나(상태를 바꾸지 않는다). 순서: 잠김 → 없음 → 빌림 → 빼기 → 가득 → 영웅 한도.</summary>
        public BagToggleResult Evaluate(int uid)
        {
            if (Committed) return BagToggleResult.Locked;
            BagEntry e = Entry(uid);
            if (e == null) return BagToggleResult.NotFound;
            if (e.Borrowed) return BagToggleResult.Borrowed;
            if (!e.Baggable) return BagToggleResult.NotFound;
            if (e.Picked) return BagToggleResult.Removed;
            if (Picked.Count >= Limits.Slots) return BagToggleResult.Full;
            if (e.Hero && HeroPicked >= Limits.HeroCap) return BagToggleResult.HeroLimit;
            return BagToggleResult.Added;
        }

        /// <summary>
        /// [자동 담기]: 지금 고른 것을 비우고 정산 서비스의 AutoPick 순서대로 다시 담는다. 각 블록은 같은 Toggle 규칙을 거친다
        /// (칸·영웅 한도를 넘거나 빌린 블록이면 담지 않는다). 담은 수를 돌려준다.
        /// </summary>
        public int AutoPick()
        {
            if (Committed || _svc == null || View == null) return 0;
            ClearPicks();
            List<int> order = _svc.AutoPick(View);
            int added = 0;
            if (order != null)
                for (int i = 0; i < order.Count; i++)
                    if (ToggleCore(order[i], false) == BagToggleResult.Added) added++;
            LastReject = null;
            if (added > 0) Play(SettlementNameKeys.SoundBagPut);
            return added;
        }

        void ClearPicks()
        {
            for (int i = 0; i < Candidates.Count; i++) Candidates[i].Picked = false;
            Picked.Clear();
            HeroPicked = 0;
        }

        /// <summary>담지 못한 블록의 골드(확정 전 = 미리보기, 확정 뒤 = 결과 값).</summary>
        public long LeftoverGold
        {
            get
            {
                if (Result != null) return Result.GoldFromLeftovers;
                return SettlementRules.LeftoverGold(_db, Summary != null ? Summary.WildBlocks : null, Picked, LibraryRoom);
            }
        }

        /// <summary>서고에 실제로 들어갈 수(고른 수와 서고 남은 자리 중 작은 쪽).</summary>
        public int WillBag => Picked.Count < LibraryRoom ? Picked.Count : (LibraryRoom > 0 ? LibraryRoom : 0);

        // ================================================================ 가져가는 것

        /// <summary>재료 꾸러미를 연다(여는 연출). 이미 열었거나 꾸러미가 없으면 false.</summary>
        public bool OpenBundle()
        {
            if (!Committed || BundleOpened) return false;
            BundleOpened = true;
            Play(SettlementNameKeys.SoundBundleOpen);
            return true;
        }

        /// <summary>끝 버튼은 3쪽(확정 뒤)에서만 된다.</summary>
        public bool EndButtonsEnabled => Committed && Page == SettlementPage.Takeaways;

        public SettlementNav Again() => EndButtonsEnabled ? SettlementNav.Again() : null;
        public SettlementNav ToEnhance() => EndButtonsEnabled ? SettlementNav.ToEnhance(CharacterNumber) : null;
        public SettlementNav ToMenu() => EndButtonsEnabled ? SettlementNav.ToMenu() : null;

        /// <summary>
        /// Esc. 정산을 건너뛸 수 없다 — 확정 전에는 아무 일도 없다(꺼짐 소리). 확정 뒤 3쪽이면 메인 메뉴로.
        /// </summary>
        public SettlementNav Back()
        {
            if (EndButtonsEnabled) return SettlementNav.ToMenu();
            Play(SettlementNameKeys.SoundDisabled);
            return null;
        }

        void Play(string id)
        {
            if (_sound != null && !string.IsNullOrEmpty(id)) _sound.Play(id, SoundBus.Ui);
        }
    }
}
