# 13. 화면 흐름과 상태 — 구현용 명세서

게임: "엄청엄청 위대한 마법사 키우기". 이 문서는 **그림이 아닌 화면 로직**(무엇을 보여 주는가, 어떤 버튼이 언제 켜지는가, 어떤 상태에서 어디로 넘어가는가)만 다룬다. 위치·크기(px)·그림 장수는 `docs/ui-assets.md`에 그대로 두고, 여기서는 그 값이 **규칙**이 되는 경우(칸 수, 줄 수, 글자 수 같은 것)만 옮긴다.

- 최우선 문서는 `docs/DECISIONS.md`다. 다른 문서와 다르면 DECISIONS가 맞다(19절 "충돌").
- 원본: `ui-assets.md` 0절, 1.3절, 4절(각 화면의 데이터·버튼·상태·전이), `effects.md` 0.3~0.5절, `GDD.md` 13절. 교차 확인: `online.md` 1.1~1.6, 2.4, 2.5, 3.2~3.5, 4.6, 4.12, 5.1~5.5, 7.3, 7.5, 8절, `effects.md` 0.1~0.2, 1~8, 15, 18, 19.5, 22절, `monsters.md` 3.9, 4.6~4.8, 6.1~6.2절, `GDD.md` 2, 3, 4, 8, 9절, `world-characters.md` 0절.
- 시각 요소(GUI 그래픽, 스프라이트, 이펙트, 소리)는 구현 대상이 아니다. 붙일 자리는 21절 "시각/소리 연결점"의 이벤트 이름으로만 정의한다.
- 일정·범위 이야기는 하지 않는다(DECISIONS 1.16). 구현 순서는 의존 관계로만 적는다(0.4절).

---

## 0. 읽는 법

### 0.1 표시

| 표시 | 뜻 |
|---|---|
| [확정] | DECISIONS 또는 원본이 [개발자]/[확정]으로 정한 것 |
| [조율] | 문서 사이 충돌을 풀려고 정리한 것(원본 또는 이 문서) |
| [제안] | 원본이 [제안]으로 둔 값, 또는 원본 표기가 없는 시작값(ui-assets 머리말: "모든 크기와 좌표는 시작값") |
| [임시] | 원본이 정하지 않아 이 문서가 구현을 막지 않으려고 넣은 기본값. 20절 "미정"에 모두 모았다 |
| (출처) | `ui 4.6` = `ui-assets.md` 4.6절, `fx 0.3` = `effects.md` 0.3절, `gdd 13`, `on 7.3` = `online.md`, `mon 4.7` = `monsters.md`, `wc 0` = `world-characters.md`, `DEC 2.4` = `DECISIONS.md` |

### 0.2 시간과 고정소수점

- 게임 틱은 **초당 60틱**이다(`mon 4.8`: "걸린 시간은 틱(초당 60) 단위"). 이 문서의 초 값에는 `= N t`로 60틱 환산을 붙인다. 60으로 나눠 떨어지지 않으면 `≈`와 소수로 적고 반올림 규칙을 정한다.
- **화면 타이머는 표시 계층**이다. 강화 의식, 메뉴, 알림의 시간은 게임 시간(틱)을 건드리지 않는다(`fx 0.2`: 연출은 게임 시간을 멈추지 않는다). 화면 타이머는 실시간 초로 돌려도 되지만, 테스트 재현성을 위해 **고정 60Hz 화면 틱(`UiTick`)으로 진행하는 구현을 기본**으로 한다 [임시]. 전투 HUD가 읽는 값(웨이브, 경과 틱, 체력 등)은 코어의 틱 값을 그대로 쓴다.
- 확률은 **만분율 정수(bp, 1/10000)**로 든다. 82% = 8200bp. 화면에는 정수 %(큰 숫자)와 소수 한 자리(결과 띠 툴팁, 뽑힌 값)로 보인다.
- 금액·수량은 64비트 정수. 화면 표기는 세 자리마다 쉼표(48,200) (`ui 1.4`).
- 전장 길이는 칸 단위. 1칸 = 64px(`DEC 2.1`). 칸 값이 나오면 px 환산을 같이 적는다.

### 0.3 공통 식별자 규칙

- 화면: `ScreenId` (PascalCase). 상태: `XxxState`/`XxxPhase` enum. 이벤트: `ui.<screen>.<event>` (snake_case) — 21절의 시각/소리 연결점도 이 이름을 쓴다. 설정 키: snake_case(13절).
- 화면에 찍는 이름은 전부 **이름 표**(`NameTable`, id → 문자열)에서 읽는다. 문자열을 코드에 박지 않는다 [확정] (`DEC 1.25`, `ui 0`, `gdd 13`, `wc 0`). 캐릭터 id `char.1`~`char.5`, 표시 이름 "제자 1"~"제자 5" [확정]. 스승 `master`, 벌판 `field`, 광장 `plaza`, 결투장 `arena`, 지팡이 `wand.1`~`wand.5`, 보스 `boss.*`, 적 `enemy.*`, 비기 `art.*`, 블록 `block.*`, 칭호 `title.*`, 시설 `facility.*`.
- 화면 문구(버튼 글, 안내 문장)도 문자열 표(`StringTable`, 키 → 문자열)에서 읽는다 [임시]. 이 문서의 따옴표 문구는 그 표의 시작값이다. 키 이름은 `str.<screen>.<name>`. 원문이 id를 정한 것: `phantom_first.master_line`, `revenge.master_line`.

### 0.4 의존 순서 (일정이 아니라 무엇이 먼저 있어야 하는가)

1. `IBackend`(+ `OfflineBackend`), `ProfileKind`, 저장 파일, `Wizard.Rules`(강화·정산 규칙) — 화면이 부르는 모든 값의 출처 (`on 8.3`: `OfflineBackend`가 먼저).
2. 공통 화면 기반: `ScreenStack`, `Interactable` 상태 규약(1.3절), `NameTable`/`StringTable`, 확인 창·툴팁.
3. `PresentationSettings`(14절의 연출 줄이기·번쩍 관리기·흔들림 누적기) — 첫 실행 물음이 이것을 쓴다.
4. 첫 실행 → 타이틀 → 프로필(오프라인) → 메인 메뉴 → 원정 준비 → HUD 모델 → 전리품 → 야영 → 정산 → 강화 화면. (`on 8.3` 자동 테스트 흐름: 처음 실행 → 판 → 정산 → 강화 → 다시 실행)
5. 서버 프로필 화면(로그인·가입·이메일), 광장 겹침 화면, 결투 화면, 거래·장터 — `ServerBackend`가 있어야 의미가 있다.

---

## 1. 공통 기반

### 1.1 화면 관리자

```csharp
enum ScreenLayer { Fullscreen, Panel, Modal, Toast, HudOverlay }

interface IScreen {
  ScreenId Id { get; }
  ScreenLayer Layer { get; }
  bool BlocksInput { get; }            // Modal = true
  void OnEnter(ScreenArgs args);
  void OnExit();
  bool OnBack();                       // Esc. true면 소비
}

class ScreenStack {
  // 전체 화면 메뉴는 같은 뼈대: 위 띠 + 아래 조작 안내 띠 + 내용 (ui 4 머리말)
  void Push(ScreenId id, ScreenArgs a);
  void Replace(ScreenId id, ScreenArgs a);
  void Pop();                          // "나가면 들어온 곳으로 돌아간다" (fx 1.1)
  ScreenId ReturnTarget;               // 강화 화면처럼 진입점이 둘 이상인 화면이 기억
}
```

- 진입점이 여럿인 화면(강화, 원정 준비, 허수아비 연습, 상점, 채널 고르기, 결투 게시판)은 **같은 화면 하나**이고 `ScreenArgs.entry`로 진입점만 다르다 [확정] (`fx 1.1`, `ui 4.6`, `ui 4.14`, `ui 4.21`, `ui 4.23`, `on 2.4`).
- 광장에서 연 전체 화면은 광장을 덮는다. 그동안 내 캐릭터는 광장에 서서 머리 위 말줄임표를 띄운다(15.1절) [확정] (`fx 1.1`, `on 1.4`).

`ScreenId` 목록 (원본 4절의 26개 항목 기준, `ui 7`):

| 묶음 | ScreenId |
|---|---|
| 시작 | `FirstRunPrompt`, `Title`, `ProfileSelect`, `Login`, `Signup`, `EmailVerify`, `PasswordForgot`, `PasswordReset`, `RecoveryMailOff`, `NewDeviceNotice`, `ConnectQueue`, `ServerUnreachable`, `OfflineGuide` |
| 메뉴 | `MainMenu`, `ExpeditionPrep`, `ChannelSelect`, `Records`, `TitlePicker`, `Settings`, `KeyRebind`, `Wardrobe`, `Shop` |
| 판 | `Loading`, `BattleHud`, `LootPick`, `Camp`, `ArtTrie`, `DummyPractice`, `Pause`, `ControlsView`, `Settlement` |
| 결투 | `DuelIntro`, `DuelHud`, `DuelRoundBreak`, `SpectatorHud`, `DuelResult`, `DuelBoard`, `DuelRoom`, `RoomPassword`, `MatchFound` |
| 강화 | `Enhance`, `WandPicker`, `WandReleaseConfirm`, `MutationRemove`, `SlotEngrave`, `EnhanceTutorial` |
| 광장 | `PlazaOverlay`(머리 위·대화창·상호작용), `EmoteWheel`, `NearbyList`, `PlazaMenu`, `IncomingRequest`, `Friends`, `Inspect`, `Report`, `BlockConfirm`, `Trade`, `TradeFinal`, `TradeLossWarning`, `Market`, `MarketBuyConfirm`, `ShopBuyConfirm`, `Mailbox`, `MailCompose`, `MasterChair`, `SeasonInfo`, `SuccessionScore`, `SeasonReward`, `League`, `Leaderboard`, `Monument` |
| 공용 | `ConfirmDialog`, `Tooltip`, `Disconnected`, `RunSubmitPending`, `SaveRestored`, `OpsNotice`, `SanctionNotice`, `ExpeditionCleanupConfirm`, `DebtList` |

### 1.2 프로필 종류와 메뉴 구성

```csharp
enum ProfileKind { Offline, Server }

interface IBackend {
  ProfileKind Kind { get; }           // "이 기능을 지원하는가"가 아니라 프로필 종류 (on 8.3)
  ServerFeatureFlags Flags { get; }   // 서버만: recovery_mail_enabled 등 (on 7.3)
  // EnhanceAsync, SubmitRunAsync, ShopBuyAsync 등은 각 명세
}
```

- 화면은 `Kind`를 보고 메뉴 구성을 정한다. 있는 기능은 두 구현이 같은 모양으로 답한다 [조율] (`on 8.3`).
- 두 프로필이 같이 쓰는 것은 **설정과 키 배치뿐**이다 [조율] (`on 8.2`, `on 1.1`, `ui 4.19`). 저장 위치: 기기. 서버 프로필은 계정에도 동기화 (`on 1.1`).
- 프로필 전환은 **타이틀에서만** 한다 [조율] (`on 8.2`, `ui 4.2`).

서버에서만 열리는 기능 (`ServerOnlyFeature`) — 오프라인 프로필에서는 **흐리게 두지 않고** 묶음 상자 하나에 넣고 "서버에 접속하면 열립니다" 한 줄을 붙인다 [조율] (`gdd 13`, `on 8.1`, `ui 4.4`, `ui 8`):

| id | 기능 | 메인 메뉴 위치 |
|---|---|---|
| `duel` | 결투(사람 상대) | 모드 카드 |
| `plaza` | 광장 | 모드 카드 |
| `mailbox` | 우편함 | 메뉴 목록 |
| `friends` | 친구 | 메뉴 목록 |
| `leaderboard` | 순위표 | 메뉴 목록 |
| `season` | 시즌 | 메뉴 목록 |
| `market` | 장터 | 메뉴 목록 |

오프라인에서도 되는 것: 무한 웨이브, 조립, 서고, 강화 화면(슬롯 새기기·변이 제거 포함), 상점(같은 값, 같은 주간 한도는 기기 시계 기준), 허수아비 연습, 봇 상대 결투 연습, 옷장, 기록(기기 안 개인 기록), 주간 도전(첫 만남 뒤, 주간 보상 없음), 허상 첫 만남·복수 보상·복수 추적기 [조율] (`on 8.1`, `ui 4.4`, `ui 4.21`, `ui 4.24`).

### 1.3 누를 수 있는 것의 상태 규약

모든 누를 수 있는 것(버튼, 탭, 칸, 목록 줄, 카드)이 같은 규약을 쓴다 [제안] (`ui 1.3`).

```csharp
[Flags] enum InteractVisual { None=0, Hover=1, Cursor=2, Confirmed=4, Pressed=8, On=16, Disabled=32 }

class Interactable {
  bool enabled;                 // false = 꺼짐
  string disabledReasonKey;     // 꺼짐일 때 아래 한 줄/툴팁 ("골드가 모자란다" 등)
  bool isToggle; bool toggledOn;// 탭·토글·거르기
  bool confirmed;               // 이미 고른 것(확정)
  // Hover/Cursor/Pressed는 InputModeTracker와 커서 위치로 계산
}
```

| 상태 | 언제 | 로직 규칙 |
|---|---|---|
| 기본 `Normal` | 아무 일도 없을 때 | — |
| 올림 `Hover` | 마우스가 위에 있을 때(**마우스 전용**) | 선택 괄호는 **붙지 않는다** |
| 선택 `Cursor` | 키보드 커서가 있는 것, 또는 지금 고른 것 | 선택 괄호가 붙는다. 마우스로 눌러 고른 것에도 붙는다 |
| 누름 `Pressed` | 누르고 있는 동안 | — |
| 켜짐 `On` | 탭·토글·거르기가 켜져 있을 때 | — |
| 꺼짐 `Disabled` | 지금 쓸 수 없을 때 | **커서는 갈 수 있다.** 그때 괄호는 회청이고 아래 한 줄에 이유를 적는다. [임시] 눌러도 아무 일 없고 이유를 다시 보여 준다 |

선택 괄호 로직 [제안] (`ui 1.3`, `gdd 13`: "선택 표시는 노란 모서리 괄호로 통일"):

- 크기 3종 `BracketSize`: `Small`(한 변 64px 이하의 대상), `Medium`(65~240px), `Large`(그보다 큰 카드와 판). 고르는 식: `L = max(w,h); L <= 64 ? Small : L <= 240 ? Medium : Large` [임시: "한 변"을 긴 변으로 해석].
- 커서(지금 가리키는 것)의 괄호는 **0.5초(= 30 t) 주기**로 2px 벌어졌다 오므라든다(2프레임). 번쩍이지 않는다.
- 확정(이미 고른 것)은 괄호가 멈추고 확정 체크가 붙는다. **커서와 확정이 한 화면에 동시에 있을 수 있다**(캐릭터 선택에서 고른 캐릭터와 지금 보는 캐릭터).
- 괄호는 화면 그림 층에만 그린다. **전장 안의 적·탄·지팡이에는 괄호를 그리지 않는다.**

입력 장치 모드 `InputModeTracker` [제안] (`ui 1.3`: 마지막으로 쓴 쪽을 따른다):

```
state: lastDevice ∈ {Keyboard, Mouse}
on KeyDown(any)        → lastDevice = Keyboard; 모든 Hover 해제; 커서 대상에 괄호 표시
on MouseMove(delta≠0)  → lastDevice = Mouse;    괄호는 "고른 것"에만 남김
Hover 표시 = (lastDevice == Mouse) && pointerOver
```

### 1.4 글자 표시 규칙 (로직 부분) [제안] (`ui 1.4`, `ui 0`, `ui 4.20`)

| 규칙 | 값 |
|---|---|
| 넘침 | 줄이지 않고 끝을 "…"로 자른다. 전체 글은 툴팁 |
| 폭 계산 | 한글 한 자 = 글자 크기, 숫자·영문 한 자 = 글자 크기의 절반 |
| 숫자 | 세 자리마다 쉼표(48,200). 숫자 글꼴은 고정폭 |
| 이름 칸 | 캐릭터 카드, 초상 옆, 칭호 띠, 순위 줄, 알림의 이름 칸은 **한글 8자** 폭(F-16 기준 128px) |
| 닉네임 | 2~8자 (`on 7.3`). 목록 칸은 한글 8자 폭. 넘으면 "…" |
| 글자 크기 | 12/16/24/32/48/64px 여섯 가지만. 12px보다 작은 글자 없음 |

```csharp
static int TextWidthPx(string s, int fontPx) {
  int half = 0;                                   // 반 글자 단위로 센다(정수 유지)
  foreach (var r in s.EnumerateRunes()) half += IsWide(r) ? 2 : 1;   // 한글·기호 = 2, 숫자·영문 = 1
  return half * fontPx / 2;
}
static string Ellipsize(string s, int maxPx, int fontPx);  // 끝에서 한 자씩 빼며 (s + "…") 폭 ≤ maxPx
static string FormatThousands(long v) => v.ToString("N0", CultureInfo.InvariantCulture);  // 48,200
```

### 1.5 공용 창 (`ui 4.26`)

| 창 | 데이터 | 버튼 | 규칙 |
|---|---|---|---|
| `ConfirmDialog` | 물음 한 줄, 설명 두 줄 | [예] [아니오] | 조심할 일은 [예]가 "조심 버튼". 조심 버튼은 빨강이 아니라 호박색 테 + 경고 삼각 [제안] (`ui 1.2`) |
| `Loading` | 한 줄 도움말, 진행률 0~1 | 없음 | — |
| `Disconnected` | "까마귀가 돌아오지 않는다.", 다시 잇는 중이면 남은 시간 | 2개 [임시: 다시 시도 / 오프라인 프로필로] | 이미 시작한 판은 끝까지 하고 나중에 낸다는 안내 (`on 8.2`, `on 3.2`) |
| `RunSubmitPending` (토스트) | "지난 판을 서버에 내는 중" | 없음 | — |
| `SaveRestored` | 되돌린 사실 | [확인] | 오프라인 저장 파일이 깨져 직전 세대로 되돌렸을 때 (`on 8.3`: 직전 3세대 보관) |
| `OpsNotice` | 점검, 시즌 일정(스크롤) | [닫기] | — |
| `SanctionNotice` | 막힌 기능, 기간, 사유 | [확인] | — |
| `Tooltip` | 물건 이름(등급색), 종류와 속성, 수치, 한 줄 설명, 거래 가능 여부 | — | 최대 폭 400px |

### 1.6 서버 전체 알림 받기 `NotificationRouter` [조율] (`fx 8`, `on 1.4`, `on 5.3`, `DEC 2.4`)

```csharp
enum NoticeKind { Plus10to12, Plus13 }
record ServerNotice(NoticeKind kind, int reachedStage, string wandTypeId, int element,
                    string? nickname /* null = "어느 제자의 …" */, long serverTimeMs);
enum NoticeSurface { PlazaBand, MenuLine, DeferredCamp, DeferredDuelResult }
```

```
on ServerNotice n:
  if profile.Kind == Offline: return                      // 내보내지도 받지도 않는다
  if settings.server_enhance_notice == Off: return
  if settings.server_enhance_notice == Plus13Only && n.kind != Plus13: return
  surface = context:
     InPlaza                                   → PlazaBand
     InMenu (메인 메뉴, 강화 화면, 그 밖의 메뉴)   → MenuLine
     InBattle (무한 웨이브 전투)                 → DeferredCamp        // 다음 야영의 한 줄 소식
     InDuel                                    → DeferredDuelResult  // 결투 결과의 "미뤄 둔 소식"
  if surface is Deferred*: deferred.Add(n); return
  if surface == MenuLine && enhanceRitual.IsPlaying: 의식이 끝난 뒤에 낸다
  queue.Enqueue(n)   // Plus13은 줄 맨 앞으로

queue 소비:
  Plus13은 10초(= 600 t)에 하나, Plus10to12는 6초(= 360 t)에 하나씩 차례로
  밀린 Plus10to12가 10개를 넘으면 한 줄로 묶는다: "방금 +10 이상 성공 {count}건"
표시 길이: 광장 알림 띠 4초(240 t) / Plus13 6초(360 t, 금 테). 메뉴 한 줄 소식 4초 / Plus13 6초(금 테).
광장 띠의 4초(6초)는 띠가 펼쳐진 때부터 센다(까마귀가 1.2초에 지나가며 0.6초 시점에 쪽지를 놓음).
```

- 알리는 것은 **성공뿐**. 실패·하락·변이·"도전 중"은 누구에게도 가지 않는다 [확정] (`fx 8`, `gdd 9`).
- 대성공으로 두 단계를 한 번에 올라 +10 이상이 되면 **도착한 단계로 한 번만** 낸다 [제안] (`fx 8`).
- 보내는 쪽 이름: 계정 설정 `enhance_notice_show_name`(켬/끔, 기본 켬). 끄면 닉네임 없이 "어느 제자의 …" [제안] (`on 1.4`, `fx 8`).
- 문구 [제안] (`fx 8`): +10~+12 "{닉네임}의 {지팡이 이름}이 +{n}이 되었다." / +13 "{닉네임} — 엄청엄청 위대한 {캐릭터 표시 이름}가 나왔다. {스승 이름}: …그건 나도 못 해 봤다." 이름 셋은 이름 표에서 읽는다(지금은 "엄청엄청 위대한 제자 1").
- 광장에 있을 때는 대화창 시스템 채널에도 한 줄 [확정] (`fx 8`, `on 5.3`).
- 알림에 싣는 것: 지팡이 아이콘, 지팡이 이름, 속성색, 닉네임 (`fx 8`).
- 알림 띠는 번쩍과 흔들림을 쓰지 않고 전장 위에는 아무것도 그리지 않는다 (`fx 8`).

---

## 2. 첫 실행과 타이틀

### 2.1 부팅 흐름 (`BootFlow`)

```
Boot
 ├─ settings 파일 없음(=기기 첫 실행) ──▶ FirstRunPrompt ──(선택 즉시 저장)──▶ Title
 └─ settings 있음 ─────────────────────────────────────────────▶ Title
첫 실행이면 오프라인 프로필을 자동으로 만든다(계정 불필요, 이메일 없음).
```

- 게임을 처음 켰을 때 **타이틀보다 먼저 한 번** 묻는다: "번쩍임과 흔들림을 줄일까요?" [확정 문구는 fx 0.4] (`ui 4.1`, `fx 0.4`).
- 오프라인 프로필은 게임을 처음 켰을 때 자동으로 생긴다. 시작 상태는 `on 1.1`의 "새 프로필의 시작 상태"(캐릭터 5명, 각자 +0 지팡이 1자루, 코어에 마력탄 1개씩, 골드·재료 0) [조율] (`on 8.1`, `gdd 2.1`).

### 2.2 첫 실행 물음 `FirstRunPrompt` (`ui 4.1`, `fx 0.4`)

| 항목 | 내용 |
|---|---|
| 바탕 | 단색. **번쩍임이 없는 화면이어야 한다.** 이 화면에서는 번쩍 관리기 요청을 하나도 내지 않는다 |
| 선택지 3개 | `Full`(전체) / `Reduced`(줄임) / `Minimal`(최소). 각 선택지에 설명 |
| 확정 | 고르면 **바로** `settings.motion_preset`에 저장하고 `Title`로 간다. 확인 단계 없음 |
| 커서 기본 위치 | [임시] `Full`(설정 기본값 `fx 18`: 연출 줄이기 기본 "전체") |
| Esc / 창 닫기 | [임시] 선택하지 않고 끄면 저장하지 않는다 → 다음 실행에 다시 묻는다 |

상태: `Asking → Saved`. 전이 조건: 선택지 하나를 확정.

### 2.3 타이틀 `Title` (`ui 4.2`, `on 8.2`, `gdd 13`)

```csharp
record TitleModel(
  ProfileKind kind,
  string? nickname,            // 서버 프로필이면
  ServerStatus? serverStatus,  // 서버 프로필이면: Online / Unreachable / Queue
  string buildVersion, string credits);
```

| 요소 | 규칙 |
|---|---|
| 프로필 표지 | **지금 어느 프로필인지 항상 보인다.** 서버 프로필이면 닉네임과 서버 상태 [조율] |
| [프로필 바꾸기] | `ProfileSelect`로. 프로필 전환은 타이틀에서만 [조율] |
| 시작 안내 | "J를 눌러 시작". 2초(= 120 t) 주기로 **밝기만** 바뀐다. 깜박이지 않는다 |
| J | 지금 프로필로 진행: Offline → (처음이면 `OfflineGuide`) → `MainMenu`. Server → 저장된 갱신 토큰으로 접속 시도 → 성공 `MainMenu` / 실패 `Login` 또는 `ServerUnreachable` / 상한 초과 `ConnectQueue` |
| 판 번호, 만든 이 | 표시만 |

---

## 3. 프로필 고르기, 로그인, 가입, 이메일, 복구

규칙의 기준은 `on 7.3`, `on 8`이다. 이 절은 화면 상태와 검사만 옮긴다.

### 3.1 상태 기계 (`AuthFlow`)

```
ProfileSelect
 ├─[오프라인 카드]────────────────────────────────▶ (첫 진입이면 OfflineGuide) ▶ MainMenu
 └─[서버 카드]──▶ Login
Login
 ├─[로그인] ok ─────────▶ (새 기기면 NewDeviceNotice) ▶ (이메일 미확인 꼬리표 갱신) ▶ MainMenu
 │          fail ───────▶ Login(오류 줄: 단일 문구, 대기 중이면 남은 시간)
 │          unreachable ▶ ServerUnreachable
 │          over_cap ───▶ ConnectQueue
 ├─[가입] ─────────────▶ Signup
 └─[비밀번호를 잊었다]
        recovery_mail_enabled ? PasswordForgot : RecoveryMailOff
Signup ──[가입] ok──▶ recovery_mail_enabled ? EmailVerify : (쪽지 "지금은 확인 메일을 보낼 수 없다. 설정 > 계정에서 나중에 확인한다") ▶ MainMenu
EmailVerify
 ├─[확인] code ok ─▶ MainMenu (이메일 확인됨)
 │        wrong ───▶ EmailVerify(칸 오류 상태, 남은 횟수 표시)
 ├─[다시 보내기] (60초 뒤 켜짐, 하루 5회)
 └─[나중에 하기] ─▶ 경고 1회 "확인하지 않으면 비밀번호를 잃었을 때 되찾을 수 없다" ─▶ MainMenu
PasswordForgot
 ├─[재설정 코드 보내기](아이디+이메일) ─▶ 항상 같은 답 ─▶ PasswordReset
 └─[아이디 찾기](이메일만) ─▶ 항상 같은 답
PasswordReset ──[확인] ok──▶ 안내("모든 기기에서 로그아웃되었다. 24시간 동안 거래와 장터 등록이 막힌다") ▶ Login
RecoveryMailOff ──[닫기]──▶ Login
ServerUnreachable ──[다시 시도]──▶ Login 재시도 / [오프라인 프로필로 하기] ──▶ 오프라인 프로필 ▶ MainMenu
ConnectQueue ──(내 차례)──▶ MainMenu / [오프라인 프로필로 하기]
```

### 3.2 입력 검사 (클라이언트에서 먼저, 서버가 다시) [제안] (`on 7.3`, `ui 4.3`)

| 칸 | 규칙 | 칸 아래 조건 한 줄 |
|---|---|---|
| 아이디 `login_id` | `^[a-z0-9]{4,16}$`, 유일 | "영문 소문자·숫자 4~16자" [임시 문구] |
| 비밀번호 | 길이 ≥ 10. 문자 종류 강제 없음. 흔한 비밀번호 상위 10만 개 거절, **아이디를 포함한 비밀번호 거절** | "10자 이상" |
| 비밀번호 확인 | 위와 같아야 함 | — |
| 닉네임 | 한글·영문·숫자 2~8자 [제안] | "2~8자" |
| 이메일 | 형식만 검사: `@`와 도메인 꼴, **254자 이하** [개발자: 가입 때 받는다] | "비밀번호를 잃었을 때 이 주소로 되찾는다. 다른 데에는 쓰지 않는다" |

```csharp
static bool IsValidLoginId(string s) => Regex.IsMatch(s, "^[a-z0-9]{4,16}$");
static bool IsValidPassword(string pw, string loginId, ISet<string> common)
  => pw.Length >= 10 && !common.Contains(pw) && !pw.Contains(loginId, StringComparison.OrdinalIgnoreCase); // [임시] 대소문자 무시
static bool IsValidNickname(string s) => s.Length is >= 2 and <= 8 && s.All(c => IsHangulSyllable(c) || IsAsciiLetterOrDigit(c));
static bool IsValidEmailShape(string s) => s.Length <= 254 && Regex.IsMatch(s, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"); // [임시] 꼴 검사식
```

- 비밀번호 칸에는 가림 점과 보기 단추(눈 2상태) [제안] (`ui 4.3`).

### 3.3 서버 답과 화면 (`on 7.3`)

| 경우 | 서버 규칙 | 화면 |
|---|---|---|
| 로그인 실패 | 계정당 5회 틀리면 1초부터 두 배씩 늘어나는 대기(최대 15분). IP당 분당 20회. 오류 문구는 **하나만** | 오류 줄: 단일 문구 + 대기 중이면 남은 시간(K-36) |
| 동시 접속 | 계정당 하나. 새 접속이 기존 접속을 끊는다 | 끊긴 쪽은 `Disconnected` [임시] |
| 새 기기 | 24시간 동안 거래와 장터 등록 막음 | `NewDeviceNotice`: "이 기기에서는 24시간 동안 거래와 장터 등록이 막힌다" |
| 접속자 상한 | `config`의 상한(시작값 400) (`on 7.5`) | `ConnectQueue`: 내 순서, [오프라인 프로필로 하기] |
| 서버에 닿지 않음 | — | `ServerUnreachable`: "까마귀가 아직 안 왔다." [다시 시도] [오프라인 프로필로 하기] |

