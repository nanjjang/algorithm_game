# Scripts/UI — 화면 기반 (F3)

화면 작업(G18~G25)은 이 문서만 보고 화면을 조립한다. 네임스페이스는 `Wizard.Solo.UI`.
기준: 08 요약서 1절(화면 관리자, 메뉴 키, 상태 규약, 글자, 색, 공용 창), 00 문서 8·8.1·8.2절.

## 0. 지킬 것

- **형 이름**: 이 폴더의 형은 `Ui` 접두사(`UiButton`, `UiLabel` …). 예외는 00 문서가 이름을 정한 `ScreenManager`, `ScreenBase`, `MenuInput`.
  `UnityEngine.UI`에 있는 이름(`Button`, `Image`, `Text`, `Slider`, `Toggle`, `Outline` …)의 형을 만들지 않는다.
  `UI.Text`처럼 네임스페이스 일부만 적지 않는다. uGUI 형이 필요하면 별칭을 쓴다:
  `using UImage = UnityEngine.UI.Image; using UText = UnityEngine.UI.Text;`
- **화면 글자**는 `ctx.Names`로만(`T(열쇠)`, `F(열쇠, 이름, 값 …)`, `N(숫자)`). 코드에 한국어 리터럴 금지. 새 열쇠는 `Resources/NamesSolo/<작업 id>.ko.json`.
- **색**은 `UiTheme`의 상수만. 위험색(빨강·자홍)은 메뉴·창·플레이어 쪽에 쓰지 않는다. 조심 버튼 = `UiButtonStyle.Caution`(호박색 테 + 경고 삼각).
- **화면 인자**는 `ScreenArgKeys`의 상수와 00 문서 8.1절 표의 형으로만(character는 int, wandId는 long).
- **입력**: `UnityEngine.Input.*`, 옛 입력 모듈, `#if UNITY_EDITOR`, TextMeshPro 금지. 메뉴 키는 관리자가 `OnMenuKey`/`OnBack`으로 준다 — 화면이 키보드를 직접 읽지 않는다(키 재설정만 예외, 6절).
- **그림이 없어도 죽지 않는다**: `ctx.Art`가 null이거나 그림이 없으면 UiKit이 단색으로 그린다.

## 1. 화면 하나 만들기 — `ScreenBase`

```csharp
using UnityEngine;
using Wizard.Solo.Art;      // UiFontId
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Meta
{
    public sealed class RecordsScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Records;
        // Layer 기본 Fullscreen(위 띠 80 + 내용 952 + 아래 띠 48 뼈대). Panel/Modal/HudOverlay는 뼈대 없이 Content = Root(1920×1080).
        protected override string TitleKey => "str.records.title";
        // protected override UiKeyHint[] KeyHints => new[] { new UiKeyHint(Key.J, "str.ui.hint.pick"), new UiKeyHint(Key.Escape, "str.common.exit") };

        UiLabel _best;

        protected override void BuildContent(RectTransform content)   // 처음 열릴 때 한 번. 좌표 = content 왼쪽 위 원점 px
        {
            UiKit.Panel(Ctx, content, UiPanelKind.Board, 48, 32, 1824, 880);
            _best = UiKit.Label(Ctx, content, "", UiFontId.F32, 96, 64, 800, 40);
            UiKit.Button(Ctx, content, T("str.common.back"), 96, 800, 320, 64, () => Close(), UiButtonStyle.Large, Focus);
        }

        public override void OnEnter(ScreenArgs args)                  // 열릴 때마다(다시 열려도). base 먼저
        {
            base.OnEnter(args);
            _best.SetText(F("str.format.wave", "n", 14));
        }
        // OnBack(): Esc. 기본 false → 관리자가 Pop(돌아갈 곳 = ReturnTo 또는 바로 아래)
        // OnMenuKey(key): 기본 = Tabs(Q/E) → Focus(방향·J). K(Cancel)는 화면이 직접 처리
        // Tick(dt): 매 프레임(맨 위 + 그 아래 BlocksInput이 아닌 화면 + 토스트)
    }
}
```

