#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Text;
using Wizard.Unity.UI.Common;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Common.Dialogs;
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.Tests.EditMode.UI.Common
{
    // 시험용 화면 모델. 층과 가로막기, Esc 소비를 정할 수 있다
    public sealed class TestScreen : IScreen
    {
        public TestScreen(ScreenId id, ScreenLayer layer, bool blocksInput)
        {
            Id = id;
            Layer = layer;
            BlocksInput = blocksInput;
        }

        public ScreenId Id { get; }
        public ScreenLayer Layer { get; }
        public bool BlocksInput { get; }
        public bool ConsumeBack { get; set; }
        public int Entered { get; private set; }
        public int Exited { get; private set; }
        public int BackCount { get; private set; }

        public void OnEnter(ScreenArgs args) => Entered++;

        public void OnExit() => Exited++;

        public bool OnBack()
        {
            BackCount++;
            return ConsumeBack;
        }
    }

    // id → 층. 정하지 않은 id 는 Fullscreen. 확인 창은 ConfirmDialogModel 을 만든다
    public sealed class TestScreenFactory : IScreenFactory
    {
        private readonly Dictionary<ScreenId, ScreenLayer> _layers = new Dictionary<ScreenId, ScreenLayer>();
        private readonly Dictionary<ScreenId, bool> _blocks = new Dictionary<ScreenId, bool>();

        public ScreenStack? Stack { get; set; }
        public InputModeTracker? Mode { get; set; }
        public List<IScreen> Created { get; } = new List<IScreen>();

        public TestScreenFactory Layer(ScreenId id, ScreenLayer layer, bool blocksInput = false)
        {
            _layers[id] = layer;
            _blocks[id] = blocksInput;
            return this;
        }

        public IScreen Create(ScreenId id)
        {
            IScreen s;
            if (id == ScreenId.ConfirmDialog)
            {
                s = new ConfirmDialogModel(Stack!, Mode!);
            }
            else
            {
                ScreenLayer layer = _layers.TryGetValue(id, out ScreenLayer l) ? l : ScreenLayer.Fullscreen;
                bool blocks = _blocks.TryGetValue(id, out bool b) && b;
                s = new TestScreen(id, layer, blocks);
            }
            Created.Add(s);
            return s;
        }
    }

    // 이름 표 대신: 아는 열쇠만 글로 바꾸고 나머지는 열쇠 그대로(ITextResolver.Name 의 약속)
    public sealed class FakeTextResolver : ITextResolver
    {
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>(StringComparer.Ordinal);

        public FakeTextResolver Add(string key, string text)
        {
            _names[key] = text;
            return this;
        }

        public string Name(string nameKey) => _names.TryGetValue(nameKey, out string? t) ? t : nameKey;

        public string Format(string templateKey, IReadOnlyDictionary<string, string>? args)
        {
            string t = Name(templateKey);
            if (args != null)
            {
                foreach (KeyValuePair<string, string> a in args)
                {
                    t = t.Replace("{" + a.Key + "}", a.Value);
                }
            }
            return t;
        }

        public string ResolveTemplate(string template, IReadOnlyDictionary<string, string>? args) => Format(template, args);
    }

    // 위젯을 고루 갖춘 시험용 메뉴 화면 뷰. 뷰가 묶음을 만든다(모델이 묶음을 쥐지 않는 화면).
    //   버튼 셋(왼쪽, 가운데, 오른쪽) / 탭 줄(탭 둘) / 칸 하나 / 목록(열 줄, 창에 다섯 줄) / 꺼진 버튼 하나
    public sealed class TestMenuView : UiScreenView
    {
        public static readonly PixelRect LeftRect = new PixelRect(96, 96, 192, 48);
        public static readonly PixelRect MiddleRect = new PixelRect(352, 96, 192, 48);
        public static readonly PixelRect RightRect = new PixelRect(608, 96, 192, 48);
        public static readonly PixelRect TabBarRect = new PixelRect(96, 200, 384, 48);
        public static readonly PixelRect CellRect = new PixelRect(96, 304, 64, 64);
        public static readonly PixelRect ListRect = new PixelRect(96, 424, 480, 248);
        public static readonly PixelRect DisabledRect = new PixelRect(864, 96, 192, 48);
        public const int ListItems = 10;
        public const string DisabledReason = "str.test.reason";

        public TestMenuView(IScreen screen, UiViewContext context)
            : base(screen, context)
        {
        }

        public List<string> Activations { get; } = new List<string>();
        public List<PhysicalKeyRecord> Keys { get; } = new List<PhysicalKeyRecord>();
        public UiButton Left { get; private set; } = null!;
        public UiButton Middle { get; private set; } = null!;
        public UiButton Right { get; private set; } = null!;
        public UiButton Disabled { get; private set; } = null!;
        public UiTabBar Tabs { get; private set; } = null!;
        public UiCell Cell { get; private set; } = null!;
        public UiList List { get; private set; } = null!;
        public List<int> TabEvents { get; } = new List<int>();

        protected override void OnBuild()
        {
            var bg = new UiPanel("Background", Root, Root.LocalRect, framed: false);
            bg.SetFill(UiPalette.WoodDark);
            CreateGroup();
            Left = Add(new UiButton(this, Root, LeftRect, NewControl("left"), "str.common.back"));
            Middle = Add(new UiButton(this, Root, MiddleRect, NewControl("middle"), "str.common.ok"));
            Right = Add(new UiButton(this, Root, RightRect, NewControl("right"), "str.common.next", ButtonTone.Caution));
            Tabs = new UiTabBar(this, Root, TabBarRect, 192);
            Tabs.AddTab(NewControl("tab0", isToggle: true), "str.common.all");
            Tabs.AddTab(NewControl("tab1", isToggle: true), "str.common.none");
            Tabs.SetSelected(0);
            Tabs.TabActivated += i => TabEvents.Add(i);
            Cell = Add(new UiCell(this, Root, CellRect, NewControl("cell")));
            Cell.SetContentKey("block.fireball");
            List = new UiList(this, Root, ListRect);
            var items = new List<UiListItem>();
            for (int i = 0; i < ListItems; i++)
            {
                items.Add(UiListItem.Literal("row" + i, "row " + i));
            }
            List.SetItems(items);
            foreach (UiListRow row in List.Rows)
            {
                Add(row);
            }
            Disabled = Add(new UiButton(this, Root, DisabledRect, NewControl("disabled"), "str.common.locked"));
            Group!.SetEnabled(Disabled.Control, false, DisabledReason);
        }

        public override bool OnKey(Wizard.Client.PhysicalKey key, UiKeyAction action)
        {
            Keys.Add(new PhysicalKeyRecord(key, action));
            return action == UiKeyAction.Secondary;
        }

        private T Add<T>(T w) where T : UiInteractiveWidget
        {
            w.Activated += x => Activations.Add(x.Control.Id);
            return w;
        }
    }

    public readonly struct PhysicalKeyRecord
    {
        public readonly Wizard.Client.PhysicalKey Key;
        public readonly UiKeyAction Action;

        public PhysicalKeyRecord(Wizard.Client.PhysicalKey key, UiKeyAction action)
        {
            Key = key;
            Action = action;
        }
    }

    // 화면 한 벌(스택 + UiRoot)을 시험용으로 꾸린다. 시계는 손으로 돌린다
    public sealed class UiTestBed : IDisposable
    {
        public UiTestBed(ITextResolver? text = null, Action<ScreenViewRegistry>? register = null)
        {
            Mode = new InputModeTracker();
            Factory = new TestScreenFactory { Mode = Mode };
            Stack = new ScreenStack(Factory);
            Factory.Stack = Stack;
            Registry = new ScreenViewRegistry().WithCommonDialogs();
            Registry.Register(ScreenId.MainMenu, (s, c) => new TestMenuView(s, c));
            register?.Invoke(Registry);
            Ui = new UiRoot(Stack, Registry, Mode, text, () => Now);
        }

        public double Now { get; set; }
        public InputModeTracker Mode { get; }
        public TestScreenFactory Factory { get; }
        public ScreenStack Stack { get; }
        public ScreenViewRegistry Registry { get; }
        public UiRoot Ui { get; }
        public ScreenHost Host => Ui.Host;
        public UiInputRouter Input => Ui.Input;

        public TestMenuView OpenMenu()
        {
            Stack.Push(ScreenId.MainMenu, ScreenArgs.None);
            Host.Sync();
            return (TestMenuView)Host.ViewOf(ScreenId.MainMenu)!;
        }

        // 무대 좌표의 가운데
        public static PixelPoint CenterOf(PixelRect r) => new PixelPoint(r.X + r.Width / 2, r.Y + r.Height / 2);

        public void Click(PixelRect r)
        {
            PixelPoint p = CenterOf(r);
            Input.PointerMoveStage(p);
            Input.PointerDownStage(p);
            Input.PointerUpStage(p);
        }

        public void Tap(Wizard.Client.PhysicalKey key)
        {
            Input.KeyDown(key);
            Input.KeyUp(key);
        }

        public void Dispose() => Ui.Dispose();

        public static int CountObjects(GameObject root) => root.GetComponentsInChildren<Transform>(true).Length;
    }
}