로그인 대기 시간 함수 [제안] (`on 7.3`):

```csharp
// failCount = 연속 실패 수. 5회째 실패 뒤부터 대기.
static TimeSpan LockoutAfter(int failCount) {
  if (failCount < 5) return TimeSpan.Zero;
  int k = failCount - 5;                       // 5회째 실패 → 1초
  double sec = Math.Min(1 << Math.Min(k, 30), 900); // 최대 15분 = 900초
  return TimeSpan.FromSeconds(sec);
}
```
[임시: "5회 틀리면 1초"를 5번째 실패 직후 1초로 해석. 6번째 2초, 7번째 4초 … 14번째 512초, 15번째부터 900초. 성공하면 failCount = 0]

### 3.4 이메일 확인 `EmailVerify` [제안] (`on 7.3`, `ui 4.3`)

| 항목 | 값 |
|---|---|
| 주소 표시 | 가린 꼴 `a***@d***.com` |
| 코드 | 숫자 6자리. 칸은 숫자만 받는다 |
| 수명 | 15분 (= 54,000 t) |
| 틀림 | 5번 틀리면 폐기. 틀리면 칸이 오류 상태(호박색 테)가 되고 **남은 횟수**를 적는다 |
| 다시 보내기 | 60초(= 3,600 t)에 1회, 하루 5회. 60초가 지나야 켜지고 남은 시간을 보여 준다 |
| 나중에 하기 | 경고 한 번 더 보여 주고 넘어간다 |
| 미확인 상태 | **로그인과 플레이는 전부 된다.** 메인 메뉴와 설정 > 계정에 "이메일 미확인: 비밀번호를 잃으면 되찾을 수 없다"와 [확인 코드 보내기]를 계속 띄운다 |
| 메일 꺼짐 | 가입 직후 확인 판 대신 쪽지: "지금은 확인 메일을 보낼 수 없다. 설정 > 계정에서 나중에 확인한다" |

```csharp
static string MaskEmail(string email) {
  // "alice@daum.net" → "a***@d***.net"  [임시: 첫 글자 + *** , 도메인 첫 글자 + *** + 마지막 점 이후]
  var at = email.IndexOf('@'); var local = email[..at]; var dom = email[(at+1)..];
  var dot = dom.LastIndexOf('.');
  return $"{local[0]}***@{dom[0]}***{dom[dot..]}";
}
```

### 3.5 비밀번호 찾기·다시 정하기 [제안] (`on 7.3`, `ui 4.3`)

| 항목 | 값 |
|---|---|
| 요청 | 아이디 + 이메일. 서버는 **맞든 틀리든 같은 답을 같은 시간에** 준다. 화면 답: "맞는 계정이 있으면 메일을 보냈다. 30분 안에 코드를 넣어 달라" |
| 보내는 조건 | 아이디가 있고 이메일이 그 계정의 **확인된** 이메일과 같을 때만 |
| 재설정 코드 | 12자, 영문 대문자와 숫자에서 `0 O 1 I`를 뺀 32자 알파벳(약 60비트). **4자씩 세 묶음**으로 끊어 보이고 붙여넣기가 된다 |
| 한 번만 | 쓰면 폐기. 새 코드를 요청하면 앞 코드 전부 폐기. 5번 틀리면 폐기 |
| 만료 | 30분 (= 108,000 t) |
| 요청 한도 | 계정당 60초에 1회, 1시간에 3회, 하루 5회. IP당 1시간에 10회. 서버 전체 하루 500통(`config`). **한도를 넘어도 화면의 답은 같다** |
| 다시 정하기 칸 | 아이디, 재설정 코드, 새 비밀번호, 새 비밀번호 확인(규칙은 가입과 같다) |
| 성공 | 비밀번호 변경, 모든 기기 토큰 폐기, 알림 메일, 24시간 거래·장터 등록 막음. 화면: "모든 기기에서 로그아웃되었다. 24시간 동안 거래와 장터 등록이 막힌다" → `Login` |
| 아이디 찾기 | 이메일만 넣는다. 화면 답은 언제나 같다. 한도는 재설정 요청과 같이 센다 |
| 복구 메일 꺼짐 | 서버가 로그인 전 기능 값에 "복구 메일: 꺼짐". [비밀번호를 잊었다]는 `RecoveryMailOff`("지금은 복구 메일을 보낼 수 없다. 공지를 확인해 달라." [닫기])를 연다. 로그인 판의 [비밀번호를 잊었다] 옆에 꼬리표 "지금은 보낼 수 없음"(호박색). **로그인과 가입은 그대로 된다** |

```csharp
const string ResetAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 0,O,1,I 제외 → 32자
static string NormalizeResetCodeInput(string pasted) =>
  new string(pasted.ToUpperInvariant().Where(c => ResetAlphabet.Contains(c)).ToArray()); // 공백·하이픈 제거 [임시]
static string GroupResetCode(string code12) => $"{code12[..4]} {code12[4..8]} {code12[8..]}";
```

- 이전 판의 "복구 코드 판"(가입 때 16자 코드를 한 번 보여 주던 판)은 **없다** [확정] (`ui 4.3`, `on 7.3`).

---

## 4. 오프라인 안내와 메인 메뉴

### 4.1 오프라인 프로필 안내 `OfflineGuide` (`ui 4.4`, `DEC 2.6`, `on 8`)

- 언제: 오프라인 프로필로 **처음 들어갈 때 한 번**(`profile.offlineGuideSeen = false`일 때), 그리고 메인 메뉴의 묶음 상자에서 언제나 다시 연다 [제안].
- 내용: 제목 "오프라인 프로필". 세 줄 ① 무한 웨이브, 조립, 강화 화면, 상점, 연습이 전부 된다 ② 랭킹과 거래에 반영되지 않는다 ③ 서버로 옮길 수 없다 [확정 내용: DEC 2.6].
- 되는 것·안 되는 것 표: 두 열. **안 되는 것은 흐리게 하지 않고 빈 동그라미**로 표시.
- [확인] → `offlineGuideSeen = true` → `MainMenu`.

### 4.2 메인 메뉴 `MainMenu` (`ui 4.5`, `gdd 2.1`, `on 5.2`)

```csharp
record MainMenuModel(
  ProfileKind kind,
  string? nickname, string? titleId,
  long gold, long? seasonCoins,          // 시즌 주화는 서버 프로필
  bool hasUnreadMail,                    // 우편 새 소식 점(서버)
  bool emailUnverified,                  // 서버: 닉네임 옆 꼬리표 "이메일 미확인" → 설정 > 계정
  DebtSummary? debt,                     // "갚을 것 n" (17절)
  ModeCard[] modeCards,                  // 원정 / 결투 / 광장
  MenuItem[] menuItems,                  // 아래 표
  RevengeTrackerModel? revenge,          // 원정 카드 기록 줄 아래 한 줄 꼴 (16절)
  bool autoMatchWaiting, TimeSpan? autoMatchElapsed,
  ServerNotice? newsLine,
  int selectedCharacter, WandRef heldWand // 배경의 캐릭터와 지팡이(현재 단계)
);
```

모드 카드 3장 [제안] (`ui 4.5`):

| 카드 | 기록 한 줄 | 누르면 | 오프라인 |
|---|---|---|---|
| 원정(끝없는 웨이브) | 최고 웨이브. 복수 중이면 아래에 복수 추적기 한 줄 | `ExpeditionPrep(entry=MainMenu)` | 그대로 |
| 결투 | 리그와 점수 | `DuelBoard(entry=MainMenu)` (광장 도전 탭 없음) | 묶음 상자 안 |
| 광장 | 접속자 수 | `ChannelSelect` → `Loading` → 광장 | 묶음 상자 안 |

메뉴 목록 12줄 [제안] (`ui 4.5`: K-21 ×12, 높이 48, 간격 16, 합 752 → `12×48 + 11×16 = 752`):

| 순서 | id | 이름 | 여는 화면 | 서버 전용 |
|---|---|---|---|---|
| 1 | `enhance` | 강화 | `WandPicker` → `Enhance(entry=MainMenu)` | |
| 2 | `assembly_library` | 조립과 서고 | `Camp(mode=Library)` (탭 [조립 \| 서고]) | |
| 3 | `practice` | 연습 | `DummyPractice(entry=MainMenu)` | |
| 4 | `records` | 기록 | `Records` | |
| 5 | `wardrobe` | 옷장 | `Wardrobe` | |
| 6 | `shop` | 상점 | `Shop(kind=Gold)` | |
| 7 | `settings` | 설정 | `Settings` | |
| 8 | `mailbox` | 우편함 | `Mailbox` | ● |
| 9 | `friends` | 친구 | `Friends` | ● |
| 10 | `leaderboard` | 순위표 | `Leaderboard` | ● |
| 11 | `season` | 시즌 | `SeasonInfo` | ● |
| 12 | `market` | 장터 | `Market` | ● |

- 서버에서만 되는 다섯 줄은 **목록 아래쪽에 모아 둔다**. 오프라인 프로필에서는 묶음 상자 안에 넣고 "서버에 접속하면 열립니다" 한 줄 [조율] (`gdd 13`).
- 그 밖: 한 줄 소식(서버 알림, 1.6절), 자동 매치 대기 표시(대기 중일 때만), [나가기].
- 배경은 기기 시각에 따라 낮·해질녘·밤 색 [제안]. 시각 구간은 20절 미정 [임시].
- 오프라인 프로필은 부적과 +0 새 지팡이를 **메인 메뉴의 상점에서만** 산다 [제안] (`gdd 2.1`).

---

## 5. 원정 준비 `ExpeditionPrep` (`ui 4.6`, `gdd 2.2`, `on 1.2`, `on 1.6`, `mon 4.7`)

### 5.1 진입과 변형

| entry | 여는 곳 | 차이 |
|---|---|---|
| `MainMenu` | 메인 메뉴 "원정" | — |
| `PlazaGate` | 광장의 한뎃벌 출입문 | 같은 화면 |
| `DuelEntry` | 결투 입장(게시판·방·매치 뒤) | **시작 웨이브 줄이 빠진다**. [출발] 대신 "준비" |

### 5.2 화면 모델

```csharp
record ExpeditionPrepModel(
  int cursorCharacter, int? confirmedCharacter,     // 1..5. 커서와 확정이 함께 보인다
  CharacterCard[] cards,                            // 5장: 표시 이름(이름 표), 칭호, 속성
  StatsPanel stats,                                 // 능력치 11줄 + 패시브 + 지팡이 특성 + 고유 비기 1줄
  WandListModel wands,                              // 5.3절
  StartLoadout loadout,                             // 고른 지팡이의 출발 조립
  StartWaveOption[]? startWaves,                    // null = 줄 자체가 없다
  int selectedStartWave,
  RevengeTrackerModel? revenge,                     // 시작 웨이브 줄 위 한 줄 꼴
  DepartState depart);
```

### 5.3 지팡이 목록 (원정 준비·강화 화면 공용 `WandListModel`) [제안] (`on 1.6`, `ui 4.6`, `ui 4.12`)

```csharp
enum WandSort { StageDesc /*기본*/, StageAsc, RecentAcquired, IlDesc }
[Flags] enum WandFilter { None=0, HasMutation=1, HasExtSlot=2, TradeLocked=4, PlusZeroOnly=8 } // 강화 화면에만

record WandRow(WandId id, int character, int stage, int bestStage, int dropCount,
               int mutationCount, int extSlots /*0..3*/, int aura /*0..3*/, int il,
               long acquiredAt, bool isHeld, WandLock lockState);

[Flags] enum WandLock { None=0, OnExpedition=1, InDuel=2, MarketListed=4, PendingVerify=8, TradeLocked=16 }
```

```
SortRows(rows, sort):
  held = rows.Where(isHeld)            // 든 지팡이는 어느 정렬에서든 맨 위에 고정
  rest = rows.Where(!isHeld).OrderBy(sort 키)
         then by [임시] 단계 desc → IL desc → acquiredAt desc → id asc  (동률 깨기)
  return held ++ rest
고른 정렬은 기기에 기억한다(키: wand_list.sort).
목록은 세로 스크롤. 쪽으로 끊지 않는다. 캐릭터 탭마다 "n자루".
거르기 여러 개 = 모두 만족(AND) [임시].
```

- 개수 제한 없음 [확정] (`DEC 1.22`).
- 들어올 때 그 캐릭터의 **든 지팡이**가 먼저 골라져 있고 "든 지팡이" 꼬리표가 붙는다. 다른 지팡이를 고르면 [든 지팡이로]가 켜진다 [제안].
- 원정 중·거래 잠금 지팡이는 잠김 표시. 원정 준비에서 **고를 수 없는** 지팡이: `OnExpedition`, `InDuel` [임시: 판 하나 동시 규칙 on 1.6]. `TradeLocked`/`MarketListed`/`PendingVerify`는 원정에 들고 갈 수 있는지 원문에 없음 → 20절 [임시: 장터 등록 중은 불가, 나머지 가능].

### 5.4 출발 조건 (`DepartState`) [조율] (`on 1.1`, `on 3.2`, `ui 4.26`)

```
CanDepart():
  if selectedWand.loadout.core == empty:
       return Blocked("코어에 투사체 블록을 끼워야 출발할 수 있다")   // 코어 칸 경고 딱지, [조립 바꾸기]에 선택 괄호
  if selectedWand.lock has OnExpedition: return Blocked(잠김)
  if profile.phantomNeverMet && profile.hasUnsettledRun:
       return Blocked(그 판의 제출 또는 [원정 정리]를 먼저)            // on 3.2
  if kind == Server && !connected: return Blocked(오프라인 프로필 권유) // 서버가 씨앗을 줘야 시작 (on 3.2)
  return Ok
[출발] 꺼짐 + 이유 한 줄. 결투 입장에서도 코어 검사는 같다.
```

- [조립 바꾸기] → `Camp(mode=LoadoutEdit, wand)` (4.11절의 조립 화면).
- 경고가 있으면 조립 판에 경고 표시(K-71).

### 5.5 시작 웨이브 (`StartWaveOption`) [제안] (`mon 4.7`, `on 1.2`)

```csharp
// bestPassed: 그 캐릭터로 "넘긴" 최고 웨이브. 순위 대상(서버) 판은 이번 시즌 기록을 쓴다.
static int[] AllowedSkips(int bestPassed) {
  var list = new List<int> { 0 };
  for (int s = 10; s * 10 <= bestPassed * 7; s += 10) list.Add(s);   // s ≤ best × 0.7 (정수 비교)
  return list.ToArray();             // 첫 웨이브 = s + 1  (1, 11, 21, …)
}
bool ShowStartWaveRow(int bestPassed) => AllowedSkips(bestPassed).Length > 1; // 고를 수 없으면 줄 자체가 없다
```

- 표시 문구: "웨이브 1 / 11 / 21 …". 건너뛸 때 받는 점수와 "보상 없음"을 한 줄로 [제안] (`ui 4.6`).
- 건너뛴 점수(시작값) [제안] (`mon 4.7`): 10 → 8,398 / 20 → 28,859 / 30 → 64,764 / 40 → 120,409 / 50 → 195,829. 식은 일반 웨이브 `(B(w) × 10 + 200) × S(w) × 0.6`, 보스 웨이브 `3200 × S(w) × 0.6` (B(w)는 몬스터 명세). 화면은 규칙 라이브러리의 `SkippedScore(s)`를 부른다.
- 건너뛴 웨이브에서는 골드·블록·재료·시즌 주화가 나오지 않고 귀환 가방 칸에도 세지 않는다. 출발 상태: 체력 가득, 결정 0, 출발 조립 그대로. 건너뛴 구간의 비기 해금 차례는 출발 야영에서 차례대로 고른다 [제안].
- 주간 도전 판은 웨이브 1 출발 고정 (`on 3.3`).

### 5.6 원정 정리 (비정상 종료 판) [제안] (`ui 4.26`, `on 3.2`)

- [원정 정리] 버튼은 **종료 요약을 내지 못한 판이 있을 때만**, 지팡이 판의 "원정 중" 지팡이 아래에 나온다. 다른 기기에서도 된다.
- 확인 창 문구: "마지막 중간 지점(웨이브 N)까지 정산한다"(중간 지점이 없으면 "보상 없이 잠금만 푼다"), "그 기기에 판이 남아 있으면 그쪽에서 제출하는 편이 더 많이 받는다", "되돌릴 수 없다". [정리](조심 버튼) [그만두기].
- 중간 지점은 5웨이브마다. 귀환 가방은 등급 높은 것부터 자동. 순위에는 올리지 않는다.

---

## 6. 모드 진입과 채널 고르기 (`ui 4.7`, `on 5.1`)

```
원정: MainMenu 카드 → ExpeditionPrep → Loading → BattleHud
결투: MainMenu 카드 또는 광장 결투 게시판 → DuelBoard(자동 매치 / 방 목록 / 방 만들기 / 봇 상대 연습 / 내 전적)
      → ExpeditionPrep(entry=DuelEntry) → DuelIntro
광장: MainMenu 카드 → ChannelSelect → Loading → Plaza
```

채널 고르기 `ChannelSelect` [제안]:

| 항목 | 규칙 |
|---|---|
| 줄 | 채널 번호, 인원 "23 / 40", 친구가 있는 채널에는 친구 표시와 수 |
| 순서 | 맨 위 [알아서 들어가기]. 친구가 있는 채널이 위 [임시: 그 안에서는 친구 수 desc → 채널 번호 asc, 나머지는 채널 번호 asc] |
| 정원 | 인스턴스당 40명. 35명이 넘으면 새 인스턴스를 연다 (`on 5.1`) |
| 가득 참 | [임시] 40/40 채널 줄은 꺼짐 + "가득 찼다" |
| 서버 프로필 아님 | 이 판 대신 4.1절의 묶음 줄 안내 |
| 광장 메뉴의 [채널 옮기기] | 같은 판 |

---

## 7. 전투 HUD에 표시할 데이터

### 7.1 무한 웨이브 HUD 모델 (`gdd 13`, `ui 4.8`)

HUD는 **코어 상태를 읽기만** 한다. 화면 그림은 전장 바깥 여백에 두고, 전장 위에 겹치는 것은 캐릭터 둘레 표시와 피해 숫자뿐이다(적 예고를 가리지 않기 위해. 허수아비 방 튜토리얼 풍선만 예외) [제안].

```csharp
record BattleHudModel(
  // 맨 위
  int waveProgressPermille,        // 남은 적 체력 비율(0..1000). 오른쪽→왼쪽으로 줄어듦. 보스 웨이브에는 숨김
  // 좌상단
  int hpCells, int hpCellsMax,     // 4..8 (캐릭터별)
  int characterId, int wandStage,  // 초상 옆 "+7"
  // 상단 중앙
  int day, int wave,               // "3일째 웨이브 23", day = ceil(wave/10)
  TimeOfDay tod,                   // 낮/해질녘/밤/보스 아이콘
  VarianceId[] variances,          // 최대 2
  long elapsedTicks,               // 경과 시간(야영 시간 제외는 코어 규칙)
  int? revengeWavesLeft,           // 복수 중 && 11 ≤ wave ≤ 19 → 20 − wave
  BossHud? boss,                   // 보스 웨이브에만
  // 우상단
  long expeditionWallet, long score, int kills,
  // 캐릭터 주변
  HeatRingModel heat,              // 임계 시작 눈금 포함 (줄일 수 없음)
  CrystalCell[] crystals,          // 4..7칸, 그믐은 빚 칸(보라 테 빈 칸)
  int combo, ComboPosition comboPos,
  InputGlyph[] recentInputs,       // 최근 5개
  string nowKLine,                 // "K = 꿰뚫는 창 (◆×2)", 그믐은 뒤에 " 외상 N"
  Facing facingArrow,              // 전장 층 (줄일 수 없음)
  // 하단 중앙
  CircuitCell[] circuit,           // 코어 1 + 연결(최대 8) → 최대 9칸. 실행 중인 칸이 순서대로 켜짐
  // 그 밖
  HudNotice? notice,               // 한 줄 안내: "웨이브 클리어", "과열 잠금 3초"
  TutorialBalloon? tutorial,
  string? phantomLineKey);         // 첫 만남 쓰러짐 대사 판(phantom_first.master_line)

record BossHud(string bossNameId, int rageStacks, int weakElement, int resistElement,
               int hpPermille, int[] phaseTickPermille, bool finalRage, bool revengeTag,
               bool isPhantomFirstMeeting);
```

| 규칙 | 값 | 표시 |
|---|---|---|
| 날 계산 | `day = (wave + 9) / 10` (정수 올림) | [확정] (`gdd 2.2`: N일째 = 올림(웨이브 ÷ 10)) |
| 보스 웨이브 | `wave % 10 == 0` | [조율] (`DEC 2.3`) |
| 보스 웨이브 표시 | 웨이브 진행 바를 숨기고 보스 표시가 그 자리를 대신. 날·웨이브 줄은 한 줄로 줄여 아래로 | [제안] |
| 최종 격노 | 보스 바의 테만 빨강(HUD에 허용된 위험색 세 곳 중 하나) | [확정] (`fx 0.1`) |
| 복수 꼬리표 | 복수 중인 프로필이 웨이브 20의 허상을 만나면 이름 왼쪽에 "복수" | [확정] (`gdd 13`) |
| 첫 만남 체력 바 | 웨이브 10 첫 만남에서는 바가 50%에서 멈추지만 **눈금이나 자물쇠로 미리 알리지 않는다** | [제안] (`mon 3.9`) |
| 허상까지 n웨이브 | 복수 중 && 웨이브 11~19 → "허상까지 {20−wave}웨이브" | [확정] (`gdd 13`) |
| 체력 칸 | 4~8칸 | [제안] |
| 결정 칸 | 4~7칸 | [제안] |
| 회로 | 코어 1 + 연결 최대 8 = 9칸 | [제안] |
| 최근 입력 | 5개 + 이음줄 | [확정] (`gdd 13`) |
| 콤보 위치 | 옵션 `combo_position`: 캐릭터 옆(기본) / HUD(초상 옆) | [제안] (`fx 18`) |
| 줄일 수 없는 것 | 적 예고, 적 탄, 열 링, 피격 표시, 시야 화살표 — 어떤 옵션으로도 꺼지지 않는다 | [확정] (`gdd 13`, `fx 18`) |
| 첫 만남 대사 판 | 쓰러짐 연출 중 느루의 한마디(문자열 표 `phantom_first.master_line`). 초상 없음(목소리만). 그동안 입력 잠김, 예고 없음 | [제안] (`ui 4.8`, `fx 15.1`) |
| 과열 잠금 안내 | 한 줄 안내 머리에 남은 시간 딱지 | [제안] |

피해 숫자 종류 (`DamageNumberKind`) [확정] (`gdd 13`):

| kind | 모양 |
|---|---|
| `Normal` | 흰색 |
| `Critical` | 1.5배 크기 흰색 + 검정 굵은 테 + 느낌표. **금색을 쓰지 않는다** |
| `Weak` | 1.25배 + ▲ + 속성 문양 |
| `Resist` | 회청색 0.8배 + ▼ |
| `Gold` | 금색 "+숫자" |

피해 숫자 옵션: 투명도 0~100%, 표시 `All`/`CritWeakOnly`/`Off` (`fx 18`).

튜토리얼 풍선 (`gdd 13`, `ui 4.26`): 첫 판 전 허수아비 방에서 5단계 — ① 이동·발사 ② 자세 ③ 임계 사격 ④ 반동 사격 ⑤ 블록 끼우기 [제안]. 각 단계 완료 조건은 20절 [임시].

### 7.2 결투 HUD 모델 (`gdd 13`, `ui 4.9`, `on 4.12`)

무한 웨이브 HUD에서 웨이브 바, 날과 웨이브, 원정 지갑·점수·처치 수, 보스 표시를 빼고 아래를 더한다.

```csharp
record DuelHudModel(
  int myHp, int myHpMax,              // 바, 100마다 눈금 (결투 체력 = 100 × 체력 칸)
  int oppHp, int oppHpMax, string oppNickname, int oppCharacter,
  CrystalCell[] oppCrystals,
  RoundDot[] myRounds, RoundDot[] oppRounds,  // 각 최대 3. Empty / MyWin / OppWin / Draw
  long remainingTicks, bool thornsActive,
  MarginLevel margin,                 // Green ≥ 15ms / Yellow / Amber < 5ms (빨강 금지)
  bool inputDroppedFlash,             // 버려진 순간부터 1.5초(= 90 t)
  bool aimLine,                       // 내 화면에만. 0.5칸(= 32px)
  NamePlate myPlate, NamePlate oppPlate, // 깎여 적용되면 "+11 (적용 +9)"
  bool phraseWheelOpen,               // 경기 중 정해진 문구 8개만
  int? countdown);                    // 3, 2, 1 (번쩍 없이 크기만)
```

- 마감 여유 단계: 초록 / 선택 노랑 / 호박색, 막대 수 3 / 2 / 1. 세 번째 단계에 빨강을 쓰지 않는다 [조율] (`ui 4.9`, `gdd 13`, `on 4.12`). 경계: 초록 ≥ 15ms, 호박 < 5ms, 그 사이 노랑 [제안] (`on 4.12`).

```csharp
static MarginLevel ClassifyMargin(double marginMs) => marginMs >= 15 ? MarginLevel.Green : marginMs < 5 ? MarginLevel.Amber : MarginLevel.Yellow;
```

- 결투 시작 화면(매치가 잡힌 뒤 3초 = 180 t): 양쪽 닉네임, 칭호, 리그 휘장, 장착 IL, 깎여 적용 꼬리표, 방 규칙 줄(라운드 수와 시간, 켜진 옵션). 지팡이는 실제 단계 그대로 [제안].
- 라운드 사이(3초 = 180 t): 라운드 결과 한 줄, 양쪽 남은 체력, 그 라운드에 준 피해 [제안].
- 관전 HUD: 체력 바·결정·라운드·남은 시간은 같고 **마감 여유·입력 버려짐·조준선은 없다**. "관전 중 · 2초 늦게 본다" + [나가기] [제안].

### 7.3 일시정지 `Pause` (`ui 4.15`, `on 3.4`)

| 모드 | 버튼 | 게임 시간 |
|---|---|---|
| 무한 웨이브 | 계속, 설정, 조작 보기, 판 포기(조심 버튼), 메인 메뉴로 | 멈춘다 |
| 결투 | 계속, 설정, 기권(조심 버튼)뿐 | **멈추지 않는다** |

- 지금 판 요약: 캐릭터, 지팡이, 조립, 지금 점수, **누적 일시정지 시간**(순위 대상 판의 한도 안내: 판당 누적 10분까지 인정) [제안] (`on 3.4`).
- "판 포기" = 정상 종료(직접 넘긴 웨이브 전부 정산) [제안] (`on 3.2`). [임시] 확인 창 한 번.
- "메인 메뉴로"의 의미(판 포기와 같은가)는 20절 [임시: 판 포기 확인 창을 거친 뒤 정산 → 메인 메뉴].
- 조작 보기: **지금 키 배치 그대로**의 조작표(13.4절의 바인딩을 읽어 그린다).

---

## 8. 전리품, 야영, 조립

### 8.1 전리품 카드 고르기 `LootPick` (`ui 4.10`, `mon 6.2.1`, `mon 4.6`, `gdd 2.2`)

웨이브가 끝나면 **야영 화면보다 먼저** 뜬다.

| 때 | 카드 | 고르는 수 |
|---|---|---|
| 일반 웨이브 | 4 | 2 |
| 엘리트를 한 마리라도 잡은 웨이브 | 5 | 2 |
| 보스 처치 | 5 (1장 희귀 이상 확정, 웨이브 30부터 그 1장 영웅 확정) | 2 |
| 허상과의 첫 만남(첫 웨이브 10) | 4 (확정 칸 없음) | 2 |
| 웨이브 6 (1일째만) | 위에 더해 영웅 1장을 그냥 받는다(**빌린 블록**: "빌림" 꼬리표, 점선 테) | — |

```
state LootPickState { Revealing, Choosing, OverflowDiscard, Done }
Revealing: 카드가 한 장씩 0.1초(= 6 t) 간격으로 뒤집혀 뜬다
Choosing:
  toggle(card): picked.Contains(card) ? remove : (picked.Count < pickCount ? add : 무시)
  [다시 뽑기]: 원정 지갑 ≥ 30 → 지갑 −30, 같은 등급 확률로 카드 전부 새로(확정 칸은 확정 유지), picked 비움
  [확인] enabled ⇔ picked.Count == pickCount  ("고르는 수를 채워야 켜진다")
  확인 → storage.Count + pickCount > 12 ? OverflowDiscard : Done
OverflowDiscard: 버릴 블록을 골라 storage ≤ 12. 버린 블록은 그 자리에서 골드(일반 5, 희귀 15, 영웅 50; 빌린 블록 50)
Done → 전장(모닥불) → 모닥불 옆 J → Camp
```

- 카드 내용: 종류 꼬리표, 아이콘, 이름, 열·피해·간격 세 줄(해당하는 것만), 메모 한 줄. 커서와 고른 카드가 함께 보인다 [제안].
- 제목 문구: "전리품 · {pickCount}장을 고른다".

### 8.2 야영 화면 `Camp` (`ui 4.11`, `gdd 13`, `mon 4.6`)

`mode`: `Camp`(판 중, 탭 [조립 | 비기]) / `Library`(메인 메뉴 "조립과 서고", 탭 [조립 | 서고]) / `LoadoutEdit`(원정 준비의 [조립 바꾸기]). **어느 모드에도 강화 탭은 없다** [확정] (`DEC 2.3`, `gdd 13`).

