#nullable enable
using System;
using UnityEngine;
using Wizard.Client;
using Wizard.Client.Run;
using Wizard.Content;
using Wizard.Sim;
using Wizard.Unity.Input;

namespace Wizard.Unity.App.Core
{
    // 조립 루트(ARCHITECTURE 5). 앱에 하나뿐이고 장면을 넘어 산다(DontDestroyOnLoad). 장면의 시작 개체(Bootstrap)가 처음 만든다.
    // 여기서 만든 것: 단조 시계, Input System 입력원, 고정 틱 구동기, 장면 흐름, 기본 팩의 ContentDb. 전역 싱글턴은 두지 않는다
    // (찾는 쪽은 Ensure 로 장면에서 찾는다. 세계와 규칙은 받은 ContentDb 만 본다).
    // 렌더 프레임마다 Update 에서 FixedTickDriver.RunFrame 을 부른다. Time.timeScale 을 쓰지 않는다.
    //
    // 장면마다 [임시](OPEN-QUESTIONS):
    //   - Boot: 시작 화면·메인 메뉴(M6)가 아직 없으므로 곧바로 Battle 로 간다.
    //   - Battle: 고른 판이 없으면 연습 세션(허수아비 세계, 기록 없음)을 꽂는다. 일시정지 화면이 없으므로 Pause 키로 푼다(PauseKeyResume).
    //   - 그 밖의 장면: 세션을 뺀다.
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class CompositionRoot : MonoBehaviour
    {
        public const string ObjectName = "App";
        public const int ExecutionOrder = -1000;          // 장면의 다른 스크립트보다 먼저(표시기가 같은 프레임의 결과를 읽게)

        private IMonotonicClock? _clock;
        private UnityInputSource? _input;
        private FixedTickDriver? _driver;
        private SceneFlow? _flow;
        private ContentDb? _content;
        private string _contentMessage = string.Empty;
        private bool _contentReported;

        public IMonotonicClock Clock => _clock ?? throw NotComposed();

        public IInputSource Input => _input ?? throw NotComposed();

        public FixedTickDriver Driver => _driver ?? throw NotComposed();

        public SceneFlow Flow => _flow ?? throw NotComposed();

        // 기본 팩을 읽지 못했으면 null(ContentMessage 에 까닭)
        public ContentDb? Content => _content;

        public string ContentMessage => _contentMessage;

        // 있으면 그것을, 없으면 새로 만든다
        public static CompositionRoot Ensure()
        {
            CompositionRoot? existing = FindAnyObjectByType<CompositionRoot>();
            if (existing != null)
            {
                return existing;
            }
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            return go.AddComponent<CompositionRoot>();
        }

        private void Awake()
        {
            _clock = new InputSystemClock();
            _input = new UnityInputSource(KeyBindings.Default, _clock);
            _input.Enable();
            _driver = new FixedTickDriver(_clock, _input);
            _flow = new SceneFlow(new UnitySceneLoader());
            _flow.Entered += OnSceneEntered;
            ContentPackFile.TryLoadDefault(out _content, out _contentMessage);
        }

        private void Update()
        {
            FixedTickDriver? driver = _driver;
            if (driver == null)
            {
                return;
            }
            try
            {
                driver.RunFrame();
            }
            catch (Exception e)
            {
                // 세션의 결함. 같은 예외를 프레임마다 내지 않게 세션을 뺀다
                Debug.LogException(e);
                driver.SetSession(null);
            }
        }

        private void OnDestroy()
        {
            if (_flow != null)
            {
                _flow.Entered -= OnSceneEntered;
            }
            _driver?.SetSession(null);
            _input?.Dispose();
        }

        private void OnSceneEntered(GameScene scene)
        {
            FixedTickDriver driver = Driver;
            driver.SetSession(null);
            switch (scene)
            {
                case GameScene.Boot:
                    Flow.Request(GameScene.Battle);
                    break;
                case GameScene.Battle:
                    StartPractice(driver);
                    break;
            }
        }

        private void StartPractice(FixedTickDriver driver)
        {
            if (_content == null)
            {
                if (!_contentReported)
                {
                    Debug.LogError(_contentMessage);
                    _contentReported = true;
                }
                return;
            }
            var session = new PracticeSession(_content, new PracticeSetup(), Clock.NowSeconds);
            driver.SetSession(new PracticeTickSession(session, resumeOnPauseKey: true));
        }

        private static InvalidOperationException NotComposed() => new InvalidOperationException("CompositionRoot is not composed yet");
    }
}
