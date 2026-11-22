using System;
using System.Collections.Generic;
using System.Text;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using Wizard.Solo.Tests;

namespace Wizard.Solo.Tests.Story
{
    /// <summary>순서를 적는 공용 기록.</summary>
    public sealed class StoryLog
    {
        public readonly List<string> Lines = new List<string>();
        public void Add(string s) => Lines.Add(s);
        public int IndexOf(string s) => Lines.IndexOf(s);
    }

    public sealed class FakeWands : IWandService
    {
        public int BuildRunConfigCalls;
        public int PracticeCalls;
        public int LastCharacter;
        public long LastWandId;
        public RunMode LastMode;
        public LoadoutSpec LastOverride = new LoadoutSpec();
        public ulong LastSeed;
        public bool Fail;
        public RunConfig Returned;

        public List<WandListEntry> List(int character, WandSort sort) => new List<WandListEntry>();
        public Result<WandData> Equip(int character, long wandId) => Result<WandData>.Fail(ErrorCode.NotEligible);
        public Result<WandData> SetLoadout(long wandId, LoadoutData loadout) => Result<WandData>.Fail(ErrorCode.NotEligible);
        public Result<long> Release(IReadOnlyList<long> wandIds, string typedName) => Result<long>.Fail(ErrorCode.NotEligible);
        public long Appraise(WandData wand) => 0;
        public int LibraryFree(string blockId) => 0;
        public int LibraryTotal() => 0;
        public int LibraryCap() => 0;

        public Result<RunConfig> BuildRunConfig(int character, long wandId, int startWave, ulong seed)
        {
            BuildRunConfigCalls++;
            return Result<RunConfig>.Fail(ErrorCode.NotEligible);
        }

        public Result<RunConfig> BuildPracticeConfig(int character, long wandId, RunMode mode, LoadoutSpec overrideLoadout, ulong seed)
        {
            PracticeCalls++;
            LastCharacter = character;
            LastWandId = wandId;
            LastMode = mode;
            LastOverride = overrideLoadout;
            LastSeed = seed;
            if (Fail) return Result<RunConfig>.Fail(ErrorCode.CoreEmpty);
            Returned = new RunConfig
            {
                Mode = mode, Seed = seed, RunId = string.Empty, CharId = Ids.Char.Of(character), WandId = wandId,
                PracticeInfiniteCrystal = mode == RunMode.Tutorial, TutorialStep = 1, StartWave = 1
            };
            return Result<RunConfig>.Success(Returned);
        }
    }

    public sealed class FakeSession : IGameSession
    {
        readonly StoryLog _log;
        ScreenId _input = ScreenId.None;
        public readonly List<ScreenId> InputHistory = new List<ScreenId>();
        public readonly List<RunConfig> Started = new List<RunConfig>();
        public int EndRunCalls;
        public int PausedSets;
        bool _paused;

        public FakeSession(StoryLog log) { _log = log; }

        public SimWorld World { get; private set; }
        public SimWorld MainWorld => World;
        public bool HasRun => World != null;
        public bool HasSubRun => false;
        public bool Paused { get => _paused; set { _paused = value; PausedSets++; } }
        public float PausedSeconds => 0f;
        public ScreenId InputScreen
        {
            get => _input;
            set { _input = value; InputHistory.Add(value); _log?.Add("InputScreen=" + value); }
        }

