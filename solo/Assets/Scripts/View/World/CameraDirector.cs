using UnityEngine;

namespace Wizard.Solo.View
{
    /// <summary>
    /// 전장 카메라의 움직임(순수 계산, 엔진 호출 없음). WorldViewHost가 매 프레임 Step을 부르고 결과(위치·정사영 크기)를 카메라에 넣는다.
    /// - 확대: 1칸 = 64px × UI 배율 × zoom. UI 배율이 1 이상(정수)이고 integer_zoom이면 1칸의 화면 px를 64의 정수 배로 맞춘다(픽셀 그림이 뭉개지지 않게).
    /// - 따라가기: 플레이어(여럿이면 평균) + 바라보는 쪽으로 조금 앞(look-ahead). 지수 감쇠로 부드럽게.
    /// - 전장 경계: 보이는 사각형이 전장(view 좌표 0..W × 0..H) 밖으로 나가지 않게 가둔다. 전장이 화면보다 좁은 축은 가운데에 둔다.
    /// - 동적 줌: wantZoomMul(보스전·넓은 예고 = 1 미만)으로 천천히 바꾼다. 줌 펀치(비기·큰 처치)는 잠깐 더 당겼다가 돌아온다.
    /// - 픽셀 맞춤: 위치를 화면 1px 격자에 맞춘다.
    /// 모든 수치는 Juice.json camera 절(표시 전용, 솔로 임시).
    /// </summary>
    public sealed class CameraDirector
    {
        public float Zoom = 2f;
        public bool IntegerZoom = true;
        public float FollowRate = 7f;
        public float LookAheadCells = 0.9f;
        public float LookAheadRate = 3f;
        public float ZoomRate = 2.5f;
        public float PunchMax = 0.3f;
        public float PunchDecay = 9f;
        public bool PixelSnap = true;
        /// <summary>위 HUD 띠가 아래 띠보다 두꺼운 만큼 플레이어를 가려지지 않은 영역 가운데에 두는 치우침(기준 화면 px, + = 카메라를 위로).</summary>
        public float HudBiasPx = 12f;
        /// <summary>움직임 연출 배율(줌 펀치에만). 흔들림 옵션과 연출 감도에서 호스트가 넣는다. 0이면 펀치 없음.</summary>
        public float MotionScale = 1f;

        Vector2 _pos, _look;
        float _zoomMul = 1f, _punch;
        bool _has;

        public Vector2 Position { get; private set; }
        public float OrthographicSize { get; private set; }
        /// <summary>화면 px / 1칸(지금 줌·펀치 포함).</summary>
        public float ScreenPixelsPerUnit { get; private set; }
        public float ZoomMul => _zoomMul;
        public float Punch => _punch;
        public bool HasPosition => _has;

        /// <summary>다음 Step에서 목표로 바로 간다(판 시작·보조 판 전환).</summary>
        public void Reset()
        {
            _has = false;
            _punch = 0f;
            _zoomMul = 1f;
            _look = Vector2.zero;
        }

        /// <summary>줌 펀치(+ = 당김, − = 잠깐 물러남). 한도 ±PunchMax.</summary>
        public void AddPunch(float amount)
        {
            if (float.IsNaN(amount)) return;
            _punch = Mathf.Clamp(_punch + amount * Mathf.Max(0f, MotionScale), -PunchMax, PunchMax);
        }

        /// <summary>UI 배율과 줌에서 1칸의 화면 px(줌 펀치·동적 줌 제외).</summary>
        public float BasePixelsPerUnit(float uiScale)
        {
            float s = uiScale > 0f ? uiScale : 1f;
            float z = Zoom > 0f ? Zoom : 1f;
            float k = s * z;
            if (IntegerZoom && s >= 1f) k = Mathf.Max(1f, Mathf.Round(k));
            return 64f * k;
        }

        /// <param name="screenW">카메라 px 너비.</param>
        /// <param name="screenH">카메라 px 높이.</param>
        /// <param name="uiScale">UiScale.Compute 값.</param>
        /// <param name="focus">따라갈 점(view 좌표).</param>
        /// <param name="lookDir">바라보는 쪽(view 좌표 단위 벡터, 없으면 0).</param>
        /// <param name="wantZoomMul">동적 줌 목표(1 = 기본).</param>
        /// <param name="arenaW">전장 너비(칸).</param>
        /// <param name="arenaH">전장 높이(칸).</param>
        public void Step(float dt, int screenW, int screenH, float uiScale, Vector2 focus, Vector2 lookDir, float wantZoomMul, float arenaW, float arenaH)
        {
            if (screenW <= 0 || screenH <= 0) return;
            if (dt < 0f || float.IsNaN(dt)) dt = 0f;
            if (wantZoomMul <= 0f || float.IsNaN(wantZoomMul)) wantZoomMul = 1f;

            if (!_has) _zoomMul = wantZoomMul;
            else _zoomMul = Mathf.Lerp(_zoomMul, wantZoomMul, 1f - Mathf.Exp(-ZoomRate * dt));
            _punch *= Mathf.Exp(-PunchDecay * dt);
            if (Mathf.Abs(_punch) < 1e-4f) _punch = 0f;

            float ppu = BasePixelsPerUnit(uiScale) * _zoomMul * (1f + _punch);
            if (ppu < 1f) ppu = 1f;
            ScreenPixelsPerUnit = ppu;
            float halfH = screenH / (2f * ppu);
            float halfW = halfH * screenW / screenH;
            OrthographicSize = halfH;

            Vector2 wantLook = lookDir.sqrMagnitude > 1e-6f ? lookDir.normalized * LookAheadCells : Vector2.zero;
            _look = _has ? Vector2.Lerp(_look, wantLook, 1f - Mathf.Exp(-LookAheadRate * dt)) : wantLook;
            float s = uiScale > 0f ? uiScale : 1f;
            Vector2 target = focus + _look + new Vector2(0f, HudBiasPx * s / ppu);

            if (!_has) { _pos = target; _has = true; }
            else _pos = Vector2.Lerp(_pos, target, 1f - Mathf.Exp(-FollowRate * dt));
            _pos = Clamp(_pos, halfW, halfH, arenaW, arenaH);

            Vector2 outPos = _pos;
            if (PixelSnap)
            {
                outPos.x = Mathf.Round(outPos.x * ppu) / ppu;
                outPos.y = Mathf.Round(outPos.y * ppu) / ppu;
                outPos = Clamp(outPos, halfW, halfH, arenaW, arenaH);
            }
            Position = outPos;
        }

        /// <summary>보이는 사각형(가운데 c, 반너비·반높이)이 전장 안에 있게 가운데를 옮긴다. 전장이 더 좁은 축은 전장 가운데.</summary>
        public static Vector2 Clamp(Vector2 c, float halfW, float halfH, float arenaW, float arenaH)
        {
            c.x = 2f * halfW >= arenaW ? arenaW * 0.5f : Mathf.Clamp(c.x, halfW, arenaW - halfW);
            c.y = 2f * halfH >= arenaH ? arenaH * 0.5f : Mathf.Clamp(c.y, halfH, arenaH - halfH);
            return c;
        }
    }
}
