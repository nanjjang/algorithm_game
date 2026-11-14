using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Meta.Enhance
{
    /// <summary>
    /// 강화 규칙을 프로필에 적용한다(07 요약서 4절). 전제 검사 → 비용 차감 → 난수 한 번 → 지팡이 갱신 → 결과 꾸러미 → 저장.
    /// 규칙상의 "안 됨"은 ErrorCode로 돌려준다. 프로필을 바꾼 호출은 매번 스스로 저장한다(9.3절).
    /// 다섯 지팡이가 같은 확률·비용·실패 규칙을 쓴다. 오프라인이라 서버 알림은 항상 null이고, 유료 재화 값은 읽지 않는다.
    /// </summary>
    public sealed class EnhanceService : IEnhanceService
    {
        // enhance_log_tail 한 줄의 종류.
        const string LogEnhance = "enhance", LogMutation = "mutation", LogCarve = "carve", LogRemove = "remove_mutation";

        readonly GameContext _ctx;
        readonly IEnhanceRandom _rng;
        EnhanceTuning _tuning;

        public EnhanceService(GameContext ctx) : this(ctx, new Pcg32EnhanceRandom()) { }

        public EnhanceService(GameContext ctx, Pcg32 rng) : this(ctx, new Pcg32EnhanceRandom(rng)) { }

        public EnhanceService(GameContext ctx, IEnhanceRandom rng)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        EnhanceConfig Cfg => _ctx.Content.Enhance;
        IContentDb Db => _ctx.Content;
        ProfileData Profile => _ctx.Profile;

        public EnhanceTuning Tuning => _tuning ?? (_tuning = EnhanceTuning.From(_ctx.Content));

        // ------------------------------------------------------------------ 읽기

        public ProbBreakdown Preview(long wandId, IReadOnlyList<string> sacrifices, bool useCharm)
        {
            WandData w = Profile != null ? Profile.Wand(wandId) : null;
            if (w == null) return new ProbBreakdown { Blocked = ErrorCode.NotFound };
            ProbBreakdown b = BreakdownOf(w, sacrifices);
            b.Blocked = Validate(w, sacrifices, useCharm);
            return b;
        }

        /// <summary>제물 보너스의 내역 두 줄(등급 합 / 같은 속성). 화면의 내역 줄이 쓴다.</summary>
        public SacrificeBreakdown SacrificeOf(long wandId, IReadOnlyList<string> sacrifices)
        {
            WandData w = Profile != null ? Profile.Wand(wandId) : null;
            return EnhanceRules.SacrificeBonus(Cfg, Db, sacrifices, w != null ? ElementOf(w) : null);
        }

        public float StageDamageMul(int stage) => EnhanceRules.StageDamageMul(Cfg, stage);

        public List<ExpectedRow> ExpectedTable(long wandId, int upToStage)
        {
            WandData w = Profile != null ? Profile.Wand(wandId) : null;
            if (w == null) return new List<ExpectedRow>();
            return EnhanceRules.ExpectedTable(Cfg, w.Stage, w.Aura, w.FloorPeak, upToStage);
        }

        /// <summary>"보유 재화로 평균 N번"(07 요약서 6절). 지금 프로필의 골드·강화석으로 목표 단계까지 도전할 때.</summary>
        public int AttemptsWithOwned(long wandId, int targetStage)
        {
            ProfileData p = Profile;
            WandData w = p != null ? p.Wand(wandId) : null;
            if (w == null) return 0;
            return EnhanceRules.AttemptsWithOwned(Cfg, w.Stage, w.Aura, w.FloorPeak, targetStage, p.Currencies.Gold,
                Qty(p, Ids.Item.StoneLow), Qty(p, Ids.Item.StoneMid), Qty(p, Ids.Item.StoneHigh), Tuning.OwnedForecastMaxAttempts);
        }

        /// <summary>실패 안내 한 줄(07 요약서 4.4절).</summary>
        public FailHint HintFor(long wandId, bool useCharm)
        {
            WandData w = Profile.Wand(wandId);
            return EnhanceRules.HintFor(Cfg, w.Stage, w.FloorPeak, useCharm);
        }

        /// <summary>흔적 조각 수(07 요약서 4.9절).</summary>
        public int ScarCount(WandData w) => w == null ? 0 : EnhanceRules.ScarCount(w.Stage, w.RecordPeak, Tuning.ScarMax);

        /// <summary>그 지팡이가 지금 원정에 나가 있는가(판이 끝나 정산을 기다리는 동안은 아니다).</summary>
        public bool IsLocked(long wandId)
        {
            IGameSession session = _ctx.Session;
            SimWorld world = session != null ? session.MainWorld : null;
            if (world == null || world.Run == null || world.Run.Config == null) return false;
            if (world.Run.Phase == RunPhase.RunEnded || world.Run.Config.Mode != RunMode.Endless) return false;
            return world.Run.Config.WandId == wandId;
        }

        // ------------------------------------------------------------------ 강화

        public Result<EnhanceResult> Enhance(long wandId, IReadOnlyList<string> sacrifices, bool useCharm)
        {
            ProfileData p = Profile;
            WandData w = p != null ? p.Wand(wandId) : null;
            if (w == null) return Result<EnhanceResult>.Fail(ErrorCode.NotFound);
            ErrorCode blocked = Validate(w, sacrifices, useCharm);
            if (blocked != ErrorCode.None) return Result<EnhanceResult>.Fail(blocked);

            EnhanceConfig cfg = Cfg;
            ProbBreakdown b = BreakdownOf(w, sacrifices);
            var r = new EnhanceResult
            {
                AttemptId = p.NextAttemptId++,
                StageBefore = w.Stage,
                StageAfter = w.Stage,
                AuraBefore = w.Aura,
                AuraAfter = w.Aura,
                Breakdown = b,
                ServerNotice = null,
            };
            int recordBefore = w.RecordPeak;

            // 비용은 시도 순간에 빠지고 어떤 결과든 돌아오지 않는다(4.3절). 부적만 결과 뒤에(막았을 때만) 빠진다.
            p.Currencies.Gold -= b.CostGold;
            Take(p, b.StoneId, b.StoneCount);
            if (sacrifices != null)
                for (int i = 0; i < sacrifices.Count; i++) Take(p, sacrifices[i], 1);

            r.RollBp = _rng.Next(EnhanceRules.BpFull);
            JudgeBand band = EnhanceRules.Judge(r.RollBp, b.SuccessBp, b.GreatBp, b.MutationEndBp);

            string candidate = null;
            if (band == JudgeBand.Mutation)
            {
                List<string> pool = EnhanceRules.MutationPool(w.Mutations, EnhanceRules.CoreIsExplosive(Db, w.Loadout));
                candidate = EnhanceRules.DrawCandidate(pool, _rng);
                if (candidate == null) band = JudgeBand.Fail;   // 후보가 없으면 일반 실패(4.7절)
            }

            switch (band)
            {
                case JudgeBand.GreatSuccess:
                    r.Outcome = EnhanceOutcome.GreatSuccess;
                    w.Stage = EnhanceRules.StageAfterGreat(cfg, w.Stage);
                    w.Aura = 0;
                    if (EnhanceRules.GreatGivesSlot(cfg, r.StageBefore))
                    {
                        if (w.SlotsExtra < cfg.SlotExtraMax) { AddSlot(w); r.SlotExpanded = true; }
                        else r.SlotAlreadyFull = true;
                    }
                    break;
                case JudgeBand.Success:
                    r.Outcome = EnhanceOutcome.Success;
                    w.Stage = EnhanceRules.StageAfterSuccess(cfg, w.Stage);
                    w.Aura = 0;
                    break;
                case JudgeBand.Mutation:
                {
                    // 거절하면 받을 결과를 그 자리에서 계산해 같이 저장한다(4.6절). 결정 전에 꺼도 다음에 카드부터 보인다.
                    FallbackResult fb = EnhanceRules.ResolveFail(cfg, w.Stage, w.FloorPeak, w.Aura, useCharm);
                    r.Outcome = EnhanceOutcome.MutationOffered;
                    r.MutationCandidate = candidate;
                    r.MutationPushedOut = EnhanceRules.WouldPushOut(w.Mutations, MutationCapOf(w));
                    r.HasFallback = true;
                    r.Fallback = fb;
                    w.PendingOffer = new MutationOffer
                    {
                        AttemptId = r.AttemptId,
                        Candidate = candidate,
                        WouldPushOut = r.MutationPushedOut,
                        FallbackOutcome = fb.Outcome.ToString(),
                        FallbackStageAfter = fb.StageAfter,
                        FallbackCharmConsumed = fb.CharmConsumed,
                        FallbackAuraAfter = fb.AuraAfter,
                        RollBp = r.RollBp,
                        SuccessPct = b.SuccessPct,
                        GreatBp = b.GreatBp,
                        MutationEndBp = b.MutationEndBp,
                    };
                    break;
                }
                default:
                    ApplyFail(p, w, EnhanceRules.ResolveFail(cfg, w.Stage, w.FloorPeak, w.Aura, useCharm), r);
                    break;
            }

            FinishStageChange(w, r, recordBefore);
            FillBalances(p, w, r);
            Records.EnhanceRecordBook.NoteAttempt(w, r);   // 보여 주기만 하는 기록(판정·확률과 무관)
            Log(p, LogEnhance, w, r);
            Save();
            return Result<EnhanceResult>.Success(r);
        }

        public Result<EnhanceResult> ResolveMutation(long wandId, long attemptId, bool accept)
        {
            ProfileData p = Profile;
            WandData w = p != null ? p.Wand(wandId) : null;
            if (w == null) return Result<EnhanceResult>.Fail(ErrorCode.NotFound);
            MutationOffer offer = w.PendingOffer;
            if (offer == null) return Result<EnhanceResult>.Fail(ErrorCode.NoPendingMutation);
            if (offer.AttemptId != attemptId) return Result<EnhanceResult>.Fail(ErrorCode.InvalidArgument);
            if (IsLocked(w.Id)) return Result<EnhanceResult>.Fail(ErrorCode.WandLocked);

            EnhanceConfig cfg = Cfg;
            var r = new EnhanceResult
            {
                AttemptId = offer.AttemptId,
                StageBefore = w.Stage,
                StageAfter = w.Stage,
                AuraBefore = w.Aura,
                AuraAfter = w.Aura,
                MutationCandidate = offer.Candidate,
                ServerNotice = null,
            };
            RestoreFromLog(p, offer.AttemptId, w, r);
            if (offer.RollBp >= 0) RestoreFromOffer(offer, w, r);
            int recordBefore = w.RecordPeak;

            if (accept)
            {
                // 받음: 단계 그대로, 넘치면 가장 오래된 것을 밀어내고, 기운 +1. 부적은 변이를 막지 않으니 쓰지 않는다.
                r.Outcome = EnhanceOutcome.MutationAccepted;
                r.MutationPushedOut = EnhanceRules.AcceptMutation(w.Mutations, offer.Candidate, MutationCapOf(w), offer.AttemptId);
                w.Aura = Math.Min(cfg.AuraMax, w.Aura + 1);
            }
            else
            {
                var fb = new FallbackResult
                {
                    Outcome = ParseOutcome(offer.FallbackOutcome),
                    StageAfter = offer.FallbackStageAfter,
                    CharmConsumed = offer.FallbackCharmConsumed,
                    AuraAfter = offer.FallbackAuraAfter,
                };
                // 맡아 둔 부적이 저장 파일 손질 등으로 없어졌으면 부적 없는 실패로 다시 계산한다(없는 것을 쓰지 않는다).
                if (fb.CharmConsumed && Qty(p, Ids.Item.Charm) < 1)
                    fb = EnhanceRules.ResolveFail(cfg, w.Stage, w.FloorPeak, w.Aura, false);
                ApplyFail(p, w, fb, r);
            }
            w.PendingOffer = null;

            FinishStageChange(w, r, recordBefore);
            FillBalances(p, w, r);
            Records.EnhanceRecordBook.NoteResolve(w, r);   // 보여 주기만 하는 기록(판정·확률과 무관)
            Log(p, LogMutation, w, r);
            Save();
            return Result<EnhanceResult>.Success(r);
        }

        public Result<WandData> RemoveMutation(long wandId, string mutationId)
        {
            ProfileData p = Profile;
            WandData w = p != null ? p.Wand(wandId) : null;
            if (w == null) return Result<WandData>.Fail(ErrorCode.NotFound);
            if (IsLocked(w.Id)) return Result<WandData>.Fail(ErrorCode.WandLocked);
            if (w.PendingOffer != null) return Result<WandData>.Fail(ErrorCode.MutationPending);
            int index = w.Mutations.FindIndex(m => m.Id == mutationId);
            if (index < 0) return Result<WandData>.Fail(ErrorCode.InvalidArgument);
            long price = Cfg.MutationRemoveGold;
            if (p.Currencies.Gold < price) return Result<WandData>.Fail(ErrorCode.NotEnoughGold);

            // 고른 하나만 없어진다. 단계·기운·슬롯·새기기 누적은 그대로다(4.7절).
            p.Currencies.Gold -= price;
            w.Mutations.RemoveAt(index);
            Log(p, LogRemove, w, null, mutationId);
            Save();
            return Result<WandData>.Success(w);
        }

        // ------------------------------------------------------------------ 슬롯 새기기 (4.8절)

        public ProbBreakdown PreviewCarve(long wandId)
        {
            ProfileData p = Profile;
            WandData w = p != null ? p.Wand(wandId) : null;
            if (w == null) return new ProbBreakdown { Blocked = ErrorCode.NotFound };
            EnhanceConfig cfg = Cfg;
            int pct = EnhanceRules.CarvePct(cfg, w.CarveBonus);
            int raw = EnhanceRules.CarveRawPct(cfg, w.CarveBonus);
            var b = new ProbBreakdown
            {
                BasePct = cfg.Carve.BasePct,
                RawPct = raw,
                SuccessPct = pct,
                Capped = raw >= EnhanceRules.PctFull,
                SuccessBp = pct * EnhanceRules.BpPerPct,
                MutationEndBp = pct * EnhanceRules.BpPerPct,
                FloorStage = EnhanceRules.DisplayFloor(cfg, w.FloorPeak),
                CostGold = EnhanceRules.CarveGold(cfg, w.SlotsExtra),
                StoneId = Ids.Item.StoneHigh,
                StoneCount = cfg.Carve.HighStones,
            };
            b.Blocked = ValidateCarve(p, w, b);
            return b;
        }

        public Result<CarveResult> CarveSlot(long wandId)
        {
            ProfileData p = Profile;
            WandData w = p != null ? p.Wand(wandId) : null;
            if (w == null) return Result<CarveResult>.Fail(ErrorCode.NotFound);
            ProbBreakdown b = PreviewCarve(wandId);
            if (b.Blocked != ErrorCode.None) return Result<CarveResult>.Fail(b.Blocked);

            p.Currencies.Gold -= b.CostGold;
            Take(p, b.StoneId, b.StoneCount);

            var r = new CarveResult { ChancePct = b.SuccessPct, RollBp = _rng.Next(EnhanceRules.BpFull) };
            r.Success = EnhanceRules.CarveSucceeds(Cfg, w.CarveBonus, r.RollBp);
            if (r.Success)
            {
                AddSlot(w);
                w.CarveBonus = 0;
            }
            else
            {
                // 재료만 사라진다. 단계·기운은 그대로, 눈금 한 칸.
                w.CarveBonus += 1;
            }
            r.SlotsExtraAfter = w.SlotsExtra;
            r.CarveBonusAfter = w.CarveBonus;
            r.GoldAfter = p.Currencies.Gold;

            var line = NewLogLine(LogCarve, w);
            line["success"] = r.Success;
            line["roll_bp"] = r.RollBp;
            line["chance_pct"] = r.ChancePct;
            line["gold"] = b.CostGold;
            AppendLog(p, line);
            Save();
            return Result<CarveResult>.Success(r);
        }

        public Result<WandData> SetScarVisibility(long wandId, bool show)
        {
            WandData w = Profile != null ? Profile.Wand(wandId) : null;
            if (w == null) return Result<WandData>.Fail(ErrorCode.NotFound);
            if (w.ShowScars != show)
            {
                w.ShowScars = show;
                Save();
            }
            return Result<WandData>.Success(w);
        }

        // ------------------------------------------------------------------ 전제 검사 (4.1절 표의 차례)

        /// <summary>시도 전제. 어긴 것이 없으면 None.</summary>
        public ErrorCode Validate(WandData w, IReadOnlyList<string> sacrifices, bool useCharm)
        {
            ProfileData p = Profile;
            EnhanceConfig cfg = Cfg;
            if (w == null || p == null) return ErrorCode.NotFound;
            if (w.Stage >= cfg.MaxStage) return ErrorCode.MaxStage;
            if (IsLocked(w.Id)) return ErrorCode.WandLocked;
            if (w.PendingOffer != null) return ErrorCode.MutationPending;
            if (p.Currencies.Gold < EnhanceRules.Cost(cfg, w.Stage)) return ErrorCode.NotEnoughGold;
            if (Qty(p, EnhanceRules.StoneId(EnhanceRules.StoneTierFor(cfg, w.Stage))) < cfg.StonesPerAttempt) return ErrorCode.NotEnoughStone;

            ErrorCode sac = ValidateSacrifices(p, sacrifices);
            if (sac != ErrorCode.None) return sac;

            if (useCharm)
            {
                if (CharmsFree(p, w) < 1) return ErrorCode.NotEnoughCharm;
                if (!EnhanceRules.DropPossible(cfg, w.Stage, w.FloorPeak)) return ErrorCode.CharmNotNeeded;
            }
            return ErrorCode.None;
        }

        ErrorCode ValidateSacrifices(ProfileData p, IReadOnlyList<string> sacrifices)
        {
            if (sacrifices == null || sacrifices.Count == 0) return ErrorCode.None;
            if (sacrifices.Count > Cfg.Sacrifice.MaxBlocks) return ErrorCode.InvalidSacrifice;

            // 서고의 여분(끼우지 않은 것)만 제물이 된다.
            var need = new Dictionary<string, int>();
            int projectilesGiven = 0;
            for (int i = 0; i < sacrifices.Count; i++)
            {
                string id = sacrifices[i];
                BlockDef def = id != null ? Db.Block(id) : null;
                if (def == null) return ErrorCode.InvalidSacrifice;
                need.TryGetValue(id, out int n);
                need[id] = n + 1;
                if (def.IsProjectile) projectilesGiven++;
            }
            foreach (KeyValuePair<string, int> kv in need)
            {
                if (!p.Items.TryGetValue(kv.Key, out ItemStack s) || s.Qty - s.QtySocketed < kv.Value) return ErrorCode.InvalidSacrifice;
            }

            // 제물을 빼도 프로필 전체(서고 + 조립)에 투사체가 하나는 남아야 한다.
            if (projectilesGiven > 0 && ProjectileTotal(p) - projectilesGiven < 1) return ErrorCode.LastProjectile;
            return ErrorCode.None;
        }

        ErrorCode ValidateCarve(ProfileData p, WandData w, ProbBreakdown b)
        {
            if (IsLocked(w.Id)) return ErrorCode.WandLocked;
            if (w.PendingOffer != null) return ErrorCode.MutationPending;
            if (!EnhanceRules.CarveEligible(Cfg, w.Stage, w.SlotsExtra)) return ErrorCode.NotEligible;
            if (p.Currencies.Gold < b.CostGold) return ErrorCode.NotEnoughGold;
            if (Qty(p, b.StoneId) < b.StoneCount) return ErrorCode.NotEnoughStone;
            return ErrorCode.None;
        }

        /// <summary>프로필 전체의 투사체 블록 수(서고 수량에 끼운 것이 들어 있다).</summary>
        int ProjectileTotal(ProfileData p)
        {
            int total = 0;
            foreach (KeyValuePair<string, ItemStack> kv in p.Items)
            {
                BlockDef def = Db.Block(kv.Key);
                if (def != null && def.IsProjectile) total += kv.Value.Qty;
            }
            return total;
        }

        /// <summary>이 지팡이가 쓸 수 있는 부적 = 보유 − 다른 지팡이의 변이 결정이 맡아 둔 것(거절하면 탈 부적).</summary>
        int CharmsFree(ProfileData p, WandData self)
        {
            int free = Qty(p, Ids.Item.Charm);
            for (int i = 0; i < p.Wands.Count; i++)
            {
                WandData other = p.Wands[i];
                if (other != self && other.PendingOffer != null && other.PendingOffer.FallbackCharmConsumed) free--;
            }
            return free;
        }

        // ------------------------------------------------------------------ 적용

        ProbBreakdown BreakdownOf(WandData w, IReadOnlyList<string> sacrifices)
        {
            SacrificeBreakdown sac = EnhanceRules.SacrificeBonus(Cfg, Db, sacrifices, ElementOf(w));
            return EnhanceRules.Breakdown(Cfg, w.Stage, w.FloorPeak, w.Aura, sac);
        }

        string ElementOf(WandData w)
        {
            CharacterDef def = Db.Character(Ids.Char.Of(w.Character));
            return def != null ? def.Element : null;
        }

        int MutationCapOf(WandData w) => EnhanceRules.MutationCap(Cfg, Db, w.Character);

        void ApplyFail(ProfileData p, WandData w, FallbackResult fb, EnhanceResult r)
        {
            int scarsBefore = ScarCount(w);
            r.Outcome = fb.Outcome;
            if (fb.Outcome == EnhanceOutcome.FailDrop)
            {
                w.Stage = fb.StageAfter;
                w.DropCount += 1;
            }
            else if (fb.Outcome == EnhanceOutcome.FailCharmBlocked)
            {
                Take(p, Ids.Item.Charm, 1);
                r.CharmConsumed = true;
            }
            w.Aura = fb.AuraAfter;
            r.ScarAdded = ScarCount(w) > scarsBefore;
        }

        /// <summary>단계가 바뀐 뒤의 공통 처리: 역대 최고, 마일스톤, 꾸러미의 뒤쪽 값.</summary>
        void FinishStageChange(WandData w, EnhanceResult r, int recordBefore)
        {
            if (w.Stage > w.FloorPeak) w.FloorPeak = w.Stage;
            if (w.Stage > w.RecordPeak)
            {
                w.RecordPeak = w.Stage;
                r.RecordPeakUpdated = true;
            }
            r.StageAfter = w.Stage;
            r.AuraAfter = w.Aura;
            if (r.StageAfter > r.StageBefore)
            {
                r.MilestoneStage = EnhanceRules.MilestoneReached(Cfg, r.StageBefore, r.StageAfter);
                r.MilestoneFirstTime = r.MilestoneStage > recordBefore;
            }
        }

        void AddSlot(WandData w)
        {
            w.SlotsExtra += 1;
            if (w.Loadout == null) w.Loadout = new LoadoutData();
            if (w.Loadout.Extra == null) w.Loadout.Extra = new List<string>();
            while (w.Loadout.Extra.Count < w.SlotsExtra) w.Loadout.Extra.Add(null);
        }

        void FillBalances(ProfileData p, WandData w, EnhanceResult r)
        {
            r.Wand = w;
            r.GoldAfter = p.Currencies.Gold;
            r.StoneLowAfter = Qty(p, Ids.Item.StoneLow);
            r.StoneMidAfter = Qty(p, Ids.Item.StoneMid);
            r.StoneHighAfter = Qty(p, Ids.Item.StoneHigh);
            r.CharmAfter = Qty(p, Ids.Item.Charm);
        }

        static int Qty(ProfileData p, string itemId) =>
            p.Items.TryGetValue(itemId, out ItemStack s) ? s.Qty : 0;

        /// <summary>수량을 뺀다. 귀속분(qty_bound)이 있으면 그것부터 쓴다(4.3절).</summary>
        static void Take(ProfileData p, string itemId, int count)
        {
            if (count <= 0 || !p.Items.TryGetValue(itemId, out ItemStack s)) return;
            int boundUsed = Math.Min(s.QtyBound, count);
            s.QtyBound -= boundUsed;
            s.Qty -= count;
            if (s.Qty < 0) s.Qty = 0;
        }

        static EnhanceOutcome ParseOutcome(string name)
        {
            if (name == EnhanceOutcome.FailDrop.ToString()) return EnhanceOutcome.FailDrop;
            if (name == EnhanceOutcome.FailCharmBlocked.ToString()) return EnhanceOutcome.FailCharmBlocked;
            return EnhanceOutcome.FailNoDrop;
        }

        void Save()
        {
            if (_ctx.Profiles != null) _ctx.Profiles.Save();
        }

        // ------------------------------------------------------------------ 기록 (enhance_log_tail)

        JObject NewLogLine(string kind, WandData w)
        {
            var line = new JObject
            {
                ["kind"] = kind,
                ["wand_id"] = w.Id,
                ["character"] = w.Character,
            };
            if (_ctx.Clock != null) line["at"] = _ctx.Clock.Now.ToString("s", CultureInfo.InvariantCulture);
            return line;
        }

        void Log(ProfileData p, string kind, WandData w, EnhanceResult r, string mutationId = null)
        {
            JObject line = NewLogLine(kind, w);
            if (r != null)
            {
                line["attempt_id"] = r.AttemptId;
                line["outcome"] = r.Outcome.ToString();
                line["stage_before"] = r.StageBefore;
                line["stage_after"] = r.StageAfter;
                line["roll_bp"] = r.RollBp;
                line["success_pct"] = r.Breakdown.SuccessPct;
                line["great_bp"] = r.Breakdown.GreatBp;
                line["mutation_end_bp"] = r.Breakdown.MutationEndBp;
                line["gold"] = r.Breakdown.CostGold;
                line["stone_id"] = r.Breakdown.StoneId;
                line["charm_consumed"] = r.CharmConsumed;
                if (r.MutationCandidate != null) line["mutation"] = r.MutationCandidate;
            }
            if (mutationId != null) line["mutation"] = mutationId;
            AppendLog(p, line);
        }

        void AppendLog(ProfileData p, JObject line)
        {
            int max = Tuning.LogTailMax;
            if (max <= 0) return;
            if (p.EnhanceLogTail == null) p.EnhanceLogTail = new List<JObject>();
            p.EnhanceLogTail.Add(line);
            if (p.EnhanceLogTail.Count > max) p.EnhanceLogTail.RemoveRange(0, p.EnhanceLogTail.Count - max);
        }

        /// <summary>변이 결정의 꾸러미에 그 시도의 뽑힌 값과 확률을 제시에 적어 둔 값으로 되살린다(기록 꼬리가 잘렸어도 남는다).</summary>
        void RestoreFromOffer(MutationOffer offer, WandData w, EnhanceResult r)
        {
            EnhanceConfig cfg = Cfg;
            r.RollBp = offer.RollBp;
            ProbBreakdown b = r.Breakdown;
            b.BasePct = EnhanceRules.BasePct(cfg, w.Stage);
            b.SuccessPct = offer.SuccessPct;
            b.SuccessBp = EnhanceRules.SuccessBp(offer.SuccessPct);
            b.GreatBp = offer.GreatBp;
            b.MutationEndBp = offer.MutationEndBp;
            b.DropPossible = EnhanceRules.DropPossible(cfg, w.Stage, w.FloorPeak);
            b.FloorStage = EnhanceRules.DisplayFloor(cfg, w.FloorPeak);
            b.StoneCount = cfg.StonesPerAttempt;
            r.Breakdown = b;
        }

        /// <summary>변이 결정의 꾸러미에 그 시도의 뽑힌 값과 확률을 되살린다(기록 꼬리에 남아 있을 때만).</summary>
        void RestoreFromLog(ProfileData p, long attemptId, WandData w, EnhanceResult r)
        {
            r.RollBp = -1;
            if (p.EnhanceLogTail == null) return;
            for (int i = p.EnhanceLogTail.Count - 1; i >= 0; i--)
            {
                JObject line = p.EnhanceLogTail[i];
                if (line == null || (string)line["kind"] != LogEnhance || (long?)line["attempt_id"] != attemptId) continue;
                EnhanceConfig cfg = Cfg;
                int pct = (int?)line["success_pct"] ?? 0;
                r.RollBp = (int?)line["roll_bp"] ?? -1;
                r.Breakdown = new ProbBreakdown
                {
                    BasePct = EnhanceRules.BasePct(cfg, w.Stage),
                    SuccessPct = pct,
                    SuccessBp = EnhanceRules.SuccessBp(pct),
                    GreatBp = (int?)line["great_bp"] ?? 0,
                    MutationEndBp = (int?)line["mutation_end_bp"] ?? 0,
                    DropPossible = EnhanceRules.DropPossible(cfg, w.Stage, w.FloorPeak),
                    FloorStage = EnhanceRules.DisplayFloor(cfg, w.FloorPeak),
                    CostGold = (long?)line["gold"] ?? 0,
                    StoneId = (string)line["stone_id"],
                    StoneCount = cfg.StonesPerAttempt,
                };
                return;
            }
        }
    }
}
