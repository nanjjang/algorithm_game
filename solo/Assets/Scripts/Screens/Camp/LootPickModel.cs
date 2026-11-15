using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Run;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>전리품 화면의 상태(08 요약서 8.1절 LootPickState).</summary>
    public enum LootPickPhase { Revealing, Choosing, OverflowDiscard, Done }

    /// <summary>카드를 끼웠을 때 미리보기의 꼴.</summary>
    public enum LootPreviewKind
    {
        None,
        /// <summary>빈 칸(또는 빈 코어)에 끼운다.</summary>
        Insert,
        /// <summary>빈 칸이 없다: 가장 좋아지는 칸의 블록 대신 끼운다.</summary>
        Replace,
        /// <summary>지팡이 칸에 든 같은 블록의 별이 오른다.</summary>
        Star,
        /// <summary>끼울 자리가 없다(연결 칸 0 + 코어에 못 끼우는 블록).</summary>
        NoSlot
    }

    /// <summary>카드 한 장의 그릴 값(엔진 없음). 글자는 열쇠로.</summary>
    public sealed class LootCardView
    {
        public int Index;
        public string BlockId;
        public string Rarity;
        public string GradeKey;
        public string KindKey;
        public BlockKind Kind;
        public string Element;
        public bool Guaranteed;
        public bool Picked;
        public bool Borrowed;
        /// <summary>열·피해·간격 줄(해당하는 것만): 열쇠 + 값 글자.</summary>
        public readonly List<KeyValuePair<string, string>> StatLines = new List<KeyValuePair<string, string>>();
        /// <summary>효과 한 줄과 붙는 규칙 한 줄(BlockDescriber).</summary>
        public BlockDescriber.Line Desc, Scope;
        /// <summary>지금 코어가 기본 공격 유형의 핵일 때 이 블록이 그 핵에서 하는 일("이 핵에서는 …", BlockDescriber.OnCore). 없으면 Key = null.</summary>
        public BlockDescriber.Line OnCore;

        // ---- 지금 조립과의 관계(시뮬레이션이 제시할 때 정한 값)
        public LootSynergy Synergy;
        public string SynergyPartnerId;
        public bool SynergyFirst;
        /// <summary>고르면 이 별이 된다(0 = 새 블록).</summary>
        public int StarTarget;
        /// <summary>StarTarget일 때 그 별이 주는 몫.</summary>
        public BlockDescriber.Line StarLine;

        // ---- 끼웠을 때 미리보기(SpellPreview 한 길: 판의 derived, 지금 조립 + 이 카드)
        public LootPreviewKind PreviewKind;
        /// <summary>끼울 자리(0 = 코어, 1..L 연결 칸).</summary>
        public int PreviewSlot = -1;
        /// <summary>Replace일 때 빠지는 블록.</summary>
        public string ReplacedId;
        public int ShotsBefore, ShotsAfter;
        public float HeatBefore, HeatAfter;
        public float DpsBefore, DpsAfter;
        /// <summary>허수아비 DPS 변화(%). 앞 DPS가 0이면 0.</summary>
        public float DpsPct => DpsBefore > 0f ? (DpsAfter / DpsBefore - 1f) * 100f : 0f;
    }

    /// <summary>
    /// 전리품 고르기의 화면 규칙(08 요약서 8.1절, 06 요약서 4.4절 + DataSolo/Loot.json 조정). 상태는 시뮬레이션(w.Run.Loot)이 정본이고, 모델은 그것을 읽어
    /// 그릴 값·버튼 상태·보낼 명령을 정한다. 명령: LootPick/LootUnpick(A = 카드 번호), LootReroll, LootSkip(A = 0 골드 / 1 체력), LootDiscard(A = 100 + i), LootConfirm.
    /// </summary>
    public sealed class LootPickModel
    {
        /// <summary>효과 설명과 미리보기를 읽을 수 있는 카드 크기와 간격.</summary>
        public const float CardWidth = 344f, CardHeight = 544f, CardGap = 24f;

        public readonly List<LootCardView> Cards = new List<LootCardView>();
        public LootCardView BorrowedCard;
        public int PickCount;
        public int Picked;
        public int Purse;
        public int RerollCost;
        /// <summary>이번에 다시 뽑은 뒤의 값(오르는 값을 미리 보여 준다).</summary>
        public int NextRerollCost;
        public int OverflowToDiscard;
        public int StorageCount;
        public int StorageCap;
        public int SkipGold;
        public int SkipHealCells;
        /// <summary>체력 건너뛰기를 받을 수 있나(가득이면 아니다).</summary>
        public bool PlayerHurt;

        /// <summary>4장 1032, 5장 1296(TV-064).</summary>
        public static float RowWidth(int cards) => cards <= 0 ? 0f : cards * CardWidth + (cards - 1) * CardGap;

        public static LootPickModel From(IContentDb db, RunState run) => From(db, run, null, null);

        /// <summary>rules·p가 있으면 카드마다 끼웠을 때 미리보기를 채운다(판 중 화면). 없으면 미리보기 없음(시험·판 밖).</summary>
        public static LootPickModel From(IContentDb db, RunState run, SimModuleSet rules, PlayerState p)
        {
            var m = new LootPickModel();
            if (run == null) return m;
            var drops = db != null ? db.Drops : null;
            var lt = LootTuning.From(db);
            m.StorageCap = drops != null ? drops.StorageCap : 0;
            m.Purse = run.Purse;
            m.StorageCount = run.Storage.Count;
            m.PlayerHurt = p != null && p.Hp < p.MaxHp;
            var offer = run.Loot;
            m.RerollCost = lt.RerollCost(0);
            m.NextRerollCost = lt.RerollCost(1);
            if (offer == null) return m;
            m.RerollCost = offer.RerollCost > 0 ? offer.RerollCost : lt.RerollCost(offer.Rerolls);
            m.NextRerollCost = lt.RerollCost(offer.Rerolls + 1);
            m.PickCount = offer.PickCount;
            m.OverflowToDiscard = offer.OverflowToDiscard;
            m.SkipGold = offer.SkipGold;
            m.SkipHealCells = offer.SkipHealCells;

            var stars = BlockDescriber.Stars(db);
            // 지금 코어가 기본 공격 유형의 핵이면 카드마다 "이 핵에서는 …" 줄(Docs/12-attack-styles.md 2.2절)
            BlockDef coreDef = null;
            if (db != null && p != null && p.Loadout != null && !p.Loadout.Core.IsEmpty) coreDef = db.Block(p.Loadout.Core.BlockId);
            SpellPreviewResult baseR = rules != null && p != null && p.Loadout != null ? SpellPreview.ForPlayer(rules, db, p) : null;
            for (int i = 0; i < offer.Cards.Count; i++)
            {
                var c = offer.Cards[i];
                var v = Card(db, c.BlockId, c.Rarity, false);
                v.Index = i;
                v.Guaranteed = c.Guaranteed;
                v.Picked = c.Picked;
                v.Synergy = c.Synergy;
                v.SynergyPartnerId = c.SynergyPartnerId;
                v.SynergyFirst = c.SynergyFirst;
                v.StarTarget = c.StarTarget;
                if (c.StarTarget > 0) v.StarLine = BlockDescriber.StarLine(db != null ? db.Block(c.BlockId) : null, c.StarTarget, stars);
                if (coreDef != null && coreDef.Style != null) v.OnCore = BlockDescriber.OnCore(db != null ? db.Block(c.BlockId) : null, coreDef);
                if (baseR != null) Preview(v, rules, db, p, run, lt, baseR);
                if (c.Picked) m.Picked++;
                m.Cards.Add(v);
            }
            if (offer.BorrowedBlockId != null)
            {
                m.BorrowedCard = Card(db, offer.BorrowedBlockId, drops != null ? drops.BorrowedBlockRarity : Ids.Rarity.Hero, true);
                m.BorrowedCard.Index = -1;
                if (coreDef != null && coreDef.Style != null) m.BorrowedCard.OnCore = BlockDescriber.OnCore(db.Block(offer.BorrowedBlockId), coreDef);
                if (baseR != null) Preview(m.BorrowedCard, rules, db, p, run, lt, baseR);
            }
            return m;
        }

        public static LootCardView Card(IContentDb db, string blockId, string rarity, bool borrowed)
        {
            var v = new LootCardView { BlockId = blockId, Rarity = rarity, Borrowed = borrowed };
            v.GradeKey = "str.block.grade." + (rarity ?? Ids.Rarity.Common);
            BlockDef def = db != null && blockId != null ? db.Block(blockId) : null;
            if (def != null)
            {
                v.Kind = def.KindEnum;
                v.KindKey = "str.block.kind." + def.Kind;
                v.Element = def.Element;
                // 해당하는 것만(08 8.1절): 열은 0이 아닐 때, 피해·간격은 투사체만.
                if (def.Heat != 0f) v.StatLines.Add(new KeyValuePair<string, string>("str.block.heat", CampFormat.OneDecimal(def.Heat)));
                if (def.IsProjectile && def.BaseDamage > 0f) v.StatLines.Add(new KeyValuePair<string, string>("str.block.damage", CampFormat.OneDecimal(def.BaseDamage)));
                if (def.IsProjectile && def.IntervalSeconds > 0f) v.StatLines.Add(new KeyValuePair<string, string>("str.block.interval", CampFormat.ShortSeconds(def.IntervalSeconds)));
                v.Desc = BlockDescriber.Describe(def);
                v.Scope = BlockDescriber.ScopeRule(def);
            }
            return v;
        }

        /// <summary>
        /// 끼웠을 때 미리보기: 같은 블록이 지팡이 칸에 있고 별이 오르면 그 칸 별 +1, 아니면 빈 코어(투사체) → 첫 빈 연결 칸,
        /// 빈 칸이 없으면 허수아비 DPS가 가장 오르는 칸(같으면 앞 칸)의 블록 대신. 보관함의 같은 블록이 자랄 때는 그 별로 끼운다고 본다.
        /// </summary>
        static void Preview(LootCardView v, SimModuleSet rules, IContentDb db, PlayerState p, RunState run, LootTuning lt, SpellPreviewResult baseR)
        {
            BlockDef def = db != null && v.BlockId != null ? db.Block(v.BlockId) : null;
            var lo = p.Loadout;
            if (def == null || lo == null) return;
            int n = lo.Links.Length;
            string core = lo.Core.IsEmpty ? null : lo.Core.BlockId;
            var links = lo.LinkIds();
            var stars = lo.SlotStars();
            v.ShotsBefore = baseR.Stats.ProjectilesPerShot;
            v.HeatBefore = baseR.Stats.HeatPerShot;
            v.DpsBefore = baseR.Stats.DummyDps;

            int newStars = 0;
            if (!v.Borrowed && v.StarTarget > 0)
            {
                int cur;
                int r = LootRules.StarTargetRef(run, lo, v.BlockId, lt.StarMax, out cur);
                if (r >= 0 && r <= n)
                {
                    var st = (int[])stars.Clone();
                    st[r] = cur + 1;
                    Apply(v, LootPreviewKind.Star, r, null, Eval(rules, db, p, core, links, st));
                    return;
                }
                newStars = v.StarTarget;
            }

            if (core == null && def.IsProjectile)
            {
                Apply(v, LootPreviewKind.Insert, 0, null, With(rules, db, p, core, links, stars, 0, v.BlockId, newStars));
                return;
            }
            for (int i = 0; i < n; i++)
                if (string.IsNullOrEmpty(links[i]))
                {
                    Apply(v, LootPreviewKind.Insert, i + 1, null, With(rules, db, p, core, links, stars, i + 1, v.BlockId, newStars));
                    return;
                }

            int best = -1;
            SpellPreviewResult bestR = null;
            for (int r = def.IsProjectile ? 0 : 1; r <= n; r++)
            {
                string old = r == 0 ? core : links[r - 1];
                if (old == v.BlockId) continue;
                var res = With(rules, db, p, core, links, stars, r, v.BlockId, newStars);
                if (bestR == null || res.Stats.DummyDps > bestR.Stats.DummyDps) { best = r; bestR = res; }
            }
            if (bestR == null) { v.PreviewKind = LootPreviewKind.NoSlot; return; }
            Apply(v, LootPreviewKind.Replace, best, best == 0 ? core : links[best - 1], bestR);
        }

        static SpellPreviewResult With(SimModuleSet rules, IContentDb db, PlayerState p, string core, string[] links, int[] stars, int slot, string id, int st)
        {
            var l2 = (string[])links.Clone();
            var s2 = (int[])stars.Clone();
            string c2 = core;
            if (slot == 0) c2 = id; else l2[slot - 1] = id;
            s2[slot] = st;
            return Eval(rules, db, p, c2, l2, s2);
        }

        static SpellPreviewResult Eval(SimModuleSet rules, IContentDb db, PlayerState p, string core, string[] links, int[] stars) =>
            SpellPreview.Run(rules, db, p.Def, p.Derived, links.Length, core, links, stars);

        static void Apply(LootCardView v, LootPreviewKind kind, int slot, string replaced, SpellPreviewResult r)
        {
            v.PreviewKind = kind;
            v.PreviewSlot = slot;
            v.ReplacedId = replaced;
            v.ShotsAfter = r.Stats.ProjectilesPerShot;
            v.HeatAfter = r.Stats.HeatPerShot;
            v.DpsAfter = r.Stats.DummyDps;
        }

        public LootPickPhase Phase => OverflowToDiscard > 0 ? LootPickPhase.OverflowDiscard : LootPickPhase.Choosing;

        /// <summary>[확인] 켜짐 ⇔ 고른 수 == 고를 수(TV-060).</summary>
        public bool ConfirmEnabled => Phase == LootPickPhase.Choosing && PickCount > 0 && Picked == PickCount;

        /// <summary>[다시 뽑기]: 지갑 ≥ 지금 값(TV-061 지갑 29 → 꺼짐, TV-192). 넘침 처리 중에는 꺼짐.</summary>
        public bool RerollEnabled => Phase == LootPickPhase.Choosing && Purse >= RerollCost;

        /// <summary>꺼진 이유 열쇠(지갑 부족이면 error.not_enough_gold).</summary>
        public string RerollDisabledReason => Purse < RerollCost ? "error.not_enough_gold" : null;

        public bool SkipGoldEnabled => Phase == LootPickPhase.Choosing && SkipGold > 0;
        public bool SkipHealEnabled => Phase == LootPickPhase.Choosing && SkipHealCells > 0 && PlayerHurt;

        /// <summary>건너뛰기 명령(Slot = 플레이어, A = 0 골드 / 1 체력).</summary>
        public static RunCommand SkipCommand(bool heal, int playerSlot) =>
            new RunCommand(RunCommandKind.LootSkip, heal ? RunFlow.SkipModeHeal : RunFlow.SkipModeGold, slot: playerSlot);

        /// <summary>
        /// 카드를 눌렀을 때 보낼 명령. 고른 카드 → 빼기, 안 고른 카드 → 고를 수가 남았으면 넣기, 아니면 무시(TV-060 3번째 무시).
        /// 보낼 것이 없으면 false.
        /// </summary>
        public bool ToggleCommand(int index, out RunCommand cmd)
        {
            cmd = default;
            if (Phase != LootPickPhase.Choosing || index < 0 || index >= Cards.Count) return false;
            if (Cards[index].Picked) { cmd = new RunCommand(RunCommandKind.LootUnpick, index); return true; }
            if (Picked >= PickCount) return false;
            cmd = new RunCommand(RunCommandKind.LootPick, index);
            return true;
        }

        /// <summary>확인 뒤 넘침으로 버려야 할 수(TV-063: 보관함 11 + 2장 = 13 → 1). 빌린 블록도 보관함에 들어간다. 별로 겹치는 카드는 칸을 쓰지 않는다.</summary>
        public static int OverflowAfterConfirm(int storageCount, int picks, bool borrowed, int cap)
        {
            int over = storageCount + picks + (borrowed ? 1 : 0) - cap;
            return over > 0 ? over : 0;
        }

        /// <summary>넘침 처리에서 보관함 i번째를 버릴 때 받는 골드(06 4.4절: 일반 5, 희귀 15, 영웅 50, 빌림 50).</summary>
        public static int DiscardGold(IContentDb db, WildBlock wb)
        {
            if (db == null || db.Drops == null || wb == null) return 0;
            return db.Drops.DiscardGold(wb.Rarity, wb.Borrowed);
        }

        /// <summary>넘침 처리의 버리기 명령(A = 보관함 자리 SlotRef 100 + i). 서고 블록 대기 칸은 대상이 아니다.</summary>
        public static RunCommand DiscardCommand(int storageIndex) => new RunCommand(RunCommandKind.LootDiscard, SlotRefs.Storage(storageIndex));
    }
}
