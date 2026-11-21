using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Prep
{
    /// <summary>출발 검사 결과(08 요약서 5.4절 CanDepart).</summary>
    public struct PrepDepartCheck
    {
        public bool Ok;
        /// <summary>꺼짐 이유 열쇠(Ok면 null).</summary>
        public string ReasonKey;
        /// <summary>코어가 비어 막혔다 → 코어 칸 경고 딱지 + [조립 바꾸기]로 커서(TV-031·032).</summary>
        public bool CoreEmpty;
    }

    /// <summary>출발을 눌렀을 때 일어날 일(화면이 그대로 따른다).</summary>
    public sealed class PrepDepartResult
    {
        public bool Ok;
        /// <summary>실패면 그 문구 열쇠(꺼짐 이유 또는 error.*).</summary>
        public string ErrorKey;
        public ErrorCode Error;
        public RunConfig Config;
        /// <summary>Replace할 화면: 첫 판 전이면 Tutorial, 아니면 Loading.</summary>
        public ScreenId Next;
        public ScreenArgs NextArgs;
    }

    /// <summary>기본 공격 카드 한 장(원정 준비 화면, Docs/12-attack-styles.md 1절).</summary>
    public struct AttackStyleCard
    {
        /// <summary>핵 블록 id = 이름 표 열쇠.</summary>
        public string Id;
        public string DescKey;
        /// <summary>특징 꼬리표 열쇠(str.attack_style.tag.*).</summary>
        public List<string> TagKeys;
        /// <summary>자리 이름 열쇠(str.attack_style.slot.a/b/c).</summary>
        public string SlotKey;
        public string Element;
        public bool Unlocked;
        /// <summary>잠겨 있으면 여는 최고 웨이브(해금 손잡이가 꺼져 있으면 0).</summary>
        public int UnlockWave;
        public bool Selected;
    }

    /// <summary>
    /// 원정 준비 화면의 규칙과 상태(엔진 없음). 08 요약서 5절, 01 요약서 2.3·4.6절, 00 문서 8.1절 표.
    /// 서비스(ctx.Wands, ctx.Settlement, ctx.Profiles, ctx.Rules)가 null이어도 빈 상태로 돌고 예외를 던지지 않는다.
    /// 봇 결투 입장(DuelEntry, 08 5.1절)은 솔로에서 넣지 않는다(00 문서 D-51).
    /// </summary>
    public sealed class ExpeditionPrepModel
    {
        readonly GameContext _ctx;
        readonly List<WandListEntry> _rows = new List<WandListEntry>();
        readonly List<int> _skips = new List<int>();

        public ExpeditionPrepModel(GameContext ctx)
        {
            _ctx = ctx;
            if (ctx != null && ctx.Settings != null && ctx.Settings.Current != null)
                Sort = SettingsOptions.WandListSort(ctx.Settings.Current);
        }

        /// <summary>고른 캐릭터(1~5).</summary>
        public int Character { get; private set; } = 1;
        public string CharId => Ids.Char.Of(Character);
        /// <summary>고른 지팡이 id(0 = 없음).</summary>
        public long WandId { get; private set; }
        /// <summary>정렬(기본 단계 높은 순). 기기 설정 wand_list.sort에 기억한다(08 5.3절).</summary>
        public WandSort Sort { get; private set; } = WandSort.StageDesc;
        public IReadOnlyList<WandListEntry> Rows => _rows;
        /// <summary>고를 수 있는 건너뛰는 수(첫 칸은 0). ctx.Settlement.StartSkipOptions의 값.</summary>
        public IReadOnlyList<int> SkipOptions => _skips;
        public int SkipIndex { get; private set; }
        public int Skip => _skips.Count > 0 && SkipIndex >= 0 && SkipIndex < _skips.Count ? _skips[SkipIndex] : 0;
        /// <summary>첫 웨이브 = skip + 1(01 4.6절).</summary>
        public int StartWave => Skip + 1;
        /// <summary>선택지가 0 하나뿐이면 시작 웨이브 줄 자체가 없다(08 5.5절 ShowStartWaveRow).</summary>
        public bool ShowStartWaveRow => _skips.Count > 1;

        ProfileData Profile => _ctx != null ? _ctx.Profile : null;
        IContentDb Db => _ctx != null ? _ctx.Content : null;

        // ------------------------------------------------------------------ 기본 공격 유형 (Docs/12-attack-styles.md 1절)

        public const string StyleDescPrefix = "str.attack_style.";
        public const string StyleDescSuffix = ".desc";
        public const string StyleTagPrefix = "str.attack_style.tag.";
        public const string StyleSlotPrefix = "str.attack_style.slot.";
        const string CorePrefix = "block.core.";

        /// <summary>고른 기본 공격(핵 블록 id). 유형 표가 없으면 null(출발 조립 그대로).</summary>
        public string SelectedStyle { get; private set; }

        /// <summary>그 캐릭터의 기본 공격 카드 3장(A, B, C 순).</summary>
        public List<AttackStyleCard> StyleCards()
        {
            var cards = new List<AttackStyleCard>(3);
            var ids = AttackStyles.StylesOf(Db, CharId);
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                var b = Db != null ? Db.Block(id) : null;
                var tags = new List<string>();
                if (b != null && b.Style != null && b.Style.Tags != null)
                    for (int k = 0; k < b.Style.Tags.Count; k++) tags.Add(StyleTagPrefix + b.Style.Tags[k]);
                string slot = AttackStyles.SlotOf(Db, CharId, id);
                cards.Add(new AttackStyleCard
                {
                    Id = id,
                    DescKey = StyleDescKey(id),
                    TagKeys = tags,
                    SlotKey = slot != null ? StyleSlotPrefix + slot : null,
                    Element = b != null ? b.Element : null,
                    Unlocked = AttackStyles.IsUnlocked(Db, CharId, id, BestWave),
                    UnlockWave = AttackStyles.UnlockWave(Db, CharId, id),
                    Selected = id == SelectedStyle
                });
            }
            return cards;
        }

        /// <summary>
        /// 출발 조립 판에 보일 코어. 판 시작 규칙(RunFlow.StartRun → AttackStyles.ApplyToLoadout)과 같다:
        /// 저장된 코어가 replace_cores(다섯 캐릭터 공통 시작 블록 마력탄)이면 고른 유형의 핵, 일부러 끼운 다른 투사체면 그대로.
        /// </summary>
        public string EffectiveCore(string savedCore)
        {
            if (string.IsNullOrEmpty(savedCore) || SelectedStyle == null) return savedCore;
            var applied = AttackStyles.ApplyToLoadout(Db, new LoadoutSpec { Core = savedCore }, SelectedStyle);
            return applied != null ? applied.Core : savedCore;
        }

        /// <summary>"block.core.ember_mortar" → "str.attack_style.ember_mortar.desc".</summary>
        public static string StyleDescKey(string styleId)
        {
            if (string.IsNullOrEmpty(styleId)) return null;
            string key = styleId.StartsWith(CorePrefix, System.StringComparison.Ordinal) ? styleId.Substring(CorePrefix.Length) : styleId;
            return StyleDescPrefix + key + StyleDescSuffix;
        }

        /// <summary>
        /// 기본 공격을 고른다(그 캐릭터의 유형이고 열려 있을 때만). 캐릭터 기록의 attack_style에 적고 저장한다 — 메인 메뉴의 연습과
        /// 튜토리얼 판 설정도 이 값을 쓴다(WandService.BaseConfig). 바뀌었으면 true.
        /// </summary>
        public bool SelectStyle(string styleId)
        {
            if (!AttackStyles.IsUnlocked(Db, CharId, styleId, BestWave)) return false;
            bool changed = SelectedStyle != styleId;
            SelectedStyle = styleId;
            SaveStyle();
            return changed;
        }

        /// <summary>캐릭터 기록의 선택(없거나 잠겼거나 다른 캐릭터의 값이면 A).</summary>
        void ReloadStyle()
        {
            var p = Profile;
            var rec = p != null ? p.Character(Character) : null;
            string wanted = AttackStyles.Resolve(Db, CharId, rec != null ? rec.AttackStyle : null);
            if (wanted != null && !AttackStyles.IsUnlocked(Db, CharId, wanted, BestWave)) wanted = AttackStyles.DefaultOf(Db, CharId);
            SelectedStyle = wanted;
        }

        void SaveStyle()
        {
            var p = Profile;
            var rec = p != null ? p.Character(Character) : null;
            if (rec == null || rec.AttackStyle == SelectedStyle) return;
            rec.AttackStyle = SelectedStyle;
            _ctx.Profiles?.Save();
        }

        public WandListEntry SelectedEntry
        {
            get
            {
                for (int i = 0; i < _rows.Count; i++) if (_rows[i].Wand.Id == WandId) return _rows[i];
                return null;
            }
        }

        public WandListEntry HeldEntry
        {
            get
            {
                for (int i = 0; i < _rows.Count; i++) if (_rows[i].Equipped) return _rows[i];
                return null;
            }
        }

        /// <summary>고른 지팡이가 든 지팡이가 아니다 → [든 지팡이로]가 켜진다.</summary>
        public bool CanSetHeld
        {
            get { var e = SelectedEntry; return e != null && !e.Equipped && _ctx != null && _ctx.Wands != null; }
        }

        /// <summary>그 캐릭터로 넘긴 최고 웨이브(프로필).</summary>
        public int BestWave
        {
            get
            {
                var p = Profile;
                var rec = p != null ? p.Character(Character) : null;
                return rec != null ? rec.BestWave : 0;
            }
        }

        // ------------------------------------------------------------------ 들어오기·캐릭터

        /// <summary>들어올 때: 처음 고른 캐릭터 = profile.last_character(1~5 밖이면 1), 그 캐릭터의 든 지팡이를 고른다.</summary>
        public void Enter()
        {
            var p = Profile;
            int c = p != null ? p.LastCharacter : 1;
            Character = ValidCharacter(c) ? c : 1;
            WandId = 0;
            Reload();
            ReloadStyle();
        }

        public static bool ValidCharacter(int c) => c >= 1 && c <= Ids.Char.Count;

        /// <summary>
        /// 캐릭터를 고른다(5명 전부 처음부터). profile.last_character에 적고 저장한다 — 메인 메뉴의 연습·조립과 서고·강화가
        /// 이 캐릭터로 열린다(00 문서 D-56). 지팡이는 그 캐릭터의 든 지팡이로 다시 고른다. 1~5 밖이면 false.
        /// </summary>
        public bool SelectCharacter(int c)
        {
            if (!ValidCharacter(c)) return false;
            Character = c;
            var p = Profile;
            if (p != null && p.LastCharacter != c)
            {
                p.LastCharacter = c;
                _ctx.Profiles?.Save();
            }
            WandId = 0;
            Reload();
            ReloadStyle();
            return true;
        }

        // ------------------------------------------------------------------ 지팡이 목록

        /// <summary>목록·선택지를 다시 읽는다(조립 바꾸기에서 돌아올 때도). 고른 지팡이가 남아 있으면 그대로, 없으면 든 지팡이 → 첫 줄.</summary>
        public void Reload()
        {
            _rows.Clear();
            var wands = _ctx != null ? _ctx.Wands : null;
            List<WandListEntry> raw = null;
            if (wands != null) raw = wands.List(Character, Sort);
            if (raw != null)
            {
                // 지팡이는 그 캐릭터만 쓴다(불변식): 다른 캐릭터의 줄이 섞여 와도 버린다.
                var mine = new List<WandListEntry>(raw.Count);
                for (int i = 0; i < raw.Count; i++)
                    if (raw[i] != null && raw[i].Wand != null && raw[i].Wand.Character == Character) mine.Add(raw[i]);
                _rows.AddRange(WandListModel.SortRows(mine, Sort));
            }
            if (SelectedEntry == null)
            {
                var held = HeldEntry;
                WandId = held != null ? held.Wand.Id : _rows.Count > 0 ? _rows[0].Wand.Id : 0;
            }
            ReloadSkips();
        }

        public bool SelectWand(long wandId)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Wand.Id != wandId) continue;
                WandId = wandId;
                return true;
            }
            return false;
        }

        public void SetSort(WandSort sort)
        {
            if (Sort == sort) return;
            Sort = sort;
            if (_ctx != null && _ctx.Settings != null && _ctx.Settings.Current != null)
            {
                SettingsOptions.SetWandListSort(_ctx.Settings.Current, sort);
                _ctx.Settings.Save();
            }
            Reload();
        }

        /// <summary>정렬을 차례로 바꾼다(단계 높은 순 → 단계 낮은 순 → 최근 얻은 순).</summary>
        public WandSort NextSort()
        {
            WandSort next = Sort == WandSort.StageDesc ? WandSort.StageAsc : Sort == WandSort.StageAsc ? WandSort.Recent : WandSort.StageDesc;
            SetSort(next);
            return next;
        }

        /// <summary>고른 지팡이를 든 지팡이로(ctx.Wands.Equip). 실패하면 오류 코드.</summary>
        public ErrorCode SetHeld()
        {
            var e = SelectedEntry;
            var wands = _ctx != null ? _ctx.Wands : null;
            if (e == null || wands == null) return ErrorCode.NotFound;
            if (e.Equipped) return ErrorCode.None;
            var r = wands.Equip(Character, e.Wand.Id);
            if (!r.Ok) return r.Error;
            long keep = e.Wand.Id;
            Reload();
            SelectWand(keep);
            return ErrorCode.None;
        }

        // ------------------------------------------------------------------ 시작 웨이브

        void ReloadSkips()
        {
            int keep = Skip;
            _skips.Clear();
            var st = _ctx != null ? _ctx.Settlement : null;
            var opts = st != null ? st.StartSkipOptions(BestWave) : null;
            if (opts != null)
                for (int i = 0; i < opts.Count; i++)
                    if (opts[i] >= 0 && !_skips.Contains(opts[i])) _skips.Add(opts[i]);
            if (!_skips.Contains(0)) _skips.Insert(0, 0);
            int idx = _skips.IndexOf(keep);
            SkipIndex = idx >= 0 ? idx : 0;
        }

        /// <summary>건너뛰는 수를 고른다. 선택지에 없으면 거절(false, 01 4.6절 "skip 15 → 거절").</summary>
        public bool SelectSkip(int skip)
        {
            int idx = _skips.IndexOf(skip);
            if (idx < 0) return false;
            SkipIndex = idx;
            return true;
        }

        /// <summary>◀ ▶ 한 칸. 끝에서 멈춘다. 바뀌었으면 true.</summary>
        public bool StepSkip(int dir)
        {
            int idx = SkipIndex + (dir < 0 ? -1 : 1);
            if (idx < 0 || idx >= _skips.Count) return false;
            SkipIndex = idx;
            return true;
        }

        /// <summary>건너뛴 웨이브 1..skip의 점수 합(01 6.3절 SkipScore, 계산은 ctx.Rules.Score). 0이면 0.</summary>
        public long SkippedScore(int skip)
        {
            var rules = _ctx != null ? _ctx.Rules : null;
            if (skip <= 0 || rules == null || rules.Score == null) return 0;
            long sum = 0;
            for (int w = 1; w <= skip; w++) sum += rules.Score.SkipScore(_ctx.Content, w);
            return sum;
        }

        // ------------------------------------------------------------------ 복수 추적기

        /// <summary>복수 추적기 값(Core의 RevengeTracker 하나로만 계산).</summary>
        public RevengeTrackerView Tracker => RevengeTracker.Build(Profile, _ctx != null ? _ctx.Content : null);

        /// <summary>한 줄 꼴(revenge.tracker.short)을 그리는가: 복수 중일 때만(첫 만남 전에는 어디에도 없다).</summary>
        public bool ShowTrackerLine
        {
            get { var v = Tracker; return v.Visible && v.State == RevengeState.Pursuing; }
        }

        // ------------------------------------------------------------------ 출발

        /// <summary>08 5.4절: 코어가 비면 막힘 → 원정 중 잠금이면 막힘 → 된다.</summary>
        public PrepDepartCheck CanDepart()
        {
            var e = SelectedEntry;
            if (e == null) return new PrepDepartCheck { ReasonKey = PrepKeys.ErrorNotFound };
            bool coreEmpty = e.CoreEmpty || e.Wand.Loadout == null || string.IsNullOrEmpty(e.Wand.Loadout.Core);
            if (coreEmpty) return new PrepDepartCheck { ReasonKey = PrepKeys.BlockedCoreEmpty, CoreEmpty = true };
            if (e.Locked) return new PrepDepartCheck { ReasonKey = PrepKeys.BlockedOnExpedition };
            return new PrepDepartCheck { Ok = true };
        }

        /// <summary>[조립 바꾸기]가 열 화면의 인자: Camp(mode = LoadoutEdit, 그 지팡이 — long). 지팡이가 없으면 null.</summary>
        public ScreenArgs ChangeLoadoutArgs()
        {
            if (SelectedEntry == null) return null;
            return ScreenArgs.From(ScreenId.ExpeditionPrep)
                .With(ScreenArgKeys.CampMode, CampMode.LoadoutEdit)
                .With(ScreenArgKeys.WandId, WandId);
        }

        /// <summary>
        /// 출발 J. 검사를 통과하면 ctx.Wands.BuildRunConfig(int 캐릭터, 지팡이, skip + 1, seed). 실패면 오류 코드의 문구.
        /// 성공이면 첫 판 전(flags.tutorial_dummy_done == false)이면 Tutorial(그 판을 RunConfig로 넘긴다), 아니면 Loading → Battle.
        /// </summary>
        public PrepDepartResult Depart(ulong seed)
        {
            var check = CanDepart();
            if (!check.Ok) return new PrepDepartResult { ErrorKey = check.ReasonKey, Error = check.CoreEmpty ? ErrorCode.CoreEmpty : ErrorCode.WandLocked };
            var wands = _ctx != null ? _ctx.Wands : null;
            if (wands == null) return new PrepDepartResult { ErrorKey = PrepKeys.ErrorNotFound, Error = ErrorCode.NotFound };
            var r = wands.BuildRunConfig(Character, WandId, StartWave, seed);
            if (!r.Ok || r.Value == null)
            {
                var code = r.Ok ? ErrorCode.NotFound : r.Error;
                return new PrepDepartResult { Error = code, ErrorKey = PrepErrorKeys.Of(code) };
            }
            var cfg = r.Value;
            // 고른 기본 공격(Docs/12 1절). 판 시작 때 Run이 출발 조립의 코어(마력탄)를 이 핵으로 바꾼다. 기록에도 남긴다(연습·튜토리얼이 읽는다).
            if (SelectedStyle != null)
            {
                cfg.AttackStyle = SelectedStyle;
                SaveStyle();
            }
            var p = Profile;
            bool tutorialDone = p != null && p.Flags != null && p.Flags.TutorialDummyDone;
            var runArgs = ScreenArgs.From(ScreenId.ExpeditionPrep).With(ScreenArgKeys.RunConfig, cfg);
            if (!tutorialDone)
                return new PrepDepartResult { Ok = true, Config = cfg, Next = ScreenId.Tutorial, NextArgs = runArgs };
            return new PrepDepartResult
            {
                Ok = true,
                Config = cfg,
                Next = ScreenId.Loading,
                NextArgs = ScreenArgs.ViaLoading(ScreenId.ExpeditionPrep, ScreenId.Battle, runArgs),
            };
        }

        // ------------------------------------------------------------------ 표시 도우미(순수)

        /// <summary>단계 묶음 번호(b0~b4) = 그 단계 이하의 이정표 수(enhance.json milestones 4·7·10·13 → +0~3, +4~6, +7~9, +10~12, +13).</summary>
        public static int StageBand(IContentDb db, int stage)
        {
            var ms = db != null && db.Enhance != null ? db.Enhance.Milestones : null;
            int band = 0;
            if (ms == null) return band;
            for (int i = 0; i < ms.Count; i++) if (ms[i] <= stage) band++;
            return band;
        }

        /// <summary>칭호 열쇠 "title.stage.N.bK"(07 요약서 칭호 표).</summary>
        public static string StageTitleKey(IContentDb db, int character, int stage) =>
            "title.stage." + character + ".b" + StageBand(db, stage);

        /// <summary>단계 별명 열쇠 "wand.N.stage.K.nickname".</summary>
        public static string NicknameKey(int character, int stage) => Ids.Char.WandKindOf(character) + ".stage." + stage + ".nickname";
    }
}
