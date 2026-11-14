using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Meta.Settlement
{
    /// <summary>
    /// 판 종료 → 프로필(01 요약서 10절, 06 요약서 5절). 오프라인은 기기가 그 자리에서 확정한다.
    /// 재료·부적은 판 씨앗이 아니라 기기 난수로 정산 때 뽑는다(생성자에 난수기를 주면 그것을 쓴다 — 테스트용).
    /// 프로필을 바꾼 메서드는 스스로 저장한다. 서버로 옮기는 길은 없다.
    /// </summary>
    public sealed class SettlementService : ISettlementService
    {
        /// <summary>보충 수치 조각 이름(DataSolo/G11.json).</summary>
        public const string ExtraName = "G11";
        /// <summary>자동 정산(중간 지점)으로 붙인 판 목록 줄의 EndReason.</summary>
        public const string EndReasonCheckpoint = "Checkpoint";

        const int PercentScale = 100;

        readonly GameContext _ctx;
        readonly Pcg32 _rng;
        SettlementTuning _tuning;

        public SettlementService(GameContext ctx, Pcg32 rng = null)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _rng = rng ?? new Pcg32(DeviceSeed());
        }

        static ulong DeviceSeed()
        {
            ulong ticks = unchecked((ulong)DateTime.UtcNow.Ticks);
            ulong salt = unchecked((ulong)(uint)Guid.NewGuid().GetHashCode());
            return ticks ^ (salt << 32) ^ salt;
        }

        IContentDb Db => _ctx.Content;
        ProfileData Profile => _ctx.Profiles != null ? _ctx.Profiles.Current : null;

        SettlementTuning Tuning
        {
            get
            {
                if (_tuning == null) _tuning = SettlementTuning.From(Db != null ? Db.SoloExtra(ExtraName) : null);
                return _tuning;
            }
        }

        // ------------------------------------------------------------ 순수 규칙

        /// <summary>귀환 가방 칸과 영웅 한도(01 요약서 10.2절). clearedDirect = 이번 판에서 직접 넘긴 웨이브 수.</summary>
        public BagLimits BagLimitsFor(int clearedDirect)
        {
            var limits = new BagLimits();
            DropsConfig d = Db != null ? Db.Drops : null;
            if (d == null || clearedDirect <= 0) return limits;
            if (d.BagWavesPerSlot > 0)
            {
                int slots = clearedDirect / d.BagWavesPerSlot;
                limits.Slots = slots < d.BagSlotCap ? slots : d.BagSlotCap;
            }
            if (d.BagWavesPerHero > 0) limits.HeroCap = clearedDirect / d.BagWavesPerHero;
            return limits;
        }

        /// <summary>건너뛸 수 있는 수(0, 10, 20 …): skip × 10 ≤ best × 7 (01 요약서 4.6절).</summary>
        public List<int> StartSkipOptions(int bestWave)
        {
            var list = new List<int> { 0 };
            SettlementTuning t = Tuning;
            if (t.StartSkipStep <= 0 || bestWave <= 0) return list;
            for (int skip = t.StartSkipStep; (long)skip * t.StartSkipLimitDen <= (long)bestWave * t.StartSkipLimitNum; skip += t.StartSkipStep)
                list.Add(skip);
            return list;
        }

        /// <summary>그 건너뛰는 수를 고를 수 있나(단위가 아니거나 한도를 넘으면 거절).</summary>
        public bool IsValidStartSkip(int bestWave, int skip) => StartSkipOptions(bestWave).Contains(skip);

        /// <summary>허상에 낸 가장 큰 흠집 % = floor((최대 체력 − 가장 낮았던 체력) × 100 ÷ 최대 체력) (05 요약서 10.4절).</summary>
        public static int DentPercent(long hpMax, long hpMin)
        {
            if (hpMax <= 0) return 0;
            if (hpMin < 0) hpMin = 0;
            if (hpMin > hpMax) hpMin = hpMax;
            return (int)((hpMax - hpMin) * PercentScale / hpMax);
        }

        /// <summary>
        /// 재료 표(06 요약서 5.2절)로 기회 목록을 재료로 바꾼다. 확정 줄은 그 수, 확률 줄은 줄마다 독립 1회(rollBp(bp)가 참이면 1개).
        /// 줄 순서는 표의 순서(하급, 중급, 상급, 상급 추가, 부적)이고 bp가 0이면 추첨하지 않는다. 엘리트는 한 마리마다 한 번.
        /// 까마귀 보따리는 crowAllowed일 때 한 번만.
        /// </summary>
        public static List<MaterialGain> DrawMaterials(MaterialsDef table, IReadOnlyList<DropChance> chances, bool crowAllowed,
            Func<int, bool> rollBp)
        {
            var gains = new List<MaterialGain>();
            if (table == null || chances == null) return gains;
            bool crowDone = false;
            for (int i = 0; i < chances.Count; i++)
            {
                DropChance c = chances[i];
                int day = DayCycle.Day(c.Wave);
                switch (c.Source)
                {
                    case DropChanceSource.CrowBundle:
                        if (!crowAllowed || crowDone || table.CrowBundle == null) break;
                        crowDone = true;
                        AddRows(gains, table.CrowBundle.Rows, day, c.Source, null);
                        break;
                    case DropChanceSource.FifthWave:
                        AddTier(gains, table.FifthWave, day, c.Source, rollBp);
                        break;
                    case DropChanceSource.BossKill:
                        AddTier(gains, table.BossKill, day, c.Source, rollBp);
                        break;
                    case DropChanceSource.EliteKill:
                        if (c.Wave < table.EliteMinWave) break;
                        for (int n = 0; n < c.Count; n++) AddTier(gains, table.EliteKill, day, c.Source, rollBp);
                        break;
                    case DropChanceSource.PhantomFirst:
                        AddRows(gains, table.PhantomFirst, day, c.Source, null);   // 확정만, 추첨 없음
                        break;
                }
            }
            return gains;
        }

        static void AddTier(List<MaterialGain> gains, List<MaterialTierDef> tiers, int day, DropChanceSource source, Func<int, bool> rollBp)
        {
            MaterialTierDef tier = MaterialTierDef.ForDay(tiers, day);
            if (tier != null) AddRows(gains, tier.Rows, day, source, rollBp);
        }

        static void AddRows(List<MaterialGain> gains, List<MaterialRowDef> rows, int day, DropChanceSource source, Func<int, bool> rollBp)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Count; i++)
            {
                MaterialRowDef row = rows[i];
                if (row.IsFixed) { AddGain(gains, row.Item, row.Fixed, true, false, source); continue; }
                if (rollBp == null) continue;
                int bp = row.ChanceBp(day);
                if (bp > 0 && rollBp(bp)) AddGain(gains, row.Item, 1, false, false, source);
            }
        }

        static void AddGain(List<MaterialGain> gains, string item, int qty, bool guaranteed, bool bound, DropChanceSource source)
        {
            if (qty <= 0 || string.IsNullOrEmpty(item)) return;
            for (int i = 0; i < gains.Count; i++)
            {
                MaterialGain g = gains[i];
                if (g.ItemId == item && g.Guaranteed == guaranteed && g.Bound == bound && g.Source == source) { g.Qty += qty; return; }
            }
            gains.Add(new MaterialGain { ItemId = item, Qty = qty, Guaranteed = guaranteed, Bound = bound, Source = source });
        }

        // ------------------------------------------------------------ 보기

        static bool IsSettleable(RunSummary s) => s != null && s.Mode == RunMode.Endless;

        string RarityOf(WildBlock b)
        {
            if (!string.IsNullOrEmpty(b.Rarity)) return b.Rarity;
            BlockDef def = Db != null ? Db.Block(b.BlockId) : null;
            return def != null ? def.Rarity : Ids.Rarity.Common;
        }

        static bool CanBag(WildBlock b) => b != null && !b.Borrowed && !b.FromLibrary;

        /// <summary>판 점수 = Σ 직접 넘긴 웨이브 + 건너뛴 점수. 요약의 Score가 건너뛴 점수를 뺀 값이어도 같은 답이 나오게 큰 쪽을 쓴다.</summary>
        static long RunScore(RunSummary s, out ScoreBreakdown total)
        {
            total = default;
            long sum = 0;
            if (s.WaveResults != null)
                for (int i = 0; i < s.WaveResults.Count; i++)
                {
                    WaveResult r = s.WaveResults[i];
                    if (r == null) continue;
                    ScoreBreakdown b = r.Score;
                    total.Kills += b.Kills; total.Boss += b.Boss; total.Clear += b.Clear; total.Fast += b.Fast;
                    total.NoHit += b.NoHit; total.Graze += b.Graze; total.Variance += b.Variance; total.PhantomFirst += b.PhantomFirst;
                    sum += b.Total;
                }
            sum += s.SkippedScore;
            if (s.Score > sum) sum = s.Score;
            total.Skipped = s.SkippedScore;
            total.Total = (int)sum;
            return sum;
        }

        int LibraryTotal(ProfileData p)
        {
            int n = 0;
            foreach (KeyValuePair<string, ItemStack> kv in p.Items)
                if (kv.Value != null && Db.Block(kv.Key) != null) n += kv.Value.Qty;
            return n;
        }

        int LibraryRoom(ProfileData p)
        {
            EconomyConfig eco = Db.Economy;
            if (eco == null) return int.MaxValue;
            int room = eco.LibraryBaseCap + p.LibraryExpansions * eco.ShopLibraryStepBlocks - LibraryTotal(p);
            return room > 0 ? room : 0;
        }

        static bool RevengeEligible(ProfileData p, RunSummary s) =>
            s.KilledPhantomAtWave20 && s.RevengeActive && p.Flags.RevengeDoneAt == null;

        bool HasFlawlessNight(RunSummary s)
        {
            List<int> slots = Tuning.FlawlessNightSlots;
            if (slots.Count == 0 || s.WaveResults == null) return false;
            for (int i = 0; i < s.WaveResults.Count; i++)
            {
                WaveResult first = s.WaveResults[i];
                if (first == null || DayCycle.Slot(first.Wave) != slots[0]) continue;
                bool all = true;
                for (int k = 0; k < slots.Count && all; k++)
                    all = NoHitAt(s, first.Wave + slots[k] - slots[0]);
                if (all) return true;
            }
            return false;
        }

        static bool NoHitAt(RunSummary s, int wave)
        {
            for (int i = 0; i < s.WaveResults.Count; i++)
            {
                WaveResult r = s.WaveResults[i];
                if (r != null && r.Wave == wave) return r.Stats != null && r.Stats.NoHit;
            }
            return false;
        }

        /// <summary>이 판을 정산하면 새로 받을 칭호(아직 없는 것만). 프로필을 바꾸지 않는다.</summary>
        List<string> TitlesToGrant(ProfileData p, CharacterRecord rec, RunSummary s)
        {
            var list = new List<string>();
            SettlementTuning t = Tuning;
            int best = rec != null && rec.BestWave > s.LastClearedWave ? rec.BestWave : s.LastClearedWave;
            for (int i = 0; i < t.RecordTitles.Count; i++)
                if (best >= t.RecordTitles[i].Wave) AddTitle(p, list, t.RecordTitles[i].Title);
            if (t.BloomTitleCount > 0 && (long)p.Feats.BloomCount + s.BloomCasts >= t.BloomTitleCount) AddTitle(p, list, Ids.Title.Bloom100);
            if (HasFlawlessNight(s)) AddTitle(p, list, Ids.Title.FlawlessNight);
            if (RevengeEligible(p, s)) AddTitle(p, list, Ids.Title.MirrorBreaker);
            return list;
        }

        static void AddTitle(ProfileData p, List<string> list, string title)
        {
            if (string.IsNullOrEmpty(title) || p.Titles.Owned.Contains(title) || list.Contains(title)) return;
            list.Add(title);
        }

        public SettlementView Prepare(RunSummary summary)
        {
            var view = new SettlementView { Summary = summary };
            ProfileData p = Profile;
            if (summary == null || p == null || Db == null) return view;
            long score = RunScore(summary, out view.Score);
            if (!IsSettleable(summary)) return view;

            view.Bag = BagLimitsFor(summary.ClearedDirect);
            if (summary.WildBlocks != null)
                for (int i = 0; i < summary.WildBlocks.Count; i++)
                    if (CanBag(summary.WildBlocks[i])) view.BagCandidates.Add(summary.WildBlocks[i]);
            view.LibraryRoom = LibraryRoom(p);

            CharacterRecord rec = p.Character(Ids.Char.NumberOf(summary.CharId));
            if (rec != null)
            {
                view.NewBestScore = score > rec.BestScore;
                view.NewBestWave = summary.LastClearedWave > rec.BestWave;
            }
            view.NewTitles = TitlesToGrant(p, rec, summary);
            return view;
        }

        /// <summary>
        /// 자동 가방(06 요약서 5.3절 U-17): 빌림 제외 → 등급 내림(영웅, 희귀, 일반) → 등급 안 블록 표 순서 → uid 오름.
        /// 영웅은 한도까지만, 칸이 차면 멈춘다.
        /// </summary>
        public List<int> AutoPick(SettlementView view)
        {
            var picks = new List<int>();
            if (view == null || view.BagCandidates == null || view.Bag.Slots <= 0) return picks;
            var sorted = new List<WildBlock>();
            for (int i = 0; i < view.BagCandidates.Count; i++)
                if (CanBag(view.BagCandidates[i])) sorted.Add(view.BagCandidates[i]);
            sorted.Sort(CompareForBag);

            int heroes = 0;
            for (int i = 0; i < sorted.Count && picks.Count < view.Bag.Slots; i++)
            {
                WildBlock b = sorted[i];
                if (picks.Contains(b.Uid)) continue;
                if (RarityOf(b) == Ids.Rarity.Hero)
                {
                    if (heroes >= view.Bag.HeroCap) continue;
                    heroes++;
                }
                picks.Add(b.Uid);
            }
            return picks;
        }

        static int RarityRank(string rarity) =>
            rarity == Ids.Rarity.Hero ? 0 : rarity == Ids.Rarity.Rare ? 1 : 2;

        int OrderInRarity(WildBlock b, string rarity)
        {
            IReadOnlyList<string> order = Db != null ? Db.BlocksOfRarity(rarity) : null;
            if (order != null)
                for (int i = 0; i < order.Count; i++)
                    if (order[i] == b.BlockId) return i;
            return int.MaxValue;
        }

        int CompareForBag(WildBlock a, WildBlock b)
        {
            string ra = RarityOf(a), rb = RarityOf(b);
            int c = RarityRank(ra).CompareTo(RarityRank(rb));
            if (c != 0) return c;
            c = OrderInRarity(a, ra).CompareTo(OrderInRarity(b, rb));
            if (c != 0) return c;
            return a.Uid.CompareTo(b.Uid);
        }

        // ------------------------------------------------------------ 확정

        public Result<SettlementResult> Commit(RunSummary summary, IReadOnlyList<int> bagUids) =>
            CommitCore(summary, bagUids, false);

        Result<SettlementResult> CommitCore(RunSummary summary, IReadOnlyList<int> bagUids, bool fromCheckpoint)
        {
            ProfileData p = Profile;
            if (summary == null || p == null || Db == null || Db.Drops == null) return Result<SettlementResult>.Fail(ErrorCode.InvalidArgument);
            // 연습·튜토리얼·보조 판은 정산하지 않는다.
            if (!IsSettleable(summary)) return Result<SettlementResult>.Fail(ErrorCode.NotEligible);
            // 같은 판은 한 번만(자동 정산 포함).
            if (!string.IsNullOrEmpty(summary.RunId) && summary.RunId == p.LastSettledRunId)
                return Result<SettlementResult>.Fail(ErrorCode.NotEligible);
            int charNumber = Ids.Char.NumberOf(summary.CharId);
            CharacterRecord rec = p.Character(charNumber);
            if (rec == null) return Result<SettlementResult>.Fail(ErrorCode.InvalidArgument);

            // 가방 검증: 칸 초과·영웅 초과·빌림 포함·없는 블록·같은 블록 두 번은 거절(프로필은 그대로).
            DropsConfig drops = Db.Drops;
            BagLimits limits = BagLimitsFor(summary.ClearedDirect);
            var wild = summary.WildBlocks ?? new List<WildBlock>();
            var chosen = new List<WildBlock>();
            int heroes = 0;
            if (bagUids != null)
                for (int i = 0; i < bagUids.Count; i++)
                {
                    WildBlock b = wild.Find(x => x != null && x.Uid == bagUids[i]);
                    if (!CanBag(b) || chosen.Contains(b)) return Result<SettlementResult>.Fail(ErrorCode.InvalidArgument);
                    if (RarityOf(b) == Ids.Rarity.Hero) heroes++;
                    chosen.Add(b);
                }
            if (chosen.Count > limits.Slots || heroes > limits.HeroCap) return Result<SettlementResult>.Fail(ErrorCode.InvalidArgument);

            var result = new SettlementResult { FromCheckpoint = fromCheckpoint };
            string nowIso = NowIso();
            SettlementTuning tuning = Tuning;

            // 칭호는 프로필을 바꾸기 전 상태로 판단한다(Prepare와 같은 답).
            List<string> newTitles = TitlesToGrant(p, rec, summary);
            bool revenge = RevengeEligible(p, summary);

            // 1. 가방 → 서고(한도까지. 넘친 것은 골드), 남은 야생 블록 → 골드.
            int room = LibraryRoom(p);
            for (int i = 0; i < chosen.Count; i++)
            {
                if (room <= 0) break;
                p.Item(chosen[i].BlockId).Qty += 1;
                room--;
                result.Bagged.Add(chosen[i]);
            }
            long leftovers = 0;
            for (int i = 0; i < wild.Count; i++)
            {
                WildBlock b = wild[i];
                if (b == null || b.FromLibrary || result.Bagged.Contains(b)) continue;
                leftovers += drops.DiscardGold(RarityOf(b), b.Borrowed);
            }

            // 2. 골드: 지갑 잔액 + 가져가지 못한 블록.
            long purse = summary.Purse > 0 ? summary.Purse : 0;
            result.GoldFromLeftovers = leftovers;
            result.GoldGained = purse + leftovers;
            p.Currencies.Gold += result.GoldGained;

            // 3. 재료: 확정분 + 기회마다 줄별 독립 추첨(기기 난수). 까마귀 보따리는 프로필당 한 번.
            bool lowReceivedBefore = HasReceivedLowStone(p);
            var chances = new List<DropChance>();
            if (summary.DropChances != null) chances.AddRange(summary.DropChances);
            MaterialsDef table = drops.MaterialTable;
            bool crowInList = chances.Exists(c => c.Source == DropChanceSource.CrowBundle);
            if (summary.CrowBundleEarned && !crowInList && table != null && table.CrowBundle != null)
            {
                chances.Insert(0, new DropChance { Source = DropChanceSource.CrowBundle, Wave = table.CrowBundle.Wave, Count = 1 });
                crowInList = true;
            }
            bool crowAllowed = !p.Flags.CrowBundleGiven;
            result.Materials = DrawMaterials(table, chances, crowAllowed, _rng.RollBp);
            if (crowInList && crowAllowed)
            {
                p.Flags.CrowBundleGiven = true;
                result.CrowBundle = true;
            }

            // 4. 첫 만남 표시: 웨이브 10을 넘긴 것으로 정산된 판만.
            bool doneBefore = p.Flags.RevengeDoneAt != null;
            if (summary.PhantomFirst && !p.Flags.PhantomMet && tuning.PhantomFirstWave > 0 && summary.LastClearedWave >= tuning.PhantomFirstWave)
            {
                p.Flags.PhantomMet = true;
                result.PhantomMetNow = true;
            }
            if (revenge) p.Flags.PhantomMet = true;

            // 5. 추적기: 첫 만남이 든 판부터 복수 판까지 센다(같은 판이면 1). 흠집은 웨이브 20에서 진 판의 최댓값.
            if (!doneBefore && p.Flags.PhantomMet)
            {
                p.Revenge.RunsSinceFirstMeet += 1;
                if (summary.PhantomAttempted)
                {
                    p.Revenge.PhantomTries += 1;
                    if (!summary.KilledPhantomAtWave20 && summary.PhantomDentPercent > p.Revenge.PhantomBestDent)
                        p.Revenge.PhantomBestDent = summary.PhantomDentPercent;
                }
            }

            // 6. 복수 보상(그 프로필이 웨이브 20 허상을 처음 잡은 판. 잡은 뒤 죽어도 받는다).
            if (revenge)
            {
                if (!p.Cosmetics.Owned.Contains(Ids.Cosmetic.CrackedMirrorShard)) p.Cosmetics.Owned.Add(Ids.Cosmetic.CrackedMirrorShard);
                if (drops.RevengeReward != null)
                    for (int i = 0; i < drops.RevengeReward.Count; i++)
                        AddGain(result.Materials, drops.RevengeReward[i].Item, drops.RevengeReward[i].Count, true, true, DropChanceSource.BossKill);
                p.Revenge.RevengeRuns = p.Revenge.RunsSinceFirstMeet;
                p.Flags.RevengeDoneAt = nowIso;
                result.RevengeReward = true;
            }

            // 재료를 프로필에 넣는다(귀속분은 qty_bound에도).
            int lowGained = 0;
            for (int i = 0; i < result.Materials.Count; i++)
            {
                MaterialGain g = result.Materials[i];
                ItemStack stack = p.Item(g.ItemId);
                stack.Qty += g.Qty;
                if (g.Bound) stack.QtyBound += g.Qty;
                if (g.ItemId == Ids.Item.StoneLow) lowGained += g.Qty;
            }

            // 7. 캐릭터 기록: 최고 점수·최고 웨이브(넘긴 웨이브)·누적 웨이브·야생 물 단계.
            long score = RunScore(summary, out _);
            if (score > rec.BestScore) { rec.BestScore = score; result.NewBestScore = true; }
            if (summary.LastClearedWave > rec.BestWave) { rec.BestWave = summary.LastClearedWave; result.NewBestWave = true; }
            if (summary.ClearedDirect > 0) rec.TotalWaves += summary.ClearedDirect;
            int stain = 0;
            for (int i = 0; i < tuning.WildStainTotalWaves.Count; i++)
                if (rec.TotalWaves >= tuning.WildStainTotalWaves[i]) stain++;
            if (stain > rec.WildStainStageMax) rec.WildStainStageMax = stain;

            // 8. 칭호(기록·손맛·복수).
            if (summary.BloomCasts > 0) p.Feats.BloomCount += summary.BloomCasts;
            for (int i = 0; i < newTitles.Count; i++)
                if (!p.Titles.Owned.Contains(newTitles[i])) p.Titles.Owned.Add(newTitles[i]);
            result.NewTitles = newTitles;

            // 8b. 도전 과제: 판 요약을 캐릭터 누적 기록에 더하고, 새로 이룬 과제의 보상(칭호·꾸미기·서고 칸)을 넣는다(DataSolo/Challenges.json).
            Records.ChallengeBook.AddRunCounters(rec, summary);
            result.NewChallenges = Records.ChallengeBook.Evaluate(p, Records.ChallengeBook.Load(Db), nowIso);

            // 9. 새 비기(판 중 RecordArtUnlock이 이미 적었어도 빠진 것이 없게 한 번 더 맞춘다).
            if (summary.NewArts != null)
                for (int i = 0; i < summary.NewArts.Count; i++)
                {
                    string art = summary.NewArts[i];
                    if (string.IsNullOrEmpty(art)) continue;
                    if (!rec.UnlockedArts.Contains(art)) rec.UnlockedArts.Add(art);
                    if (!result.NewArts.Contains(art)) result.NewArts.Add(art);
                }

            // 10. 알아낸 약점·저항을 합친다(중복 없이).
            if (summary.AffinityKnown != null)
                for (int i = 0; i < summary.AffinityKnown.Count; i++)
                {
                    string key = summary.AffinityKnown[i];
                    if (!string.IsNullOrEmpty(key) && !p.KnownAffinities.Contains(key)) p.KnownAffinities.Add(key);
                }

            // 11. 판 목록 한 줄. 상한을 넘으면 가장 오래된 것부터 지운다.
            var record = new RunRecordData
            {
                RunId = summary.RunId,
                Date = nowIso,
                Character = charNumber,
                ReachedWave = summary.ReachedWave,
                StartWave = summary.StartWave,
                Score = score,
                WandStage = summary.WandStage,
                Loadout = summary.LoadoutIds != null ? new List<string>(summary.LoadoutIds) : new List<string>(),
                EndReason = fromCheckpoint ? EndReasonCheckpoint : summary.EndReason.ToString()
            };
            p.RunHistory.Add(record);
            if (tuning.RunHistoryCap > 0)
                while (p.RunHistory.Count > tuning.RunHistoryCap) p.RunHistory.RemoveAt(0);
            result.Record = record;

            // 12. 강화 안내: 하급 강화석을 처음 받은 정산에서 한 번.
            if (lowGained > 0)
            {
                result.ShowEnhanceTutorial = !lowReceivedBefore && !p.Flags.EnhanceTutorialShown;
                MarkLowStoneReceived(p);
            }

            // 13. 중간 지점을 지우고, 이 판을 정산한 판으로 적고, 저장한다.
            p.RunCheckpoint = null;
            p.LastSettledRunId = summary.RunId;
            _ctx.Profiles.Save();
            return Result<SettlementResult>.Success(result);
        }

        static bool HasReceivedLowStone(ProfileData p)
        {
            if (p.Flags.LowStoneReceived) return true;
            return p.Items.TryGetValue(Ids.Item.StoneLow, out ItemStack s) && s != null && s.Qty > 0;
        }

        static void MarkLowStoneReceived(ProfileData p)
        {
            p.Flags.LowStoneReceived = true;
        }

        string NowIso()
        {
            DateTime now = _ctx.Clock != null ? _ctx.Clock.Now : DateTime.Now;
            return now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------ 중간 지점

        public void SaveCheckpoint(RunSummary summary)
        {
            ProfileData p = Profile;
            if (p == null || !IsSettleable(summary)) return;
            p.RunCheckpoint = new RunCheckpointData
            {
                RunId = summary.RunId,
                Wave = summary.LastClearedWave,
                Summary = CheckpointCodec.ToJson(summary)
            };
            _ctx.Profiles.Save();
        }

        public SettlementResult SettlePendingCheckpoint()
        {
            ProfileData p = Profile;
            if (p == null || p.RunCheckpoint == null) return null;
            RunSummary summary = CheckpointCodec.FromJson(p.RunCheckpoint.Summary);
            Result<SettlementResult> r = default;
            bool tried = false;
            if (summary != null)
            {
                SettlementView view = Prepare(summary);
                r = CommitCore(summary, AutoPick(view), true);
                tried = true;
            }
            if (tried && r.Ok) return r.Value;
            // 읽을 수 없거나 이미 정산한 판의 중간 지점이다 — 버린다(다음 실행 때 다시 물지 않게).
            p.RunCheckpoint = null;
            _ctx.Profiles.Save();
            return null;
        }

        public void RecordArtUnlock(int character, string artId)
        {
            ProfileData p = Profile;
            if (p == null || string.IsNullOrEmpty(artId)) return;
            CharacterRecord rec = p.Character(character);
            if (rec == null || rec.UnlockedArts.Contains(artId)) return;
            rec.UnlockedArts.Add(artId);
            _ctx.Profiles.Save();
        }
    }
}
