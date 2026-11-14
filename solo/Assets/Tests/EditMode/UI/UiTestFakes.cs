using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Data;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.UI
{
    /// <summary>호출을 기록하는 가짜 화면.</summary>
    public sealed class FakeScreen : IScreen
    {
        public readonly List<string> Log;
        public ScreenArgs LastArgs;
        public bool BackResult;
        public Func<MenuKey, bool> KeyHandler;
        public Action OnBackAction;
        public int Ticks;
        public int Enters, Exits;

        public FakeScreen(ScreenId id, ScreenLayer layer, List<string> log, bool blocks = false)
        {
            Id = id; Layer = layer; Log = log; BlocksInput = blocks;
        }

        public ScreenId Id { get; }
        public ScreenLayer Layer { get; }
        public bool BlocksInput { get; }
        public void Build(GameContext ctx, RectTransform root) { }
        public void OnEnter(ScreenArgs args) { LastArgs = args; Enters++; Log?.Add("enter " + Id); }
        public void OnExit() { Exits++; Log?.Add("exit " + Id); }
        public bool OnBack() { Log?.Add("back " + Id); OnBackAction?.Invoke(); return BackResult; }
        public bool OnMenuKey(MenuKey key) { Log?.Add("key " + Id + " " + key); return KeyHandler != null && KeyHandler(key); }
        public void Tick(float dt) { Ticks++; }
    }

    /// <summary>id마다 가짜 화면을 새로 만드는 공급자(등록 표 = layers).</summary>
    public sealed class FakeProvider
    {
        public readonly List<string> Log = new List<string>();
        public readonly Dictionary<ScreenId, ScreenLayer> Layers = new Dictionary<ScreenId, ScreenLayer>();
        public readonly Dictionary<ScreenId, bool> Blocks = new Dictionary<ScreenId, bool>();
        public readonly List<FakeScreen> Created = new List<FakeScreen>();
        public readonly List<string> Warnings = new List<string>();

        public FakeProvider Add(ScreenId id, ScreenLayer layer, bool blocks = false)
        {
            Layers[id] = layer;
            Blocks[id] = blocks;
            return this;
        }

        public IScreen Provide(ScreenId id)
        {
            if (!Layers.TryGetValue(id, out var layer)) return null;
            var s = new FakeScreen(id, layer, Log, Blocks[id]);
            Created.Add(s);
            return s;
        }

        public FakeScreen Last(ScreenId id)
        {
            for (int i = Created.Count - 1; i >= 0; i--) if (Created[i].Id == id) return Created[i];
            return null;
        }

        public UiScreenStack NewStack() => new UiScreenStack(Provide, w => Warnings.Add(w));
    }

    /// <summary>눌린 키를 손으로 정하는 가짜 키 읽개.</summary>
    public sealed class FakeKeyReader : IUiKeyReader
    {
        public readonly HashSet<Key> Down = new HashSet<Key>();
        public bool AnyPressed;
        public Vector2 Delta;
        public bool IsDown(Key key) => Down.Contains(key);
        public bool AnyKeyPressedThisFrame => AnyPressed;
        public Vector2 MouseDelta => Delta;
    }

    /// <summary>열쇠 → 글 사전. "{name}" 채우기만 한다.</summary>
    public sealed class FakeNames : INameTable
    {
        public readonly Dictionary<string, string> Map = new Dictionary<string, string>();
        public FakeNames Set(string k, string v) { Map[k] = v; return this; }
        public string Get(string key) => key != null && Map.TryGetValue(key, out var v) ? v : key;
        public bool Has(string key) => key != null && Map.ContainsKey(key);
        public string Format(string key, params object[] pairs)
        {
            var s = new StringBuilder(Get(key));
            for (int i = 0; i + 1 < pairs.Length; i += 2) s.Replace("{" + pairs[i] + "}", Convert.ToString(pairs[i + 1]));
            return s.ToString();
        }
        public string Number(long value) => UiTextFit.FormatThousands(value);
    }

    /// <summary>불린 사건을 기록하는 소리 재생기.</summary>
    public sealed class FakeSound : ISoundPlayer
    {
        public readonly List<string> Played = new List<string>();
        public void Play(string eventId, SoundBus bus = SoundBus.Sfx, float volume = 1f, float pitch = 1f) { Played.Add(eventId + "@" + bus); }
        public void SetMusic(string trackId) { }
        public void SetVolume(SoundBus bus, float volume01) { }
    }

    /// <summary>호출을 기록하는 가짜 화면 관리자(Loading 흐름 시험용).</summary>
    public sealed class FakeScreenManager : IScreenManager
    {
        public readonly List<string> Calls = new List<string>();
        public ScreenArgs LastArgs;
        public void Register(ScreenId id, Func<IScreen> factory) { Calls.Add("register " + id); }
        public void Push(ScreenId id, ScreenArgs args = null) { Calls.Add("push " + id); LastArgs = args; }
        public void Replace(ScreenId id, ScreenArgs args = null) { Calls.Add("replace " + id); LastArgs = args; }
        public void Pop(object result = null) { Calls.Add("pop"); }
        public void PopTo(ScreenId id, ScreenArgs args = null) { Calls.Add("popto " + id); }
        public ScreenId Top => ScreenId.None;
        public bool IsOpen(ScreenId id) => false;
        public void Close(IScreen screen, object result = null) { Calls.Add("close"); }
        public void Confirm(string titleKey, string bodyKey, bool caution, Action<bool> onResult) { Calls.Add("confirm"); }
        public void ConfirmText(string titleKey, string bodyText, bool caution, Action<bool> onResult) { Calls.Add("confirmtext"); }
        public void Toast(string text, float seconds = 3f) { Calls.Add("toast " + text); }
        public void ShowTooltip(string text, UnityEngine.RectTransform anchor) { Calls.Add("tooltip " + text); }
        public void HideTooltip(UnityEngine.RectTransform anchor = null) { Calls.Add("hidetooltip"); }
        public bool CaptureKeys { get; set; }
    }
}
