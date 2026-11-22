using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensBattle
{
    /// <summary>쌓임을 흉내 내는 가짜 화면 관리자(호출 기록).</summary>
    public sealed class BattleFakeScreens : IScreenManager
    {
        public readonly List<ScreenId> Stack = new List<ScreenId>();
        public readonly List<KeyValuePair<ScreenId, ScreenArgs>> Pushed = new List<KeyValuePair<ScreenId, ScreenArgs>>();
        public readonly List<KeyValuePair<ScreenId, ScreenArgs>> Replaced = new List<KeyValuePair<ScreenId, ScreenArgs>>();
        public readonly List<ScreenId> PoppedTo = new List<ScreenId>();
        public int PopCount;
        public string LastConfirmTitle, LastConfirmBody;
        public bool LastConfirmCaution;
        public Action<bool> LastConfirm;
        public int ConfirmCount;

        public void Register(ScreenId id, Func<IScreen> factory) { }
        public void Push(ScreenId id, ScreenArgs args = null) { Pushed.Add(new KeyValuePair<ScreenId, ScreenArgs>(id, args)); Stack.Add(id); }
        public void Replace(ScreenId id, ScreenArgs args = null)
        {
            Replaced.Add(new KeyValuePair<ScreenId, ScreenArgs>(id, args));
            if (Stack.Count > 0) Stack.RemoveAt(Stack.Count - 1);
            Stack.Add(id);
        }
        public void Pop(object result = null) { PopCount++; if (Stack.Count > 0) Stack.RemoveAt(Stack.Count - 1); }
        public void PopTo(ScreenId id, ScreenArgs args = null)
        {
            PoppedTo.Add(id);
            int i = Stack.LastIndexOf(id);
            if (i < 0) { Stack.Clear(); Stack.Add(id); return; }
            Stack.RemoveRange(i + 1, Stack.Count - i - 1);
        }
        public ScreenId Top => Stack.Count > 0 ? Stack[Stack.Count - 1] : ScreenId.None;
        public bool IsOpen(ScreenId id) => Stack.Contains(id);
        public void Close(IScreen screen, object result = null) { Pop(result); }
        public void Confirm(string titleKey, string bodyKey, bool caution, Action<bool> onResult)
        {
            ConfirmCount++;
            LastConfirmTitle = titleKey; LastConfirmBody = bodyKey; LastConfirmCaution = caution; LastConfirm = onResult;
        }
        public void ConfirmText(string titleKey, string bodyText, bool caution, Action<bool> onResult) => Confirm(titleKey, bodyText, caution, onResult);
        public void Toast(string text, float seconds = 3f) { }
        public void ShowTooltip(string text, RectTransform anchor) { }
        public void HideTooltip(RectTransform anchor = null) { }
        public bool CaptureKeys { get; set; }

        public int CountPushed(ScreenId id)
        {
            int n = 0;
            for (int i = 0; i < Pushed.Count; i++) if (Pushed[i].Key == id) n++;
            return n;
        }
    }

    /// <summary>가짜 세션. Paused set 횟수를 센다(커맨드 초기화는 진짜 세션이 set 안에서 한다).</summary>
    public sealed class BattleFakeSession : IGameSession
    {
        public SimWorld Main, Sub;
        bool _paused;
        public int PausedSetTrueCount, PausedSetFalseCount;
        public readonly List<RunConfig> Started = new List<RunConfig>();
        public RunSummary Summary;
        public float PausedSecondsValue;

        public SimWorld World => Sub ?? Main;
        public SimWorld MainWorld => Main;
        public bool HasRun => Main != null;
        public bool HasSubRun => Sub != null;
        public bool Paused
        {
            get => _paused;
            set { if (value) PausedSetTrueCount++; else PausedSetFalseCount++; _paused = value; }
        }
        public float PausedSeconds => PausedSecondsValue;
        public ScreenId InputScreen { get; set; }
        public void StartRun(RunConfig cfg) { Started.Add(cfg); }
        public void EndRun() { Main = null; Sub = null; }
        public void PushSubRun(RunConfig cfg) { }
        public void PopSubRun() { Sub = null; }
        public RunSummary LastSummary => Summary;
        public void SetInputOverride(IInputSource source) { }
        public event Action<SimWorld> RunStarted;
        public event Action<SimWorld, SimEvent> SimEventRaised;
        public event Action<RunSummary> RunEnded;
        public event Action PauseRequested;

        public void Raise(SimWorld w, SimEvent e) => SimEventRaised?.Invoke(w, e);
        public void RaisePause() => PauseRequested?.Invoke();
        public int SimEventSubscribers => SimEventRaised == null ? 0 : SimEventRaised.GetInvocationList().Length;
        public int PauseSubscribers => PauseRequested == null ? 0 : PauseRequested.GetInvocationList().Length;
        public void Unused() { RunStarted?.Invoke(null); RunEnded?.Invoke(null); }
    }

    public sealed class BattleFakeSettlement : ISettlementService
    {
        public readonly List<RunSummary> Checkpoints = new List<RunSummary>();
        public readonly List<KeyValuePair<int, string>> ArtUnlocks = new List<KeyValuePair<int, string>>();
        public BagLimits BagLimitsFor(int clearedDirect) => default;
        public bool IsValidStartSkip(int bestWave, int skip) => StartSkipOptions(bestWave).Contains(skip);
        public List<int> StartSkipOptions(int bestWave) => new List<int>();
        public SettlementView Prepare(RunSummary summary) => null;
        public Result<SettlementResult> Commit(RunSummary summary, IReadOnlyList<int> bagUids) => default;
        public List<int> AutoPick(SettlementView view) => new List<int>();
        public void SaveCheckpoint(RunSummary summary) => Checkpoints.Add(summary);
        public SettlementResult SettlePendingCheckpoint() => null;
        public void RecordArtUnlock(int character, string artId) => ArtUnlocks.Add(new KeyValuePair<int, string>(character, artId));
    }

    /// <summary>OnCommandReset 호출을 센다(일시정지 진입 때 화면이 부르지 않아야 한다).</summary>
    public sealed class CountingArtModule : ArtModule
    {
        public int ResetCalls;
        public override void OnCommandReset(SimWorld w, PlayerState p, CommandResetReason reason) { ResetCalls++; }
    }

    /// <summary>BuildSummary가 판 표시를 단 요약을 돌려준다.</summary>
    public sealed class MarkerRunModule : RunModule
    {
        public int BuildCalls;
        public override RunSummary BuildSummary(SimWorld w)
        {
            BuildCalls++;
            return new RunSummary { RunId = "run.test", ReachedWave = w.Run.Wave };
        }
    }

    /// <summary>열쇠와 값 쌍을 그대로 적는 이름 표.</summary>
    public sealed class EchoNames : INameTable
    {
        public string Get(string key) => key;
        public bool Has(string key) => false;
        public string Format(string key, params object[] nameValuePairs)
        {
            var sb = new StringBuilder(key);
            sb.Append('(');
            for (int i = 0; i + 1 < nameValuePairs.Length; i += 2)
            {
                if (i > 0) sb.Append(',');
                sb.Append(nameValuePairs[i]).Append('=').Append(nameValuePairs[i + 1]);
            }
            sb.Append(')');
            return sb.ToString();
        }
        public string Number(long value) => value.ToString();
    }

    public static class BattleTestWorlds
    {
        /// <summary>빈 모듈 세계 + 슬롯 0 활동(char.N), 단계 phase.</summary>
        public static SimWorld Make(RunPhase phase, string charId = "char.1", SimModuleSet mods = null)
        {
            var w = new SimWorld(null, mods ?? SimModuleSet.Stubs());
            w.Run.Phase = phase;
            w.Players[0].Active = true;
            w.Players[0].CharId = charId;
            return w;
        }

        public static SimEvent Phase(RunPhase now, RunPhase prev) =>
            new SimEvent { Type = SimEventType.PhaseChanged, A = (int)now, B = (int)prev, Slot = -1 };

        public static SimEvent Ev(SimEventType t, int a = 0, int b = 0, int slot = -1, string id = null) =>
            new SimEvent { Type = t, A = a, B = b, Slot = slot, Id = id };
    }
}
