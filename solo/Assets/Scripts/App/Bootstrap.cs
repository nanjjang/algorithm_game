using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wizard.Solo.UI;

namespace Wizard.Solo.App
{
    /// <summary>
    /// 부팅(00 문서 4절, 08 요약서 2절). 장면은 빈 Main.unity 하나이고 모든 것을 코드로 만든다.
    /// - 자동 부팅은 활성 장면 이름이 Main일 때만(PlayMode 테스트의 InitTestScene…에서는 돌지 않는다).
    /// - Boot는 AppFactory.Build 뒤 첫 화면을 연다: 설정 파일이 없었으면 FirstRunPrompt, 있으면 Title.
    ///   첫 실행이고 사람이 조작하면(봇 입력이 아니면) 인트로 영상을 그 위에 먼저 튼다 — 끝나면 닫혀 FirstRunPrompt가 나온다.
    ///   인자: ScreenArgKeys.ProfileRestored(bool), ScreenArgKeys.PendingSettled(자동 정산 결과, 없으면 넣지 않음).
    /// - 다시 부르면 앞서 만든 GameObject(실행기, 카메라, 캔버스, EventSystem …)를 먼저 걷는다. 걷을 대상은 Build 동안
    ///   새로 생긴 장면 뿌리 객체 전부다(누가 만들었든) — 기반 작업이 무엇을 만드는지 여기서 알 필요가 없다.
    /// - ctx의 서비스가 null이어도(기반 작업이 아직 없을 때) 죽지 않는다.
    /// </summary>
    public static class Bootstrap
    {
        public const string MainSceneName = "Main";

        // 다시 부팅 때 걷을 것. 전역 서비스 창구가 아니다(GameContext는 BootResult로만 돌려준다).
        static BootResult _current;
        static readonly List<GameObject> _owned = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            _owned.Clear();
            AppFactory.ResetAppStatics();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (SceneManager.GetActiveScene().name != MainSceneName) return;
            // 전용 협동 서버(11-online.md 3단계): 화면·프로필 없이 서버만 세운다.
            if (!Application.isEditor && Wizard.Solo.Net.DedicatedServer.RequestedByCommandLine())
            {
                Wizard.Solo.Net.DedicatedServer.Launch(Wizard.Solo.Net.DedicatedServer.PortFromCommandLine(Wizard.Solo.Screens.Online.CoopLobbyModel.DefaultPort));
                return;
            }
            Boot(new BootOptions { BuildVersion = Application.version });
        }

        /// <summary>앱을 세운다. 앞서 세운 것이 있으면 먼저 걷는다.</summary>
        public static BootResult Boot(BootOptions options)
        {
            Shutdown();
            if (options == null) options = new BootOptions { BuildVersion = Application.version };

            var before = new HashSet<GameObject>(SceneRoots());
            BootResult boot;
            try
            {
                boot = AppFactory.Build(options);
            }
            finally
            {
                // Build가 중간에 실패해도 만든 것은 다음 Boot/Shutdown에서 걷히게 기억한다.
                foreach (var go in SceneRoots())
                    if (go != null && !before.Contains(go)) _owned.Add(go);
            }
            _current = boot;
            ApplySavedDisplay(boot);
            OpenFirstScreen(boot, options.InputOverride == null);
            return boot;
        }