등록은 자기 폴더의 `ScreenRegistry.<묶음>.cs`(00 문서 2.1절):

```csharp
namespace Wizard.Solo.UI
{
    public static partial class ScreenRegistry
    {
        static partial void RegisterMeta(IScreenManager m, GameContext ctx) { m.Register(ScreenId.Records, () => new Screens.Meta.RecordsScreen()); }
    }
}
```

`ScreenBase`가 주는 것: `Ctx`, `Root`, `Content`, `TopBar`, `BottomBar`, `TitleLabel`, `HintLabel`, `Focus`(UiFocusGroup), `Tabs`(넣으면 Q/E를 먼저 받음),
`Args`, `Manager`(ScreenManager, 테스트에선 null), `Screens`(IScreenManager), `T/F/N`, `SetTitle(글)`, `SetHints(UiKeyHint[])`, `PlayUi(사건)`, `Close(result)`.
`ScreenBase.TopBarHeight = 80`, `BottomBarHeight = 48`, `ContentHeight = 952`.

## 2. 화면 관리자 — `IScreenManager` (`ctx.Screens`)

| 호출 | 하는 일 |
|---|---|
| `Push(id, args)` | 맨 위에 쌓는다. args가 null이면 `Entry = 지금 Top` |
| `Replace(id, args)` | 맨 위를 바꾼다 |
| `Pop(result)` | 맨 위를 닫고 들어온 곳으로. `result`는 닫힌 화면 `args.OnResult`로(닫기가 끝난 뒤) |
| `PopTo(id, args)` | id가 맨 위가 될 때까지 닫는다. 없으면 전부 닫고 id를 연다 |
| `Top` / `IsOpen(id)` | Toast·Tooltip 층은 세지 않는다(전투 입력의 문) |
| `Confirm(titleKey, bodyKey, caution, ok => …)` | 예/아니오 창. caution이면 [예]가 조심 버튼, 처음 커서는 [아니오] |
| `ConfirmText(titleKey, 본문 글, caution, ok => …)` | Confirm과 같고 본문이 이미 틀을 채운 글(이름 표를 거친 것) |
| `Toast(글, 초)` | 오른쪽 위 480×96 한 줄 알림(이름 표를 거친 글). 어느 화면 위에든 뜬다 |
| `Close(screen, result)` | 그 화면을 닫는다(맨 위가 아니어도). 화면 안에서는 `Close(result)` |
| `ShowTooltip(글, rect)` / `HideTooltip(rect)` | 툴팁(최대 폭 400). 보통은 `UiKit.AttachTooltip`이 대신 부른다 |
| `CaptureKeys` | true인 동안 메뉴 키(Esc 포함)를 화면에 보내지 않는다(키 재설정) |

위는 전부 `IScreenManager`(공용 계약)에 있다 — `ctx.Screens`를 구체형으로 바꿔 쓰지 않는다.
`ScreenManager`(`Manager`)에만 있는 것은 기반 안쪽 것뿐이다: `InputMode`(UiInputModeTracker), `Menu`(MenuInput), `Tuning`(UiTuning — 6절),
`Dispatch(MenuKey)`(봇·테스트), `IsRegistered(id)`, `Stack`(UiScreenStack).

**돌아갈 곳**: 진입점이 여럿인 화면은 같은 화면 하나이고 `args.Entry`로 구분한다. Esc·Pop 때 바로 아래가 아닌 곳으로 가야 하면
`args.With(ScreenArgKeys.ReturnTo, ScreenId.MainMenu)` — 스택에 있으면 그 위를 다 닫고, 없으면 전부 닫고 연다(정산 → 강화 → Esc = 메인 메뉴).
맨 아래 화면은 Esc로 닫히지 않는다.

