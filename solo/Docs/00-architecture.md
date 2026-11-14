# 00. 구조 — 솔로 빌드의 코드 배치와 공용 계약 (정본)

게임: "엄청엄청 위대한 마법사 키우기". 순정 Unity 6000.3.20f1(2D URP) 솔로(오프라인) 빌드.
이 문서와 `Assets/Scripts/Core/**`의 코드가 구조의 정본이다. 기획 내용은 `01~10` 요약서가 정본이고, 둘이 다르면 **13절 확정 표**가 이 빌드의 값이다.

- 시간 1초 = 60틱, 거리 1칸 = 1 Unity unit = 64px, 열은 int HU(열 1 = 120 HU), 입력 시각은 u(1초 = 15,360u).
- 화면 글자는 이름 표에서만, 수치는 JSON에서만, id는 정본 id만(`Ids.cs`).
- 일정·범위 삭감을 말하지 않는다. 아래 "순서"는 의존 관계다.

---

## 1. 큰 그림

```
Bootstrap ──만든다──▶ GameContext(Content, Names, Art, Sound, Profiles, Settings, Input, Session, Screens, 메타 서비스)
   │
   ├ ScreenManager ─ IScreen 들(코드로 조립한 uGUI) ── 메타 서비스(IEnhanceService 등) ── ProfileStore(저장)
   │        │ RunConfig                      ▲ RunSummary
   │        ▼                                │
   └ GameRunner(IGameSession) ─60Hz─▶ SimWorld.Step() ──SimEvent──▶ 표시 층(IWorldViewLayer: 월드, HUD, 소리)
                                        │  상태(읽기 전용으로 봄) ────────────▲
                                        └ SimModuleSet(Player, Spells, Arts, Traits, Threats, Enemies, Bosses, Waves, Run, Score)
```

세 겹으로 나눈다.

| 겹 | 네임스페이스 | 엔진 의존 | 하는 일 |
|---|---|---|---|
| 시뮬레이션 | `Wizard.Solo.Sim` | `UnityEngine.Vector2`/`Mathf`만(MonoBehaviour·Time·Input·Resources 금지) | 판의 규칙 전부. EditMode 테스트로 검증 |
| 메타 | `Wizard.Solo.Meta` | 없음(파일 입출력은 Save만) | 프로필을 바꾸는 규칙: 강화, 상점, 지팡이·서고, 정산 |
| 표시·화면 | `Wizard.Solo.View`, `.UI`, `.Screens.*`, `.App`, `.Art` | 있음 | 그리기, 입력 전달, 화면 전이. 규칙을 갖지 않는다 |

멀티 이식을 위해: 시뮬레이션에 전역 상태 없음, 플레이어는 배열(`SimWorld.Players`, 슬롯 순), 화면 조작은 전부 `RunCommand`, 화면은 메타 인터페이스만 부르고 `GameContext.Kind`로만 프로필 종류를 가른다. 오프라인 프로필을 서버로 옮기는 코드는 만들지 않는다.

---

## 2. 폴더, 네임스페이스, 소유

한 작업은 자기 소유 폴더만 고친다. `Core/`는 설계자 것이라 구현 작업이 고치지 않는다(모자라면 결과 보고에 "계약 추가 요청"으로 적는다).

| 폴더 (`Assets/` 아래) | 네임스페이스 | 내용 | 작업 |
|---|---|---|---|
| `Scripts/Core/` | `Wizard.Solo`, `.Sim`, `.Meta`, `.UI`, `.View`, `.Data`(인터페이스), `.Art`(인터페이스) | **공용 계약**: id, 열거형, 상태, 이벤트, 모듈 기본 클래스, 서비스 인터페이스, 등록 자리 | 설계자 |
| `Scripts/Data/` (+ `Resources/Data/`) | `Wizard.Solo.Data` | Def 클래스, `ContentDb`, `NameTable`, JSON 사본과 솔로 보충 JSON | F1 |
| `Scripts/Art/` (+ `Editor/Art/`, `Resources/Art/`, `Resources/Shaders/`) | `Wizard.Solo.Art` | `ArtLibrary`, manifest 읽기, 스프라이트 캐시, **셰이더·머티리얼**, ArtSync, 임포트 설정 | F2 |
| `Scripts/UI/` | `Wizard.Solo.UI` | UiKit, 위젯, UiTheme, FocusGroup, MenuInput, ScreenBase, ScreenManager, 공용 창(ConfirmDialog, Tooltip, Loading, SaveRestored) | F3 |
| `Scripts/App/`, `Editor/Setup/`, `Scenes/` | `Wizard.Solo.App` | Bootstrap, GameRunner(IGameSession), KeyboardInputSource, BotInputSource, WorldViewHost, SpritePool, 카메라, ProjectSetup, Main.unity | F5 |
| `Scripts/Save/`, `Scripts/Audio/` | `Wizard.Solo.Save`, `Wizard.Solo.Audio` | ProfileStore, SettingsStore, 새 프로필, 시계, 무음 재생기 | F4 |
| `Scripts/Sim/Player/` | `Wizard.Solo.Sim.Player` | PlayerModule 구현 | G01 |
| `Scripts/Sim/Spells/` | `Wizard.Solo.Sim.Spells` | SpellModule 구현 | G02 |
| `Scripts/Sim/Arts/` | `Wizard.Solo.Sim.Arts` | ArtModule 구현 | G03 |
| `Scripts/Sim/Traits/` | `Wizard.Solo.Sim.Traits` | TraitModule 구현 | G04 |
| `Scripts/Sim/Threats/` | `Wizard.Solo.Sim.Threats` | ThreatModule 구현 | G05 |
| `Scripts/Sim/Enemies/` | `Wizard.Solo.Sim.Enemies` | EnemyModule 구현 | G06 |
| `Scripts/Sim/Bosses/` (`Common`, `KilnBoar`, `MirrorPhantom` / 나머지 넷) | `Wizard.Solo.Sim.Bosses` | BossModule 구현 | G07, G08 |
| `Scripts/Sim/Waves/` | `Wizard.Solo.Sim.Waves` | WaveModule 구현 | G09 |
| `Scripts/Sim/Run/` | `Wizard.Solo.Sim.Run` | RunModule 구현 | G10 |
| `Scripts/Sim/Score/` | `Wizard.Solo.Sim.Score` | ScoreRules 구현(시뮬레이션 쪽에 둔다 — Sim이 Meta 구현에 기대지 않게) | G11 |
| `Scripts/Meta/Settlement/` | `Wizard.Solo.Meta.Settlement` | ISettlementService | G11 |
| `Scripts/Meta/Enhance/` | `Wizard.Solo.Meta.Enhance` | IEnhanceService | G12 |
| `Scripts/Meta/Inventory/`, `Scripts/Meta/Shop/` | `Wizard.Solo.Meta.Inventory`, `.Shop` | IWandService, IShopService | G13 |
| `Scripts/View/World/` | `Wizard.Solo.View.World` | 전장, 시간대, 날씨, 캐릭터, 지팡이 | G14 |
| `Scripts/View/Enemies/` | `Wizard.Solo.View.Enemies` | 적, 보스, 예고, 적 탄, 남는 구역 | G15 |
| `Scripts/View/Effects/` | `Wizard.Solo.View.Effects` | 내 탄, 적중·폭발, 비기 연출, 피해 숫자, 드랍, 소리 연결 | G16 |
| `Scripts/View/Hud/` | `Wizard.Solo.View.Hud` | 전투 HUD, 캐릭터 둘레 표시 | G17 |
| `Scripts/Screens/<묶음>/` | `Wizard.Solo.Screens.<묶음>` | 화면들(8절 표의 묶음) | G18~G25 |
| `Tests/EditMode/<모듈>/`, `Tests/PlayMode/<모듈>/` | `Wizard.Solo.Tests.*` | 그 모듈 작업의 테스트 | 모듈 작업 |
| `Tools/` | — | `compile.sh`(Unity 없이 컴파일), `artsync.sh`(Unity 없이 그림 복사), `unity-compile.sh`·`unity-test-edit.sh`·`unity-test-play.sh`(Unity 배치 모드 래퍼 — 3.1절) | 설계자, 통합 |

어셈블리(asmdef)는 넷: `Wizard.Solo`(Assets/Scripts), `Wizard.Solo.Editor`(Assets/Editor), `Wizard.Solo.Tests.EditMode`, `Wizard.Solo.Tests.PlayMode`.

### 2.1 등록 자리 — 파일이 겹치지 않게 모듈을 끼우는 법

구현 클래스를 한곳에서 `new` 하면 그 파일을 여럿이 고치게 된다. 그래서 `partial` 메서드를 쓴다. 구현이 없는 partial 메서드는 컴파일 때 사라지므로 **어느 시점에도 전체가 컴파일된다**.

| 자리 (Core) | 구현 작업이 자기 폴더에 두는 파일 | 본문 |
|---|---|---|
| `Sim.SimModuleFactory` (`Core/Sim/Modules.cs`) | `Scripts/Sim/Player/SimModuleFactory.Player.cs` | `static partial void CreatePlayer(ref PlayerModule m) { m = new Player.PlayerSystem(); }` |
| `Meta.MetaFactory` (`Core/Meta/MetaContracts.cs`) | `Scripts/Meta/Enhance/MetaFactory.Enhance.cs` | `static partial void CreateEnhance(GameContext ctx, ref IEnhanceService s) { s = new Enhance.EnhanceService(ctx); }` |
| `AppFactory` (`Core/GameContext.cs`) | `Scripts/Save/AppFactory.Save.cs` 등 | `static partial void CreateProfiles(GameContext ctx, ref IProfileStore p) { p = new Save.ProfileStore(…); }` — 기반 서비스(시계, 설정, 콘텐츠, 그림, 소리, 프로필, 입력, 세션, 화면 관리자) |
| `UI.ScreenRegistry` (`Core/UI/ScreenContracts.cs`) | `Scripts/Screens/Start/ScreenRegistry.Start.cs` | `static partial void RegisterStart(IScreenManager m, GameContext ctx) { m.Register(ScreenId.Title, () => new TitleScreen()); … }` |
| `View.ViewRegistry` (`Core/View/ViewContracts.cs`) | `Scripts/View/Hud/ViewRegistry.Hud.cs` | `static partial void AddHud(List<IWorldViewLayer> layers) { layers.Add(new HudLayer()); }` |

partial 파일의 네임스페이스는 자리의 네임스페이스(`Wizard.Solo.Sim` 등)와 같아야 한다.

### 2.2 이름 충돌을 피하는 규칙

`Wizard.Solo.*` 안에서는 `UI.Text`, `Editor.X` 같은 부분 한정 이름이 `UnityEngine.UI`·`UnityEditor`가 아니라 `Wizard.Solo.UI`·`Wizard.Solo.Editor`로 풀린다. 그래서:

- `Wizard.Solo.UI`에 만드는 형은 **`Ui` 접두사**를 붙인다(`UiButton`, `UiLabel`, `UiKit` …). `UnityEngine.UI`에 있는 이름(`Button`, `Image`, `Text`, `Slider`, `Toggle`, `Outline` …)과 같은 이름의 형을 만들지 않는다.
- 코드에서 `UI.Text`처럼 네임스페이스 일부만 적는 이름을 쓰지 않는다(`using UnityEngine.UI;` 뒤 `Text`, 또는 전체 이름).
- 의존 방향: `Sim` 네임스페이스는 `Meta`·`UI`·`View`를 참조하지 않는다(`RunConfig.Loadout`은 Sim의 `LoadoutSpec`). `Meta`는 `Sim`의 형을 쓴다. 한 어셈블리지만 시뮬레이션만 떼어 내 옮길 수 있게 지킨다.

---

## 3. 컴파일 도구

