using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Enhance;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.Data;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensEnhance
{
    /// <summary>저장 횟수만 세는 프로필 저장소.</summary>
    public sealed class G23ProfileStore : IProfileStore
    {
        public int SaveCount;
        public ProfileData Current { get; set; }
        public string Directory => null;
        public event Action Saved;
        public void LoadOrCreate(out bool created, out bool restored) { created = false; restored = false; }
        public void Save() { SaveCount++; Saved?.Invoke(); }
        public bool RestorePrevious() => false;
    }

    public sealed class G23SettingsStore : ISettingsStore
    {
        public SettingsData Current { get; set; } = new SettingsData();
        public bool ExistedAtLoad => true;
        public int SaveCount;
        public event Action Changed;
        public void Load() { }
        public void Save() { SaveCount++; Changed?.Invoke(); }
    }

    /// <summary>불린 소리 사건을 남긴다.</summary>
    public sealed class G23Sound : ISoundPlayer
    {
        public readonly List<string> Played = new List<string>();
        public readonly List<SoundBus> Buses = new List<SoundBus>();
        public void Play(string eventId, SoundBus bus = SoundBus.Sfx, float volume = 1f, float pitch = 1f) { Played.Add(eventId); Buses.Add(bus); }
        public void SetMusic(string trackId) { }
        public void SetVolume(SoundBus bus, float volume01) { }
    }

    /// <summary>호출을 남기는 화면 관리자.</summary>
    public sealed class G23Screens : IScreenManager
    {
        public readonly List<string> Calls = new List<string>();
        public readonly List<ScreenArgs> PushedArgs = new List<ScreenArgs>();
        public readonly HashSet<ScreenId> Open = new HashSet<ScreenId>();
        public ScreenId LastPopTo = ScreenId.None;
        public readonly List<string> Toasts = new List<string>();
        public void Register(ScreenId id, Func<IScreen> factory) { Calls.Add("register " + id); }
        public void Push(ScreenId id, ScreenArgs args = null) { Calls.Add("push " + id); PushedArgs.Add(args); Open.Add(id); }
        public void Replace(ScreenId id, ScreenArgs args = null) { Calls.Add("replace " + id); }
        public void Pop(object result = null) { Calls.Add("pop"); }
        public void PopTo(ScreenId id, ScreenArgs args = null) { Calls.Add("popto " + id); LastPopTo = id; }
        public ScreenId Top => ScreenId.None;
        public bool IsOpen(ScreenId id) => Open.Contains(id);
        public void Close(IScreen screen, object result = null) { Calls.Add("close " + screen.Id); }
        public void Confirm(string titleKey, string bodyKey, bool caution, Action<bool> onResult) { Calls.Add("confirm"); }
        public void ConfirmText(string titleKey, string bodyText, bool caution, Action<bool> onResult) { Calls.Add("confirm_text"); }
        public void Toast(string text, float seconds = 3f) { Toasts.Add(text); }
        public void ShowTooltip(string text, RectTransform anchor) { }
        public void HideTooltip(RectTransform anchor = null) { }
        public bool CaptureKeys { get; set; }
        public int Count(string call) { int n = 0; foreach (var c in Calls) if (c == call) n++; return n; }
    }

    /// <summary>열쇠 → 글 사전(이름 표가 "값 없음"을 흉내 낼 때).</summary>
    public sealed class G23Names : INameTable
    {
        public readonly Dictionary<string, string> Map = new Dictionary<string, string>();
        readonly INameTable _base;
        public G23Names(INameTable baseTable) { _base = baseTable; }
        public G23Names Set(string k, string v) { Map[k] = v; return this; }
        public string Get(string key) => Map.TryGetValue(key, out var v) ? v : _base != null ? _base.Get(key) : key;
        public bool Has(string key) => Map.ContainsKey(key) || (_base != null && _base.Has(key));
        public string Format(string key, params object[] pairs)
        {
            if (!Map.ContainsKey(key) && _base != null) return _base.Format(key, pairs);
            var s = new StringBuilder(Get(key));
            for (int i = 0; i + 1 < pairs.Length; i += 2) s.Replace("{" + pairs[i] + "}", Convert.ToString(pairs[i + 1]));
            return s.ToString();
        }
        public string Number(long value) => _base != null ? _base.Number(value) : value.ToString();
    }

    /// <summary>그림 창고: 그림은 하나도 없고(null), Release 호출만 센다.</summary>
    public sealed class G23Art : IArtLibrary
    {
        public readonly List<string> Released = new List<string>();
        public SpriteAnim CharacterClip(string charId, string clip, Facing3 facing) => null;
        public PlayerClip CharacterClipInfo(string charId, string clip, Facing3 facing) => null;
        public Sprite CharacterPortrait(string charId, int expression) => null;
        public Sprite CharacterSelection(string charId) => null;
        public Sprite CharacterSilhouette(string charId) => null;
        public Sprite WandHand(string charId, int stage, Facing3 facing, bool aim) => null;
        public Sprite WandInspection(string charId, int stage) => null;
        public Sprite WandIcon(string charId, int stage) => null;
        public WandStage WandStageInfo(string charId, int stage) => null;
        public PixelPoint WandGrip => default;
        public SpriteAnim WandDetachedPart(string charId) => null;
        public SpriteAnim MonsterClip(string monsterId, string clip) => null;
        public IReadOnlyList<string> MonsterClipNames(string monsterId) => new List<string>();
        public Sprite BossPortrait(string bossId) => null;
        public Sprite BossHudIcon(string bossId) => null;
        public SpriteAnim Effect(string group, string key) => null;
        public EffectEntry EffectInfo(string group, string key) => null;
        public EffectEntry LayerInfo(string key) => null;
        public UiSprite Ui(string id, string variant = null, string state = null) => default;
        public UiSprite UiSized(string id, string variant, string state, int sizePx) => default;
        public UiEntry UiInfo(string id, string variant = null, string state = null, int sizePx = 0) => null;
        public SpriteAnim UiAnim(string id, string variant = null, string state = null, int sizePx = 0) => null;
        public Sprite Icon(string contentId, int sizePx = 32) => null;
        public SpriteAnim World(string assetId, string todId = null) => null;
        public WorldEntry WorldInfo(string assetId) => null;
        public string WorldMapJson(string mapId) => null;
        public Sprite Background(string key) => null;
        public Sprite[] CommonFrames(string relPath) => System.Array.Empty<Sprite>();
        public Sprite CommonSprite(string relPath) => null;
        public Font Font(UiFontId size) => null;
        public Sprite Placeholder(int widthPx, int heightPx) => null;
        public Sprite WhitePixel => null;
        public Sprite Circle(int radiusPx, bool ringOnly = false, int ringWidthPx = 2) => null;
        public Material Material(SpriteMaterialKind kind) => null;
        public void Release(string effectGroup) { Released.Add(effectGroup); }
    }

    /// <summary>정해 둔 결과를 차례로 돌려주는 강화 서비스. Preview는 화면 규칙과 같은 식(ProbabilityDisplayModel.Compute)으로.</summary>
    public sealed class G23Enhance : IEnhanceService
    {
        readonly GameContext _ctx;
        public readonly Queue<EnhanceResult> Results = new Queue<EnhanceResult>();
        public readonly Queue<EnhanceResult> Decisions = new Queue<EnhanceResult>();
        public int EnhanceCalls, ResolveCalls;
        public ErrorCode ForceBlocked = ErrorCode.None;

        public G23Enhance(GameContext ctx) { _ctx = ctx; }

        EnhanceConfig Cfg => _ctx.Content.Enhance;

        public ProbBreakdown Preview(long wandId, IReadOnlyList<string> sacrifices, bool useCharm)
        {
            var w = _ctx.Profile.Wand(wandId);
            if (w == null) return new ProbBreakdown { Blocked = ErrorCode.NotFound };
            var def = _ctx.Content.Character(Ids.Char.Of(w.Character));
            ProbabilityDisplayModel.SacrificeParts(Cfg, _ctx.Content, sacrifices, def != null ? def.Element : null, out int g, out bool same);
            var m = ProbabilityDisplayModel.Compute(Cfg, w.Stage, w.FloorPeak, w.Aura, g, same, useCharm);
            var b = new ProbBreakdown
            {
                BasePct = m.BasePct, SacrificePct = m.SacrificeLinePct + m.SameElementPct, AuraPct = m.AuraPct, RawPct = m.RawPct,
                SuccessPct = m.TotalPct, Capped = m.Capped, GreatBp = m.Band.GreatBp, SuccessBp = m.Band.GreatBp + m.Band.SuccessBp,
                MutationEndBp = m.Band.GreatBp + m.Band.SuccessBp + m.Band.MutationBp, DropPossible = m.DropPossible,
                FloorStage = m.FloorStage, CostGold = m.CostGold, StoneId = m.StoneId, StoneCount = m.StoneCount,
            };
            b.Blocked = ForceBlocked != ErrorCode.None ? ForceBlocked : w.Stage >= Cfg.MaxStage ? ErrorCode.MaxStage
                : w.PendingOffer != null ? ErrorCode.MutationPending : _ctx.Profile.Currencies.Gold < m.CostGold ? ErrorCode.NotEnoughGold : ErrorCode.None;
            return b;
        }

        public Result<EnhanceResult> Enhance(long wandId, IReadOnlyList<string> sacrifices, bool useCharm)
        {
            EnhanceCalls++;
            if (Results.Count == 0) return Result<EnhanceResult>.Fail(ErrorCode.InvalidArgument);
            var r = Results.Dequeue();
            var w = _ctx.Profile.Wand(wandId);
            if (w != null)
            {
                w.Stage = r.Outcome == EnhanceOutcome.MutationOffered ? w.Stage : r.StageAfter;
                w.Aura = r.AuraAfter;
                if (w.Stage > w.FloorPeak) w.FloorPeak = w.Stage;
                if (w.Stage > w.RecordPeak) w.RecordPeak = w.Stage;
                r.Wand = w;
            }
            return Result<EnhanceResult>.Success(r);
        }

        public Result<EnhanceResult> ResolveMutation(long wandId, long attemptId, bool accept)
        {
            ResolveCalls++;
            var w = _ctx.Profile.Wand(wandId);
            if (w != null) w.PendingOffer = null;
            if (Decisions.Count == 0) return Result<EnhanceResult>.Fail(ErrorCode.NoPendingMutation);
            return Result<EnhanceResult>.Success(Decisions.Dequeue());
        }

        public Result<WandData> RemoveMutation(long wandId, string mutationId) => Result<WandData>.Fail(ErrorCode.InvalidArgument);
        public ProbBreakdown PreviewCarve(long wandId) => new ProbBreakdown { Blocked = ErrorCode.NotEligible };
        public Result<CarveResult> CarveSlot(long wandId) => Result<CarveResult>.Fail(ErrorCode.NotEligible);
        public Result<WandData> SetScarVisibility(long wandId, bool show)
        {
            var w = _ctx.Profile.Wand(wandId);
            if (w == null) return Result<WandData>.Fail(ErrorCode.NotFound);
            w.ShowScars = show;
            return Result<WandData>.Success(w);
        }
        public float StageDamageMul(int stage) => 1f;
        public List<ExpectedRow> ExpectedTable(long wandId, int upToStage) => new List<ExpectedRow>();
        public int AttemptsWithOwned(long wandId, int targetStage) => 0;
        public bool IsLocked(long wandId) => false;
    }

    /// <summary>프로필의 지팡이를 그대로 나열하는 지팡이 서비스(정렬: 든 것 먼저, 단계 높은 순).</summary>
    public sealed class G23Wands : IWandService
    {
        readonly GameContext _ctx;
        public G23Wands(GameContext ctx) { _ctx = ctx; }
        public List<WandListEntry> List(int character, WandSort sort)
        {
            var p = _ctx.Profile;
            var held = new List<WandListEntry>();
            var rest = new List<WandListEntry>();
            foreach (var w in p.Wands)
            {
                if (character != 0 && w.Character != character) continue;
                var rec = p.Character(w.Character);
                var e = new WandListEntry { Wand = w, Equipped = rec != null && rec.EquippedWandId == w.Id };
                (e.Equipped ? held : rest).Add(e);
            }
            rest.Sort((a, b) => sort == WandSort.StageAsc ? a.Wand.Stage.CompareTo(b.Wand.Stage) : b.Wand.Stage.CompareTo(a.Wand.Stage));
            held.AddRange(rest);
            return held;
        }
        public Result<WandData> Equip(int character, long wandId) { _ctx.Profile.Character(character).EquippedWandId = wandId; return Result<WandData>.Success(_ctx.Profile.Wand(wandId)); }
        public Result<WandData> SetLoadout(long wandId, LoadoutData loadout) => Result<WandData>.Fail(ErrorCode.InvalidArgument);
        public Result<long> Release(IReadOnlyList<long> wandIds, string typedName) => Result<long>.Fail(ErrorCode.InvalidArgument);
        public long Appraise(WandData wand) => 0;
        public int LibraryFree(string blockId) => 0;
        public int LibraryTotal() => 0;
        public int LibraryCap() => 0;
        public Result<RunConfig> BuildRunConfig(int character, long wandId, int startWave, ulong seed) => Result<RunConfig>.Fail(ErrorCode.InvalidArgument);
        public Result<RunConfig> BuildPracticeConfig(int character, long wandId, RunMode mode, LoadoutSpec overrideLoadout, ulong seed) => Result<RunConfig>.Fail(ErrorCode.InvalidArgument);
    }

    /// <summary>시험 한 벌: 진짜 콘텐츠·이름 표 + 손으로 만든 프로필 + 가짜 서비스.</summary>
    public sealed class G23Kit
    {
        public readonly GameContext Ctx;
        public readonly G23ProfileStore Store = new G23ProfileStore();
        public readonly G23SettingsStore SettingsStore = new G23SettingsStore();
        public readonly G23Sound Sound = new G23Sound();
        public readonly G23Screens Screens = new G23Screens();
        public readonly G23Art Art = new G23Art();
        public readonly G23Enhance Enhance;
        public readonly EnhanceScreenTuning Tuning;

        public G23Kit()
        {
            var p = new ProfileData();
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                p.Characters.Add(new CharacterRecord { Id = c, EquippedWandId = c });
                p.Wands.Add(new WandData { Id = c, Character = c, Loadout = new LoadoutData { Core = Ids.Block.MagicBolt } });
            }
            p.NextWandId = Ids.Char.Count + 1;
            p.Item(Ids.Block.MagicBolt).Qty = Ids.Char.Count;
            p.Item(Ids.Block.MagicBolt).QtySocketed = Ids.Char.Count;
            Store.Current = p;
            Ctx = new GameContext
            {
                Content = TestContent.Db, Names = TestContent.Names, Profiles = Store, Settings = SettingsStore,
                Sound = Sound, Screens = Screens, Art = Art,
            };
            Enhance = new G23Enhance(Ctx);
            Ctx.Enhance = Enhance;
            Ctx.Wands = new G23Wands(Ctx);
            Tuning = EnhanceScreenTuning.From(TestContent.Db);
        }

        public ProfileData Profile => Store.Current;
        public WandData Wand(int character = 1) => Profile.Wand(character);
    }

    /// <summary>결과 꾸러미 만들기.</summary>
    public static class G23Results
    {
        public static EnhanceResult Make(EnhanceOutcome o, int before, int after, int auraBefore = 0, int auraAfter = 0, int successPct = 82, int roll = 3720)
        {
            return new EnhanceResult
            {
                AttemptId = 1, Outcome = o, StageBefore = before, StageAfter = after, AuraBefore = auraBefore, AuraAfter = auraAfter,
                RollBp = roll, Breakdown = new ProbBreakdown { SuccessPct = successPct, SuccessBp = successPct * 100, CostGold = 100 },
                Wand = new WandData { Id = 1, Character = 1, Stage = after, FloorPeak = Math.Max(before, after), RecordPeak = Math.Max(before, after) },
            };
        }

        public static EnhanceScreenModel Model(EnhanceScreenTuning t) => new EnhanceScreenModel(t, TestContent.Db.Enhance.MaxStage);

        /// <summary>끝없는 구간에 닿을 때까지 틱을 돌린다(최대 limit). 돈 틱 수를 돌려준다.</summary>
        public static int RunUntilRest(EnhanceScreenModel m, int limit = 5000)
        {
            int n = 0;
            while (n < limit && m.Phase != EnhancePhase.ResultCard && m.Phase != EnhancePhase.MutationChoice && m.Phase != EnhancePhase.Idle)
            {
                m.Tick();
                n++;
            }
            return n;
        }

        /// <summary>그 상태에 들어갈 때까지 틱을 돌린다.</summary>
        public static int RunUntil(EnhanceScreenModel m, EnhancePhase phase, int limit = 5000)
        {
            int n = 0;
            while (n < limit && m.Phase != phase) { m.Tick(); n++; }
            return n;
        }
    }
}