**불러오기**: `Screens.Replace(ScreenId.Loading, ScreenArgs.ViaLoading(ScreenId.ExpeditionPrep, ScreenId.Battle, battleArgs))` —
Loading이 한 프레임 그린 뒤 자신을 `Battle(battleArgs)`로 Replace 한다(NextScreen이 없으면 Pop).

**입력 막기**: `BlocksInput`이 참인 화면(Modal은 언제나, ScreenBase 기본은 Fullscreen·Modal) 아래로는 메뉴 키와 Tick이 내려가지 않는다.
보이는 화면은 맨 위에서 첫 Fullscreen까지(그 아래는 꺼진다). Modal은 뒤를 먹색 60%로 덮고 마우스도 막는다.

## 3. 조립 함수 — `UiKit` (좌표 = 부모 왼쪽 위 원점 px, y 아래 +)

```csharp
RectTransform UiKit.Node(parent, name, x, y, w, h)
RectTransform UiKit.Place(rt, x, y, w, h)              // 왼쪽 위 원점으로 다시 놓기
RectTransform UiKit.Stretch(rt, l, t, r, b)            // 부모 꽉 채우기
RectTransform UiKit.Center(rt, w, h, dx, dy)           // 부모 가운데(창)
Image   UiKit.Rect(parent, x, y, w, h, color, raycast=false)
Image   UiKit.Panel(ctx, parent, UiPanelKind.Board|Recess|Paper|Heading|ScreenHeading|ControlStrip|Tooltip|Toast|Solid, x, y, w, h, color?)
Image   UiKit.NineSlice(ctx, parent, "K-60", "item_slot", "filled", x, y, w, h, fallbackColor)   // 09 6.1절 (id, variant, state)
Image   UiKit.Image(parent, sprite, x, y, w, h, color?, preserveAspect=true)
Image   UiKit.Icon(ctx, parent, "block.fireball", 48, x, y)                                    // ctx.Art.Icon, 없으면 대체 그림
UiLabel UiKit.Label(ctx, parent, 글, UiFontId.F16, x, y, w, h=0(줄 높이), color?, align, wrap=false, outline=false)
UiLabel UiKit.LabelKey(ctx, parent, "str.x", UiFontId.F24, x, y, w, h, …)                     // 이름 표 열쇠로
UiButton UiKit.Button(ctx, parent, 글, x, y, w, h, onClick, UiButtonStyle.Large|Medium|Small|Primary|Caution|Tab|TabVertical|Plain,
                      group=Focus, pressSound=null(ui.common.press)|""(없음)|"ui.camp.depart", fontSize=F24, icon=null)
UiButton UiKit.IconButton(ctx, parent, sprite, x, y, w, h, onClick, style=Small, group, pressSound)
UiButton UiKit.KitIconButton(ctx, parent, "K-35", "close", x, y, 32, 32, onClick, group)       // 상태 파일 자동
UiToggle UiKit.Toggle(ctx, parent, 글, x, y, w, h, value, v => …, group)                       // J·좌우, ui.common.toggle_on/off
UiSlider UiKit.Slider(ctx, parent, x, y, w, h, value, v => …, min=0, max=100, step=1, group, steps=null)   // ◀ 막대 값 ▶
UiProgressBar UiKit.ProgressBar(ctx, parent, x, y, w, h, value01)                               // .Value01
UiTabs  UiKit.Tabs(ctx, parent, 글목록, x, y, tabW, tabH, i => …, selected=0, vertical=false, gap=4, group=null)  // 화면에서 Tabs = 그것
UiScrollList UiKit.ScrollList(ctx, parent, x, y, w, h, followFocus=Focus, rowGap=4)            // list.AddRow(h) → RectTransform, Clear(), EnsureVisible(rt)
List<RectTransform> UiKit.Grid(parent, x, y, columns, count, cellW, cellH, gapX=8, gapY=8)      // UiKit.GridCell(i, …) = 순수 좌표
UiSelectionBracket UiKit.SelectionBracket(ctx, target)   // 위젯이 아닌 칸에 괄호: b.Set(UiStateRules.Bracket(enabled, cursor, picked, mode))
UiLabel UiKit.KeyHintBar(ctx, parent, x, y, w, h, hints=null)                                   // K-06 띠 + "J 고르기 · K 뒤로 · Esc 나가기"
Image   UiKit.Divider(ctx, parent, x, y, length, vertical=false)                                 // K-07
UiTooltipTrigger UiKit.AttachTooltip(gameObject, () => 글)                                       // 마우스가 머물면 / 키보드 커서가 오면
UiDragSource UiKit.MakeDraggable(gameObject, payload, dropped => …, canDrag=null)               // 끌기 그림자(60%)가 따라온다
UiDropTarget UiKit.MakeDropTarget(gameObject, payload => 받나?, payload => 놓음, hover => 강조)
UiTriangle UiKit.Triangle(parent, UiTriangleDir.Up, x, y, w, h, color) / UiKit.WarningTriangle(parent, x, y, size)
Image   UiKit.KeyCap(ctx, parent, Key.J, x, y, size=24, pressed=false)                          // K-50/51 + 키 글자
UiSprite UiKit.KitSprite(ctx, id, variant, state) / Sprite UiKit.IconSprite(ctx, id, size) / Font UiKit.FontFor(ctx, size)
```

