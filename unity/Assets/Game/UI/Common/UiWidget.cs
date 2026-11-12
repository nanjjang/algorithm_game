#nullable enable
using System;
using UnityEngine;

namespace Wizard.Unity.UI.Common
{
    // 코드로 만드는 위젯 하나(T-32). uGUI 개체 하나와 정수 px 사각형을 쥔다.
    // 위치와 크기는 8px 격자(spec 14 4.3)를 따르게 놓는 것이 화면 뷰의 몫이고, 위젯은 받은 값 그대로 놓는다(검사는 테스트가 한다).
    public class UiWidget
    {
        private PixelRect _localRect;
        private bool _visible = true;

        // 부모 위젯 아래에 만든다
        public UiWidget(string name, UiWidget parent, PixelRect localRect)
            : this(name, (parent ?? throw new ArgumentNullException(nameof(parent))).Transform, parent, localRect)
        {
        }

        // 무대(또는 위젯이 아닌 RectTransform) 바로 아래에 만든다. 이때 LocalRect 가 곧 AbsoluteRect 다
        public UiWidget(string name, RectTransform parentTransform, PixelRect localRect)
            : this(name, parentTransform, null, localRect)
        {
        }

        private UiWidget(string name, RectTransform parentTransform, UiWidget? parent, PixelRect localRect)
        {
            if (parentTransform == null)
            {
                throw new ArgumentNullException(nameof(parentTransform));
            }
            Parent = parent;
            Transform = UiObjects.CreateRect(name, parentTransform);
            GameObject = Transform.gameObject;
            SetRect(localRect);
        }

        public GameObject GameObject { get; }

        public RectTransform Transform { get; }

        public UiWidget? Parent { get; }

        public PixelRect LocalRect => _localRect;

        // 무대 왼쪽 위 기준
        public PixelRect AbsoluteRect
        {
            get
            {
                PixelRect r = _localRect;
                for (UiWidget? p = Parent; p != null; p = p.Parent)
                {
                    r = r.Offset(p._localRect.X, p._localRect.Y);
                }
                return r;
            }
        }

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                if (GameObject != null)
                {
                    GameObject.SetActive(value);
                }
            }
        }

        // 자신과 모든 부모가 보이는가(화면 뷰의 뿌리까지)
        public bool IsShown
        {
            get
            {
                for (UiWidget? w = this; w != null; w = w.Parent)
                {
                    if (!w._visible)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public bool IsDestroyed => GameObject == null;

        public virtual void SetRect(PixelRect localRect)
        {
            _localRect = localRect;
            UiObjects.Place(Transform, localRect);
        }

        public void Destroy() => UiObjects.Destroy(GameObject);
    }
}
