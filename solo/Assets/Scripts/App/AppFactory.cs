using UnityEngine;
using Wizard.Solo.App;
using Wizard.Solo.Art;
using Wizard.Solo.Audio;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Save;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo
{
    public static class AppFactory
    {
        public static BootResult Build(string buildVersion) => Build(new BootOptions { BuildVersion = buildVersion });

        public static BootResult Build(BootOptions options)
        {
            options ??= new BootOptions();
            var ctx = new GameContext
            {
                BuildVersion = options.BuildVersion,
                DataRoot = options.DataRoot,
                Clock = new SystemClock()
            };
            var boot = new BootResult { Context = ctx };

            ctx.Settings = new SettingsStore(SavePaths.SettingsFile(ctx.DataRoot));
            ctx.Settings.Load();
            boot.FirstRun = !ctx.Settings.ExistedAtLoad;

            ctx.Content = ContentDb.LoadFromResources();
            ctx.Names = NameTable.LoadFromResources();
            ctx.Art = new ArtLibrary();
            ctx.Sound = UnitySoundPlayer.Create(ctx);

            var content = ctx.Content;
            var clock = ctx.Clock;
            ctx.Profiles = new ProfileStore(
                SavePaths.ProfileDirectory(ctx.DataRoot),
                () => NewProfile.Create(content, clock),
                content?.DataVersion);
            ctx.Profiles.LoadOrCreate(out boot.ProfileCreated, out boot.ProfileRestored);

            ctx.Rules = SimModuleSet.CreateDefault();
            MetaFactory.Fill(ctx);
            ctx.Input = CreateInput(ctx);
            if (options.InputOverride != null) ctx.Input = options.InputOverride;
            ctx.Session = CreateSession(ctx);
            ctx.Screens = ScreenManager.Create(ctx);
            ScreenRegistry.RegisterAll(ctx.Screens, ctx);

            if (options.SettlePendingCheckpoint)
                boot.PendingSettled = ctx.Settlement.SettlePendingCheckpoint();

            return boot;
        }

        // 다시 부팅(PlayMode 테스트가 Boot를 여러 번 부른다) 때 앞의 것을 걷기 위한 기억. Bootstrap이 SubsystemRegistration 때 비운다.
        static KeyboardInputSource _lastKeyboard;
        static GameObject _lastRunnerGo;

        static IInputSource CreateInput(GameContext ctx)
        {
            if (_lastKeyboard != null) { _lastKeyboard.Dispose(); _lastKeyboard = null; }
            var kb = new KeyboardInputSource(ctx.Settings, new InputSystemKeyReader());
            _lastKeyboard = kb;
            return kb;
        }

        static IGameSession CreateSession(GameContext ctx)
        {
            if (_lastRunnerGo != null) { UnityObjects.DestroyNow(_lastRunnerGo); _lastRunnerGo = null; }
            var go = new GameObject("GameRunner");
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<GameRunner>();
            runner.Init(ctx);
            _lastRunnerGo = go;
            return runner;
        }

        /// <summary>앞서 만든 입력원·실행기를 걷는다(Bootstrap.Shutdown).</summary>
        internal static void ReleaseAppObjects()
        {
            if (_lastKeyboard != null) { _lastKeyboard.Dispose(); _lastKeyboard = null; }
            if (_lastRunnerGo != null) { UnityObjects.DestroyNow(_lastRunnerGo); _lastRunnerGo = null; }
            Wizard.Solo.Audio.UnitySoundPlayer.Release();   // "Sound" 오브젝트(AudioSource 풀)도 종료 때 걷는다.
        }

        /// <summary>정적 기억을 비운다(도메인 다시 읽기 없이 플레이를 다시 시작할 때).</summary>
        internal static void ResetAppStatics()
        {
            _lastKeyboard = null;
            _lastRunnerGo = null;
        }
    }
}