`UiLabel`: `SetText(글)`, `FullText`, `Truncated`, `Color`, `Wrap`, `Text`(UnityEngine.UI.Text). 한 줄 칸은 넘치면 "…"로 자르고 전체 글을 툴팁으로 단다.

## 4. 누를 수 있는 것 — `UiWidget` (UiButton, UiToggle, UiSlider의 바탕)

- 속성: `Enabled`, `On`(탭·거르기 켜짐), `Picked`(확정: 괄호 멈춤 + 확정 체크), `Cursor`(UiFocusGroup이 정함), `Hovered`, `Pressed`, `VisualState`.
- `SetDisabled("error.not_enough_gold")` = 끄고 이유를 단다. 커서는 간다. 커서가 있으면(마우스면 위에 있으면) 아래 한 줄에 이유, 눌러도 아무 일 없이
  `ui.common.disabled_press` + 이유를 다시 보인다. `SetEnabled()`.
- 겉모습 우선순위: 꺼짐 > 누름 > 켜짐 > 올림(마우스 모드에서만) > 기본. 괄호: 키보드 모드 = 커서 + 고른 것, 마우스 모드 = 고른 것만, 꺼짐이면 회청.
- `OnClick`, `PressSound`(기본 `ui.common.press`, null = 없음), `NoBracket`(괄호를 따로 그리는 칸 — InitWidget 전에).
- 카드·칸·목록 줄은 `UiButtonStyle.Plain` 버튼으로 만들고 그 위에 그림을 올린다(`b.ContentRoot`가 누름 때 2px 내려간다).
- 순수 규칙: `UiStateRules.Resolve / Bracket / ShowsDisabledReason / StateToken`, `UiBrackets.SizeFor / ArmPx / PulseFrame`.

## 5. 커서 — `UiFocusGroup` (화면마다 `Focus`)

```csharp
Focus.Add(widget);            // UiKit의 group 인자로도 넣는다. 첫 칸에 커서
Focus.Columns = 3;            // 0 = 위치로 찾기(기본), N = N열 격자(넣은 순서 = 줄 순)
Focus.Wrap = true;            // 끝에서 반대쪽으로(세로 메뉴)
Focus.SetCursor(i 또는 widget, playSound);  Focus.Current;  Focus.Index;  Focus.Remove(w);  Focus.Clear();
Focus.CursorChanged = prev => …;           // 커서가 옮겨 갔을 때(설명 칸 갱신 등)
bool used = Focus.HandleKey(key);          // 커서 칸 먼저(슬라이더 좌우) → 방향 = 이동(ui.common.cursor_move) → J = Activate
```

