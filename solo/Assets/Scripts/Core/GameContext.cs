using System.Collections.Generic;
using System;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo
{
    /// <summary>
    /// 명시적 주입용 묶음. 전역 싱글턴·서비스 로케이터를 두지 않고, Bootstrap이 하나 만들어 화면·표시·메타 서비스에 넘긴다.
    /// 시뮬레이션(SimWorld)은 이것을 모른다(Content와 모듈 묶음만 받는다).
    /// 채우는 순서(AppFactory.Build): Clock → Settings → Content·Names → Art → Sound → Profiles → MetaFactory.Fill → Input → Session → Screens.
    /// </summary>
    public sealed class GameContext
    {
        public IClock Clock;
        public IContentDb Content;
        public INameTable Names;
        public IArtLibrary Art;
        public ISoundPlayer Sound;
        public IProfileStore Profiles;
        public ISettingsStore Settings;
        public IInputSource Input;
        public IScreenManager Screens;
        public IGameSession Session;
        /// <summary>온라인 판에 붙어 있으면 그 연결(11-online.md). null = 솔로. 온라인 판은 오프라인 프로필에 아무것도 남기지 않는다.</summary>
        public IOnlineLink Online;

        public IEnhanceService Enhance;
        public IWandService Wands;
        public IShopService Shop;
        public ISettlementService Settlement;

        /// <summary>판 밖에서 순수 규칙만 쓸 때의 모듈 묶음(주문 컴파일 미리보기, 트라이 설명, 점수 계산).</summary>
        public SimModuleSet Rules;
        /// <summary>프로필 종류. 화면은 이것만 보고 메뉴를 정한다(솔로는 항상 Offline).</summary>
        public ProfileKind Kind = ProfileKind.Offline;
        /// <summary>빌드 판 글자(타이틀 표시).</summary>
        public string BuildVersion;
        /// <summary>
        /// 저장 뿌리 폴더. null이면 Application.persistentDataPath. 프로필은 &lt;DataRoot&gt;/profiles/offline/, 설정은 &lt;DataRoot&gt;/settings.json.
        /// PlayMode 테스트는 임시 폴더를 준다(개발자의 진짜 저장 파일을 건드리지 않는다). F4의 저장소가 이 값을 읽는다.
        /// </summary>
        public string DataRoot;

        public ProfileData Profile => Profiles != null ? Profiles.Current : null;
    }

    /// <summary>
    /// 판 실행기. 구현: App/GameRunner(MonoBehaviour, F5). 누적기 방식으로 60Hz 고정 틱을 돌린다.
    /// 프레임마다: 입력 Poll → 쌓인 시간만큼 World.Step 반복 → 이벤트를 표시 층에 전달 → Render(alpha).
    /// 틱을 멈추는 것은 Paused뿐이다(야영 화면이 떠 있어도 틱은 간다. 그림 정지·흔들림은 표시만). 예외는 보조 판 하나:
    /// 보조 판(PushSubRun)이 떠 있는 동안 본판은 일시정지와 같이 멈춰 있다.
    ///
    /// 일시정지의 주인은 이 세션 하나다(00 문서 4.1절):
    /// - 진입점은 Paused의 set뿐이다. false → true가 될 때 세션이 활동 중인 플레이어마다 Arts.OnCommandReset(Pause)를 한 번 부르고,
    ///   true → false가 될 때 입력원을 Resync(swallowHeldActions: true)해 일시정지 중 뗀 키를 풀리는 틱의 뗌으로 넣는다.
    /// - 실행기는 화면을 열지 않는다. LogicalKey.Pause 누름은 시뮬레이션에 넣지 않고 PauseRequested로만 알린다.
    ///   Pause 화면을 여는 것은 그 판을 연 화면(Battle)이다. Esc는 메뉴 길(ScreenManager → IScreen.OnBack)로만 오고 입력원은 Esc를 내지 않는다.
    ///
    /// 전투 입력의 문(00 문서 4.1절): ctx.Screens.Top == InputScreen일 때만 입력원의 이벤트를 세계에 넣는다(InputScreen == None이면 항상).
    /// 다른 화면이 위에 뜨면 그 프레임에 IInputSource.ReleaseAll로 눌린 키를 전부 떼고, 그 뒤로는 Poll 해서 버린다.
    /// 다시 맨 위가 되면 Resync(swallowHeldActions: true) — 메뉴에서 누르고 있던 J·K가 발사·비기로 새지 않는다.
    /// 창이 초점을 잃은 동안도 문이 닫힌 것으로 본다.
    ///
    /// 그 밖의 약속(F5 구현과 같다):
    /// - 보조 판은 RunStarted·RunEnded를 내지 않는다(SimEventRaised만 온다). 본판 없이 PushSubRun을 부르면 InvalidOperationException.
    /// - EndRun은 Paused를 조용히 푼다(커맨드 초기화·Resync 없이 false로). 판이 없을 때는 PausedSeconds가 쌓이지 않는다.
    /// - 이벤트 처리기 안에서 EndRun·StartRun·PushSubRun·PopSubRun이 불리면 그 프레임의 남은 틱은 돌지 않는다.
    /// </summary>
    /// <summary>
    /// 온라인 판 연결(11-online.md 2절). 판은 서버가 돌리고, 이쪽은 꼭두각시 세계(<see cref="World"/>, Step하지 않는다)를 그린다.
    /// 세션이 60Hz 틱마다 내 입력·화면 명령을 <see cref="Send"/>로 넘기고 <see cref="Step"/>을 부른다.
    /// </summary>
    public interface IOnlineLink
    {
        /// <summary>판이 시작됐으면 꼭두각시 세계, 아니면 null.</summary>
        SimWorld World { get; }
        /// <summary>서버가 준 내 슬롯.</summary>
        int Slot { get; }
        bool Connected { get; }
        /// <summary>이번 틱의 내 입력과 화면 명령을 보낸다(슬롯은 서버가 정한다).</summary>
        void Send(List<InputEvent> inputs, List<RunCommand> commands);
        /// <summary>한 틱: 보내기 → (이 기기가 방장 서버면) 서버 틱 → 받기.</summary>
        void Step();
        /// <summary>받은 표시 이벤트를 넘긴다.</summary>
        void DrainEvents(List<SimEvent> into);
        /// <summary>판·방에서 나간다.</summary>
        void Leave();
        /// <summary>표시 보간 비(0~1). 세션이 Render의 alpha로 쓴다.</summary>
        float InterpAlpha { get; }
    }

    /// <summary>온라인 판을 돌릴 수 있는 세션(SessionCore·GameRunner).</summary>
    public interface IOnlineSession
    {
        bool IsOnline { get; }
        /// <summary>link의 꼭두각시 세계로 판을 연다(link.World가 있어야 한다). 이 세션은 Step하지 않고 link.Step을 부른다.</summary>
        void StartOnline(IOnlineLink link);
    }

    public interface IGameSession
    {
        /// <summary>지금 도는 세계(보조 판이 떠 있으면 보조 판. 없으면 null). 화면·표시 층은 이것을 쓴다.</summary>
        SimWorld World { get; }
        /// <summary>본판의 세계(보조 판이 떠 있어도 본판. 없으면 null).</summary>
        SimWorld MainWorld { get; }
        bool HasRun { get; }
        bool HasSubRun { get; }
        bool Paused { get; set; }
        /// <summary>지금 판(본판)의 누적 일시정지 시간(초). 일시정지 화면의 요약 판이 그린다(str.pause.pause_total).</summary>
        float PausedSeconds { get; }
        /// <summary>전투 입력을 받는 화면. 판을 연 화면이 OnEnter에서 자기 ScreenId를 넣는다(Battle, DummyPractice, Tutorial).</summary>
        ScreenId InputScreen { get; set; }
        /// <summary>새 세계를 만들고(SimModuleSet.CreateDefault) 판을 시작한다. 월드 표시 층도 여기서 세운다. 이미 판이 있으면 먼저 EndRun 한다.</summary>
        void StartRun(RunConfig cfg);
        /// <summary>세계와 월드 표시를 걷는다(정산 화면으로 넘어간 뒤). 보조 판이 떠 있으면 그것도 걷는다.</summary>
        void EndRun();
        /// <summary>
        /// 보조 판을 연다(야영의 "조립 시험" = 허수아비 연습). 본판 세계는 그대로 두고 멈추며(틱·이벤트 없음), 표시 층을 걷고
        /// 보조 세계로 새로 세운다. World는 보조 세계를 돌려주고 입력도 그쪽으로 간다. 보조 판은 하나만 뜬다.
        /// 보조 판의 RunEnded·LastSummary는 본판에 영향을 주지 않는다(RunEnded 이벤트를 내지 않는다).
        /// </summary>
        void PushSubRun(RunConfig cfg);
        /// <summary>보조 판을 걷고 본판의 표시 층을 다시 세운다(상태에서 다시 그린다). 본판 입력은 Resync(swallowHeldActions: true).</summary>
        void PopSubRun();
        /// <summary>RunPhase가 RunEnded가 된 본판의 요약(다음 StartRun까지 유지).</summary>
        RunSummary LastSummary { get; }
        /// <summary>입력원을 갈아 끼운다(봇·튜토리얼·테스트). null이면 ctx.Input.</summary>
        void SetInputOverride(IInputSource source);
        event Action<SimWorld> RunStarted;
        /// <summary>틱에서 나온 이벤트마다(표시 층에 넘긴 뒤). 화면이 판 상태 전이를 따라갈 때 쓴다. 보조 판의 이벤트도 온다(첫 인자로 구분).</summary>
        event Action<SimWorld, SimEvent> SimEventRaised;
        event Action<RunSummary> RunEnded;
        /// <summary>일시정지 키(LogicalKey.Pause, Esc가 아닌 재설정 키)가 눌렸다. 입력이 흐르는 동안에만 난다. 판을 연 화면이 OnBack과 같은 길로 처리한다.</summary>
        event Action PauseRequested;
    }

    /// <summary>부팅 선택지. Bootstrap(F5)과 PlayMode 테스트가 AppFactory.Build에 넘긴다.</summary>
    public sealed class BootOptions
    {
        public string BuildVersion;
        /// <summary>저장 뿌리 폴더(null = Application.persistentDataPath). 테스트는 임시 폴더.</summary>
        public string DataRoot;
        /// <summary>null이 아니면 ctx.Input을 이것으로 바꾼다(봇 입력).</summary>
        public IInputSource InputOverride;
        /// <summary>지난 실행의 중간 지점을 자동 정산한다(00 문서 D-23).</summary>
        public bool SettlePendingCheckpoint = true;
    }

    /// <summary>부팅 결과. Bootstrap이 첫 화면을 고를 때 쓴다.</summary>
    public sealed class BootResult
    {
        public GameContext Context;
        /// <summary>설정 파일이 없었다(기기 첫 실행 → FirstRunPrompt).</summary>
        public bool FirstRun;
        public bool ProfileCreated;
        /// <summary>프로필이 깨져 이전 세대로 되돌렸다(SaveRestored 창).</summary>
        public bool ProfileRestored;
        /// <summary>지난 실행이 비정상 종료여서 중간 지점까지 자동 정산했다(없으면 null). Bootstrap이 첫 화면의 인자
        /// ScreenArgKeys.PendingSettled로 넘기고 Title이 MainMenu로 넘겨 MainMenu가 한 번 알림 창을 띄운다.</summary>
        public SettlementResult PendingSettled;
    }
}