```csharp
record CampModel(
  CampMode mode,
  string headerLine,               // "야영 · 3일째 웨이브 23 다음"
  long expeditionWallet, int hpCells, int hpCellsMax,
  WandSlots slots,                 // 첫 줄: 코어 + 연결 4칸(갈피 5칸). 둘째 줄: 확장 3칸
  CompileTreePreview tree,         // 8.3절
  AssemblyWarning[] warnings,      // 최대 3줄
  StorageModel storage,            // 판 중 보관함 12칸(6×2) + 고른 블록 설명
  CampStats stats,                 // 발사당 열, 임계 사격 가능 횟수, 허수아비 DPS
  NextWaveInfo? nextWave,          // Camp 모드에만
  ReturnBagPreview bagPreview,     // "지금 판을 끝내면 가져가는 귀환 가방 칸수와 영웅 한도" (mon 4.6)
  long healCost, long rerollCost,
  ServerNotice[] deferredNews,     // 전투 중 미뤄 둔 서버 알림
  RevengeTrackerModel? revenge,    // 다음 웨이브 칸 맨 아래 큰 꼴
  LibraryModel? library);          // Library 모드: 오른쪽 단이 서고의 거르기
```

| 기능 | 규칙 | 출처 |
|---|---|---|
| 조작 | [끼우기 J] [빼기 K] [서로 바꾸기] | [제안] `ui 4.11` |
| 체력 회복 | 비용 `30 + 5 × 웨이브` 골드, 같은 야영에서 두 번째부터 1.5배. **원정 지갑에서만** | [제안] `mon 4.6` |
| 새벽 야영 | 보스를 잡은 뒤의 야영. 체력 1칸 무료 회복 | [제안] `mon 4.6` |
| 다시 뽑기 | 30골드(원정 지갑) | [제안] `mon 4.6` |
| 허수아비 연습 | `DummyPractice(entry=Camp)` | [확정] `gdd 13` |
| 수치 | 발사당 열, 임계 사격 가능 횟수(**0이면 경고**), 허수아비 DPS. 조립을 바꾸면 바뀐 값이 0.5초(= 30 t) 밝아진다 | [확정 구성] `gdd 13` |
| 경고 줄 | 조립 경고 최대 3줄. 줄을 고르면 트리의 그 마디에 선택 괄호 | [제안] |
| 다음 웨이브 | 시간대, 틀 이름, 변덕(최대 2), 나올 적의 약점·저항 문양 줄, 보스 이름(보스 웨이브), 확정 재료 | [확정 구성] `gdd 13`, `mon 4.6` |
| 첫 만남 전 웨이브 9 뒤 | 보스 이름 "스승의 허상"만 평소대로 뜬다(미리 알리지 않음) | [제안] `ui 4.11` |
| 출발 | 모닥불 반대편 "출발" 표식에서 **J를 1초(= 60 t)** 누르면 다음 웨이브. 화면에서 바로 출발도 된다. 야영을 건너뛰고 출발해도 된다 | [제안] `mon 4.6` |
| 시간 | 시간 제한 없음. 야영 중에는 점수의 시간 계산이 멈춘다 | [제안] `mon 4.6` |
| 시간대 전환 | 야영 중 4초에 걸쳐 바뀌고 전투 중에는 바뀌지 않는다 | [조율] `gdd 2.2` |

```csharp
static long HealCost(int wave, int healsThisCamp /*이미 한 횟수*/) {
  long baseCost = 30 + 5L * wave;
  return healsThisCamp == 0 ? baseCost : baseCost * 3 / 2;   // [임시] 1.5배는 내림, 두 번째 이후 매번 1.5배(누적 아님)
}
```

귀환 가방 미리보기 (`mon 6.2.4`): `bagSlots = min(12, passed / 3)`, `heroCap = passed / 10` (passed = 이번 판에서 직접 넘긴 웨이브 수, 건너뛴 웨이브 제외).

서고(Library 모드) [제안] (`ui 4.11`, `on 2.4`):

- 16종이 8×2로 놓인다. 칸: 아이콘 + 이름 + 수량. 수량 0(가진 적 있음)은 따로 된 칸 상태. **가진 적 없는 블록은 칸 자체가 없다.**
- 위에 "서고 87 / 100"(17절). 한도는 블록 개수 합계(지팡이에 끼워 둔 블록은 빼고), 100에서 시작, 50개당 2,000골드 확장.
- 끼워 둔 블록은 서고 수량에서 "끼움"으로 따로 센다. 같은 블록 한 개를 두 지팡이에 동시에 끼울 수 없다. "전부 빼기"로 서고로 돌린다 (`on 1.6`).
- 계정에 묶인 재료 수량 옆에 "(묶임 n)" (17절).

### 8.3 컴파일 트리 미리보기 모델 (`ui 4.11`, `gdd 4`)

조립을 바꾸는 **즉시** 다시 만든다. 화면은 규칙 라이브러리의 컴파일러 결과(실행 트리)를 그대로 그린다.

```csharp
record TreeNode(int order /*실행 순서 1,2,3…*/, BlockId block, NodeShape shape /*Projectile,Control,Modifier,ScopeOwner*/,
                int depth /*0..3*/, BlockId[] modifiers, int forkWays /*0,2,3*/,
                bool omitted, bool hasWarning, int circuitSlotIndex);
record ScopeTag(int depth, string multiplierText /*"×0.85"*/, bool galpiFootnote);
record CompileTreePreview(TreeNode[] nodes, ScopeTag[] scopes, int projectilesPerShot, int heatPerShot);
```

| 규칙 | 값 | 출처 |
|---|---|---|
| 스코프 깊이 | 최대 3단(들여쓰기 한 단 = 한 깊이) | [제안] `ui 4.11`, `gdd 4` |
| 변형 꼬리 | 그 스코프의 투사체 마디 오른쪽에 아이콘을 가로로. **한 줄에 최대 6개**, 넘으면 "+N" (N = 개수 − 6) | [제안] |
| 분열 | 2갈래 / 3갈래 가지 | [제안] |
| 스코프 배율 꼬리표 | `감쇠^depth`. 감쇠 0.85, 갈피의 지팡이만 0.92 + 각주 점(깊이만큼) | [제안] `gdd 4` |
| 생략 | 한 번 발사의 투사체 수가 상한(64개)을 넘어 생략되는 스코프는 점선 마디 + "생략" 꼬리표(가장 깊은 스코프부터 생략) | [제안] `gdd 4` |
| 경고 마디 | 경고 아이콘을 오른쪽 위에 | [제안] |
| 맨 아래 줄 | "발사 1회 = 투사체 {N}개 · 열 {H}" | [제안] |
| 연동 | 마디에 커서 → 회로의 그 칸에도 선택 괄호. 경고 줄 고름 → 그 마디에 괄호 | [제안] |
| 실행 순서 번호 | 마디 왼쪽 위에 1, 2, 3 … | [제안] |

```csharp
static string ScopeMultiplierText(int depth, bool galpi) {
  double d = galpi ? 0.92 : 0.85;
  double m = Math.Pow(d, depth);
  return "×" + Math.Round(m, 2, MidpointRounding.AwayFromZero).ToString("0.00"); // [임시] 소수 둘째 자리 반올림
}
```

### 8.4 비기 트라이 그림 (비기 탭) `ArtTrie` (`ui 4.13`, `gdd 8`)

- 그 캐릭터의 트라이만: 공용 10 + 고유 1. 다른 캐릭터의 ★는 없다 [제안].
- 깊이 0~4의 다섯 열. 기술 마디 / 지나가는 점 마디. 가지 위에 입력 문양(앞, 뒤, 옆, 반대쪽 옆, 섬). 모든 기술 마디의 끝에는 K.
- 간격 표시: 기본 0.3초(= 18 t)와 **다른 간격만** 적는다("0.2초", "0.18초").
- 마디 상태 `TrieNodeState`: `Unlocked`, `Locked`, `Candidate`, `Current`(2프레임). 고유 비기 마디는 ★, **새 프로필 첫 화면에서도 `Unlocked`** [확정] (`DEC 1.23`). `Locked`/`Candidate`는 공용 비기에만.
- 설명 판: 이름, 커맨드, 난이도 점, 결정 비용, 효과 두 줄, 결정이 모자랄 때 나가는 기술.
- 해금 차례: 후보 2개(1개뿐이면 1개)에서 하나를 고른다 [제안]. 차례는 웨이브 1~6을 넘길 때마다 1번, 공용 8종을 다 해금하면 더 오지 않는다 (`gdd 8`).
- 연습 중에는 방금 넣은 입력까지의 길이 밝아진다.

### 8.5 허수아비 연습 `DummyPractice` (`ui 4.14`, `gdd 13`)

진입: 메인 메뉴 "연습", 야영의 허수아비 연습, 광장의 연습 허수아비 — **같은 화면** [확정] (`gdd 13`).

| 요소 | 데이터 |
|---|---|
| 비기 목록 | 고르면 목표 커맨드가 된다. [트라이 보기] → `ArtTrie` |
| 목표 커맨드 | 넣어야 할 입력 줄. 넣은 만큼 밝아진다 |
| 입력 간격 표시 | 입력 사이마다: 허용 간격 = 막대 길이, 내 간격 = 표식. 넘으면 표식이 막대 밖(호박색) |
| 저스트 게이지 | 0.07초(≈ 4.2 t) 창. 방향 입력 순간부터 차고, K가 창 안이면 흰 별 |
| 실패 원인 한 줄 | "K가 0.03초 늦음", "3번째 입력이 0.02초 늦음", "결정 부족" |
| 허수아비 수치 | DPS, 최근 10초 피해, 임계 비율, 결정 1개당 피해 |
| 연습 옵션 | 결정 무한, 적 탄 쏘기, 열 고정, 결투 입력 지연 체험(지연 값 고르기) |

```csharp
static string LateMessage(int inputIndex /*1-based*/, bool isK, double lateSec)
  => isK ? $"K가 {lateSec:0.00}초 늦음" : $"{inputIndex}번째 입력이 {lateSec:0.00}초 늦음";
// 0.07초 = 4.2틱 → 코어의 1/256틱 오프셋 단위로는 0.07 × 60 × 256 = 1075.2 (판정은 코어 명세)
```

---

## 9. 강화 화면 상태 기계 `Enhance`

규칙(확률·비용·실패)은 `gdd 9`, `on 1.3`~`1.6`, `on 2.4`가 기준이고 **계산은 `Wizard.Rules`가 한다**(두 프로필이 같은 소스, `on 8.3`). 이 절은 화면이 무엇을 언제 보여 주고 무엇을 막는지만 정한다. 연출 그림·소리는 21절 이벤트로만 연결한다.

### 9.1 진입과 퇴장 [확정] (`DEC 1.7`, `DEC 2.4`, `fx 1.1`, `on 1.4`)

| 항목 | 규칙 |
|---|---|
| 들어가는 곳 | 메인 메뉴 "강화", 광장 메뉴(Esc) "강화". 둘 뿐. 광장에 강화용 장소·NPC 없음 |
| 나가기 | Esc → 들어온 곳으로 돌아간다(`ReturnTarget`) |
| 열 수 없는 때 | 판(원정) 중, 야영 중 |
| 결투 대기열 | 대기열에 있는 동안 열 수 있다. 매치가 잡히면 진행 중 연출을 건너뛰고 **결과만 보여 준 뒤** 결투로 넘어간다 (`on 1.4`) |
| 광장에서 열었을 때 | 화면 전체를 덮는다. 뒤에 광장이 비치지 않는다. 남에게는 머리 위 말줄임표만(서고·옷장·우편함과 같은 표시, 강화만의 표시 없음). 구경꾼 연출 없음 |
| 오프라인 | 같은 화면, 같은 확률, 같은 연출. 결과가 기기에서 바로 나온다(늦은 결과 대기 없음) |
| 들어올 때 | 강화할 지팡이를 고른다(`WandPicker`). 화면 안에서 **Q / E**로 지팡이를 바꾼다 |
| 키 | 메뉴이므로 마우스 가능, 키보드만으로도 된다: 방향키 칸 이동, J 넣기·강화, K 빼기, Esc 나가기 |

### 9.2 지팡이 고르기 `WandPicker` [제안] (`ui 4.12`, `on 1.6`)

- 캐릭터 탭 5개, 탭마다 "n자루". 목록은 5.3절의 `WandListModel`(정렬 넷 + **거르기 넷**: 변이 있음, 확장 슬롯 있음, 거래 잠금, +0만).
- 줄 데이터: 아이콘, 이름, 단계, 변이, 슬롯, 기운. 줄마다 [든 지팡이로](지금 든 지팡이에는 단추 대신 "든 지팡이" 꼬리표)와 [떠나보내기].
- 든 지팡이 정하기: 강화 화면과 원정 화면에서 바꾼다 (`on 1.6`).

떠나보내기 가능 판정 (`CanRelease`) [제안] (`on 1.6`, `on 3.2`):

```
CanRelease(w):
  if profile.debt.Any():                     return No("갚을 것이 남아 있다")
  if HandCount(w.character) <= 1:            return No("마지막 한 자루")      // 장터 등록 중·우편 미수령은 손에 있는 것으로 치지 않음
  if w.lock has OnExpedition:                return No("원정 중")
  if w.lock has InDuel:                      return No("결투 중")
  if w.lock has MarketListed:                return No("장터 등록 중")
  if w.lock has PendingVerify:               return No("검증 대기")
  if w.lock has TradeLocked:                 return No("거래 잠금")          // 받은 뒤 7일
  return Yes
HandCount(c) = 그 캐릭터의 지팡이 중 (보관 중 ∪ 원정 중 ∪ 결투 중) 인 것의 수
```

- 단추가 꺼지면 이유 툴팁(마지막 한 자루, 원정 중, 결투 중, 장터 등록 중, 검증 대기, 거래 잠금).
- 돌려받는 골드 = 감정가(`on 5.5`)의 10%. **+0은 0골드.** 끼워 둔 블록과 덧씌운 겉모습은 서고와 옷장으로 돌아온다. 되돌릴 수 없다.
- 확인은 **한 번**. **+7 이상**은 지팡이 이름을 입력 칸에 직접 입력해야 [떠나보내기]가 켜진다.

```
ReleaseConfirmEnabled(w, typed):
  w.stage < 7 || Normalize(typed) == Normalize(NameTable[w.wandTypeId])   // [임시] 앞뒤 공백 제거, 비교는 그대로
```

여러 자루 떠나보내기 [제안] (`ui 4.12`, `on 1.6`):

```
[여러 자루 고르기] → 줄마다 체크 상자
Checkable(w) = CanRelease(w) && w.stage <= 3 && w.mutationCount == 0 && w.extSlots == 0
고른 집합 S가 어떤 캐릭터 c의 손에 있는 지팡이를 모두 포함하면 마지막 체크를 막는다   // [조율] "마지막 한 자루" 규칙을 묶음에도 적용
아래 줄: "{|S|}자루 고름 · 돌려받는 골드 합계 {Σ refund}" + [떠나보내기]
확인 창 한 번, 자루 수와 합계를 다시 적는다
```

### 9.3 화면 모델 (`EnhanceViewModel`) (`fx 1.2`, `ui 4.12`, `gdd 13`)

```csharp
record EnhanceViewModel(
  // 위 띠
  WandRef wand, int stage, string? titleId,          // "◀ 지팡이 이름 ▶ +7 칭호"
  long gold, int stoneLow, int stoneMid, int stoneHigh,
  int boundLow, int boundMid, int boundHigh,          // "(묶임 n)" 계정에 묶인 강화석
  // 왼쪽 칸
  WandSlots slots, int extSlots /*0..3, "1/3"*/, MutationRef[] mutations, int mutationCap /*2, 초롱 3*/,
  int bestStage, int floorStage, bool traceExists, bool tracePublic,
  // 가운데
  SacrificeSlot[] sacrifices /*5*/, CharmSlot charm, int aura /*0..3*/,
  // 오른쪽
  int displayPercent, bool atCap,                     // 큰 숫자, "상한"
  ProbBreakdown breakdown,                            // 기본 / 제물 합 / 같은 속성 / 기운
  ResultBand band,                                    // 4구간
  FailHint failHint,                                  // 실패 안내 한 줄
  ExpectationTable expectation,
  // 아래 띠
  LibraryStrip candidates,                            // 제물 후보(서고). 한 줄 14칸, 좌우로 넘김
  Cost cost, bool canAfford, bool dropPossibleTag /*"하락 가능"*/,
  bool fastMode, bool autoContinueArmed,
  AutoContinueTally? tally);                          // "12번째 시도 · 성공 5 · 실패 7 · 쓴 골드 44,800"
```

### 9.4 확률 표시 계산 (표시용. 판정은 서버/로컬 규칙 코드)

[확정 표] 기본 확률(`DEC 1.18`): +1 100 / +2 90 / +3 75 / +4 60 / +5 50 / +6 40 / +7 32 / +8 25 / +9 18 / +10 12 / +11 8 / +12 5 / +13 3.

```csharp
static readonly int[] BasePct = { /*+1*/100, 90, 75, 60, 50, 40, 32, 25, 18, 12, 8, 5, 3 /*+13*/ };

record ProbBreakdown(int basePct, int sacrificePct /*상한 40*/, int sameElementPct /*0 or 5*/, int auraPct /*15×aura*/, int totalPct, bool capped);

static ProbBreakdown Compute(int stage /*현재 0..12*/, IList<BlockRef> sac, int wandElement, int aura) {
  int target = stage + 1;
  int b = BasePct[target - 1];
  if (target == 1) return new(b, 0, 0, 0, 100, false);          // +1 도전은 100% (상한 예외)
  int raw = sac.Sum(s => s.grade switch { Common => 4, Rare => 8, Hero => 14 });
  int sacPct = Math.Min(40, raw);                                 // 제물 합 상한 +40
  int same = sac.Any(s => s.element == wandElement) ? 5 : 0;      // 시도당 1회
  int au = 15 * Math.Clamp(aura, 0, 3);                           // 최대 +45
  int sum = b + sacPct + same + au;
  return new(b, sacPct, same, au, Math.Min(95, sum), sum >= 95); // 상한 95. 닿으면 "상한"
}
```
[조율: 같은 속성 +5는 제물 합 상한(+40) 밖에 둔다. `fx 1.2` 내역이 "제물 합(상한 +40)"과 "같은 속성 +5"를 다른 줄로 적고, 예시가 25 + 22 + 5 + 30 = 82로 맞는다. 19절 충돌 C-07]

- 큰 확률 숫자: 값이 바뀔 때마다 **0.2초(= 12 t)** 굴러서 바뀐다. 상한 95에 닿으면 숫자 옆 "상한" [제안].
- 내역 줄에 올리면 계산이 펼쳐진다.

결과 띠 4구간 (실제 비율, bp 정수) [제안] (`fx 1.2`, `gdd 9`):

```csharp
record ResultBand(int greatBp, int successBp, int mutationBp, int failBp, bool failIsDrop);
static ResultBand Band(int totalPct, int target, bool dropPossible) {
  int p = totalPct * 100;                                 // bp
  int great = target == 1 ? 0 : p * 15 / 100;             // 성공의 15% (+1 도전엔 없음)
  int succ = p - great;
  int failAll = 10000 - p;
  int mut = failAll * 25 / 100;                           // 실패의 25%
  return new(great, succ, mut, failAll - mut, dropPossible);
}
// 구간 색: 대성공(금) · 성공(속성색) · 변이(연두) · 하락 또는 하락 없음(회색). 무늬 민무늬/빗금/점/격자. 구간에 올리면 퍼센트
```

하락 가능 판정과 실패 안내 한 줄 [확정 규칙] (`gdd 9`, `on 1.3`, `fx 1.2`):

```csharp
static int Floor(int best) => Math.Max(0, best - 2);
static bool DropPossible(int stage, int best) => stage > 3 && stage > Floor(best);   // +3 이하 하락 없음, 바닥은 역대 최고 −2

enum FailHint { DropTo /*"실패하면: +{s-1}으로 하락"*/, NoDropLow /*"실패해도 내려가지 않는다(+3 이하)"*/,
                AtFloor /*"바닥이다(역대 최고 +{best})"*/, CharmProtects /*"부적이 막아 준다"*/ }
static FailHint Hint(int stage, int best, bool charmSet) {
  if (stage <= 3) return FailHint.NoDropLow;
  if (stage <= Floor(best)) return FailHint.AtFloor;
  return charmSet ? FailHint.CharmProtects : FailHint.DropTo;
}
```

| 화면 규칙 | 조건 | 출처 |
|---|---|---|
| "하락 가능" 꼬리표(호박색, 강화 버튼 테두리 위) | `DropPossible && !charmSet`. **확인 창은 띄우지 않는다** | [제안] `fx 1.2`, `ui 4.12` |
| 부적 칸 접힘 + "지금은 필요 없다" | `!DropPossible` | [제안] `fx 1.2` |
| 부적 | 시도 1회에 1개까지. 실패했을 때만 소모. 변이는 막지 않는다. 막은 실패에는 기운이 쌓이지 않는다 | [제안] `gdd 9` |

비용 [제안] (`on 1.3`, `on 2.4`):

```csharp
static long GoldCost(int stage) => 100L * (stage + 1) * (stage + 1);    // 현재 단계 기준. +13 도전(stage 12) = 16,900
static StoneTier StoneFor(int target) => target <= 5 ? StoneTier.Low : target <= 9 ? StoneTier.Mid : StoneTier.High; // 1개
// 계정에 묶인 강화석이 있으면 묶인 것부터 쓴다 (on 5.5, 17절)
```

- 모자라면 [강화 J]가 꺼지고 모자란 쪽이 흔들린다(`ui.enhance.cost_shake`) [제안] (`fx 1.2`).
- 마지막 투사체 블록은 제물 칸에 올릴 수 없다: 그 칸은 "놓을 수 없음" + 툴팁 "마지막 투사체 블록은 올릴 수 없다" [제안] (`on 1.1`, `ui 4.26`).
- 제물 칸 5개. 같은 속성 블록에는 속성 문양 점. 서고 블록에서 고른다(검증 대기 블록도 강화에는 쓸 수 있다, `on 3.2`).

### 9.5 상태 기계

```
                    ┌────────────────────────────────────────────────────────────┐
                    ▼                                                            │
 [WandPicker] ─▶ Idle ──J(강화, 조건 충족)──▶ Committing ──(결과 꾸러미 도착)──▶ Ritual
                  ▲  │                              │                             │
                  │  ├─J/K/방향키: 제물·부적 넣고 빼기 │ (서버: 요청 중. 오프라인: 즉시)  │
                  │  ├─Q/E: 지팡이 바꾸기             ▼                             │
                  │  ├─[슬롯 새기기] ▶ SlotEngrave   (Ritual 충전 끝까지 미도착)      │
                  │  ├─[변이 제거]   ▶ MutationRemove   ─▶ WaitingLate (게이지 97%) │
                  │  ├─[흔적 공개] 토글                     5초 초과 → "까마귀가 늦는다", 잠금 해제 → Idle(결과 대기 표시)
                  │  └─Esc ▶ ReturnTarget                  도착 → ShortResult
                  │                                                              │
                  │        Ritual = Payment → Offering → Charge → Hold → Judge   │
                  │                    (J 재입력: 누름 뒤 0.3초 무시, 이후 Skip → ShortResult)
                  │                                                              ▼
                  │                                                    ResultPlay(kind)
                  │                         kind==Mutation ─▶ MutationChoice(제한 없음, 건너뛸 수 없음)
                  │                                              ├ 받는다 ─▶ MutationAccept ─┐
                  │                                              └ 거절한다 ─▶ fallback의 0.50 이후 재생 ─┤
                  │                         milestone? ─▶ Milestone(+2.5초 / +0.8초) ──────────────────┤
                  │                         reached 13? ─▶ Plus13 (9초 / 일부)  ───────────────────────┤
                  │                                                                                     ▼
                  └──────────────────────────────────────── ResultCard ◀──────────────────────────────┘
                                 (J: 같은 구성으로 다시 시도 / 이어서 강화면 조건 검사 후 자동 Committing)
```

```csharp
enum EnhancePhase {
  Idle, Committing, Payment, Offering, Charge, Hold, WaitingLate, Judge,
  ResultPlay, MutationChoice, MutationAccept, MutationRejectReplay,
  Milestone, Plus13, ResultCard, ShortResult
}
```

전이 조건 (`EnhanceController`):

| 지금 | 사건 | 조건 | 다음 | 출처 |
|---|---|---|---|---|
| Idle | J(강화) | `canAfford && 지팡이 잠김 아님(원정 중·검증 중 등은 강화 가능 여부 20절)` | Committing. **화면의 다른 조작이 잠긴다.** 결과 요청이 나간다 | [제안] `fx 2` |
| Committing | — | 즉시(연출은 결과를 기다리지 않고 시작) | Payment | [제안] `fx 2` |
| Charge 끝 | 결과 미도착 | 서버 프로필 | WaitingLate: 게이지 97%에서 떨며 기다림, 심장 소리 최고 속도 유지 | [제안] `fx 2` |
| WaitingLate | 5초(= 300 t) 경과 | — | "까마귀가 늦는다" 표시, 조작 잠금 해제. 결과는 도착하는 대로 **짧은 형태**(9.8절) | [제안] `fx 2` |
| WaitingLate | 결과 도착 | 5초 이전 | Hold → Judge (정상 흐름) | [제안] |
| Ritual(어느 단계든) | J 재입력 | 누름 뒤 0.3초(= 18 t) 이후, 넘길 수 없는 구간 아님 | ShortResult(0.5초 = 30 t) | [제안] `fx 7` |
| Judge | — | `result.kind` | ResultPlay(kind) | [제안] `fx 3` |
| ResultPlay | 끝 | kind == MutationOffer | MutationChoice | [제안] `fx 3.6` |
| MutationChoice | [받는다] | — | MutationAccept(단계 유지, 기운 +1, 변이 표식) → ResultCard | [제안] `fx 3.6` |
| MutationChoice | [거절한다] | — | 돌려받은 결과(하락 / 부적이 막음 / 하락 없음)의 **0.50 이후**를 재생 → ResultCard | [제안] `fx 3.6` |
| ResultPlay 끝 | — | 마일스톤 단계 도달(+4, +7, +10) | Milestone | [제안] `fx 3.7` |
| ResultPlay 끝 | — | 도착 단계 13 | Plus13 | [제안] `fx 3.8` |
| ResultCard | J | 같은 구성 재시도 가능 | Committing | [제안] `fx 2` |
| ResultCard | 이어서 강화 켜짐 | 9.9절 멈춤 조건 아님 | Committing(자동) | [제안] `fx 7` |
| 어느 상태든 | 매치 성사(자동 매치) | — | 연출 건너뛰고 결과만 표시 → 결투로 | [제안] `on 1.4`, `ui 4.21` |
| 어느 상태든 | 접속 끊김 | 서버 | 결과는 이미 서버에 저장됨. 다시 들어오면 **결과 연출부터** 보여 준다 | [제안] `on 1.4`, `fx 2` |

- **결과는 버튼을 누른 순간 이미 정해져 있다.** 연출은 결과를 바꾸지 않고, 충전 길이·소리·움직임은 **결과와 무관하게 같다** [확정 원칙] (`fx A부`, `fx 2`).
- 속이지 않는다: 확률 숫자는 실제 값. 결과 카드에서 뽑힌 값을 볼 수 있다 [확정 원칙].
- **대성공은 항상 실패처럼 시작한다**(실패의 첫 0.5초와 같은 그림·소리) [제안] (`fx 3.2`, `fx 22` 1번).

### 9.6 시도 시간표 (강도별) [제안] (`fx 2`, `fx 4`)

도전 단계 → 강도:

```csharp
static int Intensity(int target) => target switch {
  1 => 0,                 // +1 예외: 충전 0.4초, 심장 소리·숨 멈춤 없이 바로 성공
  2 or 3 => 1, >= 4 and <= 6 => 2, >= 7 and <= 9 => 3, >= 10 and <= 12 => 4, 13 => 5 };
```

| 강도 | 도전 | 충전 | 숨 멈춤 | 심장(분당) | 음높이 | 카메라 | 배경 어두움 | 마법진 겹 | 충전 입자/초 | 배경음 |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | +1 | 0.4초 = 24 t | 없음 | 없음 | — | — | — | — | — | — |
| 1 | +2, +3 | 0.8초 = 48 t | 0.15초 = 9 t | 70 → 100 | +2반음 | 확대 없음 | 60% 그대로 | 1겹 | 10 | −6dB |
| 2 | +4 ~ +6 | 1.2초 = 72 t | 0.25초 = 15 t | 70 → 120 | +4반음 | 1.04배 | 70% | 2겹 | 20 | −9dB |
| 3 | +7 ~ +9 | 1.8초 = 108 t | 0.35초 = 21 t | 70 → 140 | +5반음 | 1.08배 | 75% | 3겹 | 35 | −12dB |
| 4 | +10 ~ +12 | 2.4초 = 144 t | 0.50초 = 30 t | 70 → 160 | +7반음 | 1.10배 | 85% | 4겹 | 50 | 끔 |
| 5 | +13 | 3.2초 = 192 t | 0.80초 = 48 t | 70 → 180 | +9반음 | 1.12배 | 90% | 5겹 + 바깥 고리 | 70 | 끔 |

구간 (강도 3 기준, `fx 2`):