마우스가 칸에 올라오면(마우스 모드) 커서도 그 칸으로 옮겨 키보드로 바꿀 때 그 자리에서 이어진다. 클릭하면 커서가 옮겨 가고 Activate.
한 화면에 구역이 여럿이면 `new UiFocusGroup(Manager?.InputMode, Ctx.Sound)`를 더 만들고 `OnMenuKey`에서 어느 쪽에 넘길지 정한다.

## 6. 메뉴 키 — `MenuInput` (관리자가 갖는다)

방향키 + 설정의 이동 키 배치(없으면 W/A/S/D) = Up/Down/Left/Right, J·Z = Confirm, K·X = Cancel, **Esc = Back**(고정 키, 일시정지도 이 길로만),
Q = PrevTab, E = NextTab. 재설정한 일시정지 키는 메뉴 길에서 잡지 않는다(Battle은 `IGameSession.PauseRequested`로 받는다).
방향·Q/E는 누르고 있으면 0.4초 뒤 0.1초마다 반복([임시]). 키 재설정 화면은 "키를 누르세요" 동안 `Screens.CaptureKeys = true`로 두고
`Keyboard.current`를 직접 읽는다(Esc = 취소, TV-174). 끝나면 false + `Manager.Menu.SwallowHeld()`.
기본·대체 키 배치 표는 Core의 `Meta.KeyLayouts`가 정본이다(00 문서 D-74). 화면이 표를 따로 적지 않는다.

값이 요약서에 없는 [임시] 수치(키 반복 0.4초·0.1초, 툴팁 지연 0.3초, 누름 모습 0.1초, 꺼짐 이유 2초, 토스트 내려오기 0.2초)는
`Resources/DataSolo/F3.json`의 `ui` 절에 있고 `UiTuning`이 읽는다(`ScreenManager.Tuning`. 조각이 없으면 같은 값의 기본값 — 00 문서 D-75).

## 7. 소리

화면 사건은 그 화면이 요약서의 시점에 `PlayUi("ui.camp.depart")` 또는 `ctx.Sound.Play(열쇠, SoundBus.Ui)`로 낸다(열쇠 = event_bindings.json).
공용 사건은 `UiSounds`(press, cursor_move, disabled_press, hover_enter, toggle_on/off, tooltip_show, screen_open/close)이고 위젯과 관리자가 이미 낸다.

## 8. 공용 창 (`Dialogs/`, 등록 = `ScreenRegistry.Common.cs`)

| 화면 | 형 | 인자 |
|---|---|---|
| `ConfirmDialog` 560×320 | `UiConfirmDialog` | `TitleKey`(없으면 str.confirm_dialog.title), `BodyKey` 또는 `Text`(만든 글), `Caution` + `OnResult(bool)` — `Confirm`/`ConfirmText`로 연다 |
| `Loading` 전체 | `UiLoadingScreen`(+ 순수 `UiLoadingFlow`) | `NextScreen`, `NextArgs` — `ScreenArgs.ViaLoading` |
| `SaveRestored` 640×360 | `UiSaveRestoredDialog` | 없음. [확인] = Pop |
| `Tooltip` 최대 폭 400 | `UiTooltipScreen` | `Text` — `ShowTooltip(글, rect)`로 연다(Toast 층) |
| (토스트) 480×96 | `UiToastScreen` | `Toast(글, 초)` |

## 9. 부팅

`AppFactory.Screens.cs`의 `CreateScreens`가 `ScreenManager.Create(ctx)`를 부른다: 층마다 캔버스(`UiCanvasFactory.Create(name, UiSortOrders.Screen(layer))`),
EventSystem 하나 + `InputSystemUIInputModule`, 관리자 GameObject(전부 DontDestroyOnLoad). 다시 불리면 앞의 것을 `Teardown(true)`로 걷는다.
HUD 캔버스(순서 0)는 F5가 같은 함수로 만든다. EventSystem은 여기만 만든다.
