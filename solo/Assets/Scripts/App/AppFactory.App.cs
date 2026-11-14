using UnityEngine;
using Wizard.Solo.App;

namespace Wizard.Solo
{
    /// <summary>F5의 등록 자리: 전투 입력원과 판 실행기(00 문서 2.1절). 다시 불리면 앞서 만든 것을 걷는다.</summary>
    public static partial class AppFactory
    {
        // 다시 부팅(PlayMode 테스트가 Boot를 여러 번 부른다) 때 앞의 것을 걷기 위한 기억. Bootstrap이 SubsystemRegistration 때 비운다.
        static KeyboardInputSource _lastKeyboard;
        static GameObject _lastRunnerGo;

        static partial void CreateInput(GameContext ctx, ref IInputSource input)
        {
            if (_lastKeyboard != null) { _lastKeyboard.Dispose(); _lastKeyboard = null; }
            var kb = new KeyboardInputSource(ctx.Settings, new InputSystemKeyReader());
            _lastKeyboard = kb;
            input = kb;
        }

        static partial void CreateSession(GameContext ctx, ref IGameSession session)
        {
            if (_lastRunnerGo != null) { UnityObjects.DestroyNow(_lastRunnerGo); _lastRunnerGo = null; }
            var go = new GameObject("GameRunner");
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<GameRunner>();
            runner.Init(ctx);
            _lastRunnerGo = go;
            session = runner;
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