| 구간 | 시각 | 틱 | 규칙 |
|---|---|---|---|
| Payment 값 치르기 | 0.00~0.15 | 0~9 | 골드 숫자가 굴러 내려가고 강화석 하나가 떨어진다 |
| Offering 제물 바치기 | 0.15~0.75 | 9~45 | 제물이 ①부터 **0.12초(= 7.2 t) 간격**. 부적이 있으면 마지막에 부적이 한 번 빛난다. **제물이 없으면 0.2초(= 12 t)** |
| Charge 충전 | 0.75~2.55 | 45~153 | 강도별 길이. 게이지: 처음 40% 시간에 80%까지, 남은 60% 시간에 마지막 20% |
| Hold 숨 멈춤 | 2.55~2.90 | 153~174 | 모두 멈춤, 모든 소리 끊김 |
| Judge 판정 | 2.90 | 174 | 결과 연출 시작 |

```csharp
// 시작 시각(초)을 강도·제물 유무로 계산. UiTick 정수로 들려면 ×60 후 반올림.
static (double offerEnd, double chargeEnd, double judge) Timeline(int intensity, bool hasSacrifice) {
  double pay = 0.15;
  double offer = hasSacrifice ? 0.60 : 0.20;
  double charge = new[] { 0.4, 0.8, 1.2, 1.8, 2.4, 3.2 }[intensity];
  double hold = new[] { 0.0, 0.15, 0.25, 0.35, 0.50, 0.80 }[intensity];
  return (pay + offer, pay + offer + charge, pay + offer + charge + hold);
}
static double ChargeGauge(double t01) => t01 <= 0.4 ? t01 / 0.4 * 0.8 : 0.8 + (t01 - 0.4) / 0.6 * 0.2; // [임시] 구간 선형
static int OfferTick(int i /*0-based*/) => 9 + (int)Math.Round(i * 7.2);   // 9, 16, 23, 31, 38 [임시] 반올림
```
[조율: 제물 5개면 마지막이 0.63초에 들어가고 0.75초에 끝난다. 0.12초 간격은 60틱에 나눠 떨어지지 않으므로 화면 틱에서 반올림한다]

### 9.7 결과별 길이와 결과 카드 [제안] (`fx 3`, `fx 3.9`)

```csharp
enum EnhanceResultKind { Success, GreatSuccess, FailDrop, FailCharmBlocked, FailNoDrop, MutationOffer }
```

| 결과 | 길이 | 번쩍 | 흔들림 | 화면 정지 | 어두워짐 | 결과 카드 문구 |
|---|---|---|---|---|---|---|
| Success | 2.0초 = 120 t | 속성 보조색 25% × 1 | 소(1~2단) / 중(3~5단) | 0.08초 | 없음 | 캐릭터 한마디 |
| GreatSuccess | 3.6초 = 216 t | 금색 35% × 1 | 대 | 0.12초 | 0.00~1.07 | 금 테. 확장 3칸이 이미 차 있으면 "슬롯은 이미 다 자랐다" |
| FailDrop | 2.2초 = 132 t | 없음 | 없음 | 없음 | 0.00~1.20 | 느루 "원래 그런 거다." + 흔적 줄 "흔적이 남았다 · 지금은 [비공개]"와 바로 바꾸는 단추 |
| FailCharmBlocked | 2.4초 = 144 t | 없음 | 없음 | 없음 | 0.00~1.00 | "부적 1개가 대신 탔다." |
| FailNoDrop | 1.6초 = 96 t | 없음 | 없음 | 없음 | 0.00~0.80 | "내려가지는 않았다." / 바닥이면 "더는 안 내려간다 (역대 최고 +{best})" |
| MutationOffer | 고를 때까지 | 없음 | 없음 | 없음 | 선택 때까지 | 변이 카드: 이름, 이득, 대가, [받는다] [거절한다]. 칸이 가득이면 "가장 오래된 {변이}가 밀려난다" |
| Milestone(첫 도달) | +2.5초 = 150 t | 없음 | 소 | 없음 | 없음 | 칭호 띠 "{옛 칭호} → {새 칭호}" |
| Milestone(다시 도달) | +0.8초 = 48 t | 없음 | — | — | — | 칭호 띠만 작게 |
| Plus13 | 9초 = 540 t | 흰 금색 35% × 1 | 특대 | 0.15초 | 0.00~0.80 | 금 테. 서버 전체 알림 |

- 기운 반영: 성공 → 기운 0(불씨가 지팡이로 빨려 들며 꺼짐). FailDrop / FailNoDrop / 변이(받든 거절하든) → 기운 +1(최대 3). FailCharmBlocked → 기운 그대로 [제안] (`gdd 9`, `fx 3`). 기운 불씨가 켜질 때 큰 확률 숫자가 그 자리에서 "+15" 굴러 오른다(실패 직후 다음 희망) [제안] (`fx 3.3`).
- 대성공: 단계가 두 번 내려찍힌다("+7 → +8", 0.25초 뒤 "→ +9"). +12에서 대성공이면 한 번만 찍고 Plus13으로 [제안] (`fx 3.2`, `gdd 9`).
- 실패 연출은 강도가 올라도 커지지 않고 **길어진다**: 0.50 뒤 침묵 0.2초(1단) → 0.6초(5단) [제안] (`fx 4`). [임시] 2~4단은 0.3 / 0.4 / 0.5초(선형).
- 하락 없음(1단)의 어두워짐은 40%가 아니라 70%까지만 (`fx 3.5`).
- 마일스톤: 칭호가 바뀌는 **+4, +7, +10**에 닿았을 때 성공·대성공 연출 뒤에 덧붙인다. 역대 최고를 새로 쓰며 도달 → 2.5초 / 전에 닿은 적 있는 단계 → 0.8초 [제안] (`fx 3.7`, `fx 22` 4번은 +13 포함으로 적지만 +13은 3.8절의 별도 연출).
- **"서버 전체에 알렸다" 줄**: 서버 프로필 && 도착 단계 ∈ {10, 11, 12, 13} && 성공/대성공. 오프라인은 이 줄만 빠진다 [제안] (`fx 3.7`, `fx 4`, `fx 8`).
- Plus13: **역대 최고 갱신일 때만** 전체(9초)를 재생. +13에서 내려갔다가 다시 올라온 경우는 0.80~3.00만. **처음 2초(= 120 t)는 건너뛸 수 없고**, 그 뒤는 J로 넘길 수 있다. 두 번째 번쩍은 없다 [제안] (`fx 3.8`).
- 결과 카드에는 "뽑힌 값 {roll:0.0} / {p:0.0} 이하면 성공"을 펼쳐 볼 수 있다 [제안] (`fx 7`).

결과 꾸러미 (`EnhanceResultPacket`) — 화면이 받는 유일한 입력 [제안] (`fx 0.5`, `on 1.4`, `gdd 9`):

```csharp
record EnhanceResultPacket(
  EnhanceResultKind kind,
  int stageBefore, int stageAfter, bool newBest,
  bool slotExtended, bool charmConsumed,
  int auraBefore, int auraAfter,
  MutationId? mutationCandidate, MutationId? mutationPushedOut,
  ProbBreakdown breakdown, int rollTenths /*0..1000, 뽑힌 값 ×10*/,
  EnhanceResultKind? fallbackIfRejected);   // MutationOffer일 때 거절하면 갈 결과 [임시: 꾸러미에 함께 담는다]
```

### 9.8 건너뛰기, 빠른 강화, 짧은 형태 [제안] (`fx 7`, `fx 6`)

| 기능 | 동작 | 제한 |
|---|---|---|
| 건너뛰기 | 의식 중 J를 다시 누르면(누름 뒤 0.3초 무시) 판정 시각으로 넘어가고 결과는 **0.5초 짧은 형태** | 변이 카드, +13 첫 도달의 처음 2초는 넘길 수 없다 |
| 빠른 강화 (켬/끔) | 충전 0.3초(= 18 t), 심장 소리·숨 멈춤·카메라·어두워짐 없음. 결과는 짧은 형태. **시도 한 번 약 0.8초(= 48 t)** | **+10 이상 도전은 빠른 강화를 켜도 전체 의식이 기본**. 설정에서 풀 수 있다 |
| 짧은 형태 | 숫자 내려찍기 + 속성색 또는 회색 고리 + 한 음. 대성공은 금색 고리와 두 번의 숫자 내려찍기, 변이는 연두 고리 | 기운 불씨와 확률 숫자의 변화는 생략하지 않는다 |

```csharp
static bool UseFullRitual(int target, Settings s) =>
  s.enhance_ritual == RitualMode.Full || (target >= 10 && !s.enhance_fast_allow_high);   // enhance_fast_allow_high 기본 false [임시 id]
```

강화 화면 효과 한도 (`fx 6`) [제안]: 한 번의 시도에 전체 화면 번쩍 최대 1회 / 연속 시도에서 번쩍 사이 0.4초 이상(빠른 강화의 번쩍은 15% 이하) / 어두워짐은 0.1초에 걸친 1회 전환, 빠른 강화에서는 쓰지 않음 / 충전 중 떨림은 지팡이 그림만 4px, 화면은 떨지 않음 / 화면 균열 덮개는 정지 그림 3장, 깜빡이지 않음.

### 9.9 이어서 강화 [제안] (`fx 7`, `ui 4.12`)

```
AutoContinue:
  같은 제물 구성(같은 등급·속성의 블록이 서고에 남아 있을 때) + 같은 부적 사용 여부로 반복
  시도마다 따로 결과를 요청한다(미리 여러 번 뽑아 두지 않는다)
  tally: n번째 시도, 성공 수, 실패 수, 쓴 골드 합 → "12번째 시도 · 성공 5 · 실패 7 · 쓴 골드 44,800"

StopIf(any):
  stageAfter >= targetStage               // 목표 단계 도달 (기댓값 표의 목표 깃발과 같은 값)
  !canAfford(gold, stone)                  // 골드·강화석 부족
  !CanRefillSacrifices()                   // 제물 부족
  charmWanted && charmCount == 0           // 부적 부족
  kind == MutationOffer                    // 변이 제시
  kind == GreatSuccess                     // 대성공
  milestoneReached                         // 마일스톤 도달
  stopOnDrop && kind == FailDrop           // (선택) 단계 하락. stopOnDrop 기본 [임시] true
  userPressed(J or K)
```

- "실패" 수에 변이·부적이 막음·하락 없음을 모두 센다 [임시].
- 이어서 강화는 지팡이를 바꾸면(Q/E) 끝난다 [임시].

### 9.10 기댓값 표 [제안] (`gdd 9`, `on 1.3`, `ui 4.12`)

- 보여 주는 것: 목표 단계 고르기, 평균 시도·골드·강화석, "보유 재화로 평균 N번".
- 계산 상태: (단계 0~13) × (기운 0~3) × (역대 최고와의 차 0~2) = **최대 168개**. 앞으로 제물과 부적을 쓰지 않는다고 가정한다.

```
ExpectedToTarget(stage, aura, gap, target):     // gap = min(2, best - stage)
  E[s,a,g] = 0                         if s >= target
  E[s,a,g] = 1 + Σ_outcome P(o|s,a) · E[next(s,a,g,o)]     otherwise
  - P: 9.4절의 Band(total(s, 제물 0, 같은 속성 0, a))
  - next(성공)     = (s+1, 0, max(0,g-1))
  - next(대성공)   = (min(13,s+2), 0, max(0,g-2))       // 확장 슬롯은 기댓값에 영향 없음
  - next(실패: 하락 가능) = (s-1, min(3,a+1), g+1)
  - next(실패: 하락 없음) = (s, min(3,a+1), g)
  - next(변이)     = 받는다고 가정 → (s, min(3,a+1), g)   [임시: 변이를 받은 것으로 계산. 거절도 단계 유지 + 기운 +1이라 같은 상태]
  선형 방정식(168원)을 가우스 소거 또는 가치 반복(수렴 1e-9)으로 푼다.
  골드·강화석 기대값은 같은 식에서 시도 1회 비용 항(GoldCost(s), StoneFor(s+1))을 1 대신 넣어 따로 푼다.
```

- "보유 재화로 평균 N번": 정의는 20절 [임시].
- 시뮬레이션 검산값(제물·부적 없음, +0에서): +5 7회 / 8,500, +7 15회 / 37,600, +10 49회 / 272,000, +13 175회 / 1,780,000, +13까지 강화석 하/중/상 9 / 48 / 118 (`on 2.5`, 2만 번 시뮬레이션). 위 식의 해가 이 값과 반올림 범위에서 맞아야 한다(18절 TV).

### 9.11 슬롯 새기기 `SlotEngrave`, 변이 제거 `MutationRemove`, 흔적 공개 [제안] (`on 1.5`, `on 2.4`, `fx 1.3`, `ui 4.12`)

슬롯 새기기:

| 항목 | 값 |
|---|---|
| 대상 | 단계 **+7 이상** && 확장 < 3칸 |
| 비용(1회) | 상급 강화석 2개 + `20,000 × (현재 확장 칸 수 + 1)` 골드 |
| 성공 확률 | 40%. **화면에 표시** |
| 실패 | 재료만 사라짐. 단계·기운 불변. 다음 시도 +20%p. 같은 칸에서 4번째 시도는 100% |
| 누적 표시 | 실패할 때마다 눈금 한 칸이 찬다(지팡이마다 저장) |

```csharp
static int EngraveChancePct(int failsOnThisSlot) => Math.Min(100, 40 + 20 * failsOnThisSlot); // 40, 60, 80, 100
static long EngraveGold(int extSlots) => 20_000L * (extSlots + 1);                          // 20,000 / 40,000 / 60,000
```

변이 제거: 지울 변이를 고르고 값 확인. 변이 1개당 3,000골드.

흔적 공개 (`TracePublic`) [확정] (`DEC 1.15`, `fx 1.3`, `gdd 9`):

| 항목 | 규칙 |
|---|---|
| 흔적이 있는 조건 | `stage < bestStage`. 흔적 개수 = `min(2, best − stage)` (바닥이 역대 최고 −2라 최대 2) |
| 설정 위치 | 강화 화면 왼쪽 칸 "흔적 공개 [켬 / 끔]". **지팡이마다 따로 저장**. 이름은 다섯 지팡이 모두 "흔적 공개". **설정 화면에는 없다** |
| 기본값 | 끔(비공개) |
| 내 화면 | 항상 보인다. 끔일 때 흔적 옆 가림 표시 + 안내 "남에게는 안 보인다 · 거래에서는 항상 보인다" |
| 남의 화면(광장, 결투, 살펴보기, 순위표 프로필) | 켬: 흔적 겹그림 + "역대 최고 +N". 끔: 흔적·역대 최고·실패 횟수 꼬리표 모두 숨김. 현재 단계 모양만 |
| 거래(직접 거래 창, 장터 목록·상세) | **설정과 무관하게 항상** 흔적 + "역대 최고 +N, 지금 +M". 거래 창에 올리는 순간 주인 화면에 "거래 상대에게는 흔적이 보인다" |
| 흔적이 사라질 때 | 역대 최고까지 다시 올리면 조각이 하나씩 떨어져 나간다(성공 연출 0.40 시점) |
| 하락 직후 | 결과 카드에 "흔적이 남았다 · 지금은 [비공개]"와 바로 바꾸는 단추 |

```csharp
static bool ShowTraceTo(Viewer v, Wand w) => v.IsOwner || v.Context is TradeWindow or MarketList or MarketDetail || w.tracePublic;
```

### 9.12 강화 화면 첫 안내 `EnhanceTutorial` [제안] (`gdd 13`, `ui 4.26`)

- 4쪽 안내. **하급 강화석을 처음 받은 정산 뒤 한 번**(그 프로필에서 처음 웨이브 3을 넘긴 판 = 까마귀 보따리 하급 3개).
- 첫 판에서 웨이브 3을 넘기지 못하면 그 판이 나올 때까지 미룬다(시작 재료 0이므로).

```
OnSettlementDone(run):
  if !profile.enhanceTutorialShown && run.grantedFirstCrowBundle:
     pendingEnhanceTutorial = true          // 정산의 [강화 화면으로] 또는 다음에 강화 화면을 열 때 보여 준다 [임시]
```

---

## 10. 정산과 결과 (무한 웨이브) `Settlement` (`ui 4.16`, `gdd 2.2`, `on 1.2`, `on 3.2`, `mon 6.2.4`, `mon 6.1`)

판이 끝나면 차례로 넘어가는 **세 쪽**이다.

```
enum SettlementPage { Record = 1, ReturnBag = 2, Takeaways = 3 }
Record ──[다음]──▶ ReturnBag ──[다음](가방 확정)──▶ Takeaways ──[다시 원정] ▶ ExpeditionPrep
                                                         ├[강화 화면으로] ▶ Enhance(entry=MainMenu) [임시: 돌아올 곳 = 메인 메뉴]
                                                         └[메인 메뉴] ▶ MainMenu
서버 프로필: ReturnBag 확정 시점에 종료 요약(가방 선택 포함)을 제출한다. 서버가 칸수·한도를 다시 계산해 확인한다.
```

### 10.1 1쪽: 기록

| 데이터 | 규칙 |
|---|---|
| 기록 줄 | "{day}일째 웨이브 {wave}". 출발 웨이브가 1이 아니면 "웨이브 {start}에서 출발" |
| 캐릭터와 지팡이 | 큰 그림 자리(쓰러진 캐릭터 받침) |
| 점수 내역 | 처치, 보스, 웨이브 클리어, 빠른 처리, 무피격, 스쳐 피하기, 변덕 배율, 건너뛴 웨이브. 합계 |
| 딱지 | 최고 기록 갱신, 검증 대기(서버 프로필) |
| 복수 추적기 | 복수 중인 프로필만(큰 꼴 1040 폭). 첫 만남이 든 판은 내역에 "허상과의 첫 만남 3,648" 줄이 따로 있고 추적기가 처음 나타난다. 웨이브 20의 허상을 처음 잡은 판은 "복수 완료" 꼴 |

```csharp
record ScoreBreakdown(long kills, long boss, long clear, long fast, long noHit, long graze,
                      int varianceMulPct /*100,115,120,125*/, long skipped, long phantomFirst /*3,648 or 0*/, long total);
```
점수 식 자체는 몬스터 명세(`mon 4.8`)를 따른다. 화면은 코어가 낸 내역을 그대로 보인다.

### 10.2 2쪽: 귀환 가방 [제안] (`mon 6.2.4`, `on 1.2`, `on 2.4`)

```csharp
static int BagSlots(int passedWaves) => Math.Min(12, passedWaves / 3);   // 기본 칸 없음, 최대 12 (36웨이브에서 도달)
static int HeroCap(int passedWaves)  => passedWaves / 10;                // 칸수 안에서
```

```
ReturnBagState:
  candidates = 판이 끝날 때 들고 있는 야생 블록(보관함 + 지팡이에 끼워 둔 야생 블록)
  빌린 블록: 후보로 보이지만 담을 수 없다는 표시 (판 끝에 50골드)
  Toggle(b):
     if b.borrowed → 거부
     if picked.Count == BagSlots → 거부("가방이 찼다")
     if b.grade == Hero && picked.Count(Hero) == HeroCap → 거부("영웅 한도")
  서고 한도: 서고가 가득 차면 "서고가 가득 찼다 · 담지 못한 블록은 골드로 바뀐다"
     [임시] 서고 남은 자리 < picked.Count 이면 남은 자리까지만 담을 수 있다
  [다음] = 확정. 고르지 않고 끝난 판(접속 끊김 등)은 AutoFill
  담지 못한 것 = candidates − picked → 골드 합 표시 (일반 5, 희귀 15, 영웅 50, 빌린 블록 50)

AutoFill(candidates, slots, heroCap):
  order = candidates.Where(!borrowed).OrderByDescending(grade)  then [임시] 블록 id asc
  for b in order: if picked < slots && (b.grade != Hero || heroPicked < heroCap) → pick
```

- 세는 웨이브: 이번 판에서 **직접 넘긴** 웨이브만. 건너뛴 웨이브는 세지 않는다. 비정상 종료는 마지막 중간 지점(5웨이브마다)까지 넘긴 수.
- 화면은 받은 칸수만큼 칸을 **한 줄 8칸**으로 그린다(12칸 → 8 + 4).
- 가방 칸을 쓰지 않는 것: 강화석, 부적, 시즌 주화, 한정 드랍, 골드.
- 주간 도전 판은 그 주의 넷째 판부터 야생 블록을 담지 못하고 전부 골드 (`mon 6.2.4`, `on 2.2`).
- 출발 조립에 쓴 서고 블록은 야생 블록이 아니다. 가방과 상관없이 잃지 않는다.

### 10.3 3쪽: 가져가는 것 (`ui 4.16`, `mon 3.9.4`)

| 줄 | 데이터 |
|---|---|
| 복수 보상 줄(그 판만, **맨 위**) | 거울 표식(복수 완료 꼴), 칭호 "거울을 깬"(작업용), 머리 장식 "금 간 거울 조각"(작업용), 재료 중급 강화석 3 · 상급 1 · 하락 방지 부적 1. 재료와 머리 장식에 자물쇠 + "계정에 묶임" |
| 골드 | 원정 지갑 → 프로필 |
| 확정 재료 | 강화석, 부적 |
| 재료 꾸러미 | 여는 연출(몬스터 명세의 그림) |
| 시즌 주화, 한정 드랍 | 서버 프로필 |
| 새로 해금한 비기 | — |
| 단추 | [다시 원정] [강화 화면으로] [메인 메뉴] |

- 서버 프로필의 보상은 "검증 대기" 표시가 붙는다. 대기 중인 골드·물건은 강화·조립에는 쓰고 거래·장터·선물에는 못 쓴다 (`on 3.2`).
- 판이 끝난 뒤 지팡이 조립은 출발 조립으로 되돌아간다 (`on 1.1`).

---

## 11. 결투 결과 `DuelResult` (`ui 4.17`)

| 데이터 | 규칙 |
|---|---|
| 승패 | `Win` / `Lose` / `Draw` / `Void`(서버가 내려갔을 때). 패배를 빨갛게 칠하지 않는다. 승패 띠에 금색을 쓰지 않는다 |
| 라운드별 요약 | 라운드마다 남은 체력, 준 피해, 쓴 비기 수 |
| 점수 변동 | **자동 매치만** "1512 → 1530". 광장 도전과 방은 "전적에만 남는다" |
| 받은 것 | 골드, 시즌 주화 |
| 미뤄 둔 소식 | 결투 중 뜨지 않은 서버 알림(1.6절 `DeferredDuelResult`) |
| 단추 | [다시 대기열] [다시 보기] [상대 살펴보기] [나가기] |

## 12. 기록 `Records`, 칭호 고르기 `TitlePicker` (`ui 4.18`)

- 오프라인 프로필은 **기기 안의 개인 기록만** (`on 8.1`).
- 캐릭터 탭 6개: 전체 + 다섯 캐릭터.
- 요약: 최고 점수, 최고 웨이브, 누적 웨이브(야생 물 단계), 결투 전적, 시즌 결과, 얻은 칭호. "전체" 탭 맨 아래 복수 기록: 복수 중 → 추적기 큰 꼴 / 복수 완료 → "복수 완료 · {날짜} · 첫 만남에서 {n}판째" 한 줄 + 거울 표식 / **첫 만남 전 → 이 자리 없음**.
- 판 목록 줄: 날짜, 캐릭터, 도달 웨이브, 출발 웨이브, 점수, 지팡이 단계, 조립(아이콘 줄), [다시 보기].
- 칭호 고르기: 얻은 칭호 중 닉네임 앞에 붙일 것 하나.

---

## 13. 설정과 키 재설정 `Settings`, `KeyRebind`

설정과 키 배치는 **두 프로필이 같이 쓴다**(기기에 저장, 서버 프로필은 계정에도 동기화) [조율] (`ui 4.19`, `on 8.2`, `on 1.1`).

### 13.1 화면 구조 [제안] (`ui 4.19`)

- 분류 6개 `SettingsCategory`: `Display`(화면), `Sound`(소리), `Controls`(조작), `CombatView`(전투 보기), `ChatPrivacy`(대화와 공개), `Account`(계정).
- 옵션 줄: 이름 + 값(토글 / 슬라이더 / 단계 고르기 / 펼침 고르기 중 하나). 줄을 고르면 선택 괄호(보통).
- 설명과 미리보기: 고른 옵션의 설명. **"연출 줄이기", "색각 보조", "예고 채움 진하기", "내 탄 투명도"는 작은 전장 그림에서 바로 보여 준다**(미리보기 겹그림: 예고 원, 적 탄, 내 탄, 줄무늬 채움, 흔들림 표시, 번쩍 표시).
- 아래: [기본값으로] [닫기]. [임시] [기본값으로]는 지금 분류만 되돌리고 확인 창 한 번.
- **줄일 수 없는 것(적 예고, 적 탄, 열 링, 피격 표시, 시야 화살표)은 옵션 목록에 나오지 않는다** [확정] (`gdd 13`, `fx 18`).
- **"흔적 공개"는 설정에 두지 않는다.** "대화와 공개"에 "흔적 공개는 강화 화면에서 지팡이마다 바꾼다" 한 줄 안내만 [확정] (`fx 1.3`, `ui 4.19`).

### 13.2 옵션 표 (`PresentationSettings` / `GameSettings`)

| 분류 | 키 | 이름 | 타입·범위 | 기본 | 출처 |
|---|---|---|---|---|---|
| 화면 | `window_mode` | 창·전체 화면 | enum Windowed / Fullscreen | [임시] Fullscreen | `ui 4.19` |
| 화면 | `resolution` | 해상도 | (w,h). 최소 1920×1080 창 | [임시] 기기 화면 | `ui 1.1` |
| 화면 | `motion_preset` | 연출 줄이기 | enum Full / Reduced / Minimal | Full (첫 실행 물음 값) | `fx 0.4`, `fx 18` |
| 화면 | `shake_scale` | 흔들림 | int 0~100 | 100 | `fx 0.4`, `fx 18` |
| 화면 | `flash_scale` | 번쩍 | int 0~100 | 100 | `fx 0.4`, `fx 18` |
| 화면 | `hitstop_enabled` | 그림 정지 | bool | 켬 | `fx 0.4`, `fx 18` |
| 소리 | `vol_master` | 전체 | int 0~100 | [임시] 100 | `ui 4.19` |
| 소리 | `vol_bgm` | 배경음 | int 0~100 | [임시] 100 | `ui 4.19`, `fx 19.5` |
| 소리 | `vol_sfx` | 효과음 | int 0~100 | [임시] 100 | `ui 4.19` (19절 C-05) |
| 소리 | `vol_wand` | 지팡이 소리 | int 0~100 | [임시] 100 | `ui 4.19`, `fx 18`, `fx 19.5` |
| 소리 | `vol_ui` | 화면 소리 | int 0~100 | [임시] 100 | `ui 4.19`, `fx 19.5` |
| 소리 | `vol_notice_bell` | 서버 알림 종 | int 0~100 | [임시] 100 | `ui 4.19` |
| 소리 | `vol_enemy` | 적 소리 | int 0~100 | [임시] 100 | `fx 19.5` (19절 C-05) |
| 소리 | `vol_plaza_others` | 광장의 남 소리 | int 0~100 | [임시] 100 | `fx 19.5` (19절 C-05) |
| 조작 | `key_layout` | 대체 배치 고르기 | enum Default / Alternate / Custom | Default | `ui 4.19`, `gdd 3` |
| 조작 | (13.4절) | 키 재설정 | — | — | `ui 4.19` |
| 전투 보기 | `my_bullet_opacity` | 내 탄 투명도 | int 30~100 % | 100 | `fx 18` [GDD] |
| 전투 보기 | `damage_number_opacity` | 피해 숫자 투명도 | int 0~100 % | 100 | `fx 18` |
| 전투 보기 | `damage_number_mode` | 피해 숫자 표시 | enum All / CritWeakOnly / Off | All | `fx 18` |
| 전투 보기 | `wand_fx_level` | 지팡이 효과 | enum All / Reduced / Minimal | All | `fx 18` |
| 전투 보기 | `wand_fx_opacity` | 지팡이 효과 투명도 | int 20~100 % | 100 | `fx 18` |
| 전투 보기 | `others_wand_fx` | 남의 지팡이 효과(광장) | enum All / Reduced / Off. 줄임·끔에서도 본체 모양과 단계 표식은 보인다 | All | `fx 18` |
| 전투 보기 | `bullet_trail` | 탄 꼬리 | enum On / Short / Off | On | `fx 18` |
| 전투 보기 | `ground_marks` | 바닥 흔적(서리, 그을음, 긁힘) | bool | 켬 | `fx 18` |
| 전투 보기 | `kill_shards` | 처치 조각 | enum On / SmokeOnly | On | `fx 18` |
| 전투 보기 | `art_name_popup` | 기술 이름 띄우기 | enum On / Difficulty5Only / Off | On | `fx 18` |
| 전투 보기 | `combo_position` | 콤보 숫자 위치 | enum NearCharacter / Hud | NearCharacter | `fx 18`, `ui 4.8` |
| 전투 보기 | `clone_zone_opacity` | 사본·지대 투명도(베껴 쓰기, 불가마) | int 30~100 % | 100 | `fx 18` |
| 전투 보기 | `weather_particles` | 날씨·환경 입자 | bool | 켬 | `fx 18` |
| 전투 보기 | `telegraph_fill` | 예고 채움 진하기 | enum 70 / 80 / 90 (%) | 70 | `fx 18` |
| 전투 보기 | `colorblind_assist` | 색각 보조 | bool. 켬 = 차오르는 채움에 줄무늬, 적 탄을 주황 줄무늬로 | 끔 | `fx 18` |
| 대화와 공개 | `chat_mute_<channel>` | 채널별 채팅 끄기 | bool × 7 채널 | 끔 | `on 5.3` |
| 대화와 공개 | `chat_mute_all` | 전체 끄기 | bool | 끔 | `on 5.3` |
| 대화와 공개 | `loadout_public` | 조립 공개 | bool (단계와 겉모습은 항상 보임) | [임시] 켬 | `on 5.4` |
| 대화와 공개 | `enhance_notice_show_name` | 강화 알림에 내 이름 | bool | 켬 | `on 1.4` |
| 대화와 공개 | `server_enhance_notice` | 서버 강화 알림 보기 | enum All / Plus13Only / Off | All | `fx 8`, `fx 18` |
| 대화와 공개 | `enhance_ritual` | 강화 의식 | enum Full / Fast | Full | `fx 7`, `fx 18` |
| 대화와 공개 | `enhance_fast_allow_high` | +10 이상도 빠른 강화 | bool | 끔 | `fx 7`, `fx 22` 5번 (19절 C-09) |
| 대화와 공개 | `block_list` | 차단 목록 | 목록(최대 200명) | — | `on 5.3` |
| 계정 | — | 13.3절 | | | `ui 4.19`, `on 7.3` |