`Tools/compile.sh [-o 출력폴더] [-f 경로필터] [-w]` — Unity를 띄우지 않고 Unity 설치본의 Roslyn(`DotNetSdkRoslyn/csc.dll`, C# 9)으로 네 어셈블리를 차례로 컴파일하고 오류만 보여 준다.

- 출력 폴더를 안 주면 임시 폴더를 새로 만든다 → 여러 작업이 동시에 돌려도 겹치지 않는다.
- `-f Assets/Scripts/Sim/Player/,Assets/Tests/EditMode/Player/`처럼 주면 그 경로의 진단만 보이고 끝 코드도 그 범위만 본다(쉼표로 여러 개). **폴더 경계로 맞춘다**(`…/EditMode/Art`는 `…/EditMode/Arts/`를 잡지 않는다).
- **끝 코드: 0 = 확인됨 / 1 = 필터 안에 확실한 오류 / 2 = 도구 문제 / 3 = 미확인.** 3은 통과가 아니다: 필터에 걸리는 단위가 컴파일되지 못했거나(앞 단위가 통째로 깨짐), 남의 깨진 파일을 빼고 다시 컴파일했더니 필터 안에 진단이 나온 경우다. 다시 돌려 0이 나와야 끝이다. 끝내 3이면 결과 보고에 "확인하지 못함"으로 적는다.
- 남의 파일이 깨져 있어도 되도록 끝까지 본다: 한 단위가 dll을 못 만들면 오류 난 파일을 빼고 다시 컴파일해(최대 4번) 뒤 단위(테스트)를 계속 컴파일한다. 모든 파일로 컴파일한 1차 진단은 "확실", 그 뒤의 진단은 "불확실"로 따로 나온다. 요약 줄에 단위별 상태(`ok / reduced / failed / skipped`)가 나온다.
- 정의: 런타임(`Assets/Scripts`)과 PlayMode 테스트는 `UNITY_EDITOR` 없이, `UnityEditor` 참조 없이 컴파일한다(플레이어에서도 도는 코드만). `Assets/Editor`와 EditMode 테스트는 `UNITY_EDITOR`를 정의하고 에디터 참조를 준다.
- 덧검사(컴파일러가 못 잡는 규칙. `error SOLOnnnn`): `SOLO0001` 런타임 코드의 `#if UNITY_EDITOR` / `SOLO0002` `Shader.Find`(Art 폴더 밖) / `SOLO0003` 옛 입력(`Input.GetKey` 등, `StandaloneInputModule`).
- 참조는 asmdef와 같다(`UnityEngine.UI`, `Unity.InputSystem`, Newtonsoft). URP 어셈블리는 asmdef에도 여기에도 넣지 않는다(7절 "평범한 Camera만"). 둘은 항상 같이 고친다.
- 병렬 단계에서는 이것만 쓴다. Unity 배치 모드(EditMode/PlayMode 테스트, 빌드)는 한 번에 한 프로세스라 통합 단계에서만 돈다.
- 설계자가 확인한 것: 공용 계약 + Def 껍데기 + 본보기 테스트가 오류 0, 경고 0으로 컴파일됨. 일부러 깨뜨려 끝 코드 1·3, 폴더 경계, 덧검사 셋, 깨진 파일 빼고 이어 가기를 확인함. 테스트 실행(Unity 러너)은 통합 단계에서 3.1절의 래퍼로 한다.

### 3.1 Unity 배치 모드 래퍼 (통합 단계)

Unity는 이 프로젝트에 한 번에 한 프로세스만 뜬다. 래퍼는 이미 떠 있으면 끝 코드 2로 거절한다. 로그·결과는 `Logs/unity/`(git 무시), 스크린숏은 `Logs/shots/`.

| 스크립트 | 하는 일 | 끝 코드 |
|---|---|---|
| `Tools/unity-compile.sh [-m 메서드] [-l 로그]` | 프로젝트를 열어 진짜 컴파일·임포트를 확인한다(`-quit`). `-m Wizard.Solo.Editor.ProjectSetup.Run`(장면·빌드 설정), `-m Wizard.Solo.Editor.ArtSync.Run`(그림 복사)처럼 에디터 메서드 하나를 같이 돌릴 수 있다 | 0 = 오류 없음 / 1 = 컴파일 오류·메서드 실패 / 2 = 도구 문제 |
| `Tools/unity-test-edit.sh [-f 거르개] [-c 분류] [-r xml] [-l 로그]` | EditMode 테스트(`Wizard.Solo.Tests.EditMode` 어셈블리만). **`-quit`을 붙이지 않는다** | 0 = 1개 이상 돌았고 전부 통과 / 1 = 실패·컴파일 오류 / 2 = 도구 문제 / 3 = 결과 없음(통과가 아니다) |
| `Tools/unity-test-play.sh [같은 인자]` | PlayMode 테스트(`Wizard.Solo.Tests.PlayMode`만). 화면을 실제로 그리므로 `-nographics` 없이 돈다 | 위와 같다 |

`-assemblyNames`로 우리 어셈블리만 돌린다(`Packages/manifest.json`의 `testables`에 Input System이 있어 그냥 돌리면 패키지 테스트까지 돈다). 결과 XML을 읽어 실패한 테스트의 이름·메시지와 `total/passed/failed/skipped` 한 줄을 찍는다.

NUnit은 테스트 클래스의 객체 하나로 그 클래스의 테스트를 전부 돌린다. 필드에 든 목록·상태는 `[SetUp]`에서 비운다(Unity 밖의 반사 실행기는 테스트마다 새 객체를 만들어 이 잘못이 보이지 않는다).

---

## 4. 부팅과 주입

장면은 `Assets/Scenes/Main.unity` 하나(빈 장면). `App.Bootstrap`의 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`가 `Bootstrap.Boot(new BootOptions { BuildVersion = Application.version })`을 부르고, `Boot`는 `AppFactory.Build(options)`(Core, 아래 1~9를 한다) 뒤 10을 한다. 서비스 구현은 각 기반 작업이 `AppFactory`의 partial 메서드로 끼운다.

**자동 부팅은 활성 장면의 이름이 `Main`일 때만 한다.** PlayMode 테스트(테스트 러너가 만든 `InitTestScene…`)에서는 자동 부팅이 돌지 않고, 테스트가 `Bootstrap.Boot(new BootOptions { DataRoot = <임시 폴더>, InputOverride = <봇> })`을 직접 부른다 → 개발자의 진짜 저장 파일(`persistentDataPath`)을 읽지도 덮어쓰지도 않고, 첫 실행 여부가 기기 상태에 흔들리지 않는다. `Boot`는 만든 `BootResult`를 돌려주고, 다시 부르면 앞서 만든 GameObject(캔버스, 실행기, EventSystem)를 먼저 걷는다. 저장 경로는 `ctx.DataRoot`(null = `Application.persistentDataPath`) 아래다.

```
1. GameContext ctx 생성(BuildVersion, DataRoot), Clock
2. Settings.Load()            (파일이 없었으면 기기 첫 실행)
3. ContentDb.LoadFromResources(), NameTable.LoadFromResources()
4. ArtLibrary(Resources/Art), SilentSoundPlayer
5. ProfileStore.LoadOrCreate(out created, out restored)
6. ctx.Rules = SimModuleSet.CreateDefault();  MetaFactory.Fill(ctx)
7. KeyboardInputSource(설정의 키 배치. options.InputOverride가 있으면 그것), GameRunner(IGameSession), 카메라, HUD 캔버스, 화면 캔버스 + EventSystem, ScreenManager
8. ScreenRegistry.RegisterAll(ctx.Screens, ctx)
9. boot.PendingSettled = Settlement.SettlePendingCheckpoint()  (지난 실행이 비정상 종료였으면 자동 정산한 결과)
10. 첫 화면(Bootstrap): 설정 파일이 없었으면 FirstRunPrompt, 있으면 Title. 인자에 ScreenArgKeys.ProfileRestored·PendingSettled를 실어 보낸다
    (Title이 SaveRestored 창을 띄우고, PendingSettled는 MainMenu까지 넘겨 MainMenu가 알림 창을 한 번 띄운다)
```

`GameContext`가 유일한 주입 통로다. 정적 싱글턴을 두지 않는다. 시뮬레이션은 `GameContext`를 모른다(`SimWorld(IContentDb, SimModuleSet, seed)`).

`IGameSession`(= GameRunner): `StartRun(RunConfig)`가 `new SimWorld(ctx.Content, SimModuleSet.CreateDefault())` → `world.StartRun(cfg)` → `ViewRegistry.CreateAll()`로 표시 층을 세운다. 프레임마다:

```
Update:
  if Paused: PausedSeconds += unscaledDeltaTime; 표시만 Render(alpha, dt = 0); return
  w = 지금 도는 세계(보조 판이 떠 있으면 보조 세계)
  open = (InputScreen == None) || (ctx.Screens.Top == InputScreen)          // 전투 입력의 문
  if open이 방금 닫힘: input.ReleaseAll(simTimeU, events)                     // 눌린 키를 전부 뗌으로
  if open이 방금 열림: input.Resync(simTimeU, events, swallowHeldActions: true)
  input.Poll(simTimeU, events)          // simTimeU = 누적 시뮬레이션 시간 × 15,360. 문이 닫혀 있으면 받은 것을 버린다
  LogicalKey.Pause 누름 → 세계에 넣지 않고 PauseRequested 이벤트만 낸다(문이 열려 있을 때)
  나머지 → w.EnqueueInput(0, e)
  accumulator += Time.unscaledDeltaTime (최대 0.25초)
  while accumulator >= 1/60: w.Step(); w.DrainEvents(buf); accumulator -= 1/60
  buf의 이벤트를 차례로 layer.OnEvent(...) 와 SimEventRaised 로
  본판의 RunPhase == RunEnded 가 되면 LastSummary = Run.BuildSummary(world), RunEnded 이벤트(한 번)
  layer.Render(w, alpha = accumulator × 60, dt)
```

틱을 멈추는 것은 일시정지뿐이다. 야영·전리품 화면이 떠 있어도 틱은 간다(점수용 `WaveState.WaveTick`만 멈춘다). 그림 정지·화면 흔들림은 표시 층 안에서만.

### 4.1 일시정지, 전투 입력의 문, 보조 판 — 주인은 세션 하나

**일시정지.** 진입점은 `IGameSession.Paused`의 set 하나다.

- `false → true`: 세션이 활동 중인 플레이어마다 `Arts.OnCommandReset(p, Pause)`를 한 번 부른다(T-F19). `true → false`: `input.Resync(…, swallowHeldActions: true)` — 일시정지 중 뗀 키는 풀리는 틱의 뗌으로 들어가고, 그동안 새로 누른 J·K는 뗐다 다시 누를 때까지 삼킨다.
- **실행기는 화면을 열지 않는다.** Pause 화면을 여는 것은 판을 연 화면(Battle)뿐이다: `BattleScreen.OnBack`(Esc)과 `IGameSession.PauseRequested`(재설정한 일시정지 키) 둘 다 같은 메서드로 가서, 되비침·쓰러짐(`PlayerState.InputLocked`) 중이 아니면 `Paused = true` + `Push(Pause)`.
- **Esc는 메뉴 길 하나로만 온다**(`MenuInput` → `ScreenManager` → 맨 위 화면의 `OnBack`). 전투 입력원은 Esc를 `LogicalKey.Pause`로 내지 않는다(Esc는 일시정지의 고정 키이고 설정의 키 칸에 넣지 않는다). 그래서 한 번의 Esc가 두 길로 들어와 Pause가 두 번 쌓이거나 열자마자 닫히는 일이 없다.
- Pause 화면: [계속]과 Esc = `Paused = false` + Pop. DummyPractice의 Esc = 나가기, Tutorial의 Esc = 건너뛰기 확인 창(둘은 일시정지 화면을 쓰지 않는다).
- 누적 일시정지 시간은 `IGameSession.PausedSeconds`(일시정지 화면의 판 요약이 `str.pause.pause_total`로 그린다). 순위 판용 10분 안내(`pause_limit_note`)만 그리지 않는다(D-11).

**전투 입력의 문.** 메뉴 키(J/K/방향)와 전투 키는 같은 물리 키다. 화면이 떠 있는 동안 누른 키가 시뮬레이션으로 새지 않게, 실행기는 `ctx.Screens.Top == session.InputScreen`일 때만 입력을 세계에 넣는다. 판을 연 화면(Battle, DummyPractice, Tutorial)이 `OnEnter`에서 `InputScreen`에 자기 id를 넣고 `OnExit`에서 앞의 값으로 되돌린다. 다른 화면(전리품, 야영, 일시정지, 확인 창)이 위에 뜨면 그 프레임에 `ReleaseAll`로 눌린 키가 전부 떼어지고(캐릭터가 멈춘다), 닫히면 `Resync(swallowHeldActions: true)` — 전리품 확정의 J가 야영 모닥불의 J로 이어지지 않는다. 창이 초점을 잃을 때(`OnApplicationFocus(false)`)도 `ReleaseAll`, 돌아오면 `Resync`. `Keyboard.current`가 null이어도(배치 모드) 죽지 않는다.

**보조 판.** 야영의 "조립 시험"(허수아비 연습)은 판 중에 두 번째 세계가 필요하다. `PushSubRun(cfg)`는 본판 세계를 그대로 둔 채 멈추고(틱·이벤트 없음 — 일시정지와 같다), 표시 층을 걷어 보조 세계로 새로 세우고, `World`와 입력을 보조 세계로 돌린다. `PopSubRun()`은 보조 세계를 버리고 본판의 표시 층을 다시 세운다(층은 이벤트 없이 지금 상태만 보고 그릴 수 있어야 한다 — `IWorldViewLayer.Build`의 약속). 메인 메뉴의 연습은 `StartRun`, 야영의 연습은 `PushSubRun`이고 화면(DummyPractice)은 같은 하나다(TV-082).

**세션의 그 밖의 약속**(`IGameSession` 주석이 정본): 보조 판은 `RunStarted`·`RunEnded`를 내지 않는다(`SimEventRaised`만, 첫 인자로 구분). 본판 없이 `PushSubRun`을 부르면 `InvalidOperationException`. `EndRun`은 `Paused`를 조용히 푼다(커맨드 초기화·Resync 없음). 판이 없을 때는 `PausedSeconds`가 쌓이지 않는다. 이벤트 처리기 안에서 `EndRun`·`StartRun`·`PushSubRun`·`PopSubRun`이 불리면 그 프레임의 남은 틱은 돌지 않는다. 창이 초점을 잃은 동안은 전투 입력의 문이 닫힌 것으로 본다.

---

## 5. 시뮬레이션

### 5.1 틱 처리 순서 (`SimWorld.Step`, 이것 하나뿐)

| 단계 | 부르는 것 | 내용 |
|---|---|---|
| 0 | `Run.Stage0_Commands` | 화면 명령(전리품, 야영, 조립, 비기 선택, 출발, 포기) 적용 |
| 1 | `Player.Stage1_Timers` | 과열 잠금 풀림(t ≥ UnlockTick → 열 0) |
| 2 | `Player.Stage2_Input` | 이 틱의 입력을 정렬(시각, 분류: 자세·반전 0 → 방향 1 → J 2 → K 3, 순번)해 적용: 키 상태 → 자세 → 시야 → `Arts.OnCommandReset/OnDirectionPress/OnSkillPress` |
| 3 | `Player.Stage3_ResolveFacing` | 대각 시야 유지 창 만료 |
| 4 | `Player.Stage4_Move` | 이동, 벽, 장애물 |
| 5 | `Arts.Stage5_Arts` | 보관된 K, 준비 시간 끝난 비기, 만개 반복, 불가마·사본·병풍 갱신 |
| 6 | `Player.Stage6_Fire` | J: 면제 → 임계 판정 → 불가마 흡수 → 열 → 잠금 → `Spells.Execute` → `Arts.OnPlayerFired` |
| 7 | `Waves.Stage7_Spawns` → `Enemies.Stage7_Ai` → `Bosses.Stage7_Ai` | 묶음 방출, AI(id 오름차순), 예고는 **요청**만 |
| 8 | `Threats.Stage8_Queue` | 동시 예고 상한·R5 대기 줄 → 허가된 요청이 구역이 됨 |
| 9 | `Enemies.Stage9_Move` → `Spells.Stage9_Projectiles` → `Threats.Stage9_Shots` → `Threats.Stage9_Zones` → `Player.Stage9_GrazeAndPickups` | 적 이동·돌진·접촉 → 내 탄↔적 → 적 탄↔플레이어 → 판정 틱 구역·남는 구역 → 피격 판정 **뒤** 스침(`p.HasPendingHit`이면 그 틱의 스침은 전부 버린다). (줍기는 주인 모듈이 11단계에서: 별똥 = Waves, 체력 조각 = Run) |
| 10 | `Player.Stage10_ResolveHits` → `Enemies.Stage10_Deaths` → `Bosses.Stage10_AfterDamage` | 피격 확정(같은 틱이면 플레이어 피해가 먼저) → 적 사망 처리 → 보스 단계·첫 만남 종료 조건 |
| 11 | `Traits.Stage11_TickEnd` → `Player.Stage11_TickEnd` → `Waves.Stage11_TickEnd` → `Run.Stage11_Phase` | 탐식 창 → 냉각·콤보 타이머 → 웨이브 시계·지체·별똥·날씨 → 판 상태 전이 |
| 12 | `Compact()`, `Tick++` | Dead 개체 제거 |

같은 단계 안에서 개체는 id 오름차순, 플레이어는 슬롯 순. 모듈은 `Stage*`를 직접 부르지 않는다.

상태별 가동(`SimWorld`의 편의 속성): `CombatActive`(WaveCombat, Practice, PhantomFinale) — 적 AI·방출·내 탄 피해가 돈다. `PlayerDamageActive`(WaveCombat, Practice) — 플레이어가 피해를 받는다(WaveClearing부터 없음). `PlayerControlActive` — 이동·시야(전리품 고르기, 야영 화면이 열린 동안 — Camp와 StartCamp 둘 다 —, 판 종료는 아님). `InCamp` — Camp 또는 StartCamp. `AttackAllowed(p)` — 발사·비기가 되는가: `WavePrelude`·`WaveCombat`·`Practice`이고 `AttackLocked`(보스 등장 1.2초, 주인 Run)·`InputLocked`(되비침·쓰러짐, 주인 Bosses)·`Downed`가 아니다. `Player.Stage6_Fire`와 `Arts.OnSkillPress`·`Stage5_Arts`가 이것 하나로 본다(막힌 K는 결정을 쓰지 않고 버린다). 야영(`FireIdle`)의 J는 모닥불·출발 표식 상호작용 전용이다(D-49).

### 5.2 상태 구조와 주인

상태는 `Core/Sim/States.cs`, `RunTypes.cs`, `SpellTypes.cs`에 있다. **읽기는 누구나, 쓰기는 주인 모듈만.** 남의 상태를 바꿔야 하면 주인 모듈의 메서드를 부른다.

| 상태 | 형 | 주인(쓰는 모듈) | 내용 |
|---|---|---|---|
| `Players[i]` | `PlayerState` | Player (단, `Derived`·`Wand.TempStageDrop` = Traits / `ArtScratch`·`KilnAbsorbLeftHu`·`InKilnZone`·`UnlockedArts` = Arts / `TraitScratch` = Traits / `Loadout` = Run / `EnvSpeedMul` = Threats / `AttackLocked` = Run만 / `InputLocked` = Bosses만) | 위치, 시야, 자세, 키, 체력, 무적, 열, 잠금, 스침, 콤보, 결정, 빚, 조립, 컴파일된 주문 |
| `Enemies` | `EnemyState` | Enemies (보스 몸통은 Bosses. `Hp`·`PendingDeath`·`KillingHit` = Spells의 피해 적용 / `Slows`·`CorrosionUntilTick`·기절·빙결은 `Enemies.Apply*` 로만) | 적·엘리트·졸개·보스 몸통 |
| `Boss` | `BossState` | Bosses(만들기 `SpawnBoss`, 걷기 `Dismiss` → null. `State`·`StateStartTick`의 시계는 Bosses만 센다) | 단계, 격노, 싸움 상태, 첫 만남 |
| `Projectiles` | `ProjectileEntity` | Spells | 플레이어 탄(사본 마법진 포함) |
| `EnemyShots`, `Zones`, `Hazards`, `Obstacles` | `EnemyShotEntity` 등 | Threats | 적 탄, 예고 구역, 남는 구역, 장애물 |
| `PlayerZones` | `PlayerZoneEntity` | Arts | 불가마, 사본 마법진, 얼음 병풍 |
| `Drops` | `DropEntity` | Run | 체력 조각, 재료 꾸러미(연출), 골드 조각(연출) |
| `Wave` | `WaveState` | Waves (`Stats`: Wave·IsBoss·PhantomFirst·Whims = `Waves.BeginWave` / 처치·무피격·스침 = Run의 훅 / ElapsedTicks = Run이 클리어 처리에서) | 웨이브 시계, 묶음, 변덕 값, 통계 |
| `Starfall`, `Weather` | | Waves | 별똥, 날씨·소품 배치 |
| `Run` | `RunState` | Run (단, `AffinityKnown`에 더하기 = Spells의 피해 함수) | 판 상태 기계, 지갑, 점수, 보관함, 서고 블록 대기 칸, 전리품, 야영(모닥불·출발 표식 자리), 기회 목록, 복수 중 여부, 연습 옵션, 튜토리얼 단계 |

`PlayerDerived`(임계 시작, 잠금 틱, 냉각, 감쇠, 강화 배율, 변이 플래그 …)는 Traits가 판 시작·임시 하락 때 채우고 나머지 모듈은 읽기만 한다 → 캐릭터·지팡이 특성·변이 분기를 한곳에 모은다. 만드는 함수는 `Traits.ComputeDerived(db, 캐릭터, 실효 단계, 변이)` 하나이고(순수, 판 밖에서도 쓴다), 주문 컴파일 문맥은 `Traits.CompileContext(derived, 연결 칸 수)` 하나다 — 판(G01), 출발 검사(G13), 서고·야영 화면의 미리보기와 수치판(G21)이 전부 `ctx.Rules.Traits`의 이 두 함수로만 만든다.

### 5.3 모듈 계약 (`Core/Sim/Modules.cs`)

모듈마다 가상 메서드가 전부 빈 본문인 **기본 클래스**가 있다. 구현은 자기 폴더에서 상속한다. 기본 클래스 그대로가 빈 구현이므로 `SimModuleSet.Stubs()`에 자기 모듈만 갈아 끼워 테스트한다.

| 모듈 | 기본 클래스 | 맡는 것 | 요약서 |
|---|---|---|---|
| Player | `PlayerModule` | 입력 적용, 이동·시야·자세, 체력·피격·무적, 과열, 스침 보상, 콤보, 결정·빚 | 02, 10(1·2절) |
| Spells | `SpellModule` | 컴파일러, `Execute`, 내 탄 이동·충돌, **피해 공식과 적용**, 표준 T, 조립 수치 | 03 |
| Arts | `ArtModule` | 트라이·판정기·결제, 비기 15종 효과, 플레이어 지대, 해금 차례 | 04 |
| Traits | `TraitModule` | `PlayerDerived`, 패시브 5, 지팡이 특성 5, 변이 6 | 02(8·9절) |
| Threats | `ThreatModule` | 예고 수명, 동시 예고 상한·대기 줄, R5, 적 탄, 남는 구역, 장애물, 받는 피해 칸 계산 | 05(1~3절) |
| Enemies | `EnemyModule` | 일반 12종 + 졸개 + 엘리트 AI, 이동, 접촉, 꼬리표별 군중 제어, 사망 | 05(4~7절) |
| Bosses | `BossModule` | 보스 6종, 격노, 단계, 허상 첫 만남·복수 | 05(8~10절) |
| Waves | `WaveModule` | 곡선(H, 예산), 구성 생성, 묶음 방출, 변덕, 지체 방지, 별똥, 날씨 | 06(1~3절), 01(7절) |
| Run | `RunModule` | 판 상태 기계, 전리품, 야영, 지갑, 판 중 드랍, 기회 목록, 연습·튜토리얼 방 | 06(4절), 01 |
| Score | `ScoreRules` | 웨이브 점수, 건너뛴 점수 | 01(6절) |

### 5.4 모듈 간 호출 규칙

모듈끼리는 `w.Mods.<모듈>.<메서드>`로만 부른다. 구현 클래스를 직접 참조하지 않는다. 이벤트(`SimEvent`)는 표시 전용이고 규칙 연결에 쓰지 않는다.

**(가) 발사** — `Player.Stage6_Fire`:
발사가 실제로 나갈 조건(J, 간격, 잠금 아님, `w.AttackAllowed(p)`, 유효한 주문)을 다 통과한 뒤에만: `Traits.RollUnstableCore`(정확히 한 번. 자폭이면 탄·열 없이 `EnterLock(…, fromUnstableCore: true)` + 콤보 0) → 면제 → 임계 판정 → `Arts.AbsorbKilnHeat` → 열 반영 → `Spells.Execute(req)`(안에서 `Traits.RollSpreadDegrees`, 거울상) → `Arts.OnPlayerFired` → `Traits.OnPlayerFired`(불안정 핵의 다음 발사분 굴림·0.3초 전 예고) → 넘쳤으면 `Player.EnterLock` → `Traits.OnHeatLockEntered`. 연습 방의 "열 고정"(`w.Run.PracticeHeatFreeze`)이 켜져 있으면 열이 변하지 않고 잠금에 들어가지 않는다, "결정 무한"(`PracticeInfiniteCrystal`)이면 결정을 써도 줄지 않는다(둘 다 Player가 읽는다).

**(나) 적이 피해를 받는다** — 내 탄 적중(`Spells.Stage9_Projectiles`)과 직접 피해(`Spells.DealDirect`; 비기·패시브·지대가 부른다) 모두 Spells 안의 한 함수로 모인다:

```
1. 대상이 Targetable이 아니면 끝(죽어 가는 적 = PendingDeath, 땅속 → Immune. 관통 횟수를 쓰지 않는다)
2. 피해 = 기본 × (탄이 든 배율) × 속성 배율(약점 1.25 / 저항 0.8) × 잠식 중이면 1.15
3. 피해 = Enemies.ModifyIncomingDamage(e, 피해, isCrit, ignoresArmor, fromPos)   // 철갑, 기절 ×1.5, 보스면 Bosses.ModifyIncomingDamage
4. e.Hp -= 피해 (HpFloor 아래로 안 감). Hp ≤ 0 → e.PendingDeath = true, e.KillingHit = hit
5. 상태 적용: Enemies.ApplySlow / ApplyCorrosion / ApplyCc(반동 넉백, Enemies.TryCancelDash)
6. 훅: Player.OnPlayerHitEnemy(hit) (콤보) → Traits.OnEnemyHit(hit) (살얼음)
7. 첫 적중이면 트리거(하위 스코프 탄은 Pending으로 틱 끝에 붙음)
8. Emit EnemyDamaged
9. 약점·저항 적중이고 AffinityKey.Of(종, 약점/저항)가 w.Run.AffinityKnown에 없으면 더하고 Emit AffinityDiscovered (D-54)
```

**(다) 적이 죽는다** — `Enemies.Stage10_Deaths`(id 오름차순, 보스 몸통 포함):
사망 부수효과(분열, 유폭 …) → `Threats.CancelTelegraphsOf(id)` → `Run.OnEnemyKilled(e, hit)`(골드·점수 재료·체력 조각·재료 기회) → `Traits.OnEnemyKilled`(탐식) → `Waves.OnEnemyKilled`(마지막 숨) → `Emit EnemyKilled` → `e.Dead = true`. 보스는 그 뒤 `Bosses.Stage10_AfterDamage`가 사망 연출·졸개 제거(`Enemies.RemoveAll(minionsOnly: true)` = IsBoss가 아닌 전부)·`Threats.ClearShots`를 한다. `PendingDeath`가 선 순간부터 이 처리까지 그 적은 "없는 것"이다(5.6절 규칙 2). 같은 틱에 플레이어가 먼저 쓰러져 `RunPhase`가 이미 `RunEnded`면 `Run.OnEnemyKilled`는 지갑·점수·기회를 바꾸지 않고 Bosses는 `BossKilled`·`RevengeKill`을 내지 않는다(D-45).

**(라) 플레이어가 맞는다** — 9단계에 Threats(탄·구역·남는 구역), Enemies(접촉·돌진), Bosses가 `Player.QueueHit(p, hit)`을 부른다. 칸 수는 **부르는 순간** `Threats.DamageCells(기본 칸, 분류, 주인)`로 구한다(개체는 기본 칸만 든다. 돌진은 분류 `Dash` = 예고 공격과 같은 성장). `AttackId` = 위협 개체의 EntityId(탄·구역·남는 구역은 자기 id, 접촉·돌진은 적 id). `QueueHit`은 **bool**을 돌려준다: 무적·쓰러짐·`!PlayerDamageActive`면 false. 접촉·남는 구역의 쿨다운은 true일 때만 소비한다(같은 틱 다른 후보에 밀려도 소비). 10단계 `Player.Stage10_ResolveHits`가 AttackId가 가장 작은 하나만 확정: 체력, 무적, 콤보 0, 결정 손실, `HitThisTick`(다음 틱 1단계에 끈다) → `Traits.OnPlayerHit`(유리 지팡이) → `Run.OnPlayerHit`(무피격 깨짐) → 체력 0이면 `p.Downed` + `Run.OnPlayerDowned`(첫 만남이면 `Bosses.BeginCollapse`, 아니면 그 자리에서 `RunEnded`). 각본으로 쓰러뜨릴 때(첫 만남 되비침 +114틱)는 `Player.ForceDown(p, Scripted)` — 무적·가동 여부를 무시하고 체력 0·`Downed`·이벤트만 하며 `Run.OnPlayerDowned`를 부르지 않는다.

**(마) 스침** — `Player.Stage9_GrazeAndPickups`가 `Threats.CollectGraze(p, list)`로 후보를 받아 `hit_radius < SurfaceDistance ≤ graze_radius`인 것을 `AwardGraze` → `Traits.OnGrazeAwarded`(피뢰침) → `Run.OnGraze`(점수용 횟수). 벼락 질주의 길 위 탄은 Arts가 `Player.AwardGraze`를 직접 부른다.

**(바) 적의 공격** — Enemies/Bosses는 `Threats.RequestTelegraph(req)`로 표를 받고 `Threats.Status(ticket)`를 매 틱 본다(Waiting → Active → Detonated). 판정과 플레이어 피해는 Threats가 한다. **틱 대응**: 요청은 7단계, 허가되면 같은 틱 8단계에 구역이 생기고(시작 틱 s), 판정 틱은 s + N(9단계). 주인의 자체 타격(돌진 시작, 예고 뒤 탄 쏘기, Strike 전이)은 `Threats.TryGetTiming(ticket, out s, out 판정 틱)`으로 읽어 **판정 틱과 같은 틱의 7단계**에 시작한다(늑대: 요청 틱 T → T + 30의 7단계에 돌진 시작, 그 틱 9단계부터 몸이 움직인다). 대기 줄에서 기다린 틱은 WindUp에 들지 않는다(주인 상태 = Waiting). 표는 주인이 `CancelTicket` 하거나 600틱이 지날 때까지 남는다. 상한 = `w.Wave.TelegraphCap`(0 이하 = 상한 없음). 마지막 숨의 원은 `StaggerIfBlockedTicks = 12`(상한에 세지 않고 R5만 보며, 막히면 늦게 청한 것부터 12틱씩 미룬다). 탄은 `Threats.SpawnShot`, 남는 구역은 `ZoneSpec.LeavesHazard` 또는 `SpawnHazard`. 돌진은 주인이 `EnemyState.DashActive/DashCastId/DashBaseDamage`를 켜고 몸 충돌은 Enemies가(분류 `Dash`), 스침 후보는 Threats가 그 필드를 읽어 만든다. 얼음 병풍: 적 탄·적 이동이 `Arts.SegmentHitsScreen`을 묻고 닿으면 `Arts.OnEnemyTouchedScreen`. 불러낸 개체는 `EnemySpawnSpec.SummonerId`에 주인 id를 넣는다(소환 상한을 이것으로 센다).

**(사) 비기** — Arts는 결제에 `Player.CanAfford/SpendCrystals/SpendAllCrystals`, 트리 실행에 `Spells.Execute`(Source = ManaDischarge/ArtOther/CopyCircle), 직접 피해에 `Spells.DealDirect`(Amount = n × `Spells.StandardT` × `Traits.CharacterMul(쓴 뒤 결정 수)`), 군중 제어에 `Enemies.ApplyCc`, 이동에 `Player.MoveByArt`·`GrantInvuln`, 방열에 `Player.ConsumeHeatForVent`·`Threats.ClearShots`, 보스 멈춤에 `Bosses.ActionHalt`.

**(아) 판 흐름** — Run이 상태를 옮기며 부른다: `Waves.BuildPlan`(야영 진입 때 다음 웨이브) → 출발 → `Waves.BeginWave`(좁은 들 예고 틱을 돌려줌. `Stats`의 Wave·IsBoss·PhantomFirst·Whims도 여기서 채운다), `Traits.OnWaveStart`, `Arts.OnCommandReset(WaveStart)` → 보스면 `Bosses.SpawnBoss` → 전투 → 클리어 판정(`Waves.IsCleared` 또는 보스 사망) → 클리어 처리(결정 환전, 골드, 기회, `Stats.ElapsedTicks = w.Wave.WaveTick` → `Score.WaveScore`, `Player.ResetForWaveEnd`, `Threats.ClearAll`, `Arts.ClearZones`, `Spells.ClearProjectiles`, `Waves.EndWave`, 보스 웨이브면 끝에 **항상 `Bosses.Dismiss`**(→ `w.Boss = null`)) → 전리품 → 야영. 판 끝 요약은 `Run.BuildSummary`. 연습·튜토리얼 방(`Practice`)에 들어갈 때 Run은 `w.Wave`를 기본값(Wave 1, 상한 없음)으로 둔다 — 적 체력의 H(w)는 `max(1, w.Wave.Wave)`로 읽는다.

### 5.5 판 상태 기계 (`RunPhase`, 06 요약서 4.1절)

```
NotStarted ─StartRun─▶ (StartWave == 1) WavePrelude(1) | (StartWave > 1) StartCamp ─출발─▶ WavePrelude
WavePrelude ─좁은 들 예고 180틱(없으면 0)─▶ 보스면 BossIntro(Bosses의 Intro가 끝날 때까지) ─▶ WaveCombat
WaveCombat ─전멸/보스 사망─▶ WaveClearing(ClearingTicks) ─▶ LootPick ─2장 확정─▶ Camp ─출발─▶ WavePrelude(w+1)
WaveCombat ─체력 0─▶ RunEnded(HpZero)           (사망 연출 뒤 정산 화면은 화면 쪽이 띄운다)
첫 만남: WaveCombat ─Boss.State == Reflection─▶ PhantomFinale ─Boss.State == Collapse─▶ PhantomDowned ─Boss.State == CollapseDone─▶ 일어남 ─▶ WaveClearing
         WaveCombat ─체력 0─▶ (Bosses.BeginCollapse) PhantomDowned
아무 상태 ─GiveUp 명령─▶ RunEnded(Abandon)        (되비침·쓰러짐 중에는 무시)
Practice: 연습·튜토리얼 방(웨이브 없음, 허수아비)
야영 하위(Camp와 StartCamp 공통): FireIdle ⇄ CampScreen, FireIdle ─출발 표식 위 J 60틱─▶ DepartHold ─▶ 출발
```

**시계의 주인은 하나다.** 보스 등장 72틱, 되비침 180틱, 쓰러짐 240틱은 Bosses가 `BossState.State`·`StateStartTick`으로 세고 Run은 그 상태만 읽어 `RunPhase`를 옮긴다(같은 구간을 두 모듈이 따로 세지 않는다 → 1틱 어긋남과 등장 두 번이 없다).

| Bosses가 하는 것 (7단계·10단계) | Run이 보는 것 (같은 틱 11단계) |
|---|---|
| `SpawnBoss`: State = Intro, StateStartTick = t | Phase = BossIntro, `AttackLocked = true` |
| Intro 72틱 뒤 State = Waiting, `BossIntroEnd` | State ≠ Intro → Phase = WaveCombat, `AttackLocked = false` |
| 첫 만남 종료 조건(체력 0 > 50% > 2,700틱) → State = Reflection, `InputLocked = true`, `Threats.ClearAll` | Phase = PhantomFinale |
| Reflection +114틱: `Player.ForceDown(p, Scripted)`. 180틱 뒤 State = Collapse | Phase = PhantomDowned |
| (플레이어 체력 0이 먼저면 Run.OnPlayerDowned → `Bosses.BeginCollapse` → State = Collapse) | Phase = PhantomDowned |
| Collapse 72틱에 `PhantomMasterLine`, 240틱 뒤 State = CollapseDone, `InputLocked = false` | `Player.Rise` → `Bosses.Dismiss` → `PhantomFirstDone = true`, **`RevengeActive = true`** → 웨이브 10을 넘긴 것으로 클리어 처리 → WaveClearing |
| 보통 보스: 체력 0 → State = Dead, `BossKilled`(복수 중 웨이브 20 허상이면 `RevengeKill`) | 클리어 처리 → WaveClearing → 끝에 `Bosses.Dismiss` |

`Bosses.Stage7_Ai`는 `RunPhase`·`CombatActive`와 무관하게 이 상태 시계를 센다(패턴 고르기·실행만 `CombatActive`일 때). `AttackLocked`는 Run만, `InputLocked`는 Bosses만 켜고 끈다.

**WaveClearing의 길이**(`RunState.ClearingTicks`, D-58): 보통 `camp.json wave_clear_seconds`(1초 = 60틱) / 보스를 잡은 웨이브 `boss_clear_seconds`(2.0초: 사망 느린 연출 1.0초 + 드랍 1.0초) / **복수 처치**(`RevengeKill`이 난 웨이브) `revenge_clear_ticks`(264틱: 0~60 느린 연출·거울 금 / 60 흰 번쩍 / 84~144 머리 장식 / 144~264 칭호 띠) — `revenge_skip_after_ticks`(84틱)이 지난 뒤 J 누름(`p.JPressedThisTick`)이 있으면 Run이 바로 LootPick으로 넘긴다(연출 층은 `RevengeKill` 이벤트의 틱 기준으로 그리고, 전리품 화면이 뜨면 스스로 접는다). 화면(G20)은 `PhaseChanged`만 보고 연다 — 연출 길이를 화면이 재지 않는다.

**복수 중 여부**는 `RunState.RevengeActive`(= `Config.RevengePending || PhantomFirstDone`) 하나로 읽는다: 웨이브 20의 복수 꼬리표와 `RevengeKill`(Bosses), HUD의 허상까지 n웨이브(G17), 야영·정산의 추적기(G21·G22), 첫 쪽지(G25). 판에서 첫 만남을 치른 직후부터 켜진다(프로필의 `phantom_met`은 정산 때 적힌다). 추적기 값 계산은 `Meta.RevengeTracker.Build(profile, db, 지금 웨이브, metThisRun: w.Run.RevengeActive)` 하나를 모든 화면이 쓴다.

**야영 전장의 상호작용**(D-49·D-52). 모닥불·출발 표식의 자리와 반경은 Run이 야영(Camp·StartCamp)에 들어갈 때 `camp.json`에서 `RunState.CampFirePos / DepartMarkerPos / CampInteractRadius / DepartMarkerRadius`에 적고, 표시(G14)는 이 값만 읽는다. `FireIdle`에서 **Run이 직접** 판정한다(`Stage11_Phase`):

- 플레이어가 모닥불 반경 안 + `p.JPressedThisTick` → `CampSub = CampScreen`, `CampChanged`. (`CampOpen` 명령은 마우스·테스트·화면이 다시 열 때 쓰는 같은 전이다.)
- 플레이어가 출발 표식 반경 안 + J 눌림 → `DepartHold`(시작 틱 기록, `DepartHold` 이벤트 A = 누른 틱). 60틱(`depart_hold_seconds`) 유지하면 출발, 떼거나 반경을 벗어나면 `FireIdle`(A = 0). 남은 해금 차례가 있으면 출발하지 않고 `CampScreen`(비기 탭)으로 보낸다.
- 화면(G20)은 `PhaseChanged`(Camp·StartCamp)와 `CampChanged`에서 `w.Run.CampSub == CampScreen`이면 Camp 화면을 Push, 화면이 닫힐 때 `CampClose` 명령. 출발 누름의 남은 시간 고리는 G14가 표식 위에 그린다(`DepartHoldStartTick`).

**판 중 조립과 서고 블록**(D-53). 출발 조립의 서고 블록(`SlotItem.WildUid == 0`)을 칸에서 빼면 보관함이 아니라 `RunState.LibraryBench`(대기 칸, 상한 없음, `FromLibrary = true`, SlotRef 300 + i)로 간다. 대기 칸의 블록은 다시 끼울 수 있고, 보관함 12칸·전리품 넘침·버림 골드·귀환 가방·`RunSummary.WildBlocks`에 들지 않는다(서고 블록은 복제되지도 팔리지도 않는다. 판이 끝나면 조립은 출발 조립으로 돌아간다).

**연습·튜토리얼 방**(`Practice`, D-56·D-57·D-59). 설정은 `IWandService.BuildPracticeConfig(character, wandId, mode, overrideLoadout, seed)`가 만든다(프로필을 바꾸지 않는다 — 출발 횟수가 오르지 않는다).

- 방에 들어갈 때 Run: 허수아비(`Ids.Enemy.Dummy`) 등장, `w.Wave` 기본값, 옵션(`RunState.PracticeInfiniteCrystal / PracticeEnemyFire / PracticeHeatFreeze`)을 `RunConfig`에서 복사. 옵션은 `PracticeSetOption` 명령(A = `PracticeOption`, B = 1/0)으로 바뀐다. 읽는 곳: 결정 무한·열 고정 = Player, 적 탄 쏘기 = Enemies의 허수아비 두뇌(`w.Run.PracticeEnemyFire`).
- Practice 단계에서도 조립 명령(`LoadoutMove`, `LoadoutRemoveAll`)을 받는다.
- 수치판: Run이 `practice.json report_interval_seconds`마다 `PracticeReport`(Id = `PracticeReportKeys.*`)를 낸다. 입력 간격 막대와 저스트 게이지는 `Arts.GetRecentInputs`(TimeU, GapU, AllowedGapU)와 `Arts.Preview`(LastDirTimeU, JustWindowU)로 그린다.
- 튜토리얼(Mode = Tutorial): 시작 때 보관함에 `practice.json tutorial_block_id` 블록 1개, 결정 무한 켬. `RunState.TutorialStep`이 지금 단계(1~5). Run이 단계의 완료 조건(08 요약서 6.4절)을 보고 `TutorialStepDone(단계)`를 낸다. ⑤는 Practice 단계의 `LoadoutMove`로 빈 칸에 블록이 들어가면 완료. 다음 단계로 넘기는 것은 화면의 `TutorialAdvance` 명령(A = 넘어갈 단계, 건너뛰기 포함. 6 = 끝).

화면은 `IGameSession.SimEventRaised`의 `PhaseChanged`를 보고 LootPick·Camp·Settlement 화면을 연다. 화면 조작은 전부 `world.Enqueue(new RunCommand(…))`.

### 5.6 모듈 공통 규칙

`Core/Sim/Modules.cs` 머리 주석의 아홉 줄이 정본이다(구현 작업 전부가 읽는다). 요지:

1. 상태는 주인 모듈만 쓴다.
2. `PendingDeath`가 선 적은 그 틱의 남은 단계에서 없는 것으로 본다(`EnemyState.Alive` / `Targetable`로 거른다). `KillingHit`는 처음 것 유지.
3. `NearestPlayer`가 null이면 AI는 그 틱에 가만히 있는다.
4. 예고 표의 틱: 판정 틱과 같은 틱의 7단계에 주인의 자체 타격을 시작한다(`TryGetTiming`).
5. 시계의 주인은 하나(보스 상태 = Bosses, `RunPhase` = Run, `AttackLocked` = Run, `InputLocked` = Bosses).
6. 받는 피해 칸은 `QueueHit`을 부르는 순간 `Threats.DamageCells`로.
7. 발사·비기 허용은 `SimWorld.AttackAllowed(p)` 하나로.
8. `RunEnded` 뒤의 훅은 지갑·점수·기회를 바꾸지 않는다.
9. 플레이어와 무관한 이벤트의 Slot은 −1.

### 5.7 게임플레이 조립(통합 단계 1)에서 바꾼 계약

모듈 25개의 결과 보고(`Docs/impl-reports.md` "계약 변경 필요")를 따져 반영한 것. 모듈 폴더의 우회 코드는 걷어 냈다.

| 계약 | 바뀐 것 | 요청 |
|---|---|---|
| `PlayerState.PlayerScratch` | 플레이어 모듈 전용 상태 자리(정적 `ConditionalWeakTable` 우회를 걷음) | G01 |
| `PlayerModule.CanAfford(w, p, cost, credit)`, `RingState(w, p)` | 세계 인자를 받는다(연습 "결정 무한", 수치를 `w`에서 읽는다) | G01 |
| `ProjectileEntity.BaseMul`·`CritBonus`, `CompiledSpell.TreeShotCap` | 발사 순간 배율 스냅숏, 디버그 출력의 탄 상한 | G02 |
| `ArtTrieNodeView.JustState` | 섬 기술(뇌광창)의 잠김·후보 상태를 K 기술과 따로 | G03 |
| `RunModule.OnArtFired(w, p, artId)` | 비기 발동 통지. 만개 발동 수(`BloomCasts`)와 튜토리얼 ④(반동 사격)를 Run이 센다 — Arts가 `RunState`를 직접 쓰지 않는다 | G03, G10 |
| `RunModule.OnCrystalsSpent(w, p, count)` | 비기 값 지불 통지(결정 무한이어도 치른 값). 연습 수치판 "결정 1개당 피해"의 분모 | G10 |
| `ThreatModule.RemoveHazard`·`MoveHazard`·`DestroyObstacle` | 보스가 남의 개체를 지우거나 옮길 때(불씨 소화, 안개 흐름, 돌진이 깨는 바위) | G05, G07, G08 |
| `ZoneSpec.BaseDamage` 주석 | 0 이하 = 판정·스침 없는 예고(보스 돌진의 예고 띠) | G07 |
| `HazardSpec.MaxCount` 주석 | 같은 종류의 전장 전체 동시 상한(주인 무관) | G05 |
| `EnemyShotEntity`(`ScopeDepth`), `WaveState.Scratch`, `StarfallState.ScheduledTick` 주석 | 허상 복사 탄의 스코프 깊이, 웨이브별 보조 상태, 별똥 시계 기준(웨이브 시계) | G07, G09 |
| `BossState.SegmentRadius`, `EnemyModule/BossModule/IBossBrain.ModifyIncomingDamage(…, fromPos, hitPos)` | 플레이어 탄·폭발·연쇄 번개가 밤삼킴의 마디에도 맞고(`Spells`가 마디 원을 몸으로 본다), 두뇌는 맞은 자리(`hitPos`, 몸 겉면)로 부위 배율을 정한다. `fromPos`는 정면 철갑용 온 쪽 | G08 |
| `SimEventType.ProjectilePassed`, `EnemyDashCancelled`, 줄 주석 보탬 | 그림자 구체가 적을 지남(물결·C-24), 반동 사격이 돌진 예고를 끊음("끊음" 표식·흔들림·T-18). `EnemyDamaged`의 S = 때린 슬롯 등 | G02, G05, G07, G16 |
| `RunCommandKind`·`RunState.Wave`·`Score` 주석 | 전리품·야영 명령 인자, 야영 중 `Wave` = 다음 웨이브, 점수 = 건너뛴 점수 포함 | G10 |
| `ProbBreakdown.SameElementPct` | 제물 보너스 중 같은 속성 몫(내역 두 줄) | G12 |
| `IEnhanceService.AttemptsWithOwned`, `IsLocked` | "보유 재화로 평균 N번"(화면의 따로 셈 몬테카를로를 걷음), 원정 중 잠금 | G12, G23 |
| `MutationOffer.RollBp`·`SuccessPct`·`GreatBp`·`MutationEndBp` | 변이 결정의 결과 꾸러미가 그 시도의 뽑힌 값을 기록 꼬리 없이도 돌려준다 | G12 |
| `IWandService.Release(ids, typedName)` | +7 이상 이름 확인(TV-RL-04). 확장 메서드 우회를 걷음 | G13, G23 |
| `ISettlementService.IsValidStartSkip` | 시작 웨이브 검증(T085) | G11 |
| `ProfileFlags.LowStoneReceived` | 하급 강화석을 처음 받은 정산(덧필드 `low_stone_received` → 같은 열쇠의 정식 필드) | G11 |
| `OptionKeys.WandListSort`(`wand_list.sort`, 숨김) | 지팡이 목록 정렬을 기기에 기억(08 5.3절). 원정 준비와 강화의 지팡이 고르기가 같이 쓴다. [기본값으로]가 지우지 않는다 | G19, G23 |
| `DataSolo/G18.json`, `G20.json` | 시작 묶음 화면 수치, 중간 지점 간격(D-23) — 코드의 기본값을 데이터로 | G18, G20 |

받아들이지 않고 지금 꼴을 유지한 것(동작이 맞아서):
- `WaveModule.HpMul/Budget/TelegraphCap`에 콘텐츠 인자 없음 — 구현이 세계를 처음 받을 때 `w.Content`를 붙잡는다(주석에 적음).
- `EnemyModule.CountSummonedBy` — 보스는 `BossContext.CountSummoned`(SummonerId로 센다)를 쓰고, 보스가 부른 졸개의 상성 제거는 Enemies.Spawn이 불러낸 적이 보스면 한다(`EnemySpawnSpec.Minion` 불필요).
- 밑줄 칸 상태(`PlayerState.UnderlineSlot`) — `Derived.Underline`과 연결 칸 수로 정해지므로 HUD가 상태에서 바로 정하고(`HudCircuitPanel.UnderlineSlotOf`), Player는 재컴파일 때 `UnderlineSlotChanged`를 낸다.
- 적 탄 종료 사유(`EndReason`), `HazardSpec.InnerRadius`, 둘째 돌진 판정점, 비늘 탄 상시 예고 — 보스 두뇌 안의 어림으로 돈다(G07·G08 보고 그대로).
- `ArtModule.TryGetLastResult` — 연습 화면은 `ArtFired`/`ArtFizzled` 이벤트로 늦은 K를 안다.
- 화면 전체 번쩍임 관리자 공유(`ViewContext.Flash`) — 효과 층(G16)과 이야기 층(G25)이 각자 08 14.1절 한도를 지킨다.
- `UiEntry.CompositeAnchor`, `ScreenId.PendingSettled`, `DropChanceSource.Revenge`(복수 재료는 `BossKill` + `Bound = true`로 가린다).

---

## 6. 표시 이벤트 (`Core/Sim/SimEvents.cs`)

`SimEvent { Type, Tick, Slot, EntityId, A, B, F, Pos, Pos2, Id }`. 종류별 필드 쓰임은 `SimEventType`의 줄 주석이 정본이다. 묶음:

| 묶음 | 이벤트 |
|---|---|
| 입력·자세 | FacingChanged, StanceEntered/Exited, ReverseFlipped, CommandReset, CommandDirInput |
| 과열 | ShotFired, FireDryWhileLocked, HeatRingChanged, HeatLockEntered, HeatUnlocked, HeatExemptArmed/Consumed, KilnAbsorbed |
| 스침·피격 | GrazeAwarded, GrazeBuffStarted/Ended, LockBrokenByGraze, PlayerHit, InvulnEnded, PlayerDowned, PlayerRose, PlayerHealed |
| 콤보·결정 | ComboChanged, ComboReset, CrystalGained, CrystalsFull, CrystalGainBlocked, CrystalSpent, CrystalLost, CrystalProtected, DebtAdded, DebtRepaid |
| 비기 | ArtFired, ArtFizzled, ArtBuffered, ArtPulse, ArtLine, PlayerZoneSpawned/Expired, PlayerMovedByArt, ArtUnlocked, ArtOffered |
| 패시브·변이 | BoiloverTriggered, LightningRodStrike, UnstableCoreWarn/Exploded, WandTempStageDropped, UnderlineSlotChanged, ThinIceFroze |
| 내 탄·피해 | ProjectileSpawned/Expired/Bounced, Explosion, ChainLightningArc, EnemyDamaged, ShotCapHit, AffinityDiscovered, ProjectilePassed |
| 적 | EnemySpawnNotice, EnemySpawned, EnemyKilled, EnemyCc, EnemyStateChanged, EnemyShotFired/Blocked, EnemyShotsCleared, EnemyBuff, EnemyDashCancelled |
| 예고·구역 | TelegraphStarted/Locked/Flash/Detonated/Cancelled, HazardSpawned/Expiring/Expired, ObstacleSpawned/Destroyed |
| 보스 | BossIntroBegin/End, BossPhaseChanged, BossRageStack, BossFinalRage, BossKilled, BossMinionSpawned, BossPatternStarted, PhantomReflection, PhantomCollapse, PhantomMasterLine, PhantomMirrorCrack, RevengeKill |
| 웨이브·판 | RunStarted, PhaseChanged, WaveStarted, WaveCleared, WaveStalled, DayChanged, TodChanged, WeatherStarted/Ended, MasterAdvice, Starfall*, DropSpawned/Picked, GoldGained, PurseChanged, ScoreChanged, LootOffered, LootChanged, CampEntered, CampChanged, LoadoutChanged, DepartHold, RunEnded, TutorialStepDone, PracticeReport |

계속 보이는 것(위치, 체력, 열, 구역의 차오름, 적 애니메이션 클립)은 이벤트가 아니라 **상태를 매 프레임 읽어** 그린다. 한 번 일어나는 것만 이벤트다. 개체가 사라지는 틱에는 상태가 이미 없으므로 표시 층은 필요한 값을 Spawned/Started 때 복사해 둔다.

---

## 7. 표시 계층

`IWorldViewLayer { Build(vc, w); OnEvent(w, e); Render(w, alpha, dt); Dispose(); }` — `ViewRegistry`에 등록한 순서대로 돈다. `ViewContext`가 `World`, `WorldRoot`, `Camera`, `HudRoot`, `WorldTextRoot`, `ISpritePool`, `Options`(설정에서 읽은 표시 옵션), `EffectsLevel`, `Shake`, `HitStop`, `WorldToHud`를 준다. `Build`는 진행 중인 세계에도 불린다(보조 판에서 돌아올 때) — 층은 이벤트 없이 지금 상태만 보고 그릴 수 있어야 한다.

- 좌표: 표시 위치 = `vc.ToView(simPos)` = `(x, 14 − y)`. 방향 벡터는 `SimConst.DirToView`. 보간은 `ViewMath.Lerp(prev, cur, alpha)`(한 틱 1칸 초과면 보간 없음).
- 카메라: 정사영, `orthographicSize = UiScale.OrthographicSize(Screen.height, UiScale.Compute(Screen.width, Screen.height))`(1080p·1배 = 8.4375). UI와 같은 배율을 써서 1칸 = 64px × 배율로 맞춘다(8.2절, D-50). 전장은 기준 화면의 좌우 192·위 104·아래 80 여백 안에 둔다 → 카메라 중심 = 월드 (12, 7.1875). **평범한 `UnityEngine.Camera`만 쓴다**: URP 전용 형(`UniversalAdditionalCameraData`, `Light2D`, `Volume`, `PixelPerfectCamera`)을 코드에서 쓰지 않는다(asmdef와 compile.sh에 URP 참조가 없다).
- 층 순서: `SortBands`(sortingOrder 띠. Sorting Layer를 새로 만들지 않는다) — Floor 0, FloorMarks 100, PlayerZones 200, TelegraphFill 300, Shadows 400, Bodies 500(+`YOrder`), PlayerShots 700, Bursts 800, TelegraphEdge 900, EnemyShots 1000, PlayerMarks 1100, Weather 1200, DamageNumbers 1300. HUD는 uGUI 캔버스(HudOverlay).
- 스프라이트는 `ISpritePool`에서 빌리고 돌려준다. 그림은 `ctx.Art`(`IArtLibrary`)에서. 없는 그림은 대체 도형(회청·나무 어두움 바둑판, 단색 원·네모)으로.
- **셰이더·머티리얼의 주인은 그림 창고(F2) 하나다.** 표시 층은 `Shader.Find`·`new Material`을 쓰지 않는다(빈 장면 + 코드 생성이라 플레이어 빌드에서 셰이더가 벗겨진다. compile.sh `SOLO0002`). 머티리얼은 `ctx.Art.Material(SpriteMaterialKind)`: `Sprite`(텍스처 × 색, 단색 덮기 `_FlashColor/_FlashAmount`, 외곽선 `_OutlineColor/_OutlineSize` — 피격 흰 번쩍, 엘리트 금색 2px, 림), `SpriteAdditive`(가산, 32px 이하 그림만), `Shape`(텍스처 없이 정점 색 — 코드 메시). 개체별 값은 `MaterialPropertyBlock`으로 넣는다. 전부 조명을 받지 않는다(장면에 Light2D가 없다. `Sprite-Lit-Default`를 쓰지 않는다). 풀이 빌려줄 때 머티리얼을 넣는다.
- **색 공간**(통합 단계에서 실제 화면으로 확인, D-79): 프로젝트는 Linear이고 코드의 색 상수는 전부 sRGB 값이다. 흰 그림에 색 C를 입히면 화면의 점이 C여야 한다. 그래서 (가) 솔로 머티리얼은 **GPU 인스턴싱을 켜지 않는다** — 켜면 `SpriteRenderer.color`가 선형으로 바뀌지 않은 채 넘어와 위험색 `#FF3B30`이 `#FF8478`(분홍)로 뜬다(Unity 6000.3. 엔진의 `Sprites/Default`도 같다). (나) `Solo/Shape`는 정점 색(`Mesh.colors`/`colors32`)을 sRGB로 받아 셰이더에서 선형으로 바꾼다 — 표시 층은 메시에 `UiTheme` 같은 sRGB 색을 그대로 넣는다. (다) `MaterialPropertyBlock.SetColor`(덮기·외곽선 색)는 엔진이 바꿔 준다. PlayMode의 `SoloShaderColorTests`가 네 길을 지킨다.
- 코드로 만든 메시(예고 구역의 원·부채·띠·고리)는 `ISpritePool.RentMesh(sortingOrder, Shape)`로 빌린 `MeshRenderer`에 넣는다(지름 2칸 이상에 가산 혼합 금지). 누적 가림 상한(10 요약서 3.2절)은 오프스크린 버퍼 없이 **넓이 셈**(화면에 그린 반투명 넓이의 합을 세어 넘치면 새 일회 효과를 줄인다)으로 한다.
- **전장 위 글자**: 숫자는 스프라이트 글리프(`status/damage_glyphs`, `combo_digits`), 그 밖의 글자(+골드, 비기 이름 등)는 `vc.WorldTextRoot` 아래 uGUI `Text` + `Outline`을 `vc.WorldToHud(simPos)` 자리에 둔다. `TextMesh`를 쓰지 않는다(D-69).
- **설정 옵션**(08 요약서 12.2절)은 `vc.Options`(`ViewOptions`)로 읽는다. 누가 무엇을 적용하는지는 `Core/View/ViewOptions.cs`의 머리 주석이 정본: 호스트(F5) = 흔들림 배율·그림 정지·연출 감도 / G14 = 날씨 입자·지팡이 효과 단계와 투명도 / G15 = 예고 채움 진하기·색각 보조 / G16 = 내 탄 투명도·탄 꼬리·피해 숫자 방식과 투명도·처치 조각·기술 이름·사본·지대 투명도·바닥 흔적·번쩍 배율 / G17 = 콤보 자리.
- 위험색(빨강 `#FF3B30`, 자홍 `#FF2E88`)은 적 예고·적 탄·피격 가장자리·체력 1칸 귀퉁이·최종 격노 테에만. 플레이어 쪽·메뉴·대체 도형에 쓰지 않는다. 임계에 금색·빨강 금지.
- 줄일 수 없는 것: 적 예고, 적 탄, 열 링, 피격 표시, 시야 화살표(`EffectsLevel`과 무관).
- 표시용 난수는 `UnityEngine.Random`. 시뮬레이션 난수기(`SimWorld.Rng`)를 표시에서 쓰지 않는다.
- 소리: 표시 층 하나(`AddAudio`)가 전투 이벤트 → `ISoundPlayer.Play(eventId, bus)`로 넘기고 판 중 배경음 전환(`SetMusic`: `TodChanged`, `BossIntroBegin`, 야영)도 맡는다. **화면 쪽 사건**(`ui.*`, 강화 E-묶음)은 그 화면이 요약서의 이벤트 줄 시점에 `ctx.Sound.Play(열쇠, SoundBus.Ui)`로 직접 낸다. 섞는 규칙(동시 32개, 사건별 초당 횟수, 우선순위, 누르기)은 재생기 안의 정책 클래스(F4)가 맡는다. 구현은 무음이지만 자리는 전부 잇는다.
- 입력: 표시·화면 코드는 `UnityEngine.Input.*`·`OnMouseDown`을 쓰지 않는다(활성 입력이 Input System 전용이라 예외가 난다. compile.sh `SOLO0003`). 마우스는 `Mouse.current`와 uGUI 이벤트(`IPointer…Handler`)로.

| 등록 자리 | 층 | 작업 |
|---|---|---|
| `AddArena` | 타일·가장자리·소품·모닥불·출발 표식·시간대 전환·날씨·달빛 원 | G14 |
| `AddCharacters` | 캐릭터 몸(37클립 3방향), 지팡이(단계·자세), 그림자, 잔상, 무적 깜빡임 | G14 |
| `AddEnemies` | 적·엘리트 표식·보스 몸·체력 바·상태이상 표시·등장 예고 | G15 |
| `AddThreats` | 예고 구역(채움·테두리 두 층), 적 탄, 남는 구역, 장애물, 별똥 | G15 |
| `AddShots` | 내 탄, 꼬리, 플레이어 지대(불가마·사본·병풍) | G16 |
| `AddEffects` | 적중·폭발·처치·비기 연출, 피해 숫자, 드랍, 골드, 화면 흔들림·그림 정지 요청 | G16 |
| `AddAudio` | 이벤트 → 소리 사건 | G16 |
| `AddHud` | 전투 HUD 전부와 캐릭터 둘레 표시(열 링, 결정, 콤보, 최근 입력, 지금 K, 시야 화살표) | G17 |
| `AddStory` | 첫 만남·복수 연출, 대사 판, 튜토리얼 풍선 | G25 |

---

## 8. 화면

`IScreen`(Build/OnEnter/OnExit/OnBack/OnMenuKey/Tick) + `IScreenManager`(Register/Push/Replace/Pop/PopTo/Close/Top/IsOpen/Confirm/ConfirmText/Toast/ShowTooltip/HideTooltip/CaptureKeys). 화면은 코드로 조립한 uGUI(기준 1920×1080 캔버스 — 8.2절, `UnityEngine.UI.Text` + TTF 동적 글꼴, 9조각은 manifest의 border로 `Sprite.Create`). 메뉴 키: 방향(방향키와 이동 키 배치 둘 다), J 결정, K 뒤로/빼기, Esc 나가기, Q/E 탭·지팡이 바꾸기, 마우스(클릭, 끌어다 놓기).

| 묶음(등록 자리) | 폴더 | 화면(`ScreenId`) | 작업 |
|---|---|---|---|
| Common | `Scripts/UI/Dialogs/` | ConfirmDialog, Tooltip, Loading, SaveRestored | F3 |
| Start | `Screens/Start/` | FirstRunPrompt, Title, ProfileSelect, OfflineGuide, MainMenu | G18 |
| Prep | `Screens/Prep/` | ExpeditionPrep(캐릭터·지팡이·출발 조립·시작 웨이브) | G19 |
| Battle | `Screens/Battle/` | Battle(판 진행 주인: 세션 시작·화면 전이·사망 연출), Pause, ControlsView | G20 |
| Camp | `Screens/Camp/` | LootPick, Camp(모드 넷: Camp / Library / LoadoutEdit / Tutorial, 조립·트리 미리보기), ArtTrie, DummyPractice | G21 |
| Settlement | `Screens/Settlement/` | Settlement(기록, 귀환 가방, 가져가는 것) | G22 |
| Enhance | `Screens/Enhance/` | WandPicker, Enhance, WandReleaseConfirm, MutationRemove, SlotEngrave, EnhanceTutorial | G23 |
| Meta | `Screens/Meta/` | Records, TitlePicker, Wardrobe, Shop, ShopBuyConfirm | G24 |
| Settings | `Screens/Settings/` | Settings, KeyRebind | G24 |
| Tutorial | `Screens/Story/` | Tutorial(허수아비 방 5단계), 첫 만남·복수 연출 층, 복수 추적기 위젯(값 계산은 Core의 `Meta.RevengeTracker`) | G25 |

전이(08 요약서 2절):

```
Boot ─▶ (설정 파일 없음) FirstRunPrompt ─▶ Title ─J─▶ (처음이면 OfflineGuide) ─▶ MainMenu (자동 정산 결과가 있으면 알림 창 한 번)
MainMenu ─ 원정 ─▶ ExpeditionPrep ─출발─▶ (첫 판 전이면 Tutorial ─끝/건너뛰기─▶) Loading ─▶ Battle
                    ExpeditionPrep ─[조립 바꾸기]─▶ Camp(mode = LoadoutEdit, 그 지팡이)
         ─ 강화 ─▶ WandPicker ─▶ (처음이고 까마귀 보따리를 받았으면 EnhanceTutorial ─▶) Enhance (─▶ MutationRemove / SlotEngrave / WandReleaseConfirm)
         ─ 조립과 서고 ─▶ Camp(mode = Library)     ─ 연습 ─▶ DummyPractice(entry = MainMenu)
Tutorial ─⑤ 블록 끼우기─▶ Camp(mode = Tutorial) ─닫기─▶ Tutorial
         ─ 기록 ─▶ Records ─▶ TitlePicker           ─ 옷장 ─▶ Wardrobe
         ─ 상점 ─▶ Shop ─▶ ShopBuyConfirm           ─ 설정 ─▶ Settings ─▶ KeyRebind
         ─ 서버 전용 묶음 ─▶ OfflineGuide            ─ 나가기 ─▶ 종료
Battle ─Esc─▶ Pause ─▶ 계속 | 설정 | ControlsView | 판 포기(확인) | 메인 메뉴로(= 판 포기)
Battle ─PhaseChanged(LootPick)─▶ LootPick ─▶ (모닥불 옆 J → CampSub == CampScreen) Camp(mode = Camp) ⇄ ArtTrie, DummyPractice(entry = Camp, 보조 판) ─출발─▶ Battle
Battle ─RunEnded─▶ Settlement ─▶ ExpeditionPrep | WandPicker·Enhance(돌아올 곳 MainMenu) | MainMenu
```

`IScreenManager`에 보탠 것(통합 단계에서 계약에 넣었다 — 화면은 `ctx.Screens`를 구체형으로 바꿔 쓰지 않는다):

- `Close(screen, result)`: 맨 위가 아닌 화면도 닫는다(맨 위면 Pop과 같다). `ScreenBase.Close()`가 이것을 부른다.
- `ConfirmText(titleKey, bodyText, caution, onResult)`: 본문이 이미 틀을 채운 글일 때(예: TV-040 "웨이브 15"). 글은 이름 표를 거친 것이어야 한다.
- `ShowTooltip(text, anchor)` / `HideTooltip(anchor)`: 툴팁(최대 폭 400). 위젯에는 `UiKit.AttachTooltip`을 쓰고, 직접 띄울 때만 부른다.
- `CaptureKeys`: true인 동안 메뉴 키(Esc 포함)를 화면에 보내지 않는다(키 재설정의 "키를 누르세요").
- `ScreenId.Toast`: 토스트 화면의 id(등록하지 않고 `Toast()`가 직접 올린다). `Top`은 Toast 층(Toast·Tooltip)을 세지 않는다.

강화 화면은 메인 메뉴(와 정산 화면의 버튼)에서만 열린다. 판 중·야영의 어느 모드에도 강화 탭·버튼이 없다. 봇 상대 결투 연습·결투 입력 지연 체험·기록의 결투 전적 줄은 넣지 않는다(D-51).

### 8.1 화면 인자 — `ScreenArgKeys` (정본)

화면 사이 인자는 `ScreenArgs.Values`에 `ScreenArgKeys`의 상수로만 넣고 꺼낸다. 형이 다르면 `Get<T>`가 조용히 fallback을 주므로 아래 형을 지킨다(캐릭터는 int 1~5, 지팡이 id는 long).

| 화면 | 받는 열쇠(형) | 보내는 곳 |
|---|---|---|
| Title | `ProfileRestored`(bool), `PendingSettled`(SettlementResult) | Bootstrap, FirstRunPrompt(그대로 넘김) |
| MainMenu | `PendingSettled`(SettlementResult) | Title, OfflineGuide(그대로 넘김) |
| Loading | `NextScreen`(ScreenId), `NextArgs`(ScreenArgs) — 한 프레임 그린 뒤 자신을 NextScreen(NextArgs)으로 Replace. `ScreenArgs.ViaLoading(entry, next, nextArgs)`로 만든다 | ExpeditionPrep, Tutorial, Settlement |
| Battle | `RunConfig`(RunConfig, Mode = Endless) | Loading을 거쳐 ExpeditionPrep·Tutorial |
| Tutorial | `RunConfig`(튜토리얼 뒤에 시작할 진짜 판. 고치지 않고 그대로 Battle에 넘긴다) | ExpeditionPrep |
| Camp | `CampMode`(CampMode). Library: `Character`(int, 없으면 profile.last_character) / LoadoutEdit: `WandId`(long) / Camp·Tutorial: 인자 없음(세션의 세계) | Battle, MainMenu, ExpeditionPrep, Tutorial |
| ArtTrie | `ArtPick`(bool, 해금 차례 고르기), `Character`(int, 판 밖에서 열 때) | Camp, DummyPractice |
| DummyPractice | `PracticeEntry`(PracticeEntry). MainMenu: `Character`(int, 없으면 last_character) / Camp: 인자 없음(지금 판의 조립을 읽는다) | MainMenu, Camp |
| LootPick, Pause, ControlsView | 없음(세션의 세계) | Battle, Pause |
| Settlement | `RunSummary`(RunSummary) | Battle |
| WandPicker | `Character`(int, 없으면 last_character), `ReturnTo`(ScreenId) | MainMenu, Settlement |
| Enhance | `WandId`(long), `ReturnTo`(ScreenId) | WandPicker, Settlement |
| EnhanceTutorial | `NextScreen`·`NextArgs`(닫으면 갈 곳) | WandPicker, Enhance |
| WandReleaseConfirm | `WandIds`(List&lt;long&gt;) | WandPicker |
| MutationRemove | `WandId`(long), `MutationId`(string) | Enhance |
| SlotEngrave | `WandId`(long) | Enhance |
| ShopBuyConfirm | `Sku`(string), `Qty`(int), `Character`(int, 지팡이 상자만) | Shop |
| ConfirmDialog | `TitleKey`, `BodyKey`(string), `Caution`(bool) + `OnResult` — `IScreenManager.Confirm`이 채운다 | 전부 |
| 그 밖(Records, TitlePicker, Wardrobe, Shop, Settings, KeyRebind, ProfileSelect, OfflineGuide, FirstRunPrompt, ExpeditionPrep) | 없음 | |

### 8.2 캔버스와 입력 모듈 (정본)

- 캔버스는 둘, 둘 다 `UiCanvasFactory.Create(name, sortingOrder)`(Core)로 만든다: **HUD 캔버스**(F5, `UiSortOrders.Hud` = 0)와 **화면 캔버스**(F3). 화면 층은 화면 캔버스 안의 부모 여럿이 아니라 **층마다 캔버스 하나**(`UiSortOrders.Screen(layer)` = 100·200·300·400·500)로 둬 HUD가 모든 화면 아래에 깔리게 한다.
- 규격: Screen Space Overlay, `CanvasScaler` ConstantPixelSize + `scaleFactor = UiScale.Compute(창 크기)`(정수 배, 1920×1080보다 작을 때만 줄임. `UiScaleDriver`가 창 크기 변화를 따라간다), **`referencePixelsPerUnit = 64`**(그림을 PPU 64로 들이므로 9조각 테두리·`SetNativeSize`가 1px = 1px), 루트는 가운데 정렬 1920×1080. 화면과 HUD의 모든 것은 그 루트 아래에 둔다. 남는 곳은 먹색 `#12131C`(카메라 배경).
- UI용 `Sprite.Create`도 PPU 64, border는 manifest의 `nine_slice_ltrb`를 Unity 순서 (L, B, R, T)로 바꿔 넣는다.
- **EventSystem은 F3의 `CreateScreens`가 하나만 만들고 `UnityEngine.InputSystem.UI.InputSystemUIInputModule`을 붙인다**(프로젝트의 활성 입력은 Input System 전용이라 `StandaloneInputModule`은 매 프레임 예외를 낸다). 마우스 클릭·끌어다 놓기는 이 모듈로 uGUI 이벤트가 온다. F5는 EventSystem을 만들지 않는다.
- 글자: `ctx.Art.Font(size)`의 동적 글꼴. 배율이 정수라 글자가 정수 크기로 다시 그려진다. 전장 위 글자도 HUD 캔버스의 `WorldTextRoot` 아래에 둔다(7절).

F3이 내놓는 UI 기반(화면 작업은 이것으로만 조립한다. 정확한 서명은 F3이 `Scripts/UI/README.md`에 적는다): `UiKit`(Panel, NineSlice, Image, Label(크기 6종, 넘치면 "…"), Button(상태 5종 + 조심 버튼), IconButton, ProgressBar, Slider, Toggle, Tabs, ScrollList, Grid, SelectionBracket, KeyHintBar, 끌어다 놓기 도우미), `UiTheme`(색 상수 = 08 요약서 1.5절), `UiFocusGroup`(키보드 커서 이동), `ScreenBase`(IScreen의 기본 구현: 위 띠 80px·아래 조작 안내 48px 뼈대), `MenuInput`(MenuKey 폴링). 위젯 형 이름은 `Ui` 접두사(2.2절).

---

## 9. 데이터

| 파일 (`Assets/Resources/Data/`) | 출처 | Def (`Scripts/Data/Defs/`) | `IContentDb` |
|---|---|---|---|
| `sim/blocks.json`, `elements.json` | `data/sim` 사본 | `BlockDef`, `ElementsDef` | `Blocks`, `Block(id)`, `BlocksOfRarity`, `Elements` |
| `sim/characters.json` | 사본 | `CharactersFile`, `CharacterDef` | `Characters`, `Character(id)`, `BaselineCharacter` |
| `sim/arts.json`, `tries.json` | 사본 | `ArtDef`, `TriesDef` | `Arts`, `Art(id)`, `Tries`, `TrieArts(charId)` |
| `sim/enemies.json`, `elites.json`, `bosses.json` | 사본 | `EnemyDef`, `AttackDef`, `EliteDef`, `BossesFile`, `BossDef` … | `Enemies`, `Enemy`, `Elite`, `Elites`, `BossCommon`, `Bosses`, `Boss` |
| `sim/waves.json`, `frames.json`, `modifiers.json`, `tod.json` | 사본 | `WavesDef`, `FrameDef`, `ModifierDef`, `TodDef` | `Waves`, `Frames`, `Modifiers`, `Tods` |
| `sim/scoring.json`, `starfall.json`, `weather_events.json`, `arena.json` | 사본 | `ScoringDef`, `StarfallDef`, `WeatherEventDef`, `ArenaDef` | `Scoring`, `Starfall`, `WeatherEvents`, `Field` |
| `presentation/*.json` | `data/presentation` 사본 | (표시 작업이 필요한 만큼 읽는다) | — |
| `names/names.ko.json` | `data/names` 사본 | — | `INameTable` |
| `Assets/Resources/NamesSolo/<작업 id>.ko.json` | **새로**(작업마다 조각 하나) | 원본에 없는 열쇠만. F1의 조각이 13절 D-14·D-26의 열쇠를 담는다 | `INameTable` |
| `Assets/Resources/DataSolo/<작업 id>.json` | **새로**(필요한 작업만) | 기반 단계 뒤에 Def에 없는 수치를 둘 조각 | `IContentDb.SoloExtra(name)` |
| `Assets/Resources/DataSolo/F3.json` | **새로**(통합) | UI 기반의 솔로 임시 값(D-75) → `UI.UiTuning`(`ScreenManager.Tuning`) | `SoloExtra("F3")` |
| `Assets/Resources/DataSolo/F4.json` | **새로**(통합) | 소리 섞는 규칙(10 요약서 5.2절, D-76) → `Audio.SoundMixConfig` | `SoloExtra("F4")` |
| `solo/enhance.json` | **새로**(07 요약서 2절) | `EnhanceConfig` | `Enhance` |
| `solo/economy.json` | **새로**(07 요약서 7·8절) | `EconomyConfig` | `Economy` |
| `solo/mutations.json` | **새로**(02 요약서 9.1절) | `MutationsConfig` | `Mutations` |
| `solo/drops.json` | **새로**(06 요약서 4.4·4.5·5.2·5.3절) | `DropsConfig` | `Drops` |
| `solo/camp.json` | **새로**(06 요약서 4.1·4.3절, 04 요약서 11절 + 모닥불·출발 표식 자리 D-52, WaveClearing 길이 D-58) | `CampConfig` | `Camp` |
| `solo/const.json` | **새로**(본문에만 있던 상수 + `revenge_cracked_percent` 50) | `SoloConstDef` | `Const` |
| `solo/practice.json` | **새로**(허수아비, 튜토리얼 블록 D-59, 수치판 주기) | `PracticeConfig` | `Practice` |

- 사본은 바이트 그대로 둔다(값을 고치지 않는다). 솔로 보충 JSON의 값은 요약서의 명세 값·[임시] 값 그대로다.
- Def는 공개 필드 PascalCase, JSON은 snake_case — 로더가 `SnakeCaseNamingStrategy`로 잇는다. 단위는 JSON 그대로(초, 칸, 도)이고 쓰는 쪽이 `SimConst.Ticks/Units/HeatToHu`로 바꾼다.
- 로더: `ContentDb.LoadFromResources()`(TextAsset + Newtonsoft.Json), 테스트용 `ContentDb.LoadFromDirectory(path)`. 테스트는 `Tests/EditMode/Data/TestContent`의 정적 속성 `TestContent.Db`·`TestContent.Names`(처음 쓸 때 읽는다)를 쓴다. 조회는 없는 id에 null. `EnemyTagsUtil.Parse`로 꼬리표를 플래그로.
- 코드의 숫자 리터럴은 0, 1, 2, 단위 환산, 자료구조 크기뿐. 그 밖은 Def에서 읽는다.
- 이름 조각 규칙: 파일 하나에 `{ "열쇠": "글자", … }`, 열쇠 오름차순, 원본 `names.ko.json`에 있는 열쇠를 다시 적지 않는다. `NameTable`이 `Resources/NamesSolo`의 조각을 전부 합치고 같은 열쇠가 두 번 나오면 오류를 적는다. 새 열쇠는 그 화면의 `str.<화면>.…` 꼴로 짓는다. 한국어 글자는 이 조각과 원본 사본에만 있고 코드에는 없다.
- 수치 조각 규칙: 구현 작업이 Def에 없는 명세 수치를 만나면 `Resources/DataSolo/<작업 id>.json`에 두고 `ctx.Content.SoloExtra("<작업 id>")`로 읽는다(코드에 박지 않는다). F1이 만든 `solo/*.json`은 고치지 않는다.

---

## 10. 에셋 라이브러리

- `Wizard.Solo.Editor.ArtSync.Run`(메뉴 + 배치 메서드): `D:\algorithm_game\art`의 정본 그림(09 요약서 0절 표)과 manifest JSON을 `Assets/Resources/Art/`로 복사한다. 폴더 배치는 원본을 따른다: `players/`, `wands/`, `monsters/`, `effects/`, `ui/`, `world/`, `backgrounds/`, 보강분 `supplement/`(layers, native-64 enhancement). manifest는 `.json`(TextAsset)로.
- **들이지 않는 것**(원본은 1,164장·무압축 약 757MB인데 절반이 겹치거나 작업물이다): `ui/atlas/**`(낱장과 내용이 겹친다 — 낱장만 쓴다), `ui/font/*.png`·`*_glyphs.json`(비트맵 글꼴 — TTF를 쓴다), `**/preview*.png`·`previews/**`·`*_preview.png`·`combat_preview.png`·`ritual.gif`(미리보기), `players/source/**`, `*.py`·`*.md`(작업물). 대응표(`Editor/Art/artsync.json`)에 제외 규칙으로 적는다.
- `AssetPostprocessor`(`Resources/Art` 아래): 텍스처 = Sprite(Single), `spriteMeshType FullRect`, Point 필터, 압축 없음, PPU 64, mipmap 없음, 읽기 불필요, `alphaIsTransparency`, sRGB, **`maxTextureSize 8192` 고정**(2048을 넘는 그림이 넷 있다: `field.weather.rain` 7680×128, `field.tower` 2560×128, `ritual/screen_crack` 1920×3240 … 줄어들면 manifest rect가 전부 어긋난다), `npotScale None`. 글꼴 TTF = `fontTextureCase Dynamic`, `fontRenderingMode HintedRaster`(픽셀 글꼴이 번지지 않게), `includeFontData true`.
- 시트 자르기는 런타임: manifest의 rect(왼쪽 위 원점) → `Rect(x, TH − y − h, w, h)`, 기준점 `(px, py)` → pivot `(px / w, (h − py) / h)`로 `Sprite.Create(tex, rect, pivot, 64, 0, SpriteMeshType.FullRect, border, false)`하고 캐시한다. meta에 슬라이스를 굽지 않는다.
- **늦게 읽는다**: 부팅 때는 manifest JSON 파싱만 한다. 텍스처는 처음 요청될 때 `Resources.Load`(`LoadAll` 금지), 스프라이트는 요청된 칸만 만들어 캐시한다(시작 때 일괄 생성 금지). 캐시는 앱이 끝날 때까지 둔다. 단 강화 의식 그림(`effects`의 `ritual` 묶음, 약 100MB)은 강화 화면(G23)이 닫힐 때 `ctx.Art.Release("ritual")`로 내린다(다시 요청하면 다시 읽는다).
- 동적 글꼴 텍스처의 필터는 임포터로 못 정한다 → `ArtLibrary`가 `Font.textureRebuilt`에서 글꼴 텍스처의 `filterMode = Point`를 다시 건다. 쓰는 특수 글자(▲ ▼ … · ← → ↑ ↓)가 두 TTF에 있는지 F2가 확인하고 없는 것을 결과 보고에 적는다(없으면 OS 글꼴로 대체된다).
- 셰이더·머티리얼: `Assets/Resources/Shaders/`에 솔로 셰이더(스프라이트: 틴트 + 단색 덮기 + 외곽선 / 가산 변형 / 도형: 정점 색)를 두고 `Resources.Load<Shader>`로 읽어 머티리얼을 만든다. 속성 이름은 `SpriteShaderProps`. URP 2D 렌더러에서 조명 없이 그려지는 무조명 패스로 쓴다(통합 단계에서 실제 화면으로 확인함 — D-79). 머티리얼은 GPU 인스턴싱을 켜지 않는다(7절 "색 공간").
- API는 `IArtLibrary`(`Core/Services.cs`): `CharacterClip`, `CharacterPortrait/Selection/Silhouette`, `WandHand/Inspection/Icon`, `WandDetachedPart`, `MonsterClip`, `BossPortrait/HudIcon`, `Effect(group, key)`, `Ui(id, variant, state)`, `UiSized(…, sizePx)`, `UiAnim(…)`, `Icon(contentId, size)`, `World(assetId, tod)`, `Background`, `Font(size)`, `Placeholder`, `WhitePixel`, `Circle`, `Material(kind)`, `Release(group)`. 그림이 없으면 대체 그림을 돌려준다(null을 주지 않는다. `Effect`·`MonsterClip`은 프레임 1장짜리 대체. 예외: `WandDetachedPart`는 조각이 없는 캐릭터에 null).
- manifest 정보를 그대로 읽는 것(없으면 null — 표시 층이 붙일 자리·크기를 셈할 때): `CharacterClipInfo`(프레임별 feet·hand·head·shoulder 점), `WandStageInfo`(단계별 점), `WandGrip`(손잡이 기준점), `MonsterClipNames`, `EffectInfo`·`LayerInfo`, `UiInfo`, `WorldInfo`, `WorldMapJson(mapId)`. 형은 `Scripts/Art/ManifestModels.cs`(`PlayerClip`, `WandStage`, `PixelPoint`, `EffectEntry`, `UiEntry`, `WorldEntry`).
- 부르는 약속: `Effect`의 group은 `player/special/ritual/enemy/loot/wand/status`에 더해 보강분 `"enhance"`(native-64 강화 묶음의 `new` 항목)와 `"layers"`(캐릭터 겹 그림 — 열쇠에 `char.N`과 `열쇠:prefix` 거르개를 쓸 수 있다). `World`는 `"id:자세"`를 받는다. `Background`의 열쇠는 `"title"`, `"enhance.workbench"`, UI id, world 열쇠. `Icon`은 `"ui:<ID>/<variant>"`도 받는다. `WhitePixel`은 PPU 1(localScale = 칸 수). `Font`는 TTF를 돌려주고 부르는 쪽이 `fontSize = (int)UiFontId`로 쓴다.
- 작업용 폴더 이름(dalgu 등) ↔ `char.N` 대응은 `ArtLibrary` 안의 표 한 곳(또는 `asset_aliases.json` 사본)에만 둔다.

---

## 11. 저장

- 뿌리 폴더 = `ctx.DataRoot`(null이면 `Application.persistentDataPath`. 테스트는 임시 폴더).
- 프로필: `<뿌리>/profiles/offline/profile.json` + `.1 .2 .3`. UTF-8 JSON 본문 + **마지막 줄에 본문의 SHA-256(16진)**. 쓰기 = 임시 파일 → 세대 밀기(.2→.3, .1→.2, 현재→.1) → 이름 바꾸기. 읽기 = 해시가 안 맞거나 파싱 실패면 `.1`, `.2`, `.3` 순으로 되돌리고 `restored = true`(SaveRestored 창).
- 구조: `Core/Meta/ProfileData.cs`(07 요약서 9.3절 + 10 요약서 7절). snake_case, 정본 id. `characters[]`(해금 비기, 최고 점수·웨이브, 누적 웨이브), `wands[]`(단계, floor_peak, record_peak, 하락 수, 기운, 확장 칸, 새기기 누적, 변이, 대기 중인 변이 제시, 출발 조립, 흔적 공개), `currencies.gold`, `items{}`(강화석, 부적, 블록 수량·끼움 수), `library_expansions`, `shop_week`, `flags`, `revenge`, `titles`, `cosmetics`, `feats`, `weekly`, `run_checkpoint`, 그리고 솔로 보충: `run_history[]`(기록 화면의 판 목록, D-55), `last_settled_run_id`(같은 판 두 번 정산 막기), `known_affinities[]`(알아낸 약점·저항, D-54), `last_character`(D-56).
- `IProfileStore.RestorePrevious()`: 직전 세대(.1)를 현재로 되돌린다(설정 > 계정의 [저장 파일 되돌리기]). `Directory`는 같은 곳의 "저장 파일 위치" 줄이 그린다.
- 쓰는 때: 판 끝(정산), 강화·새기기·변이 결정·변이 제거, 조립 변경, 상점 구매, 떠나보내기, 흔적 공개, 든 지팡이 변경, 비기 해금, 중간 지점(웨이브 5의 배수 클리어). 프로필을 바꾼 메타 서비스가 스스로 `Save()`를 부른다.
- 설정: `<뿌리>/settings.json`(`SettingsData`: 키 배치 + `options{}`). 옵션 표(08 요약서 12.2절의 솔로 줄)의 열쇠·값 이름·기본값·범위는 `Core/Meta/SettingsOptions.cs`(`OptionKeys`, `OptionValues`, `SettingsOptions`)가 정본이고, 읽고 쓰는 쪽은 `SettingsOptions.Int/Bool/Str/Set`만 쓴다(연출 감도 = `motion_preset`, 음량 = `vol_master/bgm/sfx/wand/ui/enemy`). 이 파일이 없으면 기기 첫 실행이다. 프로필과 섞지 않는다. 바꾸면 즉시 저장하고 `ISettingsStore.Changed`가 난다 → 표시 옵션(`ViewOptions`), 음량(`ISoundPlayer.SetVolume`), 키 배치(입력원)가 다시 읽는다.
- 키 배치 표의 정본은 `Core/Meta/KeyLayouts.cs` 하나다(D-74): 설정 저장소(F4), 전투 입력원(`App.KeyBindings`), 키 재설정 화면(G24)이 이 표만 읽는다. `KeyLayouts.Apply(settings, layout)`으로 정해진 배치를 넣고, 불러올 때 `Normalize`가 default·alternate를 표 값으로 되맞춘다.
- 새 프로필(F4의 `NewProfile.Create(content, clock)`): 캐릭터 5명, 캐릭터마다 +0 지팡이 한 자루(코어 `block.magic_bolt`, 연결 칸 빈 채), 서고 마력탄 5(전부 끼움), 골드·재료 0, 해금 = `art.burst`, `art.recoil`, 고유 비기, `phantom_met = false`.

---

## 12. 테스트 전략

- 규칙은 전부 EditMode 테스트로 검증한다(`Assets/Tests/EditMode/<모듈>/`). 요약서의 테스트 벡터(각 요약서의 "검산"·"테스트 벡터" 절, 작업 지시가 가리킨 것)를 **전부** 테스트로 옮긴다. 기대값은 요약서에서 가져오고 구현 출력에서 베끼지 않는다. 옮길 수 없는 벡터는 빼지 말고 결과 보고에 까닭을 적는다.
- 이름: `S<명세 번호>_<벡터 id>__<설명>` (예: `S01_T_H01__HoldFireLocksAtShot26`, `S02_TV_E07__ShotCapCompensation`). 본보기: `Tests/EditMode/Core/CoreContractTests.cs`.
- 시뮬레이션 테스트 꼴: `var mods = SimModuleSet.Stubs(); mods.Player = new PlayerSystem(); var w = new SimWorld(TestContent.Db, mods, seed);` → 필요한 상태를 직접 세우고 → `w.EnqueueInput(0, new InputEvent(key, down, timeU))` → `w.Step()` 반복 → 상태와 `w.Events`를 단언. 다른 모듈이 필요하면 기본 클래스를 상속한 가짜를 테스트 폴더에 둔다. `TestContent.Db`(실제 JSON을 읽은 ContentDb)는 F1이 `Tests/EditMode/Data/`에 둔다.
- 경계값 벡터(정확히 85, 정확히 100, 1,075u, 2,765u, 4,608u)는 int HU·long u로 비교한다.
- 메타 테스트: 임시 폴더의 `ProfileStore` + 고정 난수(`Pcg32`)로 강화·정산·상점 벡터. 저장 테스트: 원자적 쓰기, 3세대, 깨진 파일 복구.
- 표시·화면은 PlayMode 테스트(통합 단계): 부팅 → 판 → 정산 → 강화 → 다시 실행 흐름을 `BotInputSource`로. PlayMode 테스트는 `[UnitySetUp]`에서 `Bootstrap.Boot(new BootOptions { DataRoot = 임시 폴더, InputOverride = 봇 })`을 직접 부른다(자동 부팅은 `Main` 장면에서만 돈다 — 4절). `UnityEditor`를 쓰지 않는다(compile.sh가 PlayMode 단위를 에디터 참조 없이 컴파일한다).
- 통합 단계의 실행은 3.1절의 래퍼로: `Tools/unity-test-edit.sh`, `Tools/unity-test-play.sh`. PlayMode 연기 테스트 `Tests/PlayMode/Integration/BootVisualSmokeTests`가 부팅 → 월드(솔로 셰이더) → 타이틀 자리 화면 → 메인 메뉴 자리 화면 → 확인 창을 돌리며 그림·글꼴·이름 표 로드를 확인하고 `Logs/shots/01-world.png … 04-confirm.png`를 남긴다. 자리 화면(`SmokeScreens.cs`)은 테스트 어셈블리에만 있고, 진짜 Title·MainMenu(G18)가 등록돼 있으면 그것을 쓴다.
- **배치 모드에는 게임 창이 없어 화면이 저절로 그려지지 않는다**(`Screen` = 640×480, `ScreenCapture`는 파일을 만들지 않고, 캔버스의 글자 메시도 만들어지지 않는다). 그래서 `SmokeCapture`가 1920×1080 RenderTexture에 월드 카메라를 직접 그리고, 그동안만 캔버스를 Screen Space Camera로 바꾼다. 게임 창에 실제로 나온 그림이 필요하면 `Tools/unity-test-play.sh -w`(창을 띄워 돌린다 — `ScreenCapture` 길).
- 병렬 단계의 확인은 `Tools/compile.sh -f <자기 폴더들>`의 **끝 코드 0**. 끝 코드 3(미확인)은 통과가 아니다 — 다시 돌리고, 끝내 3이면 "확인하지 못함"으로 적는다. 테스트를 돌려 보지 않았으면 "컴파일만 확인"이라고 적는다.

---

## 13. 확정 표 — 요약서의 미정·모순에서 이 빌드가 택한 값

요약서가 권한 쪽, [임시] 값을 우선했다. "솔로 임시"는 원문에 값이 없어 이 빌드가 넣은 것이고 QA에서 사용자가 정한다.

| # | 항목 (출처) | 이 빌드의 값 |
|---|---|---|
| D-01 | 틱 처리 순서 (02 M-01) | CT 3.2의 0~12단계 = 5.1절 |
| D-02 | 야영 중 틱 (02 M-16, 10 X-10) | 게임 틱은 간다. 점수용 `WaveTick`만 WaveCombat 동안 흐른다 |
| D-03 | 웨이브 끝 초기화 (01 Q10) | [임시] 콤보 0, 열 0·잠금 해제, 면제·가속·불가마 흡수 해제. 빚은 남는다 |
| D-04 | 불안정 핵 자폭이 끓어넘침을 일으키나 (02 M-03) | 아니다(S01). 잠금 = t + 간격 + 120틱, 이미 잠겼으면 더 긴 쪽 (M-04) |
| D-05 | 불안정 핵 자폭 발사 (02 6.3 ↔ 03 6절) | 탄이 나가지 않고 열도 오르지 않는다(03 U11). 잠금 2초 + 콤보 0 |
| D-06 | 깊이 감쇠·밑줄 상한·변이 수치의 자리 (02 M-05·M-06, 03 3절) | `solo/const.json`, `solo/mutations.json` |
| D-07 | 표준 T의 정의 (03 4.7 ↔ S04 Q-01) | S02 방식: 루트 스코프만(분열 반영, 거울상 제외) × 강화 × 변이. 캐릭터 배율은 피해를 줄 때 곱한다(비기는 쓴 뒤 결정 수) |
| D-08 | 패시브 피해의 임계·콤보 (02 M-10) | [임시] 임계 없음, 콤보 없음, 경계 포함(≤) |
| D-09 | 구역 스침 거리 (02 M-11) | 캐릭터의 감지 반지름(제자 2는 0.75) |
| D-10 | 냉각 대기 시작점 (02 M-12) | 마지막 발사의 간격이 끝난 뒤 12틱 |
| D-11 | 일시정지 키 (10 X-3) / 누적 표시 (02 M-13) | Esc(메뉴 길의 고정 키. 전투 입력원은 Esc를 내지 않는다 — 4.1절). 누적 일시정지 시간(`str.pause.pause_total`)은 일시정지 화면의 판 요약에 그린다. 순위 판용 10분 안내(`pause_limit_note`)만 그리지 않는다 |
| D-12 | 연속 스침 음높이 셈 (02 M-15) | 표시 층에서 센다 |
| D-13 | 보관된 K의 새 대기 (10 Q-10·Q-11) | 실제로 나간 틱부터 0.4초. 웨이브 시작 초기화는 보관된 K를 버리지 않는다 |
| D-14 | "지금 K"의 불발·섬 머리 글자 열쇠 (04 6절) | 이름 조각(`NamesSolo/F1.ko.json`)에 `str.battle_hud.now_k_fizzle`, `str.battle_hud.now_k_just` 추가 |
| D-15 | 고유 비기의 부모가 미해금 (04 C-02) | [임시] 불발(부모는 burst·recoil 계열이라 실제로는 fan·wings 미해금일 때만) |
| D-16 | 전리품 다시 뽑기의 자리 (10 X-1) | LootPick 화면에서만. 야영 화면에 버튼 없음 |
| D-17 | 허수아비 연습의 자리 (10 X-2) | 야영 조립 탭의 "조립 시험" 버튼 + 메인 메뉴 "연습". 같은 화면(TV-082). 야영에서는 보조 판(`IGameSession.PushSubRun`)으로 열고 그동안 본판은 멈춘다(4.1절) |
| D-18 | 회복 비용의 웨이브 (01 Q5, 10 X-4) | 방금 넘긴 웨이브: `30 + 5 × w`, 두 번째부터 ×3/2 |
| D-19 | 일시정지 "메인 메뉴로" (01 Q4) | 판 포기와 같다(확인 창 뒤 정산) |
| D-20 | 되비침·쓰러짐 중 일시정지·포기 (01 Q6) | 둘 다 막는다(입력 무시 구간). 그 전의 웨이브 10 전투 중 포기는 된다(웨이브 10 미클리어) |
| D-21 | 체력 0 뒤 정산까지 (01 Q12) | 솔로 임시: 사망 연출 2.0초(`camp.json downed_to_settlement_seconds`) 뒤 정산 화면 |
| D-22 | 도달 웨이브 (01 Q11) | [임시] 마지막으로 시작한 웨이브 |
| D-23 | 오프라인 비정상 종료 (01 Q3, 06 5.5) | 요약서 제안안: 웨이브 5의 배수를 넘길 때 중간 지점을 프로필에 적고, 다음 실행 때 그 지점까지 자동 정산(가방 자동 담기). 중간 지점이 없으면 보상 없음 |
| D-24 | 오프라인 주간 도전 (01 Q2) | 씨앗 목록 데이터가 없어 메뉴에 넣지 않는다. 저장 필드(`weekly`)만 둔다. **사용자 결정 대기** |
| D-25 | 손맛 칭호 조건 (01 Q7) | 솔로 임시: `title.feat.bloom_100` = 만개 발동 누적 100회(프로필), `title.feat.flawless_night` = 한 판에서 같은 날의 웨이브 8·9를 둘 다 무피격으로 넘김. **사용자 확인 대기** |
| D-26 | 이름 표에 없는 문구 (01 Q1, 10 G-3) | 이름 조각(`NamesSolo/F1.ko.json`)에 열쇠 추가: `str.battle_hud.death_line`, `str.settlement.best_line`, `str.enhance.fail_line` 등. 원본 사본은 고치지 않는다 |
| D-27 | 복수 칭호 띠의 이름 자리 (10 G-4) | 캐릭터 표시 이름("제자 N") |
| D-28 | 첫 만남·복수 저장 필드 (10 X-5) | 07 형식 + `phantom_met`(false = 못 만남), `revenge.phantom_tries/phantom_best_dent/revenge_runs` |
| D-29 | 저장 시점 (10 X-6) / 체크섬 (07 9.3) | 07의 상위 집합 + 비기 해금 + 중간 지점. 마지막 줄 SHA-256 |
| D-30 | 불안정 핵이 나오는 코어 (10 X-7) | `flags`에 `explodes`가 있는 투사체가 코어일 때 |
| D-31 | 드랍·야영·가방·카드 확률의 자리 (06 8.2-1) | `solo/drops.json`, `solo/camp.json` |
| D-32 | 서고 한도의 오프라인 적용 (06 8.2-3) | 적용(100, 50개당 2,000골드) |
| D-33 | 상점 주 경계 (07 8절) | [임시] 기기 현지 시간 월요일 00:00 |
| D-34 | 정산 → 강화 화면에서 돌아갈 곳 (08 2절 U-34) | [임시] 메인 메뉴 |
| D-35 | 메뉴 방향 입력 (08 1.2) | 방향키와 이동 키 배치(W/A/S/D, 대체 배치) 둘 다 |
| D-36 | "나가기" (08 2절) | [임시] 게임 종료(`Application.Quit`) |
| D-37 | 몬스터 클립 재생 속도 (09 11-9) | 솔로 임시: 8fps(`Resources/Art/solo_presentation.json` 한 곳, F2). **사용자 확인 대기** |
| D-38 | 강화 버튼 그림, +7 큰 그림 교체 (09 11-5·6) | 정본 UI K-23 9조각. production의 `inspection.png`를 그대로 쓴다(교체 안 함) |
| D-39 | 변이 아이콘 열쇠 어긋남 (09 11-7) | native-64 `mutation_icons`를 코드 순서(1~6 = `Ids.Mutation.All`)로 대응 |
| D-40 | 표지판 글자 (09 11-8) | manifest의 `displayText`를 그리지 않는다(화면 글자는 이름 표에서만) |
| D-41 | 그림 없는 블록 탄 (09 8절) | 부메랑은 `player/mana_bolt`(청록)로 대체, 변형·제어는 탄 몸을 바꾸지 않음 |
| D-42 | 글꼴 (작업 지시) | F-12 = Galmuri11 12px, F-16 = neodgm 16px, F-24 = Galmuri11 24px, F-32/48/64 = neodgm 32/48/64px. `UnityEngine.UI.Text` 동적 글꼴, HintedRaster, 글꼴 텍스처는 Point(다시 만들어질 때마다 다시 건다 — 10절) |
| D-43 | id 표기 (02 M-02, 06 0.2, 10 X-12) | 정본 id(`Ids.cs`): `passive.overflow`, `art.bolt_dash`, `whim.*`, `frame.hunting_party`, `boss.mirror_phantom`, `charm.safeguard` … |
| D-44 | 날씨·별똥 [임시] 값 (01 Q9) | 데이터 값 그대로(소나기 10%, 안개 20%, 훈수 5%, 별똥 50%·10~40초) |
| D-45 | 같은 틱에 플레이어와 마지막 적이 죽음 (10 6절) | 플레이어 피해 먼저 → 체력 0이면 그 웨이브 미클리어 |
| D-46 | 허상 체력 기준 (06 Q-13) | `bosses.json`의 `hp_scale_anchor_wave` 값 그대로 |
| D-47 | 층 순서 구현 | Sorting Layer 대신 sortingOrder 띠(`SortBands`) |
| D-49 | 야영 전장에서의 J (원문 없음) | 솔로 임시: 발사·비기 없음. 모닥불 옆 J = 야영 화면, 출발 표식 위 J 1초 = 출발. 조립을 쏴 보는 곳은 "조립 시험"(허수아비 연습) |
| D-48 | 그 밖의 [임시]·모순 | 각 요약서의 "미정·모순" 절이 고른 값 그대로(01 19절, 02 13절, 03 17절, 04 15절, 05 13절, 06 8절, 07 13·14절, 08 18절, 09 11절, 10 11절) |
| D-50 | 화면 배율 (09 1절 "확대는 정수 배", 08 12.2 resolution) | 정수 배만(`UiScale`): 창이 1920×1080의 k배 이상이면 k배, 그보다 작으면 들어가게 줄인다. 남는 곳은 먹색 띠. 창 모드의 크기 선택지는 1920×1080의 정수 배(화면에 들어가는 것만), 전체 화면은 기기 해상도 그대로(2560×1440이면 1배 + 띠). UI와 월드 카메라가 같은 배율. **사용자 확인 대기** |
| D-51 | 봇 상대 결투 연습, 연습 옵션 "결투 입력 지연 체험", 기록의 결투 전적 (08 0.2·8.5절 [임시], N-11) | **넣지 않는다 — 사용자 결정 대기.** 요약서 열 개가 결투 규칙·봇·판정을 전부 "솔로 제외"로 줄였고 화면 글자 열쇠도 미정이라, 요약서대로 만들 근거가 없다. 연습 화면에 단추·옵션 줄이 없고 기록의 결투 전적 줄은 숨긴다(N-11 [임시] "줄 숨김"). 넣기로 하면 결투 규칙 요약서, `RunMode`·봇 입력원·판정 모듈, 작업을 새로 잡는다 |
| D-52 | 야영의 모닥불·출발 표식 자리와 J가 먹는 거리 (06 4.3절: "전장 가운데 모닥불", "모닥불 반대편 출발 표식" — 수치 없음) | 솔로 임시(`camp.json`): 모닥불 = 전장 가운데, `fire_interact_radius` 1.5칸. 출발 표식 = 모닥불에서 `depart_marker_offset` (+4, 0)칸, `depart_marker_radius` 1.0칸. 판정은 Run, 그림(표식과 누름 고리)은 G14. **사용자 확인 대기** |
| D-53 | 판 중에 출발 조립의 서고 블록을 칸에서 뺐을 때 (03 13.5절 "잃지 않음"뿐) | 보관함이 아니라 서고 블록 대기 칸(`RunState.LibraryBench`, 상한 없음)으로 간다. 다시 끼울 수 있고, 보관함 12칸·넘침·버림 골드·귀환 가방·정산에 들지 않는다 |
| D-54 | 약점·저항 "처음 알아냄"의 범위 (03 14절, 10 5.1 H-15) | 프로필 단위(`known_affinities`). 판 중에는 `RunState.AffinityKnown`, 첫 약점·저항 적중 때 `AffinityDiscovered`(머리 위 문양 1.5초 = G16), 정산 때 프로필에 합친다. 야영 "다음 웨이브"는 알아낸 문양만 그리고 모르는 칸은 비운다 |
| D-55 | 기록 화면의 판 목록 (08 11.1절. 저장 필드·보관 수 없음) | `run_history[]`에 정산마다 한 줄(자동 정산 포함). 솔로 임시: 최근 100판(`DataSolo/G11.json run_history_cap`). [다시 보기] 단추는 숨긴다(N-10) |
| D-56 | 메인 메뉴 "연습"·"조립과 서고"의 캐릭터와 지팡이 (원문 없음) | 솔로 임시: 원정 준비에서 마지막으로 고른 캐릭터(`last_character`, 처음엔 1)의 든 지팡이. 화면 안에서 Q/E로 캐릭터를 바꾼다. 코어가 빈 지팡이는 연습을 열 수 없다(출발 불가와 같은 문구) |
| D-57 | 연습 옵션 "열 고정"(`.option.heat_lock`)의 뜻 (08 8.5절, 설명 없음) | 솔로 임시: 켠 순간의 열 값에 묶는다 — 발사·냉각으로 변하지 않고 잠금에 들어가지 않는다(임계 사격 연습용). 옵션 셋(결정 무한, 적 탄 쏘기, 열 고정)은 `PracticeOption` |
| D-58 | WaveClearing의 길이 (06 4.3절 1초, 05 8.3절 보스 사망 1.0초 연출, 05 10.3절 복수 264틱) | 보통 60틱 / 보스 처치 솔로 임시 2.0초(느린 연출 1.0 + 드랍 1.0) / 복수 처치 264틱, 84틱 뒤 J로 넘김. Run이 센다(5.5절) |
| D-59 | 튜토리얼 방의 재료 (08 6.4절: 조건만 있음) | 솔로 임시: (가) 시작 때 보관함에 `practice.json tutorial_block_id` 1개 — 일반 등급 고정 순서에서 투사체가 아닌 첫 블록, (나) Practice 단계에서 조립 명령을 받는다, (다) 결정 무한을 켠다(④ 반동 사격), (라) ⑤의 화면은 Camp(mode = Tutorial, 조립 탭만), (마) 풍선 본문 열쇠는 `str.tutorial.step.N.body`(값은 이름 조각 G25에, 없으면 열쇠가 찍힌다 — N-05), (바) Esc = 건너뛰기 확인 |
| D-60 | 일시정지·Esc의 주인 (02 3.2절, 08 7절) | 4.1절: `IGameSession.Paused`의 set이 유일한 진입점(커맨드 초기화 1회, 풀릴 때 Resync). Pause 화면은 Battle만 연다. Esc는 메뉴 길 하나. 메뉴가 떠 있는 동안 전투 입력은 막고(문), 돌아올 때 누르고 있던 J·K는 삼킨다(솔로 임시) |
| D-61 | 보스 등장·첫 만남 구간의 시계 (05 8.3·10.2절) | Bosses 하나가 센다. Run은 `BossState.State`만 읽는다(`CollapseDone` 추가). `AttackLocked` = Run, `InputLocked` = Bosses. 각본 쓰러짐은 `Player.ForceDown`(5.5절) |
| D-62 | 돌진 몸 충돌의 피해 분류 (05 3.1절에 없음) | 예고 공격으로 센다(`PlayerDamageSource.Dash` = 공격 정의의 damage + 웨이브 성장 + 최종 격노). 칸 수는 맞은 순간 계산 |
| D-63 | 무적 중 접촉·같은 틱에 밀린 후보의 쿨다운 (05 4절) | `QueueHit`이 true(후보로 올라감)면 쿨다운 소비, false(무적·쓰러짐·비가동)면 소비 안 함. 다른 후보에 밀려도 소비 |
| D-64 | 변덕 세부 (06 1.4절) | 마지막 숨: `StaggerIfBlockedTicks` 12(상한에 안 셈, R5만) / 좁은 들: 스폰 자리를 가시 띠 바로 안쪽으로(G09) / 느린 탄막: 탄 수 floor(n × 3/2), 한 시전의 탄 사이 간격을 1.0칸 이상으로 벌려 쏜다(G06이 발사 간격·각을 조정) |
| D-65 | 골드 상점 "꾸미기" 탭 (08 11.3절: 탭 열쇠·상품 데이터 미정) | 탭을 그리지 않는다. **사용자 결정 대기**(상품 표가 정해지면 `IShopService`의 sku와 탭을 더한다) |
| D-66 | 강화 결과 카드의 "캐릭터 한마디", 강화 안내 4쪽 본문 (08 N-05) | 열쇠만 정하고 값이 없으면 줄을 숨긴다: `str.enhance.char_line.<outcome>`, `str.enhance_tutorial.page.N.body`(G23 조각) |
| D-67 | 강화 첫 안내를 여는 때 (08 9.3절, 07 5.5절) | WandPicker·Enhance에 들어갈 때 `flags.crow_bundle_given && !flags.enhance_tutorial_shown`이면 EnhanceTutorial을 먼저 연다(닫으면 원래 가려던 화면). 정산은 버튼 안내만 한다 |
| D-68 | 비정상 종료 뒤 자동 정산의 알림 (D-23 보충) | `BootResult.PendingSettled` → MainMenu에 처음 들어갈 때 알림 창 한 번(받은 골드·재료·보따리·칭호·복수 보상. 열쇠는 G18 조각). `last_settled_run_id`로 두 번 정산을 막는다 |
| D-69 | 전장 위 글자 (10 3.1절) | 숫자 = 스프라이트 글리프, 그 밖 = HUD 캔버스의 `WorldTextRoot` 아래 uGUI Text + Outline(`vc.WorldToHud`). `TextMesh` 없음 |
| D-70 | 셰이더·URP (작업 지시 "2D URP") | 솔로 셰이더는 `Resources/Shaders`(F2), 머티리얼은 `ctx.Art.Material(kind)`. 코드에서 URP 전용 형을 쓰지 않고 평범한 Camera·SpriteRenderer·MeshRenderer만 쓴다. 조명 없음 |
| D-71 | 설정 화면의 작은 전장 미리보기 (08 12.1절) | G24가 그린다: 연출 줄이기·색각 보조·예고 채움 진하기·내 탄 투명도 네 옵션을 고르면 424×240 칸에 대체 도형(원형 예고, 내 탄, 적 탄)으로 지금 값을 보여 준다 |
| D-72 | 소리 연결 (10 5절) | 전투 사건 = G16 AudioLayer, 화면 사건(`ui.*`, 강화 E) = 그 화면, 배경음 전환 = AudioLayer(판)·화면(메뉴), 섞는 규칙 = F4의 정책 클래스, 음량 묶음 6개(`SoundBus`). 곡 목록은 미정이라 id만 흘린다 |
| D-73 | 옷장의 미리보기 조작 (08 11.2절) | 방향 돌리기·걷기·앉기·감정표현 단추를 전부 둔다. 그 클립 그림이 없으면 서 있는 그림으로 대신한다. 12분류 탭 중 가진 것이 없는 분류는 빈 격자 |
| D-74 | 키 배치 표의 자리와 대체 배치의 보조 칸 (02 3.1절, 08 12.3절 TV-171·172. F4와 F5가 표를 따로 두었고 대체 배치의 보조 칸이 서로 달랐다) | 표는 `Core/Meta/KeyLayouts.cs` 하나. default = 기본 W/A/S/D/J/K/LeftShift/Space + 보조 ↑/←/↓/→/Z/X/C/V, **alternate = 기본 ↑/←/↓/→/Z/X/C/V, 보조 없음**(요약서 표, `src/Wizard.Client`의 배치와 같다). Pause 칸은 둘 다 빈칸(Esc 고정). 시프트는 LeftShift만. custom에서 비운 칸은 정말 빈 칸. 메뉴의 방향은 방향키 + 설정의 이동 키(D-35) |
| D-75 | UI 기반의 값 없는 수치 (08 1.2·1.3·13.1절) | 솔로 임시(`DataSolo/F3.json`): 메뉴 키 반복 0.4초 뒤 0.1초마다, 툴팁 지연 0.3초, 키보드 누름 모습 0.1초, 꺼짐 이유 다시 보이기 2초, 토스트 내려오기 0.2초. 그 밖에 조심 창의 첫 커서는 [아니오], "…"는 전각(글자 크기)으로 센다. **사용자 확인 대기** |
| D-76 | 소리 섞는 규칙의 자리 (10 5.2절) | `DataSolo/F4.json`(명세 값 그대로: 동시 32, 적중 초당 8, 처치 초당 6, 예고 동시 2, 누르기 −6dB 0.4초, 상한 +6dB). 솔로 임시: 보이스 길이 1.0초(파일이 없어 길이를 모른다), 셋째 동시 예고 음은 거절(가장 오래된 것과 바꾸지 않음), UI·강화 소리의 우선순위 = 비기와 같음. `ISoundPlayer.Play`에 길이 인자는 두지 않았다 — 실제 클립을 넣을 때 재생기가 클립 길이를 쓴다 |
| D-77 | 허수아비의 몸·체력 (08 8.5절, 수치 없음) | 솔로 임시(`practice.json`): `body_radius` 0.5칸, `base_hp` 0 = 죽지 않고 받은 피해만 센다. 자리는 전장을 쓰는 쪽(Run)이 정한다. **사용자 확인 대기** |
| D-79 | 색 공간과 솔로 셰이더 (D-70 보충, 통합 단계에서 화면으로 확인) | Linear 색 공간을 유지한다. 솔로 머티리얼은 GPU 인스턴싱을 끈다(켜면 `SpriteRenderer.color`가 선형 변환 없이 온다 — 엔진 동작). `Solo/Shape`의 정점 색은 sRGB로 받아 셰이더가 선형으로 바꾼다. 세 셰이더는 URP 2D 렌더러에서 조명 없이 그려진다(`SRPDefaultUnlit`) — 스크린숏과 `SoloShaderColorTests`로 확인 |
| D-78 | 그림 창고의 솔로 임시 값 (09 5절·11-9) | 몬스터 "한 번만 도는 클립" 목록과 지팡이 분리 조각 12fps는 `Resources/Art/solo_presentation.json`·`ArtLibrary`의 솔로 임시 값. neodgm.ttf(F-16/32/48/64)에는 ← → ↑ ↓ ★ 글리프가 없다 — 그 크기에서 이 글자는 OS 글꼴로 대체된다(Galmuri11에는 다 있다) |

---

## 14. 구현 순서(의존)

```
[설계] Core 계약, Def 껍데기, asmdef, compile.sh
   ▼
[기반, 서로 독립] F1 데이터 · F2 그림 · F3 UI 기반 · F4 저장·설정·소리 · F5 앱 실행기
   ▼
[게임플레이, 병렬] G01~G25  (G08은 G07의 보스 뼈대 뒤)
   ▼
[통합] Unity 배치 모드: 컴파일 → EditMode → PlayMode(부팅 → 판 → 정산 → 강화) → 빌드
```

기반 단계 뒤의 통합(한 번 돌림)에서 한 것: `ProjectSetup.Run`(빈 `Assets/Scenes/Main.unity`, 빌드 설정의 장면 = Main 하나), `ArtSync.Run`(그림 1,193장 + manifest, 임포트 설정 확인), 계약 보탬(8절 `IScreenManager`, 9절 `Elites`, 10절 `IArtLibrary`, 11절 `KeyLayouts`, D-74~D-79), Unity 러너로 EditMode·PlayMode 실행, 3.1절 래퍼. 게임플레이 단계(G01~G25) 뒤의 통합은 PlayMode 흐름 테스트(부팅 → 판 → 정산 → 강화 → 다시 실행)와 플레이어 빌드를 더한다.

구현 작업이 지킬 것:

1. 자기 소유 폴더만 고친다. `Core/`와 남의 폴더는 읽기만. 계약이 모자라면 멈추고 "계약 추가 요청"으로 올린다(임시로 자기 폴더에 확장 메서드·보조 형을 두는 것은 된다).
2. 등록은 2.1절의 partial 파일 하나로.
3. 화면 글자 = 이름 표 열쇠, 수치 = Def, id = `Ids`.
4. 벡터를 먼저 테스트로 옮기고 구현한다. `Tools/compile.sh -f <자기 폴더들>`의 끝 코드가 0이어야 끝이다(3 = 미확인은 통과가 아니다).
5. 시뮬레이션 코드에 `MonoBehaviour`, `Time`, `Input`, `Resources`, `UnityEngine.Random`, 정적 가변 필드를 쓰지 않는다. 난수는 `w.Rng(RngStream.X)`.
6. "플레이어 한 명"을 가정하지 않는다(`w.Players`를 돌고, 대상은 `w.NearestPlayer` — null이면 가만히).
7. `Core/Sim/Modules.cs` 머리의 공통 규칙 아홉 줄(5.6절)을 지킨다.
8. 런타임 코드에 `#if UNITY_EDITOR`, `Shader.Find`(Art 밖), `UnityEngine.Input.*`·`StandaloneInputModule`, URP 전용 형, `TextMesh`를 쓰지 않는다. PlayMode 테스트에 `UnityEditor`를 쓰지 않는다.
9. `Wizard.Solo.UI`의 형 이름은 `Ui` 접두사. 화면 인자는 `ScreenArgKeys`로만. 설정 값은 `SettingsOptions`로만. 복수 추적기는 `Meta.RevengeTracker`로만.