        /// <summary>
        /// 저장된 창 모드·창 크기를 실행 파일이 뜰 때 적용한다(08 요약서 12절, D-50: 창 = 1920×1080의 정수 배, 전체 화면 = 기기 해상도).
        /// 설정 화면은 바꾸는 순간 적용하고, 다시 켰을 때는 여기서 한다. 에디터에서는 게임 창 크기를 건드리지 않는다.
        /// </summary>
        static void ApplySavedDisplay(BootResult boot)
        {
            if (!Application.isPlaying || Application.isEditor) return;
            var settings = boot != null && boot.Context != null ? boot.Context.Settings : null;
            if (settings == null || settings.Current == null) return;
            var dev = Screen.currentResolution;
            var t = Wizard.Solo.Screens.Settings.SettingsModel.TargetResolution(settings.Current, dev.width, dev.height);
            if (t.x <= 0 || t.y <= 0) return;
            bool full = Wizard.Solo.Screens.Settings.SettingsModel.IsFullscreen(settings.Current);
            Screen.SetResolution(t.x, t.y, full ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        /// <summary>첫 화면 인자(테스트용으로 공개).</summary>
        public static ScreenArgs FirstScreenArgs(BootResult boot)
        {
            var args = ScreenArgs.From(ScreenId.None).With(ScreenArgKeys.ProfileRestored, boot != null && boot.ProfileRestored);
            if (boot != null && boot.PendingSettled != null) args.With(ScreenArgKeys.PendingSettled, boot.PendingSettled);
            return args;
        }

        public static ScreenId FirstScreen(BootResult boot) =>
            boot != null && boot.FirstRun ? ScreenId.FirstRunPrompt : ScreenId.Title;

        /// <summary>첫 실행 인트로를 트는가: 첫 실행, 사람 입력, 영상 파일이 있을 때.</summary>
        public static bool PlaysIntro(BootResult boot, bool humanInput) =>
            humanInput && boot != null && boot.FirstRun
            && Wizard.Solo.Screens.Start.CinematicScreen.Available(Wizard.Solo.Screens.Start.CinematicScreen.IntroVideo);

        static void OpenFirstScreen(BootResult boot, bool humanInput)
        {
            var ctx = boot != null ? boot.Context : null;
            if (ctx == null || ctx.Screens == null) return;
            ctx.Screens.Push(FirstScreen(boot), FirstScreenArgs(boot));
            // 인트로는 첫 화면(연출 감도 물음) 위에 얹는다. 끝나거나 건너뛰면 닫혀 물음이 드러난다.
            if (PlaysIntro(boot, humanInput))
                ctx.Screens.Push(ScreenId.Cinematic,
                    Wizard.Solo.Screens.Start.CinematicScreen.ArgsFor(Wizard.Solo.Screens.Start.CinematicScreen.IntroVideo, ScreenId.None, null));
        }

        /// <summary>세운 것을 전부 걷는다(판을 끝내고 입력원을 놓고 GameObject를 부순다). 세운 것이 없으면 아무것도 안 한다.</summary>
        public static void Shutdown()
        {
            var ctx = _current != null ? _current.Context : null;
            _current = null;
            if (ctx != null)
            {
                try { if (ctx.Session != null) ctx.Session.EndRun(); }
                catch (Exception ex) { Debug.LogException(ex); }
                if (ctx.Input is IDisposable d)
                {
                    try { d.Dispose(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                // 화면을 먼저 닫는다: 화면의 OnExit(전투 화면이 입력 문을 되돌리는 것 등)이 아직 살아 있는 실행기를 부르게.
                // 실행기를 먼저 부수면 Battle.OnExit가 GameRunner.InputScreen을 건드려 "Init was not called" 예외가 난다.
                if (ctx.Screens is ScreenManager sm)
                {
                    try { sm.Teardown(true); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
            }
            AppFactory.ReleaseAppObjects();
            for (int i = 0; i < _owned.Count; i++)
                if (_owned[i] != null) UnityObjects.DestroyNow(_owned[i]);
            _owned.Clear();
        }

        /// <summary>불러온 장면 전부와 DontDestroyOnLoad 장면의 뿌리 객체.</summary>
        static List<GameObject> SceneRoots()
        {
            var list = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) list.AddRange(s.GetRootGameObjects());
            }
            if (Application.isPlaying)
            {
                // DontDestroyOnLoad 장면은 이름으로 찾을 수 없어, 잠깐 객체 하나를 넣어 그 장면을 얻는다.
                var probe = new GameObject("~BootstrapProbe");
                UnityEngine.Object.DontDestroyOnLoad(probe);
                var ddol = probe.scene;
                foreach (var go in ddol.GetRootGameObjects())
                    if (go != probe) list.Add(go);
                UnityEngine.Object.DestroyImmediate(probe);
            }
            return list;
        }
    }
}
