using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Stack;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // 공용 창(spec 13 1.5절)의 뼈대. 버튼은 누를 수 있는 것의 상태 기계(InteractableGroup)로 돈다.
    // 버튼을 끝까지 누르거나(J·포인터) Esc 로 취소 버튼을 고르면 Choose: 결과를 남기고, 스택에서 스스로 닫고, 요청한 쪽에 알린다.
    // 취소 버튼이 없는 창(불러오기, 끊김)에서 Esc 는 소비만 한다.
    public abstract class DialogScreen : IScreen
    {
        public const int ButtonWidthPx = 192;   // K-21 보통 버튼(높이 48, 최소 폭 128) [임시: 폭]
        public const int ButtonHeightPx = 48;

        private readonly ScreenStack _stack;
        private readonly InputModeTracker _mode;
        private readonly List<DialogButton> _buttons = new List<DialogButton>();
        private InteractableGroup? _group;
        private Action<DialogButtonRole>? _onChosen;

        protected DialogScreen(ScreenStack stack, InputModeTracker mode)
        {
            _stack = stack ?? throw new ArgumentNullException(nameof(stack));
            _mode = mode ?? throw new ArgumentNullException(nameof(mode));
        }

        public abstract ScreenId Id { get; }
        public abstract ScreenLayer Layer { get; }
        public virtual bool BlocksInput => Layer == ScreenLayer.Modal || Layer == ScreenLayer.Fullscreen;

        public IReadOnlyList<DialogButton> Buttons => _buttons;

        // 버튼의 상태 기계. 열려 있는 동안만 있다
        public InteractableGroup Group => _group ?? throw new InvalidOperationException("dialog is not open");

        public bool IsOpen => _group != null;

        public DialogButtonRole? Result { get; private set; }

        public event Action<DialogButtonRole>? Chosen;

        // Esc 가 고르는 버튼. null 이면 Esc 는 소비만 한다
        protected virtual DialogButtonRole? CancelRole => null;

        public void OnEnter(ScreenArgs args)
        {
            Result = null;
            _buttons.Clear();
            _group = new InteractableGroup(_mode);
            _group.Raised += OnInteraction;
            _onChosen = Setup(args);
            DialogButton? first = DefaultButton();
            if (first != null)
            {
                _group.SetCursor(first.Control);
            }
        }

        public void OnExit()
        {
            if (_group != null)
            {
                _group.Raised -= OnInteraction;
                _group.Dispose();
                _group = null;
            }
        }

        public virtual bool OnBack()
        {
            DialogButtonRole? cancel = CancelRole;
            if (cancel.HasValue && FindButton(cancel.Value) != null)
            {
                Choose(cancel.Value);
            }
            return true;
        }

        // 고른다. 열려 있지 않거나 이미 골랐으면 아무 일 없이 false
        public bool Choose(DialogButtonRole role)
        {
            if (_group == null || Result.HasValue)
            {
                return false;
            }
            DialogButton? b = FindButton(role) ?? throw new ArgumentException("no such button: " + role, nameof(role));
            if (!b.Control.Enabled)
            {
                return false;
            }
            Result = role;
            Action<DialogButtonRole>? onChosen = _onChosen;
            _stack.Close(this);
            onChosen?.Invoke(role);
            Chosen?.Invoke(role);
            return true;
        }

        public DialogButton? FindButton(DialogButtonRole role)
        {
            foreach (DialogButton b in _buttons)
            {
                if (b.Role == role)
                {
                    return b;
                }
            }
            return null;
        }

        // 요청 자료를 읽고 버튼을 더한다. 돌려준 콜백을 고른 뒤 부른다
        protected abstract Action<DialogButtonRole>? Setup(ScreenArgs args);

        // 처음 커서가 갈 버튼. 기본은 첫 버튼
        protected virtual DialogButton? DefaultButton() => _buttons.Count == 0 ? null : _buttons[0];

        protected DialogButton AddButton(DialogButtonRole role, string labelKey, ButtonTone tone = ButtonTone.Normal)
        {
            var control = new Interactable(ButtonIdOf(role), ButtonWidthPx, ButtonHeightPx);
            Group.Add(control);
            var b = new DialogButton(role, labelKey, tone, control);
            _buttons.Add(b);
            return b;
        }

        protected static T PayloadOf<T>(ScreenArgs args) where T : class =>
            args.Payload as T ?? throw new ArgumentException("payload must be " + typeof(T).Name, nameof(args));

        private static string ButtonIdOf(DialogButtonRole role)
        {
            switch (role)
            {
                case DialogButtonRole.Yes: return "yes";
                case DialogButtonRole.No: return "no";
                case DialogButtonRole.Ok: return "ok";
                case DialogButtonRole.Close: return "close";
                case DialogButtonRole.Retry: return "retry";
                case DialogButtonRole.SwitchToOffline: return "switch_to_offline";
                default: throw new ArgumentOutOfRangeException(nameof(role), role, null);
            }
        }

        private void OnInteraction(InteractionEvent e)
        {
            if (e.Kind != InteractionEventKind.Activated)
            {
                return;
            }
            foreach (DialogButton b in _buttons)
            {
                if (ReferenceEquals(b.Control, e.Target))
                {
                    Choose(b.Role);
                    return;
                }
            }
        }
    }
}
