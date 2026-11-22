using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.App;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.PlayMode.Integration;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Flow
{
    /// <summary>
    /// 실제 화면을 조작하는 흐름 테스트 도구. 화면은 사람이 하듯 조작한다: 이름 표 글자로 찾은 버튼을 누르고(UiWidget.Activate),
    /// 메뉴 키는 ScreenManager.Dispatch로 보낸다. 전투는 BotInputSource(IInputSource 주입)로 논리 키를 누르고 뗀다 —
    /// 시뮬레이션에 직접 명령을 넣지 않는다(전투 입력의 문·화면 전이를 그대로 거친다).
    /// 화면에 글자가 없는 버튼(카드·칸)만 화면 객체의 필드를 읽어 그 버튼을 누른다.
    /// 오류 로그·예외는 Unity 테스트 러너가 실패로 잡는다. 경고·오류는 따로 모아 끝에 보고한다.
    /// </summary>
    internal sealed class FlowKit
    {
        public const float DefaultTimeout = 30f;

        public BootResult Boot { get; private set; }
        public GameContext Ctx => Boot != null ? Boot.Context : null;
        public ScreenManager Screens => Ctx != null ? Ctx.Screens as ScreenManager : null;
        public IGameSession Session => Ctx != null ? Ctx.Session : null;
        public SimWorld World => Session != null ? Session.World : null;
        public BotInputSource Bot { get; private set; }
        public string DataRoot { get; private set; }
        public string ShotDir { get; private set; }
        public readonly List<string> Shots = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<SimEvent> Events = new List<SimEvent>();

        readonly bool[] _keys = new bool[SimConst.LogicalKeyCount];

        public void Start(string shotSubdir)
        {
            DataRoot = Path.Combine(Path.GetTempPath(), "wizard-solo-flow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DataRoot);
            ShotDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "shots", "flow", shotSubdir);
            Directory.CreateDirectory(ShotDir);
            Application.logMessageReceived += OnLog;
            Bot = new BotInputSource();
            Boot = Bootstrap.Boot(new BootOptions { BuildVersion = "flow", DataRoot = DataRoot, InputOverride = Bot });
            Session.SimEventRaised += OnSimEvent;
        }

        public void Stop()
        {
            var art = Ctx != null ? Ctx.Art as Wizard.Solo.Art.ArtLibrary : null;
            if (art != null)
            {
                var l = new List<string>(art.Misses);
                l.Sort(StringComparer.Ordinal);
                Log("art misses(" + l.Count + "): " + string.Join(" | ", l));
            }
            Application.logMessageReceived -= OnLog;
            if (Session != null) Session.SimEventRaised -= OnSimEvent;
            Bootstrap.Shutdown();
            try { if (DataRoot != null && Directory.Exists(DataRoot)) Directory.Delete(DataRoot, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Warning) Warnings.Add(message);
            else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(message + "\n" + stack);
        }

        void OnSimEvent(SimWorld w, SimEvent e) { Events.Add(e); }

        public int Count(SimEventType t)
        {
            int n = 0;
            for (int i = 0; i < Events.Count; i++) if (Events[i].Type == t) n++;
            return n;
        }

        public static void Log(string s) => Debug.Log("[flow] " + s);

        // ------------------------------------------------------------ 기다리기

        public IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        public IEnumerator Seconds(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        public IEnumerator WaitUntil(Func<bool> cond, string what, float timeout = DefaultTimeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end)
                    Assert.Fail("기다림 시간 초과: " + what + " (맨 위 화면 " + (Screens != null ? Screens.Top.ToString() : "?")
                                + ", 판 " + (World != null && World.Run != null ? World.Run.Phase.ToString() : "없음") + ", 보이는 버튼: " + VisibleButtons() + ")");
                yield return null;
            }
        }

        public IEnumerator WaitTop(ScreenId id, float timeout = DefaultTimeout)
        {
            yield return WaitUntil(() => Screens.Top == id, "맨 위 화면 " + id, timeout);
            yield return null;
            Log("top = " + id);
        }

        // ------------------------------------------------------------ 메뉴 조작

        public void Key(MenuKey k)
        {
            Log("menu key " + k + " on " + Screens.Top);
            Screens.Dispatch(k);
        }

        public string T(string key) => Ctx.Names.Get(key);

        static IEnumerable<UiWidget> ActiveWidgets()
        {
            foreach (var w in UnityEngine.Object.FindObjectsByType<UiWidget>(FindObjectsSortMode.None))
                if (w != null && w.isActiveAndEnabled && w.gameObject.activeInHierarchy) yield return w;
        }

        public string VisibleButtons()
        {
            var l = new List<string>();
            foreach (var w in ActiveWidgets())
            {
                var b = w as UiButton;
                string text = b != null && b.LabelText != null ? b.LabelText.text : null;
                l.Add((string.IsNullOrEmpty(text) ? w.name : "'" + text + "'") + (w.Enabled ? "" : "(꺼짐)"));
            }
            return string.Join(", ", l);
        }

        /// <summary>글자가 그 열쇠의 이름 표 글자인 켜진 버튼을 누른다.</summary>
        public void Click(string nameKey)
        {
            string text = T(nameKey);
            UiButton found = null;
            foreach (var w in ActiveWidgets())
            {
                var b = w as UiButton;
                if (b == null || b.LabelText == null || b.LabelText.text != text) continue;
                if (!b.Enabled) continue;
                found = b;
            }
            Assert.IsNotNull(found, "버튼 '" + text + "'(" + nameKey + ")이 없다. 맨 위 " + Screens.Top + ", 보이는 버튼: " + VisibleButtons());
            Log("click '" + text + "' on " + Screens.Top);
            found.Activate();
        }

        public bool HasButton(string nameKey)
        {
            string text = T(nameKey);
            foreach (var w in ActiveWidgets())
            {
                var b = w as UiButton;
                if (b != null && b.LabelText != null && b.LabelText.text == text) return true;
            }
            return false;
        }

        public void Activate(UiWidget w, string what)
        {
            Assert.IsNotNull(w, what);
            Log("activate " + what + " on " + Screens.Top);
            w.Activate();
        }

        public IScreen TopScreen => Screens.Stack.TopScreen;

        /// <summary>글자가 없는 버튼(카드·칸)을 얻으려고 화면 객체의 필드를 읽는다.</summary>
        public static T Field<T>(object o, string name)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) return (T)f.GetValue(o);
            }
            Assert.Fail("필드 " + name + "이 " + o.GetType().Name + "에 없다");
            return default;
        }

        // ------------------------------------------------------------ 스크린숏

        public IEnumerator Shot(string name)
        {
            var runner = Session as GameRunner;
            string path = Path.Combine(ShotDir, name + ".png");
            string used = null;
            yield return SmokeCapture.Capture(runner.Host.Camera, path, u => used = u);
            Assert.IsTrue(File.Exists(path), "screenshot " + name);
            Shots.Add(name);
            Log("shot " + name + " (" + used + ")");
        }

        // ------------------------------------------------------------ 전투 봇

        public void SetKey(LogicalKey k, bool down)
        {
            if (_keys[(int)k] == down) return;
            _keys[(int)k] = down;
            Bot.Now(k, down);
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < _keys.Length; i++) SetKey((LogicalKey)i, false);
        }

        /// <summary>목표 자리 쪽으로 걷는다(가까우면 멈춘다). 좌표는 y 아래가 +.</summary>
        public bool WalkTo(Vector2 target, float stopDist)
        {
            var w = World;
            var p = w.P0;
            Vector2 d = target - p.Pos;
            bool arrived = d.sqrMagnitude <= stopDist * stopDist;
            bool horiz = Mathf.Abs(d.x) > Mathf.Abs(d.y);
            SetKey(LogicalKey.Left, !arrived && d.x < -0.2f && (horiz || Mathf.Abs(d.y) < 0.3f));
            SetKey(LogicalKey.Right, !arrived && d.x > 0.2f && (horiz || Mathf.Abs(d.y) < 0.3f));
            SetKey(LogicalKey.Up, !arrived && d.y < -0.2f && (!horiz || Mathf.Abs(d.x) < 0.3f));
            SetKey(LogicalKey.Down, !arrived && d.y > 0.2f && (!horiz || Mathf.Abs(d.x) < 0.3f));
            return arrived;
        }

        public EnemyState NearestEnemy()
        {
            var w = World;
            EnemyState best = null;
            float bd = float.MaxValue;
            foreach (var e in w.Enemies)
            {
                if (!e.Targetable) continue;
                float d = (e.Pos - w.P0.Pos).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        int _artTick = -1;

        /// <summary>
        /// 전투 한 프레임: 가까운 적 쪽으로 걸어 J를 누르고 있는다(가까우면 그 자리에서 그쪽을 본다). 적이 없으면 드랍을 주우러 간다.
        /// useArts면 결정이 있을 때 가끔 자세 + K(마력 방출).
        /// </summary>
        public void CombatFrame(bool useArts)
        {
            var w = World;
            if (w == null || w.Run == null) return;
            var p = w.P0;
            var e = NearestEnemy();
            if (useArts && e != null && p.Crystals > 0 && w.Tick - _artTick > 180)
            {
                // 자세를 잡고 K. 자세 중에는 움직이지 않는다.
                if (!_keys[(int)LogicalKey.Stance]) { ReleaseAll(); SetKey(LogicalKey.Stance, true); return; }
                SetKey(LogicalKey.Skill, true);
                _artTick = w.Tick;
                return;
            }
            if (_keys[(int)LogicalKey.Skill]) { SetKey(LogicalKey.Skill, false); return; }
            if (_keys[(int)LogicalKey.Stance]) { SetKey(LogicalKey.Stance, false); return; }

            if (e != null)
            {
                SetKey(LogicalKey.Fire, true);
                Vector2 d = e.Pos - p.Pos;
                if (d.sqrMagnitude > 3f * 3f) { WalkTo(e.Pos, 3f); return; }
                // 가까우면 그쪽을 보게 그 방향만 짧게 누른다.
                bool horiz = Mathf.Abs(d.x) > Mathf.Abs(d.y);
                bool tap = (w.Tick & 3) == 0;
                SetKey(LogicalKey.Left, tap && horiz && d.x < 0);
                SetKey(LogicalKey.Right, tap && horiz && d.x > 0);
                SetKey(LogicalKey.Up, tap && !horiz && d.y < 0);
                SetKey(LogicalKey.Down, tap && !horiz && d.y > 0);
                return;
            }
            SetKey(LogicalKey.Fire, false);
            DropFrame();
        }

        /// <summary>안 주운 드랍이 있으면 그리로 걷는다. 없으면 멈춘다.</summary>
        public bool DropFrame()
        {
            var w = World;
            DropEntity best = null;
            float bd = float.MaxValue;
            foreach (var d in w.Drops)
            {
                if (d.Picked) continue;
                float dist = (d.Pos - w.P0.Pos).sqrMagnitude;
                if (dist < bd) { bd = dist; best = d; }
            }
            if (best == null) { WalkTo(w.P0.Pos, 1f); return false; }
            WalkTo(best.Pos, 0.1f);
            return true;
        }

        /// <summary>판을 조건이 될 때까지 봇으로 싸운다. god이면 플레이어를 다치지 않게 한다(무적 틱을 늘림).</summary>
        public IEnumerator Fight(Func<bool> until, string what, float timeout, bool god, bool useArts)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!until())
            {
                if (Time.realtimeSinceStartup > end)
                {
                    var w0 = World;
                    Assert.Fail("전투 시간 초과: " + what + " (판 " + (w0 != null ? w0.Run.Phase + ", 웨이브 " + w0.Run.Wave + ", 적 " + w0.Enemies.Count : "없음")
                                + ", 맨 위 " + Screens.Top + ")");
                }
                var w = World;
                if (w != null && w.Run != null && Screens.Top == ScreenId.Battle)
                {
                    if (god) w.P0.InvulnUntilTick = Mathf.Max(w.P0.InvulnUntilTick, w.Tick + 10);
                    CombatFrame(useArts);
                }
                yield return null;
            }
            ReleaseAll();
        }
    }
}