- 강화 화면 아래 띠의 [빠른 강화] 토글과 설정의 `enhance_ritual`은 **같은 값**이다 [조율] (19절 C-08).
- 서버 전용 줄(채팅·조립 공개·차단·강화 알림 이름)은 오프라인 프로필에서 [임시] 보이지 않는다(계정이 없으므로). 강화 의식 줄은 오프라인에서도 보인다.

### 13.3 계정 분류 [제안] (`ui 4.19`, `on 7.3`)

| 프로필 | 줄 |
|---|---|
| 서버 | 닉네임 바꾸기(5,000골드, `on 2.4`), 비밀번호 바꾸기(현재 비밀번호 확인 → 변경 → 모든 기기 토큰 폐기), **이메일**: 가린 꼴 주소 + "확인됨 / 미확인" 꼬리표, [확인 코드 보내기], [이메일 바꾸기](현재 비밀번호 → 새 주소 → 이메일 확인 판. 옛 주소로 "주소가 바뀌었다" 알림, 바꾼 뒤 24시간 거래·장터 등록 막힘). 복구 메일이 꺼져 있으면 두 단추가 꺼지고 "지금은 메일을 보낼 수 없다" 한 줄. 계정 삭제(조심 버튼, "이메일도 같이 지워진다", 요청 7일 뒤 삭제). 이 기기에서 로그아웃 |
| 오프라인 | 저장 파일 위치와 [저장 파일 되돌리기]만. 이메일 줄 없음 |

### 13.4 키 재설정 `KeyRebind` [제안] (`ui 4.19`, `gdd 3`, `on 5.1`)

```csharp
enum BindContext { Combat, Plaza }
enum GameAction {
  // Combat (9줄)
  MoveUp, MoveDown, MoveLeft, MoveRight, Fire, Art, Stance, ReverseStance, Pause,
  // Plaza (5줄)
  Interact, Emote, Chat, NearbyList, PlazaMenu }
record Binding(GameAction action, PhysicalKey? primary, PhysicalKey? secondary);
```

| 배치 | 위 | 아래 | 왼쪽 | 오른쪽 | 발사 | 비기 | 자세 | 반전 자세 | 일시정지 |
|---|---|---|---|---|---|---|---|---|---|
| `Default` [확정] (`DEC 1.19`) | W | S | A | D | J | K | Shift | Space | [임시] Esc |
| `Alternate` [제안] (`gdd 3`) | ↑ | ↓ | ← | → | Z | X | C | V | [임시] Esc |

광장 키 기본 [제안] (`on 5.1`): 상호작용 J, 감정표현 K, 채팅 Enter, 주변 목록 Tab, 메뉴 Esc. 대체 배치의 광장 키는 [임시] Z / X / Enter / Tab / Esc.

키 받기 상태 기계:

```
Idle ──(키 칸 고름 + J/클릭)──▶ Capturing(slot)  "키를 누르세요 · Esc 취소"
Capturing:
  KeyDown(Esc)            → Idle (취소)
  5초(= 300 t) 경과        → Idle (저절로 취소)
  KeyDown(k)              → pending[slot] = k; ValidateConflicts(); → Idle
ValidateConflicts():
  같은 BindContext 안에서 k가 다른 action의 primary/secondary에 있으면
     두 줄 모두 경고 "{다른 기능 이름}과 같은 키"  (예: "발사와 같은 키")
  canSave = 경고 0개      // 겹친 채로는 저장되지 않는다
저장: key_layout = Custom
```

- 키는 **물리 키 기준**으로 읽고, 전투 중에는 한글 입력기의 영향을 받지 않는다. 안내 한 줄: "전투 중에는 한글 입력 상태와 무관하게 키 자리로 읽는다" [확정 규칙] (`gdd 3`, `ui 4.19`).
- 키보드 그림: 지금 배치가 얹힌 키보드. 쓰이는 키가 밝다.
- 겹침 검사는 **문맥(전투 / 광장)별**로 한다 [임시]. 기본 배치에서 J·K가 전투와 광장에 함께 쓰이기 때문이다(19절 C-10).
- 전 키 재설정 가능 (`gdd 3`). Esc는 키 받기 중 취소 키라서 받을 수 없다 → 일시정지/광장 메뉴의 Esc는 고정 보조 키로 둔다 [임시] (20절).

---

## 14. 연출 줄이기와 광과민 안전 (`fx 0.2`~`0.5`)

### 14.1 항상 적용하는 한도 `PhotosensitivityLimits` [확정 규칙: 옵션과 무관하게 항상] (`fx 0.3`)

| 항목 | 한도 | 틱 환산 |
|---|---|---|
| 전체 화면 번쩍 횟수 | 어느 1초 구간에서도 **2회 이하**. 번쩍 사이 간격 **0.4초 이상** | 1초 = 60 t, 0.4초 = 24 t |
| 전체 화면 번쩍 세기 | 덮는 색의 불투명도 **35% 이하**. 순백 100%는 쓰지 않는다 | — |
| 번쩍 모양 | **0.05초 안에 켜지고 0.10초 이상에 걸쳐 꺼진다.** 1프레임짜리 켜짐/꺼짐 반복 금지 | 켜짐 ≤ 3 t, 꺼짐 ≥ 6 t |
| 빨강 전체 화면 번쩍 | **금지.** 피격 표시는 화면 가장자리(면적 25% 미만)에만 | — |
| 큰 국소 번쩍 | 화면 25% 이상을 덮으면 전체 화면 번쩍으로 센다 | — |
| 깜빡이는 넓은 무늬(줄무늬, 바둑판) | 금지 | — |
| 밝기 급락(강화 실패의 어두워짐) | 1회 전환으로만. 어두워졌다 밝아졌다를 반복하지 않는다 | — |
| 흔들림 | 폭 16px, 길이 0.6초 이하. 화면 회전·확대 떨림 금지 | 0.6초 = 36 t |
| 카메라 확대 | 1.00~1.12배, 초당 0.1배 이하의 속도 | 틱당 ≤ 0.1/60 ≈ 0.001667배 |

이 문서가 정한 기준은 널리 쓰이는 기준(1초에 3회 이하)보다 엄격하다 (`fx 0.3`).

### 14.2 번쩍 관리기 `FlashManager` [확정 규칙] (`fx 0.3`, `fx 0.4`)

전체 화면 번쩍과 큰 국소 번쩍은 **이 한 곳을 거쳐서만** 낸다. 한도를 넘는 요청은 버리지 않고 **테두리 강조(흰 선 8px, 0.15초 = 9 t)**로 바꿔 낸다(정보는 남기고 빛만 뺀다).

```csharp
record FlashRequest(Color color, float opacity /*0..1*/, float riseSec, float fallSec,
                    float coverage /*화면 비율 0..1*/, FlashSource src);
enum FlashOutcome { Flash, BorderHighlight }

class FlashManager {
  readonly Queue<long> recentStartsMs = new();      // 최근 1초 안의 번쩍 시작 시각
  long lastStartMs = long.MinValue;

  public (FlashOutcome, float appliedOpacity) Submit(FlashRequest r, long nowMs, Settings s) {
    bool isFull = r.coverage >= 0.25f;              // 25% 이상 = 전체 화면 번쩍으로 셈
    if (!isFull) return (FlashOutcome.Flash, r.opacity);          // 작은 국소 번쩍은 통과(다른 규칙은 효과 쪽 책임)
    if (IsDangerColor(r.color)) return Border();                   // 빨강 전체 화면 번쩍 금지
    float presetCap = s.motion_preset switch { Full => 0.35f, Reduced => 0.15f, _ => 0f };
    if (presetCap == 0f) return Border();                          // 최소: 번쩍 없음(테두리 강조로 대체)
    float op = Math.Min(r.opacity, presetCap) * (s.flash_scale / 100f);  // [임시] 낱개 조절은 곱
    if (op <= 0f) return Border();
    while (recentStartsMs.Count > 0 && nowMs - recentStartsMs.Peek() >= 1000) recentStartsMs.Dequeue();
    if (recentStartsMs.Count >= 2) return Border();                // 1초에 2회 이하
    if (nowMs - lastStartMs < 400) return Border();                // 간격 0.4초 이상
    recentStartsMs.Enqueue(nowMs); lastStartMs = nowMs;
    // 모양 강제: rise = min(r.riseSec, 0.05), fall = max(r.fallSec, 0.10)
    return (FlashOutcome.Flash, op);
  }
}
```

- "1초 구간"은 미끄러지는 창이다: 어느 시각 t에서도 [t−1초, t) 안의 시작이 2개 이하 [임시: 시작 시각 기준으로 센다].
- 테두리 강조 자체는 전체 화면 번쩍으로 세지 않는다 [임시].

### 14.3 흔들림 누적기 `ShakeAccumulator` [제안] (`fx 0.2`, `fx 0.3`, `fx 0.4`)

| 이름 | 폭(기준 화면) | 길이 | 쓰는 곳의 예 |
|---|---|---|---|
| 소 `Small` | 4px | 0.15초 = 9 t | 임계 적중, 난이도 3 비기 |
| 중 `Medium` | 8px | 0.25초 = 15 t | 피격, 뇌광창, 강화 성공(높은 단계) |
| 대 `Large` | 12px | 0.40초 = 24 t | 대성공, 보스 단계 전환, 만개 마지막 파동 |
| 특대 `XLarge` | 16px | 0.60초 = 36 t | +13 도달 한 곳뿐 |

```csharp
class ShakeAccumulator {                 // 표시 계층(전장 카메라)만. HUD·예고 판정·입력은 흔들리지 않는다
  float acc;                             // 0..1
  public void Add(ShakeSize s) => acc = Math.Min(1f, acc + AddFor(s));
  public void Tick(float dt) => acc = Math.Max(0f, acc - 2.5f * dt);         // 초당 2.5씩 감소
  public float AmplitudePx(Settings st) {
    float presetMul = st.motion_preset switch { Full => 1f, Reduced => 0.3f, _ => 0f };
    return acc * acc * 16f * presetMul * (st.shake_scale / 100f);            // 폭 = 누적값² × 16px, 상한 16px
  }
  static float AddFor(ShakeSize s) => s switch {   // [임시] 단독일 때 표의 폭이 나오는 값: sqrt(폭/16)
    Small => 0.5f, Medium => 0.7071f, Large => 0.8660f, XLarge => 1.0f };
}
```
[조율: 표의 "길이"와 누적기 감소(초당 2.5)가 만드는 길이가 다르다(19절 C-03). 누적기를 규칙으로 쓴다]

- 여러 사건이 겹쳐도 16px을 넘지 않는다.
- **흔들리는 것은 전장 카메라뿐**. 화면 그림(HUD와 메뉴)은 흔들림을 받지 않는다 (`fx 0.2`, `ui 1.1`).

### 14.4 그림 정지(히트스톱) [확정 규칙] (`fx 0.2`, `gdd 3`)

- **게임 시간을 멈추지 않는다.** 맞은 대상과 맞힌 탄의 **그림만** 멈춘다(게임 시간은 결투 판정과 재실행 검증에 쓰인다).
- 강화 화면은 전투가 아니므로 화면 전체를 멈춰도 된다.
- 길이 배율: `Full` 1.0 / `Reduced` 0.5 / `Minimal` 0. `hitstop_enabled == false`면 0.

```csharp
static float HitstopScale(Settings s) => !s.hitstop_enabled ? 0f : s.motion_preset switch { Full => 1f, Reduced => 0.5f, _ => 0f };
```

### 14.5 연출 줄이기 3단계 표 `MotionPreset` [제안] (`fx 0.4`)

| 항목 | Full(전체) | Reduced(줄임) | Minimal(최소) |
|---|---|---|---|
| 화면 흔들림 | 100% | 30% | 없음 |
| 전체 화면 번쩍 | 35%까지 | 15%까지 | 없음(테두리 강조로 대체) |
| 그림 정지 | 켬 | 절반 길이 | 끔 |
| 카메라 확대(강화 의식) | 켬 | 절반 | 끔 |
| 예고의 흰 번쩍 | 구역 전체 | 테두리만 굵게 | 테두리만 굵게 |
| 무적 중 캐릭터 깜빡임 | 8Hz 반투명 | 깜빡임 없이 60% 투명 + 흰 테두리 | 같음 |
| 열 아지랑이, 균열 등 화면 덮개 | 켬 | 정지 그림 | 끔 |
| 입자 예산 | 1,500 | 800 | 300 |
| 강화 의식 | 전체 | 어두워짐 40% → 20%, 균열은 화면이 아니라 마법진 안에만 | 결과 카드만 |

```csharp
record PresetValues(float shakeMul, float flashCap, float hitstopMul, float zoomMul,
                    TelegraphFlash telegraph, IFrameStyle iframe, OverlayMode overlay,
                    int particleBudget, RitualLevel ritual);
static readonly PresetValues Full    = new(1.0f, 0.35f, 1.0f, 1.0f, TelegraphFlash.Zone,         IFrameStyle.Blink8Hz,        OverlayMode.On,    1500, RitualLevel.Full);
static readonly PresetValues Reduced = new(0.3f, 0.15f, 0.5f, 0.5f, TelegraphFlash.ThickBorder,  IFrameStyle.Translucent60,   OverlayMode.Still,  800, RitualLevel.Reduced);
static readonly PresetValues Minimal = new(0.0f, 0.00f, 0.0f, 0.0f, TelegraphFlash.ThickBorder,  IFrameStyle.Translucent60,   OverlayMode.Off,    300, RitualLevel.CardOnly);

static float RitualZoom(float target /*예: 1.08*/, PresetValues p) => 1f + (target - 1f) * p.zoomMul;   // 줄임: 1.08 → 1.04
```

- 세 단계와 별개로 낱개 조절: 흔들림 0~100, 번쩍 0~100, 그림 정지 켬/끔 (`fx 0.4`).
- 입자 예산 넘침 처리의 유일한 기준은 `fx 9.3`이다(`DEC 2.8`). 이 문서는 예산 숫자만 넘긴다.
- 강화 의식 단계별 동작 [임시 해석] (20절):
  - `Reduced`: 대성공·실패의 어두워짐 깊이를 절반으로(밝기 40%까지 → 70%까지), 화면 균열 대신 마법진 안의 지팡이 빛에만 금이 간다 (`fx 3.2`).
  - `CardOnly`: 결과 꾸러미가 오면 연출 없이 결과 카드만. 변이 카드는 그대로 고르게 한다(선택이 필요). +13 첫 도달의 "처음 2초 건너뛸 수 없음"은 적용하지 않는다.
- 첫 만남 연출(`fx 15.1`): 줄임 → 번쩍 15%, 흔들림 30%. 최소 → 번쩍 대신 테두리 강조, 흔들림 없음, 무채색 전환은 그대로(밝기를 바꾸지 않으므로).
- **소리로만 전달되는 정보는 없다.** 모든 소리 신호에는 같은 뜻의 그림이 있다(예: 심장 소리 ↔ 게이지 고리의 맥동) [확정 원칙] (`fx 0.4`). → 21절 연결점마다 그림 이벤트와 소리 이벤트를 짝으로 둔다.

### 14.6 서버·결제 없이 동작하는 구조 [확정] (`fx 0.5`, `DEC 1.14`, `DEC 2.6`)

| 연결점 | 오프라인 프로필 | 서버 프로필 |
|---|---|---|
| 강화 결과 | 기기 안 구현이 결과 꾸러미를 만든다. 강화 화면과 의식이 **전부** 그대로 | 서버가 같은 꾸러미를 준다. 연출 코드는 같다 |
| 서버 전체 알림 | 내보내지도 받지도 않는다. 내 화면 의식은 똑같이 재생. 결과 카드의 "서버 전체에 알렸다" 줄만 빠진다 | +10 이상 성공과 +13 달성을 내보내고 받는다 |
| 치장 효과(강화 의식 겉모습, 발사 궤적, 처치 효과, 발자국) | 칸마다 기본 효과가 있다. **얻지 못한 효과는 목록에 나오지 않고, 빈 칸이나 잠금 표시가 화면에 없다** | 같다. 거래로 받은 효과가 목록에 더해진다 |
| 결제 | 연출 코드 어디에도 유료 여부를 보는 분기가 없다 | 나중에 결제를 붙여도 연출은 바뀌지 않는다 |

- 연출은 "어디서 온 결과인지"를 모른다. 받는 것은 결과 꾸러미 하나(9.7절 `EnhanceResultPacket`).
- 나중에 추가되는 치장 효과의 검사 항목(`CosmeticValidator`): 색 규칙(위험색·연두 금지, 금색은 정해진 곳만), 14.1절 한도, 가림 상한(`fx 9.2`), 입자 예산(`fx 9.3`). **크기와 지속 시간은 기본 효과의 120% 이하.** 판정과 무관. 결투에서 상대 화면에는 가장 단순한 형태로만 [확정] (`DEC 2.7`).
- 유료 상품이 0개면 상점에 유료 재화 탭, 잔액, 구매 버튼이 **전혀 나오지 않는다** [확정] (`gdd 13`, `ui 4.23`).

---

## 15. 광장 화면 (겹침 화면과 메뉴) (`ui 4.20`~`4.25`, `on 5`)

광장은 전장처럼 캐릭터를 움직이는 공간이고 아래는 그 위에 겹치는 화면이다. **서버 프로필에서만** 들어간다. 강화하는 장소·NPC는 없다 [확정] (`DEC 2.4`).

### 15.1 머리 위 표시 `OverheadModel` [제안] (`ui 4.20`, `on 5.1`)

```csharp
record OverheadModel(
  string nickname, LeagueId league, StatusIcon status,
  string? titleId, TitleBandKind band /*Normal, Gold(+13), Successor, Disciple*/,
  bool isMe, bool isFriend, bool isBlocked,
  bool menuOpen,                 // 메뉴를 보고 있는 사람(강화 화면 포함) → 말줄임표
  ChatBubble? bubble,
  string? interactHint);         // "J 살펴보기", "J 좌판 거리", "J 결투 게시판"
```

| 규칙 | 값 |
|---|---|
| 차단한 사람 | 닉네임 판 **전체가 없다** |
| 친구 | 닉네임 색이 좋음 초록 |
| 나 | 분필 흰색 테 |
| 칭호 띠 | 지팡이 칭호 또는 고른 칭호. +13 칭호는 금 띠, 후계자는 전용 띠 |
| 말줄임표 | 메뉴를 보고 있는 사람(강화 화면 포함). **"강화 중"이라는 표시는 없다** [확정] (`fx 1.1`) |
| 말풍선 | 주변 채널만. 한 줄 20자, **최대 4줄(80자)**. **4초(= 240 t) 뒤** 흐려진다. 귓속말은 풍선으로 뜨지 않는다 |
| 들고 있는 지팡이 | 그 캐릭터의 든 지팡이. 실제 강화 단계의 모양과 효과 그대로 (`on 5.1`). 흔적은 9.11절 규칙 |

```csharp
static string[] WrapBubble(string text) {         // 20자 × 최대 4줄
  var lines = Chunk(text, 20).Take(4).ToArray();  // [임시] 글자 수 기준 단순 자르기(80자 상한은 입력에서 이미 막힘)
  return lines;
}
```

### 15.2 대화창 `ChatPanel` [제안] (`ui 4.20`, `on 5.3`)

| 채널 `ChatChannel` | 범위 | 보내기 제한 | 줄 글자색 |
|---|---|---|---|
| `Nearby` 주변 | 반경 8칸(= 512px), 말풍선 | 2초(= 120 t)에 1회 | 분필 흰색 |
| `Plaza` 광장 | 같은 인스턴스 전체 | 5초(= 300 t)에 1회 | 회청 |
| `Trade` 거래 | 같은 인스턴스 전체, 아이템 링크 가능 | 30초(= 1,800 t)에 1회 | 놋쇠색 |
| `Whisper` 귓속말 | 접속 중인 한 사람. 차단한 사람에게서는 오지 않는다 | — | 연보라 |
| `Friends` 친구 | 접속 중인 친구 전체 | — | 좋음 초록 |
| `DuelRoom` 결투방 | 방 안. 경기 중에는 정해진 문구 8개만 | — | 하늘 |
| `System` 시스템 | 강화 알림, 운영 공지. **읽기 전용**, 서버 전체 | 보낼 수 없음 | 금색(강화 알림)·분필 흰색(공지) |

- 탭 7개. **서버 전체 채팅 탭은 없다** [확정] (`DEC 2.7`). 꺼 둔 채널 탭은 회청.
- 색과 함께 줄 머리에 채널 아이콘(색으로만 구분하지 않는다).
- 입력 줄: Enter로 연다. 남은 글자 수 "12/80". 다음에 보낼 수 있을 때까지 남은 시간 고리.
- 한 줄 최대 80자. **같은 문장 반복은 3회째부터 막는다** [제안] (`on 5.3`).
- 새 계정은 무한 웨이브 10에 닿기 전까지 **주변 채널만** (`on 5.3`).
- 가린 말: 서버 필터에 걸린 단어만 "＊"로. 주소·전화번호·계좌번호·메신저 아이디 모양은 통째로 막힘(서버).
- 대화창을 접으면 마지막 한 줄만.
- 줄을 누르면 목록: 귓속말, 살펴보기, 차단, 신고.
- 물건 이음(거래 채널): 등급색 테 이름표. 누르면 툴팁으로 상세.

```csharp
class ChatSendGate {
  static readonly Dictionary<ChatChannel,int> CooldownTicks = new() {
    [ChatChannel.Nearby] = 120, [ChatChannel.Plaza] = 300, [ChatChannel.Trade] = 1800 };
  public bool CanSend(ChatChannel ch, string text, long nowTick, Account a) {
    if (ch == ChatChannel.System) return false;
    if (text.Length == 0 || text.Length > 80) return false;
    if (!a.reachedWave10 && ch != ChatChannel.Nearby && ch != ChatChannel.Whisper && ch != ChatChannel.Friends && ch != ChatChannel.DuelRoom)
      return false;                   // [임시] "주변 채널만"을 공개 채널(광장·거래)에만 적용
    if (CooldownTicks.TryGetValue(ch, out var cd) && nowTick - lastSent[ch] < cd) return false;
    if (SameAsLast(text) && repeatCount >= 2) return false;   // 3회째부터 막음
    return true;
  }
}
```

### 15.3 상호작용과 목록 [제안] (`ui 4.20`, `on 5.1`, `on 5.2`)

| 화면 | 열기 | 내용 |
|---|---|---|
| 상호작용 목록 | 사람에게 J | 살펴보기, 귓속말, 친구 신청, 거래 신청, 결투 도전, 따라가기, 차단, 신고 (8줄) |
| 감정표현 바퀴 `EmoteWheel` | K | 8칸. 방향키로 고르고 J. 시작 8종: 인사, 박수, 도발, 절, 앉기, 춤, 지팡이 들어 올리기(단계 효과 재생), 좌절 (`on 5.1`) |
| 주변 사람 목록 `NearbyList` | Tab | 줄: 휘장, 닉네임, 캐릭터 초상(얼굴 32×32), 거리 |
| 광장 메뉴 `PlazaMenu` | Esc | **8줄: 강화, 서고, 옷장, 우편함, 순위표, 채널 옮기기, 설정, 메인 메뉴로** |
| 받은 신청 쪽지 `IncomingRequest` | 서버가 보냄 | 친구 신청, 거래 신청, 결투 도전. [수락] [거절]. 남은 시간 고리 |
| 시설 J | 시설 앞 | 5.2절 표 |

광장 메뉴 항목 → 화면:

| id | 이름 | 여는 화면 |
|---|---|---|
| `enhance` | 강화 | `WandPicker` → `Enhance(entry=PlazaMenu)` (나가면 광장으로) |
| `library` | 서고 | `Camp(mode=Library)` |
| `wardrobe` | 옷장 | `Wardrobe` |
| `mailbox` | 우편함 | `Mailbox` |
| `leaderboard` | 순위표 | `Leaderboard` |
| `channel` | 채널 옮기기 | `ChannelSelect` |
| `settings` | 설정 | `Settings` |
| `main_menu` | 메인 메뉴로 | `MainMenu` [임시: 확인 창 없음] |

시설 → 기능 (`on 5.2`):

| 시설 id | J를 누르면 |
|---|---|
| `facility.bonfire` 큰 모닥불 | 앉기 동작, 주변 채팅 |
| `facility.stalls` 좌판 거리 | `Market`(장터 등록·구매·시세). 직접 거래 신청은 상대가 가까이 있을 때 |
| `facility.peddler` 행상 수레 | `Shop(kind=Gold)` — 부적, 새 지팡이(+0), 골드 꾸미기, 서고 확장 |
| `facility.duel_board` 결투 게시판 | `DuelBoard(entry=Plaza)` |
| `facility.stone_path` 돌마당 가는 돌길 | 기능 화면 없음(결투가 잡힌 두 사람이 걸어 들어가는 연출) |
| `facility.rocking_chair` 느루의 흔들의자 | `MasterChair` |
| `facility.monument` 후계자 비석 | `Monument` |
| `facility.field_gate` 한뎃벌 출입문 | `ExpeditionPrep(entry=PlazaGate)` |
| `facility.dummy` 연습 허수아비 | `DummyPractice(entry=Plaza)` + 결투 입력 지연 체험. 남의 연습 구경 |
| `facility.mailbox` 까마귀 우편함 | `Mailbox`(친구 목록, 쪽지, 우편) |

- 광장에 오지 않고 메인 메뉴에서 열 수 있는 것: 원정, 강화, 서고, 옷장, 상점, 우편함, 친구 목록, 순위표, 시즌 화면, 장터, 자동 매치 대기열. **광장에 와야만 되는 것**: 다른 사람 만나기, 직접 거래, 광장 도전, 주변·광장 채팅, 남의 연습 구경 (`on 5.2`).
- 광장 도전 수락 시간: **20초(= 1,200 t)** (`on 4.6`). 그 밖의 신청(친구·거래)의 시간은 20절 [임시: 20초].
- 거래 조건을 못 채운 계정: 상호작용 목록의 "거래 신청"이 꺼짐 + 이유(17절).

### 15.4 결투 게시판·방·도전 `DuelBoard`, `DuelRoom` [제안] (`ui 4.21`, `on 4.6`)

- 탭 5개: 자동 매치 / 방 목록 / 방 만들기 / 봇 상대 연습 / 내 전적. 메인 메뉴에서 열면 **광장 도전만 빠진다**.
- 자동 매치: 내 리그, 보유 IL, 결투 점수, 이번 시즌 판수, 경기 중 제한(리그의 제한), 서버 왕복 지연과 회선 표시, "유선 연결 권장", [대기열 등록]. 조건을 못 채웠으면 무엇이 모자란지 적는다.
- 대기 중 표시(`AutoMatchWaiting`): 대기열에 있는 동안 광장, 메인 메뉴, 강화 화면에 떠 있다. 경과 시간, [취소].
- 매치 성사 `MatchFound`: "상대를 찾았다". **강화 연출 중이면 결과만 보여 주고 넘어간다.**
- 방 목록 줄: 방 이름, 방장, 켜진 옵션 아이콘, 인원 "1/2", 관전 "3/6", [들어가기] [관전]. 비밀번호 방은 `RoomPassword`. 찾기 칸.
- 방 만들기 옵션 (`on 4.6`): 라운드 수(1/3/5), 라운드 시간(60/90/120초), 체력 배율(×0.7/×1/×1.5), 강화 상한(+0~+13), IL 상한, 변이 허용, 영웅 블록 수 상한, 장비 평준화(양쪽 +0), 야생 난입, 가시밭 끄기, 바닥 색(낮/해질녘/밤), 관전(최대 6명), 비밀번호.
- 대기 방: 양쪽 카드(캐릭터, 지팡이 아이콘, 닉네임, 휘장, IL, 준비 표시), 방 규칙, 결투방 대화, 관전자 목록(최대 6), [준비] [캐릭터·조립 바꾸기] [나가기].
- 광장 도전 쪽지: 도전한 사람의 닉네임·캐릭터·장착 IL, 수락 남은 시간 고리(20초), "리그 규칙 적용" 체크(둘 중 낮은 리그의 제한).
- 내 전적 줄: 상대, 경로(광장 도전·방·자동 매치), 결과, 점수 변동, [다시 보기].
- 봇 상대 연습: 상대 캐릭터, 세기 3단. **오프라인 프로필에서도 된다**(진입 경로는 19절 C-11).

### 15.5 친구, 살펴보기, 신고, 차단 [제안] (`ui 4.22`, `on 5.3`, `on 5.4`)

