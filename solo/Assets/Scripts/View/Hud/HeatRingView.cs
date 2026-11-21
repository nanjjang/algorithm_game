using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 캐릭터 발밑의 열 링(반지름 44px, 임계 시작 눈금)과 시야 화살표 — 전장 층 SortBands.PlayerMarks.
    /// 둘 다 줄일 수 없다: EffectsLevel·옵션과 무관하게 언제나 그린다(00 문서 7절, 10 요약서 3.10절).
    /// 상태 6종(02 요약서 6.6절) + 면제 대기 + 불가마 표식(10 요약서 3.4절). 금색·빨강을 쓰지 않는다.
    /// </summary>
    public sealed class HeatRingView
    {
        const float RingR = HudLayout.HeatRingRadiusPx;
        const float ChevronPx = 8f;
        /// <summary>임계 쐐기가 링을 도는 빠르기(도/초). 요약서에 값이 없어 한 바퀴 2초로 둔다(표시 전용).</summary>
        const float ChevronDegPerSecond = 180f;

        readonly ViewContext _vc;
        readonly HudTuning _tuning;
        readonly HudMeshBuilder _b = new HudMeshBuilder();
        readonly Mesh _mesh;
        readonly MeshRenderer _mr;
        readonly SpriteRenderer[] _chevrons = new SpriteRenderer[2];
        readonly Sprite _chevronSprite;
        float _t;
        float _unlockFlashLeft;

        public HeatRingState LastState { get; private set; }
        public bool ExemptRingShown { get; private set; }
        public bool KilnMarkShown { get; private set; }
        public FacingArrowMode LastArrowMode { get; private set; }
        public Color LastArrowColor { get; private set; }
        public Color LastRingColor { get; private set; }
        public int LastTriangleCount => _b.TriangleCount;

        public HeatRingView(ViewContext vc, HudArt art, HudTuning tuning)
        {
            _vc = vc;
            _tuning = tuning;
            _mesh = new Mesh { name = "HudHeatRing" };
            _mesh.MarkDynamic();
            if (vc.Pool != null)
            {
                _mr = vc.Pool.RentMesh(SortBands.PlayerMarks, SpriteMaterialKind.Shape);
                if (_mr != null)
                {
                    var mf = _mr.GetComponent<MeshFilter>();
                    if (mf != null) mf.sharedMesh = _mesh;
                    _mr.transform.localScale = Vector3.one / SimConst.PixelsPerUnit;
                }
                _chevronSprite = art.EffectFrame("status", "critical_ring_chevrons", 0);
                if (_chevronSprite != null)
                    for (int i = 0; i < _chevrons.Length; i++)
                    {
                        _chevrons[i] = vc.Pool.Rent(_chevronSprite, SortBands.PlayerMarks + 2);
                        if (_chevrons[i] != null) _chevrons[i].enabled = false;
                    }
            }
        }

        public void OnUnlocked() { _unlockFlashLeft = _tuning.UnlockFlashSeconds; }

        /// <summary>feetSim = 발밑(시뮬레이션 좌표).</summary>
        public void Render(SimWorld w, PlayerState p, Vector2 feetSim, float dt)
        {
            _t += dt;
            _b.Clear();
            bool show = p != null && p.Active && !p.Downed;
            if (!show)
            {
                _b.Apply(_mesh);
                SetChevrons(false, Vector3.zero, 0f);
                return;
            }
            if (_mr != null) _mr.transform.localPosition = _vc.ToView(feetSim);
            Vector2 c = Vector2.zero;

            var state = HudModel.RingState(p);
            LastState = state;
            int crit = HudModel.CritStartHu(p);
            float tickDeg = HudModel.RingAngleDegrees(crit);
            float heatDeg = HudModel.RingAngleDegrees(p.HeatHu);
            HudColors.Ring(state, out var ringColor, out float width);
            LastRingColor = ringColor;

            // 바탕 고리(차가움이 아닐 때만)
            if (state != HeatRingState.Cold) _b.Ring(c, RingR - 1f, RingR + 1f, HudColors.RingTrack);

            switch (state)
            {
                case HeatRingState.Rising:
                case HeatRingState.Imminent:
                    _b.Arc(c, RingR - width * 0.5f, RingR + width * 0.5f, 0f, heatDeg, ringColor);
                    break;
                case HeatRingState.Critical:
                case HeatRingState.Brink:
                {
                    float jitter = state == HeatRingState.Brink ? UnityEngine.Random.Range(-1f, 1f) : 0f; // 2px 떨림(표시용 난수)
                    float r = RingR + jitter;
                    _b.Arc(c, r + width * 0.5f, r + width * 0.5f + 2f, 0f, heatDeg, HudColors.RingRim);
                    _b.Arc(c, r - width * 0.5f - 2f, r - width * 0.5f, 0f, heatDeg, HudColors.RingRim);
                    _b.Arc(c, r - width * 0.5f, r + width * 0.5f, 0f, heatDeg, ringColor);
                    if (state == HeatRingState.Brink)
                    {
                        // 눈금 맞은편 경고 삼각(흰 + 검정 테)
                        float od = tickDeg + 180f;
                        Vector2 at = c + HudMeshBuilder.OnCircle(od, RingR + 14f);
                        Vector2 up = HudMeshBuilder.OnCircle(od, 1f);
                        Vector2 side = new Vector2(-up.y, up.x);
                        _b.Triangle(at + up * 8f, at - up * 6f + side * 8f, at - up * 6f - side * 8f, HudColors.RingRim);
                        _b.Triangle(at + up * 5f, at - up * 4f + side * 5f, at - up * 4f - side * 5f, HudColors.RingCritical);
                    }
                    break;
                }
                case HeatRingState.Locked:
                {
                    float remain = HudModel.LockRemainRatio(p.LockStartTick, p.UnlockTick, w != null ? w.Tick : 0);
                    float sweep = 360f * remain;
                    float start = 360f - sweep; // 앞끝이 시계 방향으로 물러나며 줄어든다
                    _b.Arc(c, RingR - width * 0.5f - 2f, RingR + width * 0.5f + 2f, start, sweep, HudColors.RingRim);
                    _b.Arc(c, RingR - width * 0.5f, RingR + width * 0.5f, start, sweep, ringColor);
                    // 가운데 X
                    _b.Line(new Vector2(-6f, -6f), new Vector2(6f, 6f), 3f, ringColor);
                    _b.Line(new Vector2(-6f, 6f), new Vector2(6f, -6f), 3f, ringColor);
                    break;
                }
            }

            // 해제: 회청으로 한 번 퍼지고 사라짐
            if (_unlockFlashLeft > 0f)
            {
                _unlockFlashLeft -= dt;
                float k = 1f - Mathf.Clamp01(_unlockFlashLeft / Mathf.Max(0.001f, _tuning.UnlockFlashSeconds));
                float r = RingR + 12f * k;
                _b.Ring(c, r - 2f, r + 2f, UiTheme.WithAlpha(HudColors.RingSlate, 1f - k));
            }

            // 열 면제 대기: 임계 링 바깥에 흰 2px 링 한 겹
            ExemptRingShown = p.HeatExemptArmed;
            if (p.HeatExemptArmed) _b.Ring(c, RingR + 7f, RingR + 9f, HudColors.RingCritical);

            // 임계 시작 눈금(차가움 = 희미, 임박 = 초당 2회 숨 쉼)
            float tickA = state == HeatRingState.Cold ? 0.3f
                : state == HeatRingState.Imminent ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(_t * Mathf.PI * 2f))
                : 0.95f;
            _b.RadialTick(c, tickDeg, RingR - 7f, RingR + 7f, 4f, UiTheme.WithAlpha(HudColors.RingRim, tickA));
            _b.RadialTick(c, tickDeg, RingR - 6f, RingR + 6f, 2f, UiTheme.WithAlpha(HudColors.RingCritical, tickA));

            // 불가마 안: 눈금에 8×8 표식
            KilnMarkShown = p.InKilnZone;
            if (p.InKilnZone)
            {
                Vector2 at = c + HudMeshBuilder.OnCircle(tickDeg, RingR + 13f);
                _b.Square(at, 10f, HudColors.RingRim);
                _b.Square(at, 8f, UiTheme.ElementFire);
            }

            // 시야 화살표(보통 반투명 / 자세 불투명 / 반전 옅은 파랑)
            var mode = p.ArrowMode;
            LastArrowMode = mode;
            var arrowColor = HudModel.ArrowColor(mode, _tuning.FacingArrowNormalAlpha);
            LastArrowColor = arrowColor;
            Vector2 viewDir = SimConst.DirToView(Dir8Util.ToSimVector(p.Facing));
            float adeg = HudMeshBuilder.ClockDegrees(viewDir);
            float size = _tuning.FacingArrowSizePx;
            _b.Arrow(c, adeg, _tuning.FacingArrowRadiusPx - 1.5f, size + 3f, UiTheme.WithAlpha(HudColors.RingRim, arrowColor.a));
            _b.Arrow(c, adeg, _tuning.FacingArrowRadiusPx, size, arrowColor);

            _b.Apply(_mesh);

            bool chev = state == HeatRingState.Critical || state == HeatRingState.Brink;
            SetChevrons(chev, _vc.ToView(feetSim), _t * ChevronDegPerSecond);
            if (chev && _chevronSprite == null)
            {
                // 그림이 없으면 쐐기(>)를 메시로
                for (int i = 0; i < 2; i++)
                {
                    float d = _t * ChevronDegPerSecond + i * 180f;
                    Vector2 at = c + HudMeshBuilder.OnCircle(d, RingR);
                    Vector2 tangent = HudMeshBuilder.OnCircle(d + 90f, 1f);
                    Vector2 n = HudMeshBuilder.OnCircle(d, 1f);
                    _b.Triangle(at + tangent * (ChevronPx * 0.5f), at - tangent * (ChevronPx * 0.5f) + n * (ChevronPx * 0.5f),
                        at - tangent * (ChevronPx * 0.5f) - n * (ChevronPx * 0.5f), HudColors.RingCritical);
                }
                _b.Apply(_mesh);
            }
        }

        void SetChevrons(bool on, Vector3 centerView, float baseDeg)
        {
            for (int i = 0; i < _chevrons.Length; i++)
            {
                var sr = _chevrons[i];
                if (sr == null) continue;
                sr.enabled = on;
                if (!on) continue;
                float d = baseDeg + i * 180f;
                Vector2 off = HudMeshBuilder.OnCircle(d, RingR / SimConst.PixelsPerUnit);
                sr.transform.localPosition = centerView + new Vector3(off.x, off.y, 0f);
                // 그림의 ">"가 진행 방향(시계 방향 접선)을 보게
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, -d);
            }
        }

        public void Dispose()
        {
            if (_vc.Pool != null)
            {
                if (_mr != null)
                {
                    var mf = _mr.GetComponent<MeshFilter>();
                    if (mf != null) mf.sharedMesh = null;
                    _vc.Pool.Return(_mr);
                }
                for (int i = 0; i < _chevrons.Length; i++) if (_chevrons[i] != null) _vc.Pool.Return(_chevrons[i]);
            }
            HudArt.Destroy(_mesh);
        }
    }
}
