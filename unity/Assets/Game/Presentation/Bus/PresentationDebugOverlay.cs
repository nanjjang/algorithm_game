#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Unity.Presentation.Fx;
using Wizard.Unity.Presentation.Sfx;

namespace Wizard.Unity.Presentation.Bus
{
    // 빈 재생기의 디버그 겹침 화면. 그림 표식은 그 표시 좌표 위에(전장 카메라가 있으면) 식별자와 좌표를 찍고,
    // 오른쪽 아래에 최근 그림·소리 식별자 목록을 찍는다. IMGUI 라 글꼴 에셋이 필요 없다. 글자는 식별자와 숫자뿐이다(화면 글자가 아니다).
    // 이 화면은 HUD 층이라 흔들림을 받지 않는다. 표식만 전장 좌표를 따라가므로 전장과 같이 흔들린다.
    public sealed class PresentationDebugOverlay : MonoBehaviour
    {
        private const float MarginPx = 8f;                // [임시]
        private const float LineHeightPx = 16f;           // [임시]
        private const float ColumnWidthPx = 320f;         // [임시]
        private const int FontSizePx = 12;                // [임시]

        private readonly List<string> _lines = new List<string>();
        private DebugFxPlayer? _fx;
        private DebugSfxPlayer? _sfx;
        private UnityEngine.Camera? _camera;
        private Func<long>? _nowMs;
        private GUIStyle? _style;

        public void Bind(DebugFxPlayer? fx, DebugSfxPlayer? sfx, UnityEngine.Camera? worldCamera, Func<long> nowMs)
        {
            _fx = fx;
            _sfx = sfx;
            _camera = worldCamera;
            _nowMs = nowMs;
        }

        // 지금 찍을 목록 줄(그림 표식: "fx " + 식별자와 좌표 + 단계, 소리: "sfx " + 식별자와 음높이 단). 테스트와 OnGUI 가 같이 쓴다
        public IReadOnlyList<string> CollectLines()
        {
            _lines.Clear();
            if (_fx != null)
            {
                for (int i = 0; i < _fx.Count; i++)
                {
                    DebugFxMark m = _fx.Mark(i);
                    _lines.Add("fx " + m.Label + " " + m.Phase);
                }
            }
            if (_sfx != null)
            {
                for (int i = 0; i < _sfx.Count; i++)
                {
                    _lines.Add("sfx " + _sfx.Mark(i).Label);
                }
            }
            return _lines;
        }

        private void OnGUI()
        {
            if (_nowMs == null || (_fx == null && _sfx == null))
            {
                return;
            }
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = FontSizePx, wordWrap = false };
                _style.normal.textColor = Core.ShapePalette.DebugText;
            }
            long now = _nowMs();
            Color keep = GUI.color;
            if (_fx != null && _camera != null)
            {
                for (int i = 0; i < _fx.Count; i++)
                {
                    DebugFxMark m = _fx.Mark(i);
                    Vector3 sp = _camera.WorldToScreenPoint(new Vector3(m.WorldPos.X, m.WorldPos.Y, 0f));
                    GUI.color = new Color(keep.r, keep.g, keep.b, _fx.Alpha(i, now));
                    GUI.Label(new Rect(sp.x, Screen.height - sp.y, ColumnWidthPx, LineHeightPx), m.Label, _style);
                }
            }
            GUI.color = keep;
            IReadOnlyList<string> lines = CollectLines();
            float x = Screen.width - ColumnWidthPx - MarginPx;
            float y = Screen.height - MarginPx - lines.Count * LineHeightPx;
            for (int i = 0; i < lines.Count; i++)
            {
                GUI.Label(new Rect(x, y + i * LineHeightPx, ColumnWidthPx, LineHeightPx), lines[i], _style);
            }
        }
    }
}