| 화면 | 규칙 |
|---|---|
| 친구 창 | 탭 4개: 친구 / 최근 만난 사람 / 받은 신청 / 차단. 친구 줄: 닉네임, 칭호, 접속 상태와 있는 곳(광장 채널 / 원정 중 / 결투 중), [따라가기] [결투 초대] [귓속말]. 친구 수 "37 / 100"(최대 100). 위에 닉네임으로 신청 |
| 최근 만난 사람 | 최근 결투 상대와 거래 상대 20명 |
| 살펴보기 | 왼쪽: 캐릭터, 닉네임, 칭호, 리그, 결투 점수, 시즌 최고 웨이브. 가운데: 지팡이 이름, 단계, 변이, 확장 슬롯, "주인 n번째", 지팡이 문서가 주는 단계 별명 한 줄, 흔적(주인이 공개를 켠 지팡이만, "역대 최고 +N"). 회전 그림을 주는 지팡이만 좌우 키로 돌려 본다. 오른쪽: 조립한 블록, 장착 IL. 조립 비공개면 "조립 비공개". 아래 [귓속말] [친구 신청] [거래 신청] [결투 도전] [차단] [신고] |
| 신고 창 | 사유 다섯(욕설, 사기, 현금 거래, 부정 프로그램, 기타), 한 줄 설명, "최근 대화 30줄이 함께 간다" |
| 차단 확인 | 막히는 것(대화, 귓속말, 도전, 거래 신청, 친구 신청)을 적는다. 최대 200명, 계정에 저장 |

### 15.6 직접 거래, 장터, 상점 (화면 상태만) [제안] (`ui 4.23`, `on 5.5`, `on 2.4`, `on 1.6`)

직접 거래 `Trade` 상태 기계:

```
Open ─(양쪽 물건 8칸 + 골드 입력)─▶ Editing
Editing ─[잠금](내 쪽)─▶ MyLocked
  어느 쪽이든 내용을 바꾸면 → 둘 다 잠금 풀림 → Editing
BothLocked: 양쪽이 잠근 뒤 5초(= 300 t)가 지나면 [최종 확인] 켜짐
[최종 확인] ─▶ TradeFinal: "내가 주는 것 / 받는 것"을 글자로 다시. 지팡이는 한 자루당 세 줄
               (이름과 단계 / 변이와 슬롯 / 역대 최고 +N · 하락 n번)
  크게 손해 보는 거래면 ─▶ TradeLossWarning("크게 손해 보는 거래입니다", 호박색) 확인 한 번 더
[그만두기] 어느 때든 → Closed
```

- 바뀐 칸 표시: 3초(= 180 t) 동안. **번쩍임 한도를 지켜 초당 2회 이하로 밝기만** 바뀐다.
- 지팡이를 올리면 단계, 변이, 슬롯, **역대 최고 단계와 하락 횟수를 항상** 적는다. 올린 사람 화면에 "거래 상대에게는 흔적이 보인다".
- 내 가방 탭 5개: 블록 / 재료 / 꾸미기 / 지팡이 / 시즌 한정. 지팡이 탭은 5.3절 정렬. 놓을 수 없는 칸 + 이유 툴팁: 끼워 둔 블록, 거래 잠금, 마지막 한 자루, 계정에 묶임. 묶인 재료는 같은 아이콘의 따로 된 칸 + 자물쇠.
- 수수료 줄: 양쪽이 낼 수수료(오간 값의 5%, 지팡이가 끼면 감정가의 10%) (`on 2.4`).
- 한 번에 8칸. 받은 지팡이는 7일 동안 다시 거래할 수 없다 (`on 1.6`).
- "크게 손해 보는 거래"의 판정 기준은 20절 [임시].

장터 `Market`:

- 탭 4개: 사기 / 팔기 / 내 등록 / 시세. 분류: 블록(종류와 등급), 강화석, 부적, 꾸미기, 지팡이(캐릭터 다섯), 시즌 한정. 찾기와 정렬(값, 남은 시간, 단계).
- 목록 줄: 아이콘, 이름, 수량, 낱개 값, 남은 시간, 파는 사람. **지팡이 줄에는 단계 묶음 테와 흔적 딱지가 항상**.
- 상세: 지팡이는 "역대 최고 +N, 지금 +M", 하락 횟수, 주인 n번째, 거래 잠금 안내, 최근 7일 거래 중앙값과 거래량, 시세 그림, 수량 조절, [사기].
- 팔기: +0 지팡이, 마지막 한 자루, 거래 잠금 지팡이는 "놓을 수 없음" + "+0 지팡이는 장터에 올릴 수 없다. 직접 거래는 된다". 값 입력, 보증금(1%)과 수수료(판매가의 5%, 지팡이 10%), 받을 금액, 등록 기간(48시간), 값 범위 안내. 범위를 벗어나면 입력 칸이 오류 상태.
- 내 등록: 등록한 것과 남은 시간, [내리기], "12 / 20"(계정당 동시 20건).
- 구매 확인: 물건, 수량, 합계. 지팡이면 흔적 줄을 한 번 더.
- 장터에서 산 지팡이는 우편함으로 온다.

상점 `Shop` (골드 상점 = 메인 메뉴 "상점" = 광장 행상 수레, 시즌 상점 = 느루의 흔들의자):

- 골드 상점 탭: 부적 / 새 지팡이 / 꾸미기 / 그 밖(서고 확장, 닉네임 바꾸기). 시즌 상점: 이번 시즌 꾸미기. **유료 탭은 상품이 0개면 없다.**
- 품목 카드: 이름, 값, 남은 한도("이번 주 2 / 3" — 부적은 프로필당 주 3개). 새 지팡이는 캐릭터마다 한 장, 카드 아래 "지금 n자루". 보관 한도가 없으므로 **골드만 있으면 언제나 켜져 있다**.
- 값: 부적 10,000골드, 새 지팡이 500골드, 서고 확장 50개당 2,000골드, 닉네임 변경 5,000골드 (`on 2.4`, `on 1.6`).

### 15.7 우편함, 옷장, 느루의 흔들의자 [제안] (`ui 4.24`)

- 우편함 탭 3개: 우편 / 쪽지 / 보낸 쪽지. 우편 줄: 봉투 상태, 보낸 이(장터, 느루, 운영자, 친구), 제목, 남은 보관 기간. 첨부: 물건과 골드, [받기] [모두 받기]. **검증 대기 중인 보상은 잠김 칸 + "검증이 끝나면 받는다".** 서고가 가득 차면 블록 첨부는 잠김 칸 + "서고에 자리가 나면 받는다". 쪽지 쓰기: 받는 이(친구에서 고르기), 한 줄 제목, 본문.
- 옷장: 메뉴에서 연다. **오프라인 프로필에서도 된다.** 캐릭터 5, 12분류(야생 물은 "옷" 분류 맨 위에 단계 고르기), 격자에는 **얻은 것만 나온다**(빈 칸·잠긴 칸·"상점에서 구매" 표시 없음, `fx 0.5`). 정보: 이름, 어디서 얻었는가, 거래 가능 여부("계정에 묶임"). 미리보기: 방향 돌리기(아래·옆·위), 걷기·앉기·감정표현, 지팡이는 현재 단계 그대로.
- 느루의 흔들의자: 한 줄 잡담 + 선택지 4개: 시즌 안내 / 후계 점수 내역 / 시즌 보상 받기 / 시즌 상점.
  - 시즌 안내: 시즌 번호, 남은 날, 일정, 마감 임박 딱지.
  - 후계 점수 내역: E와 P 두 막대(각 0~500), 내 리그 안의 백분위, 예상 순위, 대상 조건을 못 채웠으면 무엇이 모자란지(식은 `on 6.3`).
  - 시즌 보상 받기: 지난 시즌 순위와 받는 것.

### 15.8 리그, 순위표, 후계자 비석 [제안] (`ui 4.25`, `on 3.5`)

- 리그 화면: 리그 카드 3장(휘장, 리그 이름, 보유 IL 범위, 경기 중 제한 네 줄, 보상 계수 — 값은 `on 4.8`), 내 리그 카드에 확정 체크. 내 보유 IL 막대(세 리그 경계 + 내 값, "최근 14일의 최고값", 내려가는 날짜). [위 리그로 올라가기]: **시즌에 한 번**, 확인 창 **두 번**("이번 시즌에는 못 내려온다").
- 순위표: 위 띠 "제 N 시즌 · 남은 날". 큰 탭 4개: 후계 점수 / 무한 웨이브 / 결투 / 역대. 작은 탭: 후계 점수 → 리그 셋. 무한 웨이브 → 시즌 종합, 캐릭터별(다섯), 주간, 주간 "+7 이하", 주간 도전, 친구. 결투 → 리그 셋. 줄: 순위, 변동, 닉네임과 칭호, 캐릭터, 점수, 도달 웨이브("웨이브 21에서 출발"), 지팡이 단계, 조립, [다시 보기](**상위 100위**). 한 쪽 12줄. **내 줄은 화면 아래 항상 고정.** 쪽 넘김 "1 / 34", [내 순위로]. 옆 판: 고른 줄의 사람, [살펴보기] [다시 보기], 흔적은 공개를 켠 지팡이만.
- 주간 도전 탭: 첫 만남 전 프로필은 꺼짐 + "첫 밤을 넘기면 열린다" (17절).
- 후계자 비석: 이번 시즌 "제 N 시즌 · 지금 1위 {닉네임}" + [순위표 보기]. 역대 후계자 줄: 제 n대, 닉네임, 캐릭터, 지팡이와 단계, 후계 점수(영구). 새김 연출은 **시즌이 바뀐 뒤 처음 열 때 한 번**(`profile.lastMonumentSeasonSeen < currentSeason`). 옆 설명: 후계자가 되는 조건과 받는 것(`on 6.3`, `on 6.4`).

```csharp
static int LeaderboardPages(int rows) => Math.Max(1, (rows + 11) / 12);   // 줄 높이 56 × 12줄 [제안]
```

---

## 16. 복수 추적기 `RevengeTracker` (`ui 4.26`, `mon 3.9.5`, `gdd 13`, `DEC 1.21`)

### 16.1 상태와 값

```csharp
enum RevengeState { NotMet /*만나기 전*/, Pending /*복수 중*/, Completed /*복수 완료*/ }
record RevengeTrackerModel(
  RevengeState state,
  int bestPassedWave,            // 전 캐릭터 중 가장 높은 "넘긴 최고 웨이브"
  int? bestDamagePct,            // 웨이브 20에서 진 판 가운데 허상 체력을 가장 많이 깎은 비율(첫 만남은 세지 않음). 없으면 null
  int phantomAttempts,           // 도전 횟수
  int runsToComplete,            // 복수에 걸린 판 수(완료 후 기록 화면)
  DateOnly? completedAt,
  bool justCompletedThisRun);    // 그 판의 정산에서 "복수 완료"로 한 번
```

| 상태 | 조건 | 추적기 |
|---|---|---|
| `NotMet` | 새 프로필 | **어디에도 없다.** 첫 만남을 미리 알리지 않는다 |
| `Pending` | 첫 만남을 치른 뒤부터 웨이브 20의 허상을 처음 잡을 때까지 | 보인다 |
| `Completed` | 처음 잡은 뒤 | 그 판의 정산에서 "복수 완료"로 한 번 바뀐 뒤 사라진다. 기록 화면에 날짜와 걸린 판 수 |

### 16.2 꼴과 놓이는 곳 [제안]

| 꼴 | 놓이는 곳 | 내용 |
|---|---|---|
| 큰 꼴 | 야영의 다음 웨이브 칸, 기록 화면(432 폭) / 정산의 기록 쪽(1040 폭) | 1줄 "복수: 웨이브 20의 {boss.phantom}을 쓰러뜨려라". 2줄 "넘긴 최고 웨이브 {n} / 19" + 진행 막대(n/19). 3줄 "허상에게 낸 가장 큰 흠집 {p}% · 도전 {k}번"(한 번도 닿지 못했으면 **줄 없음**) |
| 한 줄 꼴 | 메인 메뉴 원정 카드, 원정 준비의 시작 웨이브 줄 위 | "복수 · 최고 웨이브 {n} / 19" |
| HUD 꼴 | 전투 HUD(웨이브 11~19만) | "허상까지 {20 − wave}웨이브". 전장을 덮지 않는다 |
| 처음 나타나는 쪽지 | 화면 오른쪽 위 | 첫 만남 쓰러짐 연출 끝(4.00초 시점)에 한 번: "복수: 웨이브 20에서 다시 만난다". **4초(= 240 t) 뒤** 올라간다 |
| 복수 완료 꼴 | 큰 꼴과 같은 자리 | 웨이브 20의 허상을 처음 잡은 판의 정산에서 한 번. "복수 완료" + 확정 체크 |

- 거울 표식 상태: `Intact`(복수 중), `Cracked`(흠집 50% 이상), `Broken`(복수 완료) [제안] (`ui 4.26`).

```csharp
static MirrorState Mirror(RevengeTrackerModel m) =>
  m.state == RevengeState.Completed ? MirrorState.Broken : (m.bestDamagePct ?? 0) >= 50 ? MirrorState.Cracked : MirrorState.Intact;
static int TrackerProgressNumerator(int bestPassed) => Math.Min(19, bestPassed);   // "n / 19" [임시] 19에서 멈춤
static int? HudWavesLeft(RevengeState s, int wave) => s == RevengeState.Pending && wave >= 11 && wave <= 19 ? 20 - wave : null;
```

- 이름 "스승의 허상"은 이름 표에서 읽는다.
- 프로필에 남는 값과 되돌림 규칙(서버): "허상을 만난 적 있음"은 정산(1차 검사 통과) 때 바로 적고 검증 실패에도 되돌리지 않는다. "복수 완료"는 검증 대기로 적었다가 실패하면 보상과 함께 되돌린다. 도전 횟수·가장 큰 흠집·걸린 판 수는 재실행 검증을 통과한 판의 값만 [제안] (`mon 3.9.5`, `on 3.2`). → 화면은 서버가 준 값만 그린다.

---

## 17. `online.md` 규칙에 딸린 안내와 표시 (`ui 4.26`)

전부 공용 부품으로 만든다(새 그림 없음).

| id | 조건 | 놓이는 곳 | 화면 동작 | 출처 |
|---|---|---|---|---|
| `debt_owed` | 계정에 "갚을 것" > 0 | 메인 메뉴 위 띠 골드 옆, 거래·장터 위 띠 | "갚을 것 n"(호박색). 고르면 목록(골드, 강화석, 부적, 블록) + "0이 될 때까지 직접 거래, 장터, 선물, 지팡이 떠나보내기를 할 수 없다". 그동안 거래 신청·장터 등록과 구매·선물·떠나보내기 단추 꺼짐 + 이유 툴팁 | `on 3.2` 6번, `on 5.5` |
| `trade_not_open` | 무한 웨이브 10 도달 + 누적 플레이 2시간을 못 채움 | 장터 | [시세]만 켜지고 [사기] [팔기] [내 등록] 꺼짐. 위 띠 아래: "웨이브 10 도달과 누적 플레이 2시간 뒤에 거래가 열린다" + 지금까지 채운 양(진행 막대 둘). 상호작용 목록 "거래 신청"도 꺼짐 | `on 5.5` |
| `core_empty` | 고른 지팡이의 코어가 빔 | 원정 준비·결투 입장의 출발 조립 판 | 코어 칸 경고 딱지 + "코어에 투사체 블록을 끼워야 출발할 수 있다". [출발] 꺼짐, [조립 바꾸기]에 선택 괄호 | `on 1.1` |
| `last_projectile` | 서고와 조립을 합쳐 투사체 블록이 1개뿐 | 강화 제물 후보, 거래 창 내 가방, 장터 팔기 | 그 칸 "놓을 수 없음" + 툴팁 "마지막 투사체 블록은 올릴 수 없다" | `on 1.1` |
| `library_full` | 서고 수량 ≥ 한도 | 서고 탭 위, 정산 귀환 가방 쪽 위, 거래 창 내 가방 위 | "서고 87 / 100". 가득 차면 숫자 호박색 + "서고가 가득 찼다". 정산: "담지 못한 블록은 골드로 바뀐다". 거래 창: 받는 쪽 블록 칸 "놓을 수 없음". 우편 첨부: 잠김 칸 + "서고에 자리가 나면 받는다". 늘리기는 상점 "그 밖" 탭 | `on 2.4` |
| `weekly_locked` | 허상 첫 만남 전 | 순위표 주간 도전 탭, 주간 도전 시작 단추 | 꺼짐 + "첫 밤을 넘기면 열린다" | `on 3.3` |
| `email_unverified` | 서버 프로필 이메일 미확인 | 메인 메뉴 위 띠, 설정 > 계정 | "이메일 미확인: 비밀번호를 잃으면 되찾을 수 없다". **로그인과 게임은 막지 않는다** | `on 7.3` |
| `recovery_mail_off` | 서버의 메일 꺼짐 | 로그인 판, 설정 > 계정 | 복구와 확인 단추 꺼짐 + "지금은 메일을 보낼 수 없다" | `on 7.3` |
| `bound_materials` | 계정에 묶인 강화석·부적 보유 | 서고·강화 화면의 재료 수량, 거래 창 내 가방 | 수량 옆 "(묶임 n)". 툴팁 "복수 보상으로 받은 재료. 거래할 수 없고 강화에서 먼저 쓰인다" | `on 5.5` |
| `weekly_reward_mail` | 주간 도전 보상 | 우편함 | 보통 우편 한 줄. 보낸 이 "느루", 제목 "주간 도전 · 상위 n%", 첨부 시즌 주화와 (상위 10%면) 그 주의 칭호 | `on 2.2` |
| `expedition_cleanup` | 종료 요약을 내지 못한 판 | 원정 준비 지팡이 판 | 5.6절 | `on 3.2` |

```csharp
static bool TradeOpen(Account a) => a.reachedWave10 && a.totalPlayTime >= TimeSpan.FromHours(2) && a.debt.IsEmpty;
static string LibraryCounter(int used, int cap) => $"서고 {used} / {cap}";   // used = 끼워 둔 블록 제외 합계
```

---

## 18. 테스트 벡터

형식: `given` 상태 / `when` 사건 / `then` 기대값. 각 줄은 자동 테스트 하나가 된다(NUnit/xUnit, `Wizard.Rules`와 화면 모델 단위). 계산형 벡터는 위 절의 함수 이름을 쓴다.

### 18.1 부팅·타이틀·인증

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-001 | 설정 파일 없음 | 부팅 | `FirstRunPrompt`가 `Title`보다 먼저 뜬다. 오프라인 프로필이 자동 생성(캐릭터 5, 지팡이 5자루 모두 +0, 골드 0) | `ui 4.1`, `on 8.1`, `on 1.1` |
| TV-002 | `FirstRunPrompt` | `Reduced` 확정 | `settings.motion_preset == Reduced` 저장, 화면 = `Title`, 그동안 `FlashManager.Submit` 호출 0회 | `ui 4.1`, `fx 0.4` |
| TV-003 | 설정 파일 있음 | 부팅 | `FirstRunPrompt` 건너뛰고 `Title` | `fx 0.4` |
| TV-004 | 서버 프로필 닉네임 "별빛" | `Title` 진입 | 프로필 표지에 서버 종류·"별빛"·서버 상태 | `ui 4.2` |
| TV-005 | `MainMenu` | 프로필 바꾸기 시도 | 메인 메뉴에는 프로필 전환 수단이 없다(타이틀에서만) | `on 8.2` |
| TV-006 | — | `IsValidLoginId("wizard01")` / `("Wiz01")` / `("wiz")` / `("a234567890123456")` / `("a2345678901234567")` | true / false / false / true(16자) / false(17자) | `on 7.3` |
| TV-007 | 아이디 "wizard01" | `IsValidPassword("abcdefghi")` / `("abcdefghij")` / `("xxwizard01yy")` | false(9자) / true / false(아이디 포함) | `on 7.3` |
| TV-008 | — | `IsValidNickname("가")` / `("제자")` / `("엄청위대한마법사")` / `("엄청엄청위대한마법")` | false / true / true(8자) / false(9자) | `on 7.3` |
| TV-009 | — | 이메일 길이 254 / 255 | 통과 / 거절 | `on 7.3` |
| TV-010 | 연속 실패 4회 | 5번째 실패 | 대기 1초. 6번째 2초, 7번째 4초, 14번째 512초, 15번째 900초, 20번째 900초(최대 15분) | `on 7.3` |
| TV-011 | 없는 아이디 / 있는 아이디 + 틀린 비밀번호 | 로그인 | 두 경우 오류 문구가 같은 문자열 | `on 7.3` |
| TV-012 | 가입 성공, 메일 켜짐 | — | `EmailVerify`, 주소는 `MaskEmail("alice@daum.net") == "a***@d***.net"` | `ui 4.3` |
| TV-013 | `EmailVerify`, 남은 횟수 5 | 틀린 코드 | 칸 오류 상태, 남은 횟수 4. 5번째 틀림 → 코드 폐기 | `on 7.3` |
| TV-014 | 확인 코드 보낸 지 59초 | — | [다시 보내기] 꺼짐 + 남은 1초. 60초 → 켜짐. 하루 6번째 → 꺼짐 | `on 7.3`, `ui 4.3` |
| TV-015 | 코드 보낸 지 15분 1초 | 맞는 코드 입력 | 만료로 거절 | `on 7.3` |
| TV-016 | `EmailVerify` | [나중에 하기] | 경고 한 번 "확인하지 않으면 비밀번호를 잃었을 때 되찾을 수 없다" → `MainMenu`, 위 띠에 "이메일 미확인" 꼬리표 | `ui 4.3`, `ui 4.5` |
| TV-017 | 가입 성공, 메일 꺼짐 | — | `EmailVerify` 대신 쪽지 "지금은 확인 메일을 보낼 수 없다. 설정 > 계정에서 나중에 확인한다", 이메일은 미확인으로 저장 | `ui 4.3`, `on 7.3` |
| TV-018 | 복구 메일 꺼짐 | 로그인 판 [비밀번호를 잊었다] | `RecoveryMailOff`. 옆 꼬리표 "지금은 보낼 수 없음". [로그인] [가입]은 켜짐 | `ui 4.3` |
| TV-019 | 아이디·이메일 아무거나 | [재설정 코드 보내기] | 계정 유무와 무관하게 같은 문구 "맞는 계정이 있으면 메일을 보냈다. 30분 안에 코드를 넣어 달라" → `PasswordReset` | `on 7.3` |
| TV-020 | — | `ResetAlphabet.Length`, 12자 엔트로피 | 32, `12 × log2(32) = 60`비트 | `on 7.3` |
| TV-021 | 붙여넣기 "abcd-efgh jklm" | `NormalizeResetCodeInput` → `GroupResetCode` | "ABCDEFGHJKLM" → "ABCD EFGH JKLM" | `ui 4.3` |
| TV-022 | 재설정 성공 | — | "모든 기기에서 로그아웃되었다. 24시간 동안 거래와 장터 등록이 막힌다" → `Login`, `TradeOpen` false 24시간 | `ui 4.3`, `on 7.3` |
| TV-023 | 접속자 수 = 상한(400) | 새 로그인 | `ConnectQueue`(내 순서, [오프라인 프로필로 하기]) | `on 7.5`, `ui 4.3` |
| TV-024 | 서버 응답 없음 | 로그인 | `ServerUnreachable` "까마귀가 아직 안 왔다." | `ui 4.3` |
| TV-025 | 처음 보는 기기 | 로그인 성공 | `NewDeviceNotice` 후 `MainMenu` | `on 7.3` |

### 18.2 메인 메뉴·원정 준비·HUD

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-026 | 오프라인 프로필 | `MainMenu` | 결투·광장 카드와 우편함·친구·순위표·시즌·장터 줄이 묶음 상자 안, `enabled`는 바뀌지 않고 흐리게 그리지 않는다. "서버에 접속하면 열립니다" 한 줄 | `gdd 13`, `ui 4.4` |
| TV-027 | 서버 프로필 | `MainMenu` | 메뉴 목록 12줄, 서버 전용 다섯 줄이 7~11번째 뒤(아래쪽)에 모임. `12 × 48 + 11 × 16 == 752` | `ui 4.5` |
| TV-028 | 오프라인 첫 진입 | `MainMenu` 진입 전 | `OfflineGuide` 한 번. 두 번째 진입에는 안 뜸 | `ui 4.4` |
| TV-029 | 같은 캐릭터 지팡이 3자루(+5 든 지팡이, +9, +7), 정렬 단계 높은 순 | `SortRows` | [+5(든), +9, +7] (든 지팡이 맨 위 고정) | `on 1.6` |
| TV-030 | 같은 3자루, 정렬 단계 낮은 순 | `SortRows` | [+5(든), +7, +9] | `on 1.6` |
| TV-031 | 고른 지팡이 코어 빔 | `CanDepart` | Blocked, 문구 "코어에 투사체 블록을 끼워야 출발할 수 있다", [조립 바꾸기]에 커서 | `on 1.1`, `ui 4.26` |
| TV-032 | 상점에서 산 +0 지팡이(빈 조립) | 원정 준비에서 고름 | TV-031과 같다 | `on 1.6` |
| TV-033 | `bestPassed = 34` | `AllowedSkips` | [0, 10, 20] → 첫 웨이브 1, 11, 21 | `mon 4.7` |
| TV-034 | `bestPassed = 14` | `AllowedSkips`, `ShowStartWaveRow` | [0], false(줄 없음) | `mon 4.7` |
| TV-035 | `bestPassed = 15` | `AllowedSkips` | [0, 10] | `mon 4.7`, `on 1.2` |
| TV-036 | `bestPassed = 28` / `29` | `AllowedSkips` | [0, 10] / [0, 10, 20] (20웨이브 건너뛰기는 29 이상) | `mon 3.9.4` |
| TV-037 | 건너뛰기 10 / 20 / 30 / 40 / 50 | `SkippedScore` | 8,398 / 28,859 / 64,764 / 120,409 / 195,829 | `mon 4.7` |
| TV-038 | 결투 입장(`DuelEntry`) | `ExpeditionPrep` | 시작 웨이브 줄 없음, 버튼 문구 "준비" | `ui 4.6` |
| TV-039 | "허상을 만난 적 없음" 켜짐 + 정산 안 된 판 1개 | `CanDepart` | Blocked(그 판 제출 또는 [원정 정리] 먼저) | `on 3.2` |
| TV-040 | 종료 요약을 못 낸 판, 마지막 중간 지점 웨이브 15 | [원정 정리] | 확인 창 "마지막 중간 지점(웨이브 15)까지 정산한다" 외 2줄 | `ui 4.26`, `on 3.2` |
| TV-041 | 중간 지점 없음 | [원정 정리] | 확인 창 "보상 없이 잠금만 푼다" | `ui 4.26` |
| TV-042 | wave 23 / 10 / 11 / 1 / 20 | `day` | 3 / 1 / 2 / 1 / 2 | `gdd 2.2` |
| TV-043 | 복수 중, wave 14 | `HudWavesLeft` | 6 → "허상까지 6웨이브" | `ui 4.26`, `gdd 13` |
| TV-044 | 복수 중, wave 10 / 19 / 20 | `HudWavesLeft` | null / 1 / null(대신 보스 이름 옆 "복수" 꼬리표) | `gdd 13` |
| TV-045 | 만나기 전, wave 14 | `HudWavesLeft` | null | `mon 3.9.5` |
| TV-046 | 보스 웨이브(wave 30) | HUD | `waveProgress` 숨김, 보스 표시가 대신, 날·웨이브 줄은 한 줄로 축소 | `ui 4.8` |
| TV-047 | 첫 만남 웨이브 10, 허상 체력 50% | HUD 보스 바 | 50%에서 멈춤, 눈금·자물쇠 표시 없음 | `ui 4.8`, `mon 3.9` |
| TV-048 | 체력 8칸, 칸 32, 간격 4 | 폭 | `8×32 + 7×4 = 284` | `ui 4.8` |
| TV-049 | 회로 9칸, 칸 40, 간격 4 | 폭 | `9×40 + 8×4 = 392` | `ui 4.8` |
| TV-050 | 마감 여유 20 / 15 / 10 / 5 / 4.9 ms | `ClassifyMargin` | Green / Green / Yellow / Yellow / Amber. 어떤 경우에도 빨강 아님 | `on 4.12`, `ui 4.9` |
| TV-051 | 결투 중 | 일시정지 | 버튼 [계속] [설정] [기권]만, 코어 틱 계속 진행 | `ui 4.15` |
| TV-052 | 관전 HUD | — | 마감 여유·입력 버려짐·조준선 없음, "관전 중 · 2초 늦게 본다" | `ui 4.9` |
| TV-053 | 입력이 회선 때문에 버려짐 | — | 표시 1.5초(= 90 t) 뒤 사라짐 | `ui 4.9` |
| TV-054 | 피해 종류 Critical | 표시 | 1.5배, 흰색 + 검정 굵은 테 + "!", 금색 아님 | `gdd 13` |

