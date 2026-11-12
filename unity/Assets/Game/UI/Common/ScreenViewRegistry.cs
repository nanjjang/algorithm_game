#nullable enable
using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Common.Dialogs;
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.UI.Common
{
    // ScreenId → 뷰 만들기. 등록하지 않은 화면은 자리 표시 뷰(PlaceholderScreenView)로 그린다: 화면 뷰가 아직 없어도 스택은 돈다.
    // 공용 창 모델(ConfirmDialogModel 따위의 DialogScreen)은 모델 형식으로도 찾는다(WithCommonDialogs).
    public sealed class ScreenViewRegistry : IScreenViewFactory
    {
        private readonly Dictionary<ScreenId, Func<IScreen, UiViewContext, UiScreenView>> _byId =
            new Dictionary<ScreenId, Func<IScreen, UiViewContext, UiScreenView>>();

        private bool _commonDialogs;

        public int Count => _byId.Count;

        // 같은 id 를 두 번 등록하면 오류(두 작업이 한 화면을 그리지 않게)
        public ScreenViewRegistry Register(ScreenId id, Func<IScreen, UiViewContext, UiScreenView> create)
        {
            if (id == ScreenId.None)
            {
                throw new ArgumentException("ScreenId.None has no view", nameof(id));
            }
            if (_byId.ContainsKey(id))
            {
                throw new InvalidOperationException("a view is already registered for " + id);
            }
            _byId.Add(id, create ?? throw new ArgumentNullException(nameof(create)));
            return this;
        }

        // 확인 창은 ConfirmDialogView, 그 밖의 DialogScreen 은 버튼만 그리는 DialogScreenView 로 그린다(등록한 것이 먼저다)
        public ScreenViewRegistry WithCommonDialogs()
        {
            _commonDialogs = true;
            return this;
        }

        public bool IsRegistered(ScreenId id) => _byId.ContainsKey(id);

        public UiScreenView Create(IScreen screen, UiViewContext context)
        {
            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }
            if (_byId.TryGetValue(screen.Id, out Func<IScreen, UiViewContext, UiScreenView>? create))
            {
                UiScreenView view = create(screen, context) ?? throw new InvalidOperationException("view factory returned null for " + screen.Id);
                if (!ReferenceEquals(view.Screen, screen))
                {
                    throw new InvalidOperationException("view factory for " + screen.Id + " returned a view of another screen");
                }
                return view;
            }
            if (_commonDialogs)
            {
                if (screen is ConfirmDialogModel confirm)
                {
                    return new ConfirmDialogView(confirm, context);
                }
                if (screen is DialogScreen dialog)
                {
                    return new DialogScreenView(dialog, context);
                }
            }
            return new PlaceholderScreenView(screen, context);
        }
    }
}
