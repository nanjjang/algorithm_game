#nullable enable
using System;
using UnityEngine;
using Wizard.Text;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.UI.Common
{
    // 화면 쪽 한 벌을 코드로 조립한다: 캔버스와 무대, 뷰 맥락, ScreenHost, UiInputRouter, 프레임 구동기.
    // 조립 루트(Wizard.Unity.App)가 스택과 뷰 등록부, 이름 표, 입력 모드, 단조 시계를 주고 만든다. 전역에 두지 않는다.
    public sealed class UiRoot : IDisposable
    {
        public UiRoot(ScreenStack stack, IScreenViewFactory views, InputModeTracker mode, ITextResolver? text,
            Func<double> nowSeconds, Transform? parent = null, UiKeyMap? keys = null)
        {
            if (nowSeconds == null)
            {
                throw new ArgumentNullException(nameof(nowSeconds));
            }
            Canvas = new UiCanvas(parent);
            Context = new UiViewContext(new UiTextSource(text), mode, Canvas.Stage);
            Host = new ScreenHost(stack, views, Context);
            Input = new UiInputRouter(Host, Canvas, keys);
            Driver = Canvas.GameObject.AddComponent<ScreenHostDriver>();
            Driver.Attach(Host, Canvas, Input, nowSeconds);
        }

        public UiCanvas Canvas { get; }

        public UiViewContext Context { get; }

        public ScreenHost Host { get; }

        public UiInputRouter Input { get; }

        public ScreenHostDriver Driver { get; }

        public void Dispose()
        {
            if (Driver != null)
            {
                Driver.Detach();
            }
            Host.Dispose();
            Canvas.Dispose();
        }
    }
}