### 18.3 전리품·야영·조립

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-055 | 일반 웨이브 종료 | `LootPick` | 카드 4장, 고르는 수 2 | `mon 6.2.1` |
| TV-056 | 엘리트 1마리 잡은 웨이브 | `LootPick` | 5장 / 2 | `mon 6.2.1` |
| TV-057 | 웨이브 30 보스 처치 | `LootPick` | 5장, 그중 1장 영웅 확정 | `mon 6.2.1` |
| TV-058 | 첫 만남 웨이브 10 | `LootPick` | 4장, 확정 칸 없음 | `mon 3.9.3` |
| TV-059 | 1일째 웨이브 6 | `LootPick` | 카드에 더해 빌린 영웅 1장("빌림" 꼬리표) | `mon 6.2.3` |
| TV-060 | 4장 중 1장 고름 | [확인] | 꺼짐. 2장 고름 → 켜짐. 3번째 고르기 → 무시 | `ui 4.10` |
| TV-061 | 원정 지갑 29 | [다시 뽑기] | 꺼짐("골드가 모자란다") | `mon 6.2.1` |
| TV-062 | 원정 지갑 100, 2장 고른 상태 | [다시 뽑기] | 지갑 70, 카드 새로, 고른 것 비움, 확정 칸 유지 | `mon 6.2.1` |
| TV-063 | 보관함 11칸 찼음 | 2장 확정 | `OverflowDiscard`: 1개 버림 필요. 버린 희귀 → 15골드 | `mon 6.2.1`, `mon 6.1` |
| TV-064 | 4장 / 5장 카드, 폭 240, 간격 24 | 폭 | 1032 / 1296 | `ui 4.10` |
| TV-065 | 웨이브 10, 이 야영 첫 회복 / 두 번째 | `HealCost` | 80 / 120 | `mon 4.6` |
| TV-066 | 웨이브 23, 첫 회복 / 두 번째 | `HealCost` | 145 / 217 (217.5 내림) | `mon 4.6` |
| TV-067 | 보스 처치 뒤 야영(새벽 야영) | 진입 | 체력 1칸 무료 회복 | `mon 4.6` |
| TV-068 | 야영 | 강화 탭 찾기 | 없다(`Camp`·`Library`·`LoadoutEdit` 모두) | `DEC 2.3`, `gdd 13` |
| TV-069 | 임계 사격 가능 횟수 0 | `CampModel` | 수치 칸 경고 표시 | `gdd 13` |
| TV-070 | 조립 변경으로 발사당 열 8 → 11 | — | 그 값이 0.5초(30 t) 밝아짐 | `ui 4.11` |
| TV-071 | 첫 만남 전, 웨이브 9 뒤 야영 | 다음 웨이브 칸 | 보스 이름 "스승의 허상"(이름 표) 평소대로, 그 밖의 경고 없음 | `ui 4.11` |
| TV-072 | 출발 표식 위 J 59 t 누름 / 60 t | — | 출발 안 함 / 다음 웨이브 시작 | `mon 4.6` |
| TV-073 | 깊이 1 / 2 / 3, 일반 지팡이 | `ScopeMultiplierText` | "×0.85" / "×0.72" / "×0.61" | `ui 4.11`, `gdd 4` |
| TV-074 | 깊이 1 / 2 / 3, 갈피 | `ScopeMultiplierText` | "×0.92" / "×0.85" / "×0.78" + 각주 점 1/2/3개 | `gdd 4`, `wands/galpi.md` |
| TV-075 | 한 스코프에 변형 8개 | 트리 줄 | 아이콘 6개 + "+2" | `ui 4.11` |
| TV-076 | 투사체 70개가 되는 조립 | 트리 | 가장 깊은 스코프부터 점선 마디 + "생략", 맨 아래 줄 투사체 수 ≤ 64 | `gdd 4`, `ui 4.11` |
| TV-077 | 경고 3개 초과(5개) | 경고 줄 | 3줄만 | `ui 4.11` |
| TV-078 | 새 프로필의 고유 비기 마디 | `ArtTrie` | `Unlocked` + ★ | `DEC 1.23`, `ui 4.13` |
| TV-079 | 해금 후보가 1개뿐 | 해금 차례 | 카드 1장 | `ui 4.13` |
| TV-080 | 기본 간격 0.3초인 가지 / 0.18초 가지 | 간격 표시 | 표시 없음 / "0.18초" | `ui 4.13` |
| TV-081 | 3번째 입력이 0.02초 늦음 / K가 0.03초 늦음 | `LateMessage` | "3번째 입력이 0.02초 늦음" / "K가 0.03초 늦음" | `ui 4.14` |
| TV-082 | 메인 메뉴 "연습" / 야영 연습 / 광장 허수아비 | 열기 | 모두 같은 `DummyPractice` | `gdd 13` |
| TV-083 | 서고 블록: 마력탄 1(끼움), 서리 0개(가진 적 있음) | `Library` | 마력탄 칸 수량은 "끼움"으로 따로, 서리 칸은 0개 상태, 가진 적 없는 블록 칸은 없다 | `ui 4.11`, `on 1.6` |

### 18.4 강화 화면

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-084 | +7 지팡이, 역대 최고 +8, 제물 희귀+영웅(22), 같은 속성 블록 포함, 기운 2 | `Compute` | 기본 25, 제물 22, 같은 속성 5, 기운 30, 합 82, capped false | `fx 1.2` |
| TV-085 | TV-084 상태 | `Hint` (부적 없음) | `DropTo` → "실패하면: +6으로 하락", "하락 가능" 꼬리표 켜짐 | `fx 1.2` |
| TV-086 | p = 82, target 8 | `Band` | 대성공 1230bp(12.3%), 성공 6970, 변이 450, 하락 1350 | `gdd 9`, `fx 1.2` |
| TV-087 | +0 지팡이 | `Compute`, `Band` | 100%, 대성공 0bp, 성공 10000bp | `gdd 9` |
| TV-088 | +1, 제물 영웅 14 | `Compute` | 90 + 14 = 104 → 95, capped true, "상한" 표시 | `gdd 9`, `fx 1.2` |
| TV-089 | +12, 제물 영웅 3 + 희귀 1(합 50), 같은 속성, 기운 3 | `Compute` | 3 + 40 + 5 + 45 = 93 | `gdd 9` |
| TV-090 | +3 / +4, 역대 최고 같은 단계 | `DropPossible`, `Hint` | false `NoDropLow` / true `DropTo` (+3으로) | `gdd 9` |
| TV-091 | +7, 역대 최고 +9 | `Hint` | `AtFloor` → "바닥이다(역대 최고 +9)", 부적 칸 접힘 "지금은 필요 없다" | `fx 1.2` |
| TV-092 | +8, 역대 최고 +8, 부적 끼움 | `Hint` | `CharmProtects` "부적이 막아 준다", "하락 가능" 꼬리표 없음 | `fx 1.2` |
| TV-093 | 현재 0 / 7 / 12 | `GoldCost` | 100 / 6,400 / 16,900 | `on 2.4` |
| TV-094 | 도전 5 / 6 / 9 / 10 / 13 | `StoneFor` | 하 / 중 / 중 / 상 / 상 | `on 1.3` |
| TV-095 | 골드 6,399, 도전 +8 | [강화 J] | 꺼짐 + 모자란 쪽(골드) 흔들림 이벤트 | `fx 1.2` |
| TV-096 | 투사체 블록이 서고·조립 합쳐 1개 | 제물 후보 | 그 칸 "놓을 수 없음" + "마지막 투사체 블록은 올릴 수 없다" | `on 1.1` |
| TV-097 | 강도 3, 제물 있음 | `Timeline` | 제물 끝 0.75(45 t), 충전 끝 2.55(153 t), 판정 2.90(174 t) | `fx 2` |
| TV-098 | 강도 3, 제물 없음 | `Timeline` | 판정 0.15 + 0.20 + 1.80 + 0.35 = 2.50초(150 t) | `fx 2` |
| TV-099 | 강도 1 / 2 / 4 / 5, 제물 있음 | `Timeline` 판정 시각 | 1.70 / 2.20 / 3.65 / 4.75초 | `fx 4` |
| TV-100 | +1 도전 | 의식 | 충전 0.4초, 숨 멈춤 0, 심장 소리 이벤트 없음, 결과 Success만 | `fx 2` |
| TV-101 | 충전 진행 0.2 / 0.4 / 0.7 / 1.0 | `ChargeGauge` | 0.4 / 0.8 / 0.9 / 1.0 | `fx 2` |
| TV-102 | 제물 5개 | `OfferTick(0..4)` | 9, 16, 23, 31, 38 t | `fx 2` |
| TV-103 | 도전 2 / 4 / 7 / 10 / 13 | `Intensity` | 1 / 2 / 3 / 4 / 5 | `fx 4` |
| TV-104 | 충전 끝까지 결과 미도착(서버) | 2.55초 | `WaitingLate`, 게이지 97% 고정 | `fx 2` |
| TV-105 | `WaitingLate` 진입 후 5초 | — | "까마귀가 늦는다", 조작 잠금 해제. 이어서 결과 도착 → `ShortResult` 0.5초 | `fx 2` |
| TV-106 | 오프라인 프로필 | 강화 | `WaitingLate`에 들어가지 않는다 | `fx 2`, `fx 1.1` |
| TV-107 | 의식 중, 누름 뒤 0.2초 | J | 무시 | `fx 7` |
| TV-108 | 의식 중, 누름 뒤 0.5초 | J | 판정으로 넘어가 0.5초 짧은 형태 | `fx 7` |
| TV-109 | `MutationChoice` | J(건너뛰기) | 무시. [받는다]/[거절한다]만 진행 | `fx 3.6`, `fx 7` |
| TV-110 | +12 → +13 첫 도달, 1.5초 / 2.5초 | J | 무시 / 넘김 | `fx 3.8` |
| TV-111 | 결과 Success(+6 → +7), 역대 최고 6 | 결과 뒤 | Milestone 2.5초(첫 도달) | `fx 3.7` |
| TV-112 | 결과 Success(+6 → +7), 역대 최고 9 | 결과 뒤 | Milestone 0.8초(다시 도달) | `fx 3.7` |
| TV-113 | Success(+4 → +5) | 결과 뒤 | Milestone 없음 | `fx 3.7` |
| TV-114 | 서버, Success(+9 → +10) | 결과 카드 | "서버 전체에 알렸다" 줄. 오프라인이면 줄 없음 | `fx 3.7`, `fx 8` |
| TV-115 | Success(+8 → +9) | 결과 카드 | "서버 전체에 알렸다" 없음 | `fx 8` |
| TV-116 | +13 역대 최고 갱신 / 전에 +13을 찍고 내려갔다 재도달 | `Plus13` | 9초 전체 / 0.80~3.00만(2.2초) | `fx 3.8` |
| TV-117 | GreatSuccess, 확장 3칸 이미 참 | 결과 카드 | 슬롯 확장 구간 건너뜀, "슬롯은 이미 다 자랐다" | `fx 3.2` |
| TV-118 | GreatSuccess at +12 | 연출 | 숫자 한 번만 내려찍고 `Plus13`으로 | `fx 3.2` |
| TV-119 | FailDrop(+7 → +6), 흔적 공개 끔 | 결과 카드 | "흔적이 남았다 · 지금은 [비공개]" + 바꾸는 단추, 기운 +1 | `fx 1.3`, `fx 3.3` |
| TV-120 | FailCharmBlocked, 기운 1 | 결과 | 기운 1 그대로, 부적 −1, "부적 1개가 대신 탔다." | `fx 3.4`, `gdd 9` |
| TV-121 | FailNoDrop at 바닥(역대 최고 +9, 지금 +7) | 결과 카드 | "더는 안 내려간다 (역대 최고 +9)", 기운 +1 | `fx 3.5` |
| TV-122 | MutationOffer, 변이 칸 2/2(초롱 아님) | 변이 카드 | "가장 오래된 {변이}가 밀려난다" 함께 표시 | `fx 3.6` |
| TV-123 | MutationOffer, 거절, fallback FailDrop | — | FailDrop의 0.50 이후를 재생 → 단계 −1, 기운 +1 | `fx 3.6` |
| TV-124 | 기운 3 | 실패 | 기운 3 유지(최대 3) | `gdd 9` |
| TV-125 | 빠른 강화 켬, 도전 +9 / +10 | `UseFullRitual` | false(빠름) / true(전체) — `enhance_fast_allow_high` 끔 | `fx 7` |
| TV-126 | 빠른 강화 켬, 도전 +10, `enhance_fast_allow_high` 켬 | `UseFullRitual` | false | `fx 7` |
| TV-127 | 이어서 강화, 목표 +10, 시도 결과 +9 → +10 | — | 멈춤(목표 도달) | `fx 7` |
| TV-128 | 이어서 강화 | 결과 GreatSuccess / MutationOffer / 마일스톤 | 각각 멈춤 | `fx 7` |
| TV-129 | 이어서 강화, 같은 등급·속성 블록이 서고에 더 없음 | 다음 시도 전 | 멈춤(제물 부족) | `fx 7` |
| TV-130 | 이어서 강화 12번째 시도, 성공 5, 실패 7, 쓴 골드 44800 | 누계 띠 | "12번째 시도 · 성공 5 · 실패 7 · 쓴 골드 44,800" | `fx 7`, `ui 4.12` |
| TV-131 | 뽑힌 값 37.2, p 82 | 결과 카드 펼침 | "뽑힌 값 37.2 / 82.0 이하면 성공" | `fx 7` |
| TV-132 | 결투 대기열 중 강화 의식, 매치 성사 | — | 의식 건너뛰고 결과만 → 결투 시작 화면 | `on 1.4` |
| TV-133 | 판(원정) 중 | 강화 화면 열기 | 불가(메뉴에 없음) | `on 1.4` |
| TV-134 | 광장 메뉴에서 강화 열고 Esc | — | 광장으로 돌아감. 그동안 남에게 보인 것은 말줄임표뿐 | `fx 1.1` |
| TV-135 | +0, 제물·부적 없음 | `ExpectedToTarget` 목표 +5 / +7 / +10 / +13 (시도) | 6.71 / 14.78 / 49.01 / 174.92 (시뮬레이션 7 / 15 / 49 / 175) | `on 2.5`, `gdd 9` |
| TV-136 | 같음 | 골드 기대값 | 8,517 / 37,461 / 273,628 / 1,776,723 (시뮬레이션 8,500 / 37,600 / 272,000 / 1,780,000, 차이 1% 이내) | `on 2.5` |
| TV-137 | 같음, 목표 +13 | 강화석 기대값 하/중/상 | 9.0 / 48.1 / 117.8 (시뮬레이션 9 / 48 / 118) | `on 2.5` |
| TV-138 | 상태 공간 | 크기 | 14 × 4 × 3 = 168 | `gdd 9` |
| TV-139 | 확장 0 / 1 / 2칸 | `EngraveGold` | 20,000 / 40,000 / 60,000 (+ 상급 2개) | `on 1.5` |
| TV-140 | 같은 칸에서 실패 0 / 1 / 2 / 3회 | `EngraveChancePct` | 40 / 60 / 80 / 100 | `on 1.5` |
| TV-141 | +6 지팡이 / +7 확장 3칸 | [슬롯 새기기] | 꺼짐 / 꺼짐 | `on 1.5` |
| TV-142 | 변이 1개 제거 | 비용 | 3,000골드 | `on 2.4` |
| TV-143 | 흔적 있음, 공개 끔 | `ShowTraceTo` 주인 / 광장 남 / 거래 창 상대 / 장터 상세 | true / false / true / true | `fx 1.3` |
| TV-144 | 지금 +5, 역대 최고 +7 / +6 | 흔적 개수 | 2 / 1 | `fx 1.3` |
| TV-145 | 설정 화면 | 옵션 목록 | "흔적 공개" 없음, 안내 한 줄만 | `fx 1.3`, `ui 4.19` |
| TV-146 | 캐릭터 1의 지팡이가 1자루 | `CanRelease` | No("마지막 한 자루") | `on 1.6` |
| TV-147 | 2자루 중 하나가 장터 등록 중 | 남은 보관 1자루 `CanRelease` | No("마지막 한 자루": 등록 중인 것은 손에 있는 것으로 치지 않음) | `on 1.6` |
| TV-148 | +0 지팡이 떠나보내기 | 돌려받는 골드 | 0 | `on 1.6` |
| TV-149 | +7 지팡이 | 확인 창 | 이름 입력 전 [떠나보내기] 꺼짐, 정확히 입력 → 켜짐 | `on 1.6`, `ui 4.12` |
| TV-150 | 여러 자루: +3 변이 없음 확장 없음 / +4 / +2 확장 1칸 / +1 변이 1 | `Checkable` | true / false / false / false | `on 1.6` |
| TV-151 | 갚을 것 1 이상 | 어떤 지팡이든 `CanRelease` | No | `on 3.2` |
| TV-152 | 하급 강화석을 처음 받은 정산 직후 | 다음 강화 화면 진입 | `EnhanceTutorial` 4쪽 한 번. 두 번째 진입에는 없음 | `gdd 13` |
| TV-153 | 첫 판이 웨이브 2에서 끝남 | 정산 뒤 | 안내 미룸 | `gdd 13` |

### 18.5 정산·기록

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-154 | 직접 넘긴 웨이브 0, 2, 3, 8, 10, 14, 20, 30, 36, 40, 60 | `BagSlots` | 0, 0, 1, 2, 3, 4, 6, 10, 12, 12, 12 | `mon 6.2.4` |
| TV-155 | 같은 값 | `HeroCap` | 0, 0, 0, 0, 1, 1, 2, 3, 3, 4, 6 | `mon 6.2.4` |
| TV-156 | 12칸 | 칸 줄 | 8 + 4 두 줄 | `ui 4.16` |
| TV-157 | 칸 2, 영웅 한도 0, 후보 [영웅, 희귀, 일반, 빌린 영웅] | `AutoFill` | [희귀, 일반] | `mon 6.2.4` |
| TV-158 | 칸 3, 영웅 한도 1, 영웅 1개 담음 | 영웅 하나 더 고르기 | 거부("영웅 한도") | `mon 6.2.4` |
| TV-159 | 빌린 블록 | 가방에 담기 | 거부, 판 끝 50골드 | `mon 6.2.3` |
| TV-160 | 담지 못한 일반 2, 희귀 1, 영웅 1 | 골드 합 | 2×5 + 15 + 50 = 75 | `mon 6.1` |
| TV-161 | 웨이브 21 출발, 23 도달 | 기록 줄 | "3일째 웨이브 23" + "웨이브 21에서 출발" | `ui 4.16` |
| TV-162 | 첫 만남이 든 판 | 점수 내역 | "허상과의 첫 만남 3,648" 줄, 복수 추적기 처음 등장 | `ui 4.16`, `mon 3.9.3` |
| TV-163 | 웨이브 20 허상을 처음 잡은 판 | 3쪽 | 맨 위 복수 보상 줄: 중급 3, 상급 1, 부적 1 + 칭호 + 머리 장식, 재료·장식에 "계정에 묶임" | `mon 3.9.4`, `ui 4.16` |
| TV-164 | 서고 99 / 100, 가방 2개 고름 | 확정 | [임시] 1개만 담김, 나머지 골드 + "서고가 가득 찼다" | `on 2.4` |
| TV-165 | 자동 매치 결투 결과 | 점수 변동 | "1512 → 1530". 방·광장 도전이면 "전적에만 남는다" | `ui 4.17` |
| TV-166 | 결투 패배 | 승패 띠 | 회청, 빨강·금색 아님 | `ui 4.17` |
| TV-167 | 첫 만남 전 프로필 | 기록 "전체" 탭 | 복수 기록 자리 없음 | `ui 4.18` |

### 18.6 설정·키·연출 줄이기

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-168 | 기본 설정 | 값 확인 | 내 탄 100, 피해 숫자 100·전부, 연출 Full, 흔들림 100, 번쩍 100, 그림 정지 켬, 지팡이 효과 전부·100, 남의 지팡이 효과 전부, 탄 꼬리 켬, 바닥 흔적 켬, 처치 조각 켬, 기술 이름 켬, 콤보 캐릭터 옆, 사본·지대 100, 날씨 켬, 예고 채움 70, 색각 보조 끔, 서버 강화 알림 전체, 강화 의식 전체 | `fx 18` |
| TV-169 | 옵션 목록 | 검색 | 적 예고, 적 탄, 열 링, 피격 표시, 시야 화살표 항목이 없다 | `gdd 13`, `fx 18` |
| TV-170 | 내 탄 투명도 29 입력 / 지팡이 효과 투명도 19 / 사본 29 | 저장 | 30 / 20 / 30으로 고정 | `fx 18` |
| TV-171 | 기본 배치 | 바인딩 | W S A D J K Shift Space | `DEC 1.19` |
| TV-172 | 대체 배치 | 바인딩 | ↑ ↓ ← → Z X C V | `gdd 3` |
| TV-173 | 키 받는 중 | 5초(300 t) 입력 없음 | 저절로 취소 | `ui 4.19` |
| TV-174 | 키 받는 중 | Esc | 취소, 바인딩 불변 | `ui 4.19` |
| TV-175 | 내 배치, 비기 = J 지정(발사 = J) | 저장 | 두 줄에 "발사와 같은 키"/"비기와 같은 키", 저장 막힘 | `ui 4.19` |
| TV-176 | 광장 상호작용 = J, 발사 = J | 저장 | 겹침 아님(문맥이 다름) [임시] | `on 5.1` |
| TV-177 | 한글 입력기 켬 | 전투에서 J 자리 물리 키 | 발사로 읽음 | `gdd 3` |
| TV-178 | t = 0 번쩍(35%) | t = 300ms 번쩍 요청 | 테두리 강조(0.4초 미만) | `fx 0.3` |
| TV-179 | t = 0, 500ms 번쩍 수락 | t = 900ms 요청 | 테두리 강조(1초 안 2회) | `fx 0.3` |
| TV-180 | TV-179 다음 | t = 1001ms 요청 | 수락(창 안에는 500ms 하나, 간격 501ms) | `fx 0.3` |
| TV-181 | 요청 불투명도 0.5 | Full / Reduced / Minimal | 0.35 / 0.15 / 테두리 강조 | `fx 0.3`, `fx 0.4` |
| TV-182 | Full, `flash_scale` 50, 요청 0.5 | Submit | 0.175 [임시: 곱] | `fx 0.4` |
| TV-183 | 빨강 전체 화면 번쩍 요청 | Submit | 테두리 강조(금지) | `fx 0.3` |
| TV-184 | 면적 20% 국소 번쩍 / 25% | Submit | 관리기 횟수에 안 셈 / 전체 화면 번쩍으로 셈 | `fx 0.3` |
| TV-185 | 켜짐 0.2초 요청 / 꺼짐 0.05초 요청 | Submit | 켜짐 0.05초로, 꺼짐 0.10초로 강제 | `fx 0.3` |
| TV-186 | Full, 흔들림 Small / Medium / Large / XLarge 단독 | `AmplitudePx` | 4 / 8 / 12 / 16 px | `fx 0.2` |
| TV-187 | Large + Large | `AmplitudePx` | 16px(누적값 1에서 멈춤) | `fx 0.2` |
| TV-188 | Reduced, Medium | `AmplitudePx` | 2.4px | `fx 0.4` |
| TV-189 | Minimal, XLarge | `AmplitudePx` | 0 | `fx 0.4` |
| TV-190 | Small 뒤 0.1초 | `AmplitudePx` | 누적 0.25 → 1px | `fx 0.2` |
| TV-191 | Full/Reduced/Minimal, `hitstop_enabled` 켬 | `HitstopScale` | 1 / 0.5 / 0. 끔이면 모두 0 | `fx 0.4` |
| TV-192 | 그림 정지 중 | 코어 틱 | 계속 진행(그림만 멈춤) | `fx 0.2` |
| TV-193 | 강도 3 의식(카메라 1.08) | Full / Reduced / Minimal | 1.08 / 1.04 / 1.00 | `fx 0.4` |
| TV-194 | 카메라 1.00 → 1.08을 1.8초 | 속도 | 0.0444배/초 ≤ 0.1 통과. [임시] 1.00 → 1.12를 1.0초에 요청하면 1.2초로 늘린다(속도 상한 적용) | `fx 0.3` |
| TV-195 | 입자 예산 | Full / Reduced / Minimal | 1,500 / 800 / 300 | `fx 0.4` |
| TV-196 | 흔들림 요청 0.8초 | 적용 | 0.6초 이하로 자른다 | `fx 0.3` |
| TV-197 | 오프라인 | 치장 효과 목록 | 얻지 못한 효과 칸·잠금 표시 없음 | `fx 0.5` |
| TV-198 | 유료 상품 0개 | 상점 | 유료 탭·잔액·구매 버튼 없음 | `gdd 13` |
| TV-199 | 새 치장 효과 크기 121% | `CosmeticValidator` | 거부(120% 초과) | `fx 0.5` |

### 18.7 알림·광장·안내

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-200 | 오프라인 | 서버 알림 | 표시 없음 | `fx 8` |
| TV-201 | `server_enhance_notice = Plus13Only` | +11 알림 | 버림. +13은 표시 | `fx 8` |
| TV-202 | 무한 웨이브 전투 중 | +10 알림 | 띄우지 않고 다음 야영의 한 줄 소식으로 | `fx 8` |
| TV-203 | 결투 중 | +13 알림 | 결투 결과 화면의 "미뤄 둔 소식"으로 | `fx 8`, `ui 4.17` |
| TV-204 | 메인 메뉴, 내 의식 재생 중 | +10 알림 | 의식 끝난 뒤 한 줄 소식 4초 | `fx 8` |
| TV-205 | 광장, +10 알림 두 개 연속 | — | 첫째 띠 펼침 후 6초 뒤 둘째 | `fx 8` |
| TV-206 | 밀린 +10~+12 알림 11개 / 10개 | 소비 | 한 줄 "방금 +10 이상 성공 11건" / 10개 차례로 | `fx 8` |
| TV-207 | +10 알림 대기 중 +13 도착 | — | +13이 줄 맨 앞 | `fx 8` |
| TV-208 | 대성공 +9 → +11 | 보내는 알림 | 1개(+11) | `fx 8` |
| TV-209 | `enhance_notice_show_name = 끔` | 내 +10 성공 | 알림에 닉네임 없음 "어느 제자의 …" | `on 1.4` |
| TV-210 | 차단한 사람 | 머리 위 | 닉네임 판 없음 | `ui 4.20` |
| TV-211 | 강화 화면을 연 사람 | 남의 화면 머리 위 | 말줄임표만, "강화 중" 없음 | `fx 1.1` |
| TV-212 | 주변 채팅 45자 | 말풍선 | 20 / 20 / 5자 세 줄, 4초 뒤 흐려짐 | `ui 4.20` |
| TV-213 | 귓속말 | 말풍선 | 없음 | `ui 4.20` |
| TV-214 | 주변 채팅 t = 0 보냄 | t = 100 t / 120 t | 막힘 / 보냄 | `on 5.3` |
| TV-215 | 같은 문장 2번 보냄 | 3번째 | 막힘 | `on 5.3` |
| TV-216 | 웨이브 10 미도달 계정 | 광장 채널 보내기 | 막힘(주변만) | `on 5.3` |
| TV-217 | 81자 | 보내기 | 막힘(최대 80자), 입력 줄 "80/80"에서 더 안 받음 | `on 5.3` |
| TV-218 | 광장 Esc | 메뉴 | 8줄: 강화, 서고, 옷장, 우편함, 순위표, 채널 옮기기, 설정, 메인 메뉴로 | `ui 4.20` |
| TV-219 | 광장 도전 받음 | 20초(1,200 t) 지남 | 쪽지 닫힘(거절 처리) | `on 4.6` |
| TV-220 | 순위 400줄 | `LeaderboardPages` | 34 ("1 / 34") | `ui 4.25` |
| TV-221 | 채널 23명 | 줄 | "23 / 40" | `ui 4.7` |
| TV-222 | 친구 37명 | 친구 창 | "37 / 100" | `ui 4.22` |
| TV-223 | 거래 양쪽 잠금 | 4.9초 / 5초 | [최종 확인] 꺼짐 / 켜짐 | `ui 4.23` |
| TV-224 | 양쪽 잠금 후 상대가 골드 수정 | — | 두 잠금 모두 풀림 | `ui 4.23` |
| TV-225 | 거래 창에 흔적 공개 끔인 지팡이 올림 | 상대 화면 | 흔적 + "역대 최고 +N, 지금 +M", 내 화면에 "거래 상대에게는 흔적이 보인다" | `fx 1.3`, `ui 4.23` |
| TV-226 | 장터 팔기에 +0 지팡이 | 고르기 | "놓을 수 없음" + "+0 지팡이는 장터에 올릴 수 없다. 직접 거래는 된다" | `ui 4.23` |
| TV-227 | 장터 등록 20건 | 등록 | 막힘, "20 / 20" | `on 1.6` |
| TV-228 | 웨이브 10 도달, 누적 1시간 59분 | `TradeOpen` | false, [시세]만 켜짐 + 진행 막대 둘 | `on 5.5` |
| TV-229 | 웨이브 10, 2시간, 갚을 것 0 | `TradeOpen` | true | `on 5.5` |
| TV-230 | 첫 만남 전 | 순위표 주간 도전 탭 | 꺼짐 + "첫 밤을 넘기면 열린다" | `on 3.3` |
| TV-231 | 묶인 중급 강화석 3, 일반 5 | 강화 화면 수량 | "중 8 (묶임 3)" [임시 표기] , 강화에서 묶인 것부터 소모 | `on 5.5` |
| TV-232 | 상점 새 지팡이, 골드 499 / 500 | 카드 | 꺼짐 / 켜짐. 보유 수와 무관 | `on 1.6` |
| TV-233 | 부적 이번 주 3개 구입 | 부적 카드 | "이번 주 3 / 3", 꺼짐 | `on 2.4` |
| TV-234 | 복수 중, 흠집 기록 없음 | 큰 꼴 | 3줄째 없음 | `ui 4.26` |
| TV-235 | 복수 중, 흠집 62%, 도전 3 | 큰 꼴 3줄 / 거울 | "허상에게 낸 가장 큰 흠집 62% · 도전 3번" / `Cracked` | `ui 4.26` |
| TV-236 | 첫 만남 쓰러짐 연출 4.00초 | — | 쪽지 "복수: 웨이브 20에서 다시 만난다" 내려옴, 4초 뒤 올라감 | `ui 4.26`, `fx 15.1` |
| TV-237 | 넘긴 최고 웨이브 14 | 한 줄 꼴 | "복수 · 최고 웨이브 14 / 19" | `ui 4.26` |
| TV-238 | 이름 칸 한글 8자 / 9자, 16px, 칸 128px | `Ellipsize` | 그대로 / 끝 "…" | `ui 0`, `ui 1.4` |
| TV-239 | "48,200" 16px | `TextWidthPx` | 48 | `ui 1.4` |
| TV-240 | 48200 | `FormatThousands` | "48,200" | `ui 1.4` |
| TV-241 | 마우스로 칸 위 / 키보드 입력 후 | 표시 | Hover(괄호 없음) / Hover 해제 + 괄호 | `ui 1.3` |
| TV-242 | 꺼진 버튼에 키보드 커서 | 표시 | 괄호 회청 + 이유 한 줄 | `ui 1.3` |
| TV-243 | 대상 64×64 / 65×40 / 240×336 / 336×376 | `BracketSize` | Small / Medium / Large / Large | `ui 1.3` |

