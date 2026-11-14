using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 탭 줄(K-25 가로 / K-26 세로). 고른 탭은 On(바탕 #3A3F5A + 아랫변 분필 흰색 4px). Q/E(PrevTab/NextTab)와 클릭으로 바꾼다.
    /// 꺼진 탭은 Q/E가 건너뛰고, 클릭하면 이유를 보인다. 바뀌면 Selected(새 순번). Q/E는 끝에서 반대쪽으로 넘어간다.
    /// 탭 버튼을 화면의 UiFocusGroup에 넣을지는 화면이 정한다(Buttons).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiTabs : MonoBehaviour
    {
        public readonly List<UiButton> Buttons = new List<UiButton>();
        public Action<int> Selected;
        int _index = -1;
        GameContext _ctx;

        public int Index => _index;

        internal void Init(GameContext ctx) { _ctx = ctx; }

        internal void AddButton(UiButton b)
        {
            int i = Buttons.Count;
            Buttons.Add(b);
            b.PressSound = UiSounds.Press;
            b.OnClick = () => Select(i, true);
        }

        /// <summary>i번째 탭을 고른다(notify면 Selected를 부른다). 꺼진 탭은 고르지 않는다.</summary>
        public void Select(int i, bool notify = true)
        {
            if (i < 0 || i >= Buttons.Count || !Buttons[i].Enabled) return;
            bool changed = i != _index;
            _index = i;
            for (int k = 0; k < Buttons.Count; k++) Buttons[k].On = k == i;
            if (changed && notify)
            {
                try { Selected?.Invoke(i); } catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        /// <summary>Q/E. 처리했으면 true(탭이 하나 이하면 false).</summary>
        public bool HandleKey(MenuKey key)
        {
            int dir = key == MenuKey.PrevTab ? -1 : key == MenuKey.NextTab ? 1 : 0;
            if (dir == 0 || Buttons.Count <= 1) return false;
            int start = _index < 0 ? 0 : _index;
            for (int step = 1; step < Buttons.Count; step++)
            {
                int j = ((start + dir * step) % Buttons.Count + Buttons.Count) % Buttons.Count;
                if (!Buttons[j].Enabled) continue;
                UiSounds.Play(_ctx != null ? _ctx.Sound : null, UiSounds.CursorMove);
                Select(j, true);
                return true;
            }
            return true;
        }
    }
}
