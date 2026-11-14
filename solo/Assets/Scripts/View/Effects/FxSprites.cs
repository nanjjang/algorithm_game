using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.View.Effects
{
    /// <summary>한 번 재생하고 사라지는 그림 효과 하나의 요청.</summary>
    public struct FxSpec
    {
        public SpriteAnim Anim;
        /// <summary>정지 그림(Anim이 없을 때).</summary>
        public Sprite Sprite;
        public Vector2 SimPos;
        /// <summary>시작·끝 크기 배율(같으면 고정).</summary>
        public Vector2 ScaleFrom, ScaleTo;
        public float RotationDeg;
        public bool FlipX;
        public Color Color;
        /// <summary>0이면 그림 길이(반복 그림이면 1프레임 길이).</summary>
        public float Life;
        /// <summary>끝에서 이 초 동안 흐려진다(0 = 끝까지 그대로).</summary>
        public float FadeOut;
        public int SortingOrder;
        public bool Additive;
        /// <summary>누적 가림 넓이 셈에 넣는가(7·8층 반투명 일회 효과). 넣으면 상한에 맞춰 줄거나 생략된다.</summary>
        public bool CountsOcclusion;
        /// <summary>넓이 셈에 쓰는 반지름(칸). 0이면 그림 크기에서 구한다.</summary>
        public float OcclusionRadius;
        /// <summary>외곽선(임계 처치 조각 흰+검정 테 등). OutlinePx 0 = 없음.</summary>
        public Color OutlineColor;
        public float OutlinePx;
        /// <summary>따라갈 대상의 위치를 매 프레임 돌려준다(null = 제자리).</summary>
        public System.Func<Vector2?> Follow;
        /// <summary>그림 하나를 길게 늘인다(Tiled 그리기 — 이어 붙임 그림). 0 = 안 함. 값 = 칸 단위 길이.</summary>
        public float TiledLength;
        public float TiledHeight;
    }

    /// <summary>
    /// 일회 그림 효과 관리(적중 불꽃, 폭발, 처치 연기, 비기 문양 …). 풀에서 SpriteRenderer를 빌려 그리고 끝나면 돌려준다.
    /// 7·8층 효과는 OcclusionField로 들어올 때 불투명도를 받고(넘치면 줄거나 생략), 떠 있는 동안 자리 상한(예고 마스크 안 40%·번쩍 0%)으로 다시 누른다.
    /// </summary>
    public sealed class FxSprites
    {
        sealed class Fx
        {
            public SpriteRenderer Sr;
            public FxSpec Spec;
            public float Age, Life, Alpha;
            public int OccHandle;
            public float OccRadius;
            public Vector2 Pos;
        }

        readonly EffectsShared _s;
        readonly List<Fx> _live = new List<Fx>();

        public FxSprites(EffectsShared shared) { _s = shared; }

        public int LiveCount => _live.Count;

        /// <summary>효과 하나. 그림이 없거나 가림 상한으로 생략되면 false.</summary>
        public bool Spawn(FxSpec spec)
        {
            var vc = _s.Vc;
            if (vc == null || vc.Pool == null) return false;
            Sprite first = spec.Anim != null ? spec.Anim.At(0f) : spec.Sprite;
            if (first == null) return false;
            float life = spec.Life;
            if (life <= 0f) life = spec.Anim != null && spec.Anim.Duration > 0f ? spec.Anim.Duration : SimConst.TickSeconds;
            if (spec.ScaleFrom == Vector2.zero) spec.ScaleFrom = Vector2.one;
            if (spec.ScaleTo == Vector2.zero) spec.ScaleTo = spec.ScaleFrom;
            float alpha = spec.Color.a;
            int handle = 0;
            float occR = 0f;
            if (spec.CountsOcclusion && _s.Occlusion != null)
            {
                occR = spec.OcclusionRadius > 0f ? spec.OcclusionRadius : RadiusOf(first, Vector2.Max(spec.ScaleFrom, spec.ScaleTo));
                alpha = _s.Occlusion.Admit(spec.SimPos, occR, alpha);
                if (alpha <= 0f) return false;
                handle = _s.Occlusion.Register(spec.SimPos, occR, alpha);
            }
            int addMax = _s.Data.Tuning.I("additive_max_px");
            var mat = EffectOptionRules.MaterialFor(Mathf.RoundToInt(first.rect.width), Mathf.RoundToInt(first.rect.height), spec.Additive, addMax);
            var sr = vc.Pool.Rent(first, spec.SortingOrder, mat);
            if (sr == null) { if (handle != 0) _s.Occlusion.Unregister(handle); return false; }
            if (spec.TiledLength > 0f)
            {
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(spec.TiledLength, spec.TiledHeight > 0f ? spec.TiledHeight : first.rect.height / SimConst.PixelsPerUnit);
            }
            sr.flipX = spec.FlipX;
            if (spec.OutlinePx > 0f)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(SpriteShaderProps.OutlineColor, spec.OutlineColor);
                mpb.SetFloat(SpriteShaderProps.OutlineSize, spec.OutlinePx);
                sr.SetPropertyBlock(mpb);
            }
            var fx = new Fx { Sr = sr, Spec = spec, Life = life, Alpha = alpha, OccHandle = handle, OccRadius = occR, Pos = spec.SimPos };
            _live.Add(fx);
            Apply(fx);
            return true;
        }

        static float RadiusOf(Sprite s, Vector2 scale)
        {
            float w = s.rect.width / SimConst.PixelsPerUnit * Mathf.Abs(scale.x);
            float h = s.rect.height / SimConst.PixelsPerUnit * Mathf.Abs(scale.y);
            return Mathf.Max(w, h) * 0.5f;
        }

        public void Update(float dt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var f = _live[i];
                f.Age += dt;
                if (f.Age >= f.Life) { Kill(i); continue; }
                if (f.Spec.Follow != null)
                {
                    var p = f.Spec.Follow();
                    if (p.HasValue) f.Pos = p.Value;
                }
                Apply(f);
            }
        }

        void Apply(Fx f)
        {
            var vc = _s.Vc;
            float k = f.Life > 0f ? Mathf.Clamp01(f.Age / f.Life) : 1f;
            if (f.Spec.Anim != null)
            {
                var spr = f.Spec.Anim.At(f.Age);
                if (spr != null) f.Sr.sprite = spr;
            }
            var t = f.Sr.transform;
            if (vc != null) t.localPosition = vc.ToView(f.Pos);
            Vector2 sc = Vector2.LerpUnclamped(f.Spec.ScaleFrom, f.Spec.ScaleTo, k);
            t.localScale = new Vector3(sc.x, sc.y, 1f);
            t.localRotation = Quaternion.Euler(0f, 0f, f.Spec.RotationDeg);
            float a = f.Alpha;
            if (f.Spec.FadeOut > 0f)
            {
                float left = f.Life - f.Age;
                if (left < f.Spec.FadeOut) a *= Mathf.Clamp01(left / f.Spec.FadeOut);
            }
            if (f.OccHandle != 0 && _s.Occlusion != null)
            {
                float cap = _s.Occlusion.CapAt(f.Pos);
                if (a > cap) a = cap;
                _s.Occlusion.UpdateLive(f.OccHandle, f.Pos, f.OccRadius, a);
            }
            var c = f.Spec.Color;
            c.a = a;
            f.Sr.color = c;
        }

        void Kill(int i)
        {
            var f = _live[i];
            _live.RemoveAt(i);
            if (f.OccHandle != 0 && _s.Occlusion != null) _s.Occlusion.Unregister(f.OccHandle);
            if (f.Sr != null && _s.Vc != null && _s.Vc.Pool != null) _s.Vc.Pool.Return(f.Sr);
        }

        public void DisposeAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--) Kill(i);
        }
    }
}