---

## 19. 충돌

| ID | 내용 | 문서 | 택한 쪽 | 근거 |
|---|---|---|---|---|
| C-01 | 광장 Esc 메뉴의 항목 | `on 5.1`: 강화, 서고, 옷장, 우편함, 설정(5) / `ui 4.20`: 강화, 서고, 옷장, 우편함, 순위표, 채널 옮기기, 설정, 메인 메뉴로(8) | `ui 4.20`의 8줄 | DECISIONS는 "광장 메뉴에서 강화 진입"만 정함(2.4). 8줄이 5줄을 포함하고 화면 문서가 화면 구성의 주인(`ui 0`) |
| C-02 | 연출 줄이기·흔들림·번쩍·그림 정지의 분류 | `fx 18` 표는 전투 보기 옵션과 한 표 / `ui 4.19`는 "화면" 분류 | "화면" 분류에 둔다 | `fx 0.4`가 "설정 > 화면에 3단계를 둔다"고 직접 적음 |
| C-03 | 흔들림 단위의 "길이"와 누적기 | `fx 0.2` 표: 소 0.15초 등 / 같은 절 규칙: 폭 = 누적² × 16, 초당 2.5 감소 → 단독 소 0.20초, 중 0.28초, 대 0.35초, 특대 0.40초 | 누적기 규칙을 쓴다. 표의 길이는 근사로 본다 | 누적기가 "여러 사건이 겹쳐도 16px" 상한을 보장하는 유일한 장치이고 0.3절 한도(0.6초 이하)를 늘 지킨다 [조율]. 20절 U-21 |
| C-04 | 야영의 "귀환 가방 칸수와 영웅 한도" 상시 표시 | `mon 4.6` 6번: 화면 한쪽에 항상 / `ui 4.11`: 배치 표에 없음 | 표시한다(`CampModel.bagPreview`) | 규칙 문서가 요구, 화면 문서는 누락. 자리는 오른쪽 단 [임시] |
| C-05 | 소리 분류 | `ui 4.19`: 전체, 배경음, 효과음, 지팡이 소리, 화면 소리, 서버 알림 종 / `fx 19.5`: 배경음, 내 소리, 지팡이 소리, 적 소리, 화면(UI), 광장의 남 소리 | 합집합: 전체 + 배경음 + 효과음(=내 소리) + 지팡이 + 적 + 화면 + 광장의 남 소리 + 서버 알림 종 | 어느 쪽도 지우지 않는다(DECISIONS 1.16 범위 유지). "효과음"을 `fx`의 "내 소리"로 읽는다 [조율] |
| C-06 | 마일스톤 단계 | `fx 3.7`: +4, +7, +10 / `fx 22` 4번: +4, +7, +10, +13 | +4, +7, +10은 3.7 연출, +13은 3.8 연출 | 같은 뜻(+13은 별도 연출)으로 읽힌다 |
| C-07 | 같은 속성 +5가 제물 상한(+40) 안인가 | `gdd 9`: "등급 점수 … 같은 속성이면 +5 … 제물 합계 상한 +40" / `on 1.3`: "점수(일반 4, 희귀 8, 영웅 14, 같은 속성 +5)와 상한(+40)" / `fx 1.2`: 내역을 "제물 합(상한 +40)"과 "같은 속성 +5" 다른 줄, 예시 25+22+5+30=82 | 상한 밖(별도 줄) | 화면 내역이 두 줄을 따로 두고 예시가 맞는다. 규칙 라이브러리 명세와 맞춰야 한다 [조율] |
| C-08 | 빠른 강화 설정이 두 곳 | `ui 4.12`: 강화 화면 [빠른 강화] 토글 / `ui 4.19`, `fx 18`: 설정 "강화 의식(전체 / 빠른 강화)" | 같은 값 하나(`enhance_ritual`) | 두 값이 따로면 어느 쪽이 이기는지 정할 수 없다 [조율] |
| C-09 | "+10 이상도 빠른 강화" 설정 | `fx 7`: "설정에서 풀 수 있다" / `ui 4.19` 옵션 목록에 없음 | `enhance_fast_allow_high`를 "대화와 공개"에 추가(기본 끔) | 규칙 문서가 요구 |
| C-10 | 기본 키의 문맥 겹침 | 전투 J 발사·K 비기 / 광장 J 상호작용·K 감정표현 (`on 5.1`). `ui 4.19` "겹친 채로는 저장되지 않는다" | 겹침 검사는 문맥별 | 기본 배치부터 겹치므로 전역 검사는 성립하지 않는다 [조율] |
| C-11 | 오프라인의 봇 상대 연습 | `ui 4.21`·`on 8.1`: 오프라인에서도 된다 / `ui 4.4`·`gdd 13`: 오프라인은 결투 카드를 묶음 상자에 | 결투 카드는 묶음 상자에 두고, 봇 연습은 메인 메뉴 "연습" 화면 안에 진입점을 둔다 [임시] | 묶음 규칙(흐리게 두지 않고 묶음)을 지키며 오프라인 기능을 막지 않는다. 20절 U-43 |
| C-12 | 야영에서 하는 일의 목록 | `DEC 2.3`: "조립, 회복, 비기 선택만" / `gdd 2.2`·`mon 4.6`·`ui 4.11`: 허수아비 연습, 전리품 다시 뽑기도 | 다섯 다 둔다. DECISIONS 문장의 목적("강화는 없다")은 그대로 지킨다 | 연습은 상태를 바꾸지 않고, 다시 뽑기는 전리품 단계의 일부다. 개발자 확인 필요로 남긴다(U-44) [조율] |
| C-13 | 로그인 오류 줄의 대기 시간 표시와 "계정이 있는지 알 수 없게" | `ui 4.3`: 대기 중이면 남은 시간 / `on 7.3`: 계정당 대기, 오류 문구 하나 | 서버가 **입력된 아이디 문자열 기준**으로 실패를 세어 없는 아이디에도 같은 대기를 보여 준다 [조율] | 있는 아이디에만 대기가 뜨면 계정 존재가 드러난다 |
| C-14 | 귀환 가방 식 | 옛 식 "판마다 기본 2칸 + 10웨이브당 1칸, 최대 6칸" / `mon 6.2.4`: 웨이브 ÷ 3, 최대 12 | `mon 6.2.4` | `on 1.2`·`gdd 2.2`가 `mon 6.2.4`를 유일 기준으로 지정 |
| C-15 | 오프라인 서버 전용 메뉴 | 이전 판 "흐리게" / `gdd 13`·`on 8.1`·`ui 4.4`: 흐리게 두지 않고 묶음 | 묶음 | `ui 8`이 맞춰졌다고 기록 |
| C-16 | 복수 추적기 HUD 줄 | `gdd 13`: 있다 / `ui 8` 개발자 확인 6번: 뺄 수 있다 | 있다 | 확인 전까지 본문 값을 쓴다 |
| C-17 | 연출 줄이기 "줄임"의 강화 의식 "어두워짐 40% → 20%" | `fx 0.4` / `fx 3.2`의 "밝기 40%까지" 표기와 같은 숫자 | 해석은 20절 U-18 [임시] | 문자 그대로 "밝기 20%까지"는 더 어두워져 줄임의 뜻과 반대 |

DECISIONS와 직접 부딪치는 원본 값은 찾지 못했다. 위 항목은 원본끼리의 차이다.

---

## 20. 미정 (임시 기본값)

원본의 "개발자 확인 필요"와 원문이 정하지 않은 것. 구현을 막지 않도록 [임시] 값을 넣었다. 확인되면 값만 바꾼다.

### 20.1 원본의 개발자 확인 필요 (지금 본문 값을 그대로 쓴다)

| ID | 출처 | 내용 | 지금 값 |
|---|---|---|---|
| U-01 | `ui 8` 1 | 선택 괄호 노랑 `#FFE94A`가 금색 `#FFD24A`과 가깝다. 분필 흰색 괄호로 바꿀 수 있다 | 노랑 + 모양 구분. 로직은 색과 무관(`BracketStyle` 하나로 바꿀 수 있게) |
| U-02 | `ui 8` 2 | 메뉴의 경고·조심 버튼에 빨강을 허용할지 | 호박색. `CautionStyle` 하나로 둔다 |
| U-03 | `ui 8` 3 | 한글 도트 글꼴 후보 | 글꼴은 데이터(`FontSet`)로 교체 가능하게 |
| U-04 | `ui 8` 4 | 결투 체력을 바 대신 칸(칸 하나 = 100)으로 | 바. `DuelHudModel`은 수치만 주므로 그리는 쪽만 바뀐다 |
| U-05 | `ui 8` 5 | 별빛(유료 재화) 아이콘을 지금 그릴지 | 유료 상품 0개면 화면에 전혀 안 나온다(로직 같음) |
| U-06 | `ui 8` 6 | 복수 추적기를 어디까지 보일지(HUD 줄 뺄 수 있음) | 6곳 모두. `RevengeSurfaces` 플래그 집합으로 둔다 |
| U-07 | `ui 8` 7 | 지팡이 목록을 격자로 바꿀지 | 세로 스크롤. `WandListModel`은 배치와 무관 |
| U-08 | `fx 22` 1 | 대성공이 항상 가짜 실패로 시작하는가 | 항상(`fakeFailRatio = 1.0`) |
| U-09 | `fx 22` 2 | 흔적 공개 기본값 | 끔 |
| U-10 | `fx 22` 4 | 마일스톤 단계 | +4, +7, +10(+13 별도) |
| U-11 | `fx 22` 5 | +10 이상의 빠른 강화 | 전체 의식 기본, `enhance_fast_allow_high` 끔 |
| U-12 | `fx 22` 7 | 첫 만남 마무리·쓰러짐(7초) 건너뛰기 | 건너뛸 수 없음 |

### 20.2 원문이 정하지 않은 것

| ID | 내용 | [임시] 값 |
|---|---|---|
| U-13 | 첫 실행 물음을 고르지 않고 끔 / 커서 기본 | 저장 안 함 → 다음 실행에 다시 / 커서 `Full` |
| U-14 | 메인 메뉴 배경의 기기 시각 구간 | 06:00~16:59 낮, 17:00~19:59 해질녘, 20:00~05:59 밤 |
| U-15 | 꺼진 것을 눌렀을 때 | 아무 일 없음 + 이유 한 줄 다시(툴팁) + `ui.common.disabled_press` |
| U-16 | 선택 괄호 크기의 "한 변" | 긴 변 |
| U-17 | 원정에 들고 갈 수 없는 잠금 | `OnExpedition`, `InDuel`, `MarketListed` 불가. `TradeLocked`, `PendingVerify` 가능 |
| U-18 | 줄임의 강화 의식 "어두워짐 40% → 20%" | 어두워짐 깊이 절반: 밝기 40%까지 → 70%까지(강도 1단의 하락 없음과 같은 값) |
| U-19 | 최소의 강화 의식 "결과 카드만" | 의식 연출 없이 결과 카드. 변이 카드는 그대로. +13 첫 도달 2초 잠금 없음. 서버 결과 대기 중에는 카드 자리에 대기 표시 |
| U-20 | 번쩍 낱개 조절과 3단계의 조합 / "1초 구간" | 불투명도 = min(요청, 단계 상한) × 번쩍/100. 시작 시각 기준 미끄러지는 1초 창. 테두리 강조는 세지 않는다 |
| U-21 | 흔들림 사건당 더하는 값 | 단독일 때 표의 폭이 나오도록 √(폭/16): 0.5, 0.7071, 0.8660, 1.0. 흔들림 길이 상한 0.6초는 누적기 감소가 늘 지킨다(최대 0.4초) |
| U-22 | 음량 범위·기본값 | 모두 0~100, 기본 100 |
| U-23 | 창 모드·해상도 기본 | 전체 화면, 기기 화면 해상도(1920×1080 미만이면 1920×1080 창) |
| U-24 | Esc 바인딩 / 대체 배치의 광장 키 / 겹침 범위 | 일시정지·광장 메뉴의 Esc는 고정 보조 키(재설정 대상은 주 키만). 대체 광장 키 Z / X / Enter / Tab / Esc. 겹침은 문맥별 |
| U-25 | 설정 [기본값으로]의 범위 | 지금 분류만, 확인 창 한 번 |
| U-26 | 오프라인에서 서버 전용 설정 줄 | 숨김(채팅·조립 공개·차단·강화 알림 이름·서버 강화 알림 보기) |
| U-27 | `loadout_public` 기본 | 켬 |
| U-28 | 친구·거래 신청의 수락 시간 | 20초(광장 도전과 같다) |
| U-29 | "크게 손해 보는 거래" 판정 | 내가 주는 쪽 감정가 합 ≥ 받는 쪽 감정가 합 × 3 이고 차이 ≥ 10,000골드 |
| U-30 | 웨이브 10 전 계정의 채널 제한 범위 | 공개 채널(광장·거래)만 막는다. 귓속말·친구·결투방은 연다 |
| U-31 | 채널 목록 순서·가득 참 | 친구 있는 채널(친구 수 desc → 번호 asc) → 나머지(번호 asc). 40/40은 꺼짐 |
| U-32 | 서고 남은 자리 < 가방에 고른 수 | 남은 자리까지만 담기고 나머지는 골드 |
| U-33 | 강화 안내가 뜨는 때 | 그 정산 뒤 처음 강화 화면을 열 때(정산의 [강화 화면으로] 포함) |
| U-34 | 정산 [강화 화면으로]의 돌아올 곳 | 메인 메뉴 |
| U-35 | 일시정지 "메인 메뉴로" | 판 포기와 같다(확인 창 → 정산 → 메인 메뉴) |
| U-36 | 광장 메뉴 "메인 메뉴로" | 확인 창 없이 광장을 나간다 |
| U-37 | 말풍선 줄 나누기 | 글자 수 20자 단위 단순 자르기 |
| U-38 | 이메일 가린 꼴, 꼴 검사식 | `a***@d***.net`(로컬 첫 글자, 도메인 첫 글자, 마지막 점 뒤). `^[^@\s]+@[^@\s]+\.[^@\s]+$` |
| U-39 | 비밀번호의 "아이디 포함" | 대소문자 무시 |
| U-40 | 로그인 대기 "5회 틀리면 1초부터" | 5번째 실패 직후 1초, 이후 실패마다 2배, 900초 상한, 성공 시 초기화 |
| U-41 | 변이를 거절했을 때 갈 결과 | 결과 꾸러미에 `fallbackIfRejected`로 함께 담는다(시도 시점에 이미 정해짐) |
| U-42 | 매치 성사 때 변이 카드가 떠 있으면 | 결과는 이미 저장. 그 지팡이는 고를 때까지 "변이 선택 대기"로 강화·거래 불가. 결투 뒤 강화 화면을 열면 그 지팡이의 변이 카드부터 |
| U-43 | 오프라인 봇 상대 연습 진입 | 메인 메뉴 "연습" 화면에 [봇 상대 결투 연습] |
| U-44 | 야영 활동 목록(C-12) | 조립, 회복, 비기 선택, 허수아비 연습, 다시 뽑기 |
| U-45 | "보유 재화로 평균 N번"의 정의 | 지금 상태에서 제물·부적 없이 계속 도전할 때 골드나 그 구간 강화석이 처음 모자라게 되기까지의 기대 시도 수. `Wizard.Rules`가 고정 씨앗 2,000회 몬테카를로로 계산하고 (지팡이 상태, 보유량)으로 캐시 |
| U-46 | 이어서 강화 "(선택) 단계 하락" 기본 / 실패 수 / 지팡이 바꾸기 | 하락에서 멈춤 켬. 실패 수에 변이·막음·하락 없음 포함. Q/E로 바꾸면 끝 |
| U-47 | 대성공이 마일스톤을 건너뛸 때(+6 → +8) | 도착 단계의 칭호 구간(+0~3, 4~6, 7~9, 10~12, 13)이 시도 전과 다르면 마일스톤 |
| U-48 | 실패의 침묵 길이(2~4단) | 0.3 / 0.4 / 0.5초 (1단 0.2, 5단 0.6 사이 선형) |
| U-49 | 강화할 수 없는 지팡이 잠금 | `OnExpedition`(on 1.2 명시), `InDuel`, `MarketListed` 불가. `PendingVerify`, `TradeLocked` 가능(on 3.2: 대기 보상으로 강화 가능) |
| U-50 | 지팡이 정렬 동률·거르기 조합 | 단계 desc → IL desc → 얻은 시각 desc → id asc. 거르기는 AND |
| U-51 | 떠나보내기 이름 입력 비교 | 앞뒤 공백 제거 후 이름 표 문자열과 정확히 같음 |
| U-52 | 회복 1.5배의 끝수와 누적 | 내림, 두 번째부터 매번 기본값의 1.5배(곱해 쌓지 않음) |
| U-53 | 튜토리얼 5단계 완료 조건 | ① 1칸 이상 이동 + 발사 1회 ② Shift를 누른 채 1칸 이동 ③ 임계 사격 1회 ④ 반동 사격 1회 ⑤ 블록 1개 끼움 |
| U-54 | 충전 게이지 곡선 / 제물 간격 끝수 | 두 구간 선형 / 화면 틱 반올림 |
| U-55 | 스코프 배율 끝수 | 소수 둘째 자리 반올림(0.7225 → 0.72, 0.8464 → 0.85) |
| U-56 | 복수 추적기 "n / 19"의 n이 19를 넘을 때 | 19에서 멈춘다(넘겼다면 이미 웨이브 20을 치른 것) |
| U-57 | 묶인 재료 표기 | "중 8 (묶임 3)": 합계 뒤에 묶인 수 |
| U-58 | 화면 타이머 | 고정 60Hz `UiTick` |

---

## 21. 시각/소리 연결점

구현하지 않는다. 그림·이펙트·소리가 붙을 **이벤트 이름**만 정한다. 화면 로직은 이 이벤트를 `IPresentationBus.Emit(name, payload)`로 내고, 받는 쪽이 없어도 동작한다. 소리 번호(E-xx)는 `fx 19.1`의 사건 번호다. 모든 소리 이벤트에는 같은 뜻의 그림 이벤트가 짝으로 있다(`fx 0.4`).

### 21.1 공통

| 이벤트 | 때 | 데이터 |
|---|---|---|
| `ui.common.hover_enter` / `hover_exit` | 올림 | 대상 id |
| `ui.common.cursor_move` | 키보드 커서 이동 | 대상 id, `BracketSize` |
| `ui.common.cursor_bracket_pulse` | 0.5초 주기 | — |
| `ui.common.press` / `release` | 누름 | 대상 id |
| `ui.common.toggle_on` / `toggle_off` | 켜짐 | 대상 id |
| `ui.common.confirm_check` | 확정 | 대상 id |
| `ui.common.disabled_press` | 꺼진 것 누름 | 이유 키 |
| `ui.common.screen_open` / `screen_close` | 화면 열고 닫음 | `ScreenId` |
| `ui.common.tooltip_show` | 툴팁 | 대상 |
| `ui.common.input_error` | 입력 칸 오류 상태 | 칸 id |
| `ui.loading.progress` | 불러오기 | 0~1 |
| `fx.flash.full` / `fx.flash.border_highlight` | 번쩍 관리기 결과 | 색, 불투명도 |
| `fx.shake.add` | 흔들림 사건 | `ShakeSize` |
| `fx.hitstop` | 그림 정지 | 대상, 길이 |

### 21.2 시작·메뉴

| 이벤트 | 때 |
|---|---|
| `ui.first_run.choice` | 선택지 확정(값) |
| `ui.title.start_prompt_pulse` | 2초 주기 밝기 |
| `ui.title.start` | J로 시작 |
| `ui.auth.login_fail` / `login_ok` / `lockout_tick` | 로그인 |
| `ui.auth.code_sent` / `code_wrong` / `code_ok` / `resend_ready` | 이메일 확인 |
| `ui.auth.reset_done` | 비밀번호 다시 정함 |
| `ui.main_menu.mode_card_focus` | 모드 카드 커서(원정/결투/광장) |
| `ui.main_menu.mail_dot` | 새 우편 점 켜짐 |
| `ui.main_menu.bg_time_change` | 낮·해질녘·밤 바뀜 |
| `ui.offline.group_box_open` | 묶음 상자 고름 |
| `ui.prep.character_confirm` | 캐릭터 확정 |
| `ui.prep.wand_select` / `held_wand_set` | 지팡이 고름 / 든 지팡이로 |
| `ui.prep.depart_blocked` | 출발 막힘(이유) |
| `ui.prep.depart` | 출발 |

### 21.3 전투 HUD

| 이벤트 | 때 |
|---|---|
| `hud.wave_progress` | 남은 체력 비율 변화 |
| `hud.hp_change` / `hud.hp_last_cell` | 체력 변화 / 체력 1칸(귀퉁이 위험색 허용 자리) |
| `hud.portrait_hit` | 초상 0.2초 밝아짐 |
| `hud.day_wave_change` | "N일째 웨이브 M" 바뀜 |
| `hud.boss_show` / `boss_rage_stack` / `boss_final_rage` / `boss_phase_tick` | 보스 표시 |
| `hud.revenge_tag` | 웨이브 20 허상 "복수" 꼬리표 |
| `hud.revenge_waves_left` | 허상까지 n웨이브 |
| `hud.notice` | 한 줄 안내(문구 키) |
| `hud.overheat_lock` | 과열 잠금 딱지 |
| `hud.circuit_step` | 회로 칸 켜짐(칸 번호) |
| `hud.recent_input` | 최근 입력 추가 |
| `hud.damage_number` | 피해 숫자(`DamageNumberKind`) |
| `hud.tutorial_step` | 튜토리얼 풍선 단계 |
| `hud.phantom_line` | 첫 만남 대사 판 |
| `hud.duel.countdown` | 3, 2, 1 |
| `hud.duel.round_dot` | 라운드 점 |
| `hud.duel.thorns_start` | 가시밭 경고 |
| `hud.duel.margin_level` | 마감 여유 단계 |
| `hud.duel.input_dropped` | 입력 버려짐(1.5초) |
| `hud.duel.phrase_wheel` | 문구 바퀴 |
| `ui.duel.intro` / `round_break` / `result` | 결투 시작 화면 / 라운드 사이 / 결과(승패) |

### 21.4 전리품·야영·정산

| 이벤트 | 때 |
|---|---|
| `ui.loot.card_flip` | 카드 한 장 뒤집힘(0.1초 간격) |
| `ui.loot.pick` / `unpick` / `reroll` / `discard` | 전리품 |
| `ui.camp.open` | 야영 화면 열림 |
| `ui.camp.slot_insert` / `slot_remove` / `slot_swap` | 끼우기·빼기·바꾸기 |
| `ui.camp.tree_rebuild` | 트리 다시 그림 |
| `ui.camp.stat_highlight` | 바뀐 수치 0.5초 |
| `ui.camp.heal` / `ui.camp.free_heal` | 회복 |
| `ui.camp.depart_hold` / `depart` | 출발 표식 J 1초 |
| `ui.trie.unlock_candidate` / `unlock` | 비기 해금 |
| `ui.dummy.just_star` / `late_fail` | 저스트 별 / 실패 원인 |
| `ui.settlement.page` | 쪽 넘김(1~3) |
| `ui.settlement.new_best` | 최고 기록 딱지 |
| `ui.settlement.bag_open` / `bag_put` / `bag_full` | 귀환 가방 |
| `ui.settlement.bundle_open` | 재료 꾸러미 여는 연출 |
| `ui.settlement.revenge_reward` | 복수 보상 줄 |
| `ui.revenge.note_drop` / `note_lift` | 처음 나타나는 쪽지 |
| `ui.revenge.complete` | 복수 완료 꼴 |

### 21.5 강화 화면 (`fx 19.1`)

| 이벤트 | 때 | 소리 |
|---|---|---|
| `ui.enhance.open` / `close` | 화면 열림·닫힘 | E-01 |
| `ui.enhance.sacrifice_put` / `sacrifice_take` | 제물 넣기·빼기 | E-02 |
| `ui.enhance.prob_roll` | 확률 숫자 0.2초 굴림 | E-03 |
| `ui.enhance.charm_set` | 부적 끼우기 | E-04 |
| `ui.enhance.cost_shake` | 비용 모자람 | — |
| `ui.enhance.wand_switch` | Q/E | — |
| `ui.enhance.trace_toggle` | 흔적 공개 바꿈 | E-33 |
| `ui.enhance.press` | 강화 누름 | E-05 |
| `ui.enhance.pay` | 값 치르기 | E-06 |
| `ui.enhance.offer` | 제물 하나 바침(순번 1~5) | E-07 |
| `ui.enhance.charge_begin` / `charge_progress` | 충전(강도, 0~1) | E-08, E-09 |
| `ui.enhance.hold` | 숨 멈춤 | E-10 |
| `ui.enhance.late_wait` / `late_notice` | 결과 늦음 / 5초 "까마귀가 늦는다" | E-32 |
| `ui.enhance.fail_start` | 실패의 시작(= 대성공의 시작) | E-13 |
| `ui.enhance.success` | 성공 | E-11, E-12 |
| `ui.enhance.great_crack_spread` / `great_shatter` / `slot_extend` | 대성공 | E-18, E-19, E-20, E-12 |
| `ui.enhance.fail_light_out` / `wand_drop` / `stage_down` | 실패(하락) | E-14, E-15, E-16 |
| `ui.enhance.charm_catch` / `charm_burn` | 부적이 막음 | E-21, E-22 |
| `ui.enhance.no_drop` | 하락 없음 | E-23 |
| `ui.enhance.aura_ember` | 기운 불씨 +1(누적 1~3) | E-17 |
| `ui.enhance.mutation_vine` / `mutation_card` / `mutation_accept` / `mutation_reject` | 변이 | E-24, E-25, E-26 |
| `ui.enhance.milestone` / `title_band` | 마일스톤 | E-27, E-28 |
| `ui.enhance.plus13_silence` / `plus13_slam` / `plus13_theme` / `master_wakes` | +13 | E-29, E-30 |
| `ui.enhance.trace_shed` | 흔적 조각 떨어짐 | — |
| `ui.enhance.short_result` | 짧은 형태(빠른 강화·건너뛰기) | E-31 |
| `ui.enhance.result_card` | 결과 카드 | E-36(캐릭터 한마디) |
| `ui.enhance.portrait_react` | 초상 반응(서기 / 강화 반응 성공 / 실패) | — |
| `ui.enhance.engrave_try` / `engrave_ok` / `engrave_fail` | 슬롯 새기기 | — |
| `ui.enhance.mutation_remove` | 변이 제거 | — |
| `ui.enhance.release` | 지팡이 떠나보내기 | — |

### 21.6 알림·광장

| 이벤트 | 때 | 소리 |
|---|---|---|
| `ui.notice.plaza_band` | 광장 알림 띠(까마귀 1.2초, 0.6초에 펼침, 느루 한쪽 눈) | E-34 / E-35 |
| `ui.notice.plaza_sky_band` / `bonfire_tint` | +13의 하늘 빛띠·모닥불 6초 | E-35 |
| `ui.notice.menu_line` | 메뉴 한 줄 소식(0.2초에 내려옴) | E-34(−6dB) / E-35(−6dB) |
| `ui.notice.system_chat` | 대화창 시스템 줄 | — |
| `ui.plaza.ellipsis_on` / `ellipsis_off` | 머리 위 말줄임표 | — |
| `ui.plaza.bubble_show` / `bubble_fade` | 말풍선(4초 뒤) | — |
| `ui.plaza.interact_hint` | "J 살펴보기" 등 | — |
| `ui.plaza.emote` | 감정표현(8종 id) | — |
| `ui.plaza.request_in` / `request_timeout` | 받은 신청 쪽지 | — |
| `ui.chat.send` / `chat.blocked` / `chat.cooldown` | 채팅 | — |
| `ui.duel_board.queue_join` / `queue_tick` / `match_found` | 자동 매치(모래시계) | — |
| `ui.trade.slot_changed` | 바뀐 칸 3초(초당 2회 이하) | — |
| `ui.trade.lock` / `unlock_all` / `final_ready` / `loss_warning` | 직접 거래 | — |
| `ui.market.buy` / `list` / `delist` | 장터 | — |
| `ui.shop.buy` | 상점 | — |
| `ui.mail.receive` / `claim` | 우편 | — |
| `ui.monument.engrave` | 시즌이 바뀐 뒤 처음 열 때 새김 | — |
| `ui.master_chair.idle_line` | 느루 잡담 | — |
| `ui.league.promote_confirm` | 위 리그로 올라가기(두 번 확인) | — |
