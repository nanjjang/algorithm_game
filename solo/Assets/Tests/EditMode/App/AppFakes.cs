using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Wizard.Solo.App;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.App
{
    /// <summary>가짜 물리 키 읽개: 눌린 키 집합.</summary>
    public sealed class FakeKeyReader : IKeyReader
    {
        public readonly HashSet<Key> Pressed = new HashSet<Key>();
        public bool IsPressed(Key key) => key != Key.None && Pressed.Contains(key);
        public void Down(Key k) => Pressed.Add(k);
        public void Up(Key k) => Pressed.Remove(k);
    }

    public sealed class FakeSettingsStore : ISettingsStore
    {
        public SettingsData Data = new SettingsData();
        public SettingsData Current => Data;
        public bool ExistedAtLoad => true;
        public void Load() { }
        public void Save() { }
        public event Action Changed;
        public void RaiseChanged() => Changed?.Invoke();
        public int ChangedSubscriberCount => Changed == null ? 0 : Changed.GetInvocationList().Length;
    }

    /// <summary>맨 위 화면만 흉내 내는 화면 관리자.</summary>
    public sealed class FakeScreenManager : IScreenManager
    {
        public ScreenId TopId = ScreenId.None;
        public readonly List<KeyValuePair<ScreenId, ScreenArgs>> Pushed = new List<KeyValuePair<ScreenId, ScreenArgs>>();
        public void Register(ScreenId id, Func<IScreen> factory) { }
        public void Push(ScreenId id, ScreenArgs args = null) { Pushed.Add(new KeyValuePair<ScreenId, ScreenArgs>(id, args)); TopId = id; }
        public void Replace(ScreenId id, ScreenArgs args = null) { TopId = id; }
        public void Pop(object result = null) { }
        public void PopTo(ScreenId id, ScreenArgs args = null) { TopId = id; }
        public ScreenId Top => TopId;
        public bool IsOpen(ScreenId id) => TopId == id;
        public void Close(IScreen screen, object result = null) { }
        public void Confirm(string titleKey, string bodyKey, bool caution, Action<bool> onResult) { }
        public void ConfirmText(string titleKey, string bodyText, bool caution, Action<bool> onResult) { }
        public void Toast(string text, float seconds = 3f) { }
        public void ShowTooltip(string text, UnityEngine.RectTransform anchor) { }
        public void HideTooltip(UnityEngine.RectTransform anchor = null) { }
        public bool CaptureKeys { get; set; }
    }

    /// <summary>커맨드 초기화를 세는 가짜 ArtModule.</summary>
    public sealed class CountingArtModule : ArtModule
    {
        public readonly List<KeyValuePair<int, CommandResetReason>> Resets = new List<KeyValuePair<int, CommandResetReason>>();
        public override void OnCommandReset(SimWorld w, PlayerState p, CommandResetReason reason)
        {
            Resets.Add(new KeyValuePair<int, CommandResetReason>(p.Slot, reason));
        }
        public int Count(int slot, CommandResetReason reason)
        {
            int n = 0;
            for (int i = 0; i < Resets.Count; i++) if (Resets[i].Key == slot && Resets[i].Value == reason) n++;
            return n;
        }
    }

    /// <summary>판 시작에 정한 슬롯을 활동시키고, 틱마다 이벤트 하나를 내고, 정한 틱에 판을 끝내는 가짜 RunModule.</summary>
    public sealed class ScriptedRunModule : RunModule
    {
        public int[] ActiveSlots = { 0 };
        /// <summary>이 틱(Stage11 시점의 w.Tick)에서 RunEnded로. -1 = 끝내지 않음.</summary>
        public int EndAtTick = -1;
        public bool EmitEveryTick = true;
        public int SummaryBuilds;

        public override void StartRun(SimWorld w, RunConfig cfg)
        {
            for (int i = 0; i < ActiveSlots.Length; i++) w.Players[ActiveSlots[i]].Active = true;
            w.Run.Phase = cfg.Mode == RunMode.Practice ? RunPhase.Practice : RunPhase.WaveCombat;
        }

        public override void Stage11_Phase(SimWorld w)
        {
            if (EmitEveryTick) w.Emit(SimEventType.CommandReset, 0, a: w.Tick);
            if (EndAtTick >= 0 && w.Tick == EndAtTick) w.Run.Phase = RunPhase.RunEnded;
        }

        public override RunSummary BuildSummary(SimWorld w)
        {
            SummaryBuilds++;
            return new RunSummary();
        }
    }

    /// <summary>표시 호스트 가짜: 부른 횟수를 센다.</summary>
    public sealed class FakeViewHost : IWorldViewHost
    {
        public int Builds, Disposes, Events, Renders;
        public SimWorld LastBuilt;
        public SimWorld LastRendered;
        public float LastDt = -1f;
        public void Build(SimWorld w) { Builds++; LastBuilt = w; }
        public void OnEvent(SimWorld w, in SimEvent e) { Events++; }
        public void Render(SimWorld w, float alpha, float dt) { Renders++; LastRendered = w; LastDt = dt; }
        public void DisposeLayers() { Disposes++; }
    }

    /// <summary>스텁 모듈 세계를 만드는 실행기 묶음.</summary>
    public sealed class SessionRig
    {
        public readonly GameContext Ctx = new GameContext();
        public readonly FakeScreenManager Screens = new FakeScreenManager();
        public readonly BotInputSource Bot = new BotInputSource();
        public readonly FakeViewHost Host = new FakeViewHost();
        public readonly List<CountingArtModule> ArtsMade = new List<CountingArtModule>();
        public readonly List<ScriptedRunModule> RunsMade = new List<ScriptedRunModule>();
        public int[] ActiveSlots = { 0 };
        public int EndAtTick = -1;
        public readonly SessionCore Session;

        public const float Frame = 1f / 60f;

        public SessionRig(bool withScreens = true)
        {
            Ctx.Input = Bot;
            if (withScreens) Ctx.Screens = Screens;
            Session = new SessionCore(Ctx, MakeWorld, Host);
        }

        SimWorld MakeWorld()
        {
            var mods = SimModuleSet.Stubs();
            var arts = new CountingArtModule();
            var run = new ScriptedRunModule { ActiveSlots = ActiveSlots, EndAtTick = EndAtTick };
            mods.Arts = arts;
            mods.Run = run;
            ArtsMade.Add(arts);
            RunsMade.Add(run);
            return new SimWorld(null, mods);
        }

        public CountingArtModule Arts => ArtsMade[ArtsMade.Count - 1];

        /// <summary>정확히 한 틱 분량의 프레임을 n번.</summary>
        public void Frames(int n)
        {
            for (int i = 0; i < n; i++) Session.Frame(Frame);
        }

        public static List<InputEvent> Queue(SimWorld w, int slot = 0) => w.InputQueues[slot];

        public static int CountKey(SimWorld w, LogicalKey key, bool down)
        {
            int n = 0;
            var q = w.InputQueues[0];
            for (int i = 0; i < q.Count; i++) if (q[i].Key == key && q[i].Down == down) n++;
            return n;
        }
    }
}