        public void StartRun(RunConfig cfg)
        {
            Started.Add(cfg);
            var w = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), cfg != null ? cfg.Seed : 1UL);
            w.Run.Config = cfg;
            w.Run.Phase = RunPhase.Practice;
            w.Run.TutorialStep = cfg != null ? cfg.TutorialStep : 1;
            World = w;
            _log?.Add("StartRun");
        }

        public void EndRun()
        {
            EndRunCalls++;
            World = null;
            _log?.Add("EndRun");
        }

        public void PushSubRun(RunConfig cfg) { throw new InvalidOperationException(); }
        public void PopSubRun() { }
        public RunSummary LastSummary => null;
        public void SetInputOverride(IInputSource source) { }

        public event Action<SimWorld> RunStarted;
        public event Action<SimWorld, SimEvent> SimEventRaised;
        public event Action<RunSummary> RunEnded;
        public event Action PauseRequested;

        public int SimEventSubscribers => SimEventRaised == null ? 0 : SimEventRaised.GetInvocationList().Length;
        public bool PauseRequestedSubscribed => PauseRequested != null;

        public void Raise(SimEvent e) => SimEventRaised?.Invoke(World, e);
        public void Raise(SimWorld w, SimEvent e) => SimEventRaised?.Invoke(w, e);
        internal void Touch() { RunStarted?.Invoke(null); RunEnded?.Invoke(null); }
    }

    /// <summary>쌓임을 흉내 내는 화면 관리자.</summary>
    public sealed class FakeScreens : IScreenManager
    {
        readonly StoryLog _log;
        public readonly List<ScreenId> Stack = new List<ScreenId>();
        public readonly List<KeyValuePair<ScreenId, ScreenArgs>> Pushed = new List<KeyValuePair<ScreenId, ScreenArgs>>();
        public ScreenId ReplacedWith = ScreenId.None;
        public ScreenArgs ReplacedArgs;
        public readonly List<ScreenId> PopToCalls = new List<ScreenId>();
        public string ConfirmTitle, ConfirmBody;
        public Action<bool> ConfirmResult;
        public int ConfirmCalls;

        public FakeScreens(StoryLog log, params ScreenId[] initial)
        {
            _log = log;
            Stack.AddRange(initial);
        }

        public void Register(ScreenId id, Func<IScreen> factory) { }
        public void Push(ScreenId id, ScreenArgs args = null)
        {
            Stack.Add(id);
            Pushed.Add(new KeyValuePair<ScreenId, ScreenArgs>(id, args));
            _log?.Add("Push " + id);
        }
        public void Replace(ScreenId id, ScreenArgs args = null)
        {
            if (Stack.Count > 0) Stack[Stack.Count - 1] = id; else Stack.Add(id);
            ReplacedWith = id;
            ReplacedArgs = args;
            _log?.Add("Replace " + id);
        }
        public void Pop(object result = null)
        {
            if (Stack.Count > 0) Stack.RemoveAt(Stack.Count - 1);
            _log?.Add("Pop");
        }
        public void PopTo(ScreenId id, ScreenArgs args = null)
        {
            PopToCalls.Add(id);
            int i = Stack.LastIndexOf(id);
            if (i >= 0) Stack.RemoveRange(i + 1, Stack.Count - i - 1);
            else { Stack.Clear(); Stack.Add(id); }
            _log?.Add("PopTo " + id);
        }
        public ScreenId Top => Stack.Count > 0 ? Stack[Stack.Count - 1] : ScreenId.None;
        public bool IsOpen(ScreenId id) => Stack.Contains(id);
        public void Close(IScreen screen, object result = null) { }
        public void Confirm(string titleKey, string bodyKey, bool caution, Action<bool> onResult)
        {
            ConfirmCalls++;
            ConfirmTitle = titleKey;
            ConfirmBody = bodyKey;
            ConfirmResult = onResult;
            _log?.Add("Confirm");
        }
        public void ConfirmText(string titleKey, string bodyText, bool caution, Action<bool> onResult) { }
        public void Toast(string text, float seconds = 3f) { }
        public void ShowTooltip(string text, UnityEngine.RectTransform anchor) { }
        public void HideTooltip(UnityEngine.RectTransform anchor = null) { }
        public bool CaptureKeys { get; set; }
    }

    public sealed class FakeProfiles : IProfileStore
    {
        readonly StoryLog _log;
        public ProfileData Data = new ProfileData();
        public int Saves;
        public FakeProfiles(StoryLog log) { _log = log; }
        public ProfileData Current => Data;
        public void LoadOrCreate(out bool created, out bool restored) { created = false; restored = false; }
        public void Save()
        {
            Saves++;
            _log?.Add("Save tutorial_dummy_done=" + (Data.Flags.TutorialDummyDone ? "1" : "0"));
            Saved?.Invoke();
        }
        public string Directory => string.Empty;
        public bool RestorePrevious() => false;
        public event Action Saved;
    }

    public sealed class StoryFakeSound : ISoundPlayer
    {
        public readonly List<string> Played = new List<string>();
        public void Play(string eventId, SoundBus bus = SoundBus.Sfx, float volume = 1f, float pitch = 1f) { Played.Add(eventId + "@" + bus); }
        public void SetMusic(string trackId) { }
        public void SetVolume(SoundBus bus, float volume01) { }
    }

    /// <summary>열쇠를 고를 수 있는 이름 표(본문 열쇠의 있고 없음 시험용).</summary>
    public sealed class StoryFakeNames : INameTable
    {
        public readonly Dictionary<string, string> Map = new Dictionary<string, string>();
        public StoryFakeNames Set(string k, string v) { Map[k] = v; return this; }
        public string Get(string key) => key != null && Map.TryGetValue(key, out var v) ? v : key;
        public bool Has(string key) => key != null && Map.ContainsKey(key);
        public string Format(string key, params object[] pairs)
        {
            var s = new StringBuilder(Get(key));
            for (int i = 0; i + 1 < pairs.Length; i += 2) s.Replace("{" + pairs[i] + "}", Convert.ToString(pairs[i + 1]));
            return s.ToString();
        }
        public string Number(long value) => value.ToString();
    }

    /// <summary>튜토리얼 시험 한 벌.</summary>
    public sealed class TutorialRig
    {
        public readonly StoryLog Log = new StoryLog();
        public readonly FakeWands Wands = new FakeWands();
        public readonly FakeSession Session;
        public readonly FakeScreens Screens;
        public readonly FakeProfiles Profiles;
        public readonly StoryFakeSound Sound = new StoryFakeSound();
        public readonly GameContext Ctx;
        public readonly RunConfig RealCfg;

        public TutorialRig(bool withWands = true)
        {
            Session = new FakeSession(Log);
            Screens = new FakeScreens(Log, ScreenId.MainMenu, ScreenId.Tutorial);
            Profiles = new FakeProfiles(Log);
            Ctx = new GameContext
            {
                Content = TestContent.Db, Names = TestContent.Names, Sound = Sound, Profiles = Profiles,
                Session = Session, Screens = Screens, Wands = withWands ? Wands : null
            };
            RealCfg = new RunConfig { Mode = RunMode.Endless, Seed = 77UL, RunId = "run.real", CharId = Ids.Char.C3, WandId = 4242L, StartWave = 1, PhantomFirst = true };
        }

        public ScreenArgs Args() => ScreenArgs.From(ScreenId.ExpeditionPrep).With(ScreenArgKeys.RunConfig, RealCfg);
    }
}
