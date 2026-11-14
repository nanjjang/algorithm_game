using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>야영 화면의 탭.</summary>
    public enum CampTab { Assembly = 0, Arts = 1, Library = 2 }

    /// <summary>
    /// 야영 화면(ScreenId.Camp)의 모드별 구성(08 요약서 8.2절, 03 요약서 13.1절). 어느 모드에도 강화 탭·버튼은 없다(TV-068).
    /// </summary>
    public sealed class CampModeLayout
    {
        public CampMode Mode;
        public string TitleKey;
        public readonly List<CampTab> Tabs = new List<CampTab>();
        /// <summary>판 중 세계를 쓰고 조작을 RunCommand로 보낸다(Camp·Tutorial). 아니면 IWandService.SetLoadout.</summary>
        public bool UsesWorld;
        public bool ShowHeal, ShowDepart, ShowPractice, ShowNextWave, ShowBagPreview, ShowWandPicker, ShowLibraryFilter, ShowBench;
        /// <summary>Q/E가 캐릭터를 바꾼다(Library). 아니면 Q/E는 탭(Camp).</summary>
        public bool QeSwitchesCharacter;
        /// <summary>닫기 = CampClose 명령(화면을 Pop 하는 것은 Battle). 아니면 이 화면이 Pop 한다.</summary>
        public bool CloseByCommand;

        public static string TabKey(CampTab t)
        {
            switch (t)
            {
                case CampTab.Arts: return "str.camp.tab.arts";
                case CampTab.Library: return "str.camp.tab.library";
                default: return "str.camp.tab.assembly";
            }
        }

        public static CampModeLayout For(CampMode mode)
        {
            var l = new CampModeLayout { Mode = mode };
            switch (mode)
            {
                case CampMode.Camp:
                    l.TitleKey = "str.camp.title";
                    l.Tabs.Add(CampTab.Assembly);
                    l.Tabs.Add(CampTab.Arts);
                    l.UsesWorld = true;
                    l.ShowHeal = l.ShowDepart = l.ShowPractice = l.ShowNextWave = l.ShowBagPreview = l.ShowBench = true;
                    l.CloseByCommand = true;
                    break;
                case CampMode.Library:
                    l.TitleKey = "str.camp.title.library";
                    l.Tabs.Add(CampTab.Assembly);
                    l.Tabs.Add(CampTab.Library);
                    l.ShowPractice = l.ShowWandPicker = l.ShowLibraryFilter = true;
                    l.QeSwitchesCharacter = true;
                    break;
                case CampMode.LoadoutEdit:
                    l.TitleKey = "str.camp.title.loadout_edit";
                    l.Tabs.Add(CampTab.Assembly);
                    break;
                case CampMode.Tutorial:
                    l.TitleKey = "str.camp.title";
                    l.Tabs.Add(CampTab.Assembly);
                    l.UsesWorld = true;
                    break;
            }
            return l;
        }
    }

    /// <summary>체력 회복 버튼의 상태(08 8.2절 표).</summary>
    public struct HealButtonState
    {
        public bool Visible;
        public bool Enabled;
        public int Cost;
        /// <summary>꺼진 이유(지갑 부족 error.not_enough_gold, 체력 가득이면 null — 그냥 꺼짐).</summary>
        public string DisabledReasonKey;
        /// <summary>새벽 야영 문구(str.camp.heal_dawn)를 띄운다.</summary>
        public bool DawnLine;
    }

    /// <summary>다음 웨이브 칸의 종 한 줄: 알아낸 약점·저항만(모르는 칸은 비운다, D-54).</summary>
    public struct SpeciesAffinityRow
    {
        public string SpeciesId;
        /// <summary>알아낸 약점 속성 id(모르면 null).</summary>
        public string KnownWeak;
        /// <summary>알아낸 저항 속성 id(모르면 null).</summary>
        public string KnownResist;
    }

    /// <summary>다음 웨이브 칸(08 8.2절: 시간대, 틀, 변덕 최대 2, 종의 약점·저항 문양, 보스, 확정 재료).</summary>
    public sealed class NextWaveView
    {
        public int Wave;
        public string TodId;
        public string FrameId;
        public readonly List<string> Whims = new List<string>();
        public readonly List<SpeciesAffinityRow> Species = new List<SpeciesAffinityRow>();
        public string BossId;
        /// <summary>확정 재료(아이템 id, 수). 추첨분은 넣지 않는다(06 5.2절 "확정 수량은 야영 화면에 바로 보이고").</summary>
        public readonly List<KeyValuePair<string, int>> GuaranteedMaterials = new List<KeyValuePair<string, int>>();
    }

    /// <summary>야영 화면의 순수 규칙(엔진 없음).</summary>
    public static class CampRules
    {
        /// <summary>변덕은 최대 2개(08 8.2절). 자료구조 크기.</summary>
        public const int MaxWhims = 2;

        /// <summary>회복 버튼: 비용 = world.Mods.Run.HealCost(w)(값의 주인은 Run), 원정 지갑에서만, 체력이 가득이면 꺼짐.</summary>
        public static HealButtonState Heal(SimWorld w, PlayerState p)
        {
            var s = new HealButtonState();
            if (w == null || p == null) return s;
            s.Visible = true;
            s.Cost = w.Mods.Run.HealCost(w);
            s.DawnLine = w.Run.DawnCamp;
            if (p.Hp >= p.MaxHp) { s.Enabled = false; return s; }
            if (w.Run.Purse < s.Cost) { s.Enabled = false; s.DisabledReasonKey = "error.not_enough_gold"; return s; }
            s.Enabled = true;
            return s;
        }

        /// <summary>귀환 가방 미리보기 값(06 5.3절): 정산 서비스가 있으면 그 값(BagLimitsFor), 없으면 drops.json 식.
        /// TV-197: cleared 14 → 4칸 · 영웅 1.</summary>
        public static BagLimits BagPreview(ISettlementService settlement, IContentDb db, int clearedDirect)
        {
            if (settlement != null) return settlement.BagLimitsFor(clearedDirect);
            var d = db != null ? db.Drops : null;
            var b = new BagLimits();
            if (d == null || clearedDirect <= 0) return b;
            int slots = d.BagWavesPerSlot > 0 ? clearedDirect / d.BagWavesPerSlot : 0;
            b.Slots = slots > d.BagSlotCap ? d.BagSlotCap : slots;
            b.HeroCap = d.BagWavesPerHero > 0 ? clearedDirect / d.BagWavesPerHero : 0;
            return b;
        }

        /// <summary>다음 웨이브 칸. 약점·저항 문양은 known(= w.Run.AffinityKnown)에 AffinityKey.Of(종, Weak/Resist)가 있는 것만(D-54).</summary>
        public static NextWaveView NextWave(IContentDb db, WavePlan plan, ICollection<string> known)
        {
            var v = new NextWaveView();
            if (plan == null) return v;
            v.Wave = plan.Wave;
            v.TodId = plan.Tod;
            v.FrameId = plan.Frame;
            for (int i = 0; i < plan.Whims.Count && v.Whims.Count < MaxWhims; i++) v.Whims.Add(plan.Whims[i]);
            v.BossId = plan.BossId;
            for (int i = 0; i < plan.PreviewSpecies.Count; i++)
            {
                string sp = plan.PreviewSpecies[i];
                var def = db != null ? db.Enemy(sp) : null;
                var row = new SpeciesAffinityRow { SpeciesId = sp };
                if (def != null && known != null)
                {
                    if (!string.IsNullOrEmpty(def.Weak) && known.Contains(AffinityKey.Of(sp, Affinity.Weak))) row.KnownWeak = def.Weak;
                    if (!string.IsNullOrEmpty(def.Resist) && known.Contains(AffinityKey.Of(sp, Affinity.Resist))) row.KnownResist = def.Resist;
                }
                v.Species.Add(row);
            }
            GuaranteedMaterials(db, plan.Wave, plan.PhantomFirst, v.GuaranteedMaterials);
            return v;
        }

        /// <summary>
        /// 그 웨이브를 넘기면 확정으로 받는 재료(06 5.2절 표 B·C의 확정분, 첫 만남은 PhantomFirst 줄).
        /// TV-198: 다음 15 → 하급 1 · 중급 1. TV-199: 다음 30 → 중급 1 · 상급 1.
        /// </summary>
        public static void GuaranteedMaterials(IContentDb db, int wave, bool phantomFirst, List<KeyValuePair<string, int>> into)
        {
            into.Clear();
            var mt = db != null && db.Drops != null ? db.Drops.MaterialTable : null;
            if (mt == null || wave <= 0) return;
            int day = DayCycle.Day(wave);
            if (DayCycle.IsBossWave(wave))
            {
                if (phantomFirst) AddFixed(mt.PhantomFirst, into);
                else
                {
                    var tier = MaterialTierDef.ForDay(mt.BossKill, day);
                    if (tier != null) AddFixed(tier.Rows, into);
                }
            }
            if (mt.FifthWaveSlot > 0 && DayCycle.Slot(wave) == mt.FifthWaveSlot)
            {
                var tier = MaterialTierDef.ForDay(mt.FifthWave, day);
                if (tier != null) AddFixed(tier.Rows, into);
            }
        }

        static void AddFixed(List<MaterialRowDef> rows, List<KeyValuePair<string, int>> into)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (!r.IsFixed) continue;
                int at = into.FindIndex(kv => kv.Key == r.Item);
                if (at >= 0) into[at] = new KeyValuePair<string, int>(r.Item, into[at].Value + r.Fixed);
                else into.Add(new KeyValuePair<string, int>(r.Item, r.Fixed));
            }
        }

        /// <summary>야영 머리말의 "웨이브 {wave} 다음": 다음 판이 있으면 그 앞 웨이브, 없으면 마지막으로 넘긴 웨이브.</summary>
        public static int HeaderWave(RunState run)
        {
            if (run == null) return 0;
            if (run.NextPlan != null && run.NextPlan.Wave > 0) return run.NextPlan.Wave - 1;
            return run.LastClearedWave;
        }

        /// <summary>출발 전에 골라야 할 해금 차례가 남았나(04 11.3절 "야영을 떠나기 전 남은 차례를 모두 골라야 한다").</summary>
        public static bool ArtPickPending(RunState run) =>
            run != null && ((run.CurrentArtOffer != null && run.CurrentArtOffer.Candidates.Count > 0) || run.PendingArtTurns.Count > 0);

        /// <summary>임계 사격 가능 횟수 0이면 경고(TV-069). −1 = 무한(열 0).</summary>
        public static bool CritZeroWarning(in SpellStats s) => s.CritShots == 0;
    }

    /// <summary>수치판의 "바뀐 값 0.5초 밝아짐"(TV-070: 발사당 열 8 → 11이면 그 값이 30틱 밝아진다). 화면 60Hz 틱(UiTick)으로 센다.</summary>
    public sealed class StatHighlight
    {
        /// <summary>밝아지는 길이(틱). 0.5초 = 30틱(08 8.2절).</summary>
        public readonly int DurationTicks;
        readonly Dictionary<string, string> _last = new Dictionary<string, string>();
        readonly Dictionary<string, int> _left = new Dictionary<string, int>();
        float _acc;

        public StatHighlight(int durationTicks) { DurationTicks = durationTicks; }

        /// <summary>값을 알린다. 처음 본 값은 밝히지 않는다. 바뀌었으면 true(그때 ui.camp.stat_highlight).</summary>
        public bool Set(string key, string value)
        {
            string old;
            bool had = _last.TryGetValue(key, out old);
            _last[key] = value;
            if (!had || old == value) return false;
            _left[key] = DurationTicks;
            return true;
        }

        public bool IsLit(string key) { int n; return _left.TryGetValue(key, out n) && n > 0; }

        /// <summary>한 화면 틱(1/60초)을 진행한다.</summary>
        public void StepTick()
        {
            var keys = new List<string>(_left.Keys);
            for (int i = 0; i < keys.Count; i++) if (_left[keys[i]] > 0) _left[keys[i]]--;
        }

        /// <summary>실시간 dt를 60Hz 틱으로 바꿔 진행한다.</summary>
        public void Advance(float dt)
        {
            _acc += dt;
            while (_acc >= SimConst.TickSeconds) { _acc -= SimConst.TickSeconds; StepTick(); }
        }

        public void Reset() { _last.Clear(); _left.Clear(); _acc = 0f; }
    }
}
