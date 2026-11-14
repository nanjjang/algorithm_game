using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 입자 그리기(본체에서 떨어져 혼자 움직이다 사라지는 조각 — 10 3.3절). 모든 입자는 ParticleBudget에서 표를 받아야 그린다.
    /// 예산이 내보낸 입자는 one_shot_fade_seconds(0.1초)에 흐려지며 사라진다.
    /// </summary>
    public sealed class FxParticles
    {
        sealed class P
        {
            public SpriteRenderer Sr;
            public int Token;
            public Vector2 Pos, Vel;
            public float Age, Life, Alpha;
            public Color Color;
            public float Fade = -1f;
        }

        readonly EffectsShared _s;
        readonly List<P> _live = new List<P>();
        readonly Dictionary<int, P> _byToken = new Dictionary<int, P>();
        readonly List<int> _evicted = new List<int>();

        public FxParticles(EffectsShared shared) { _s = shared; }

        public int LiveCount => _live.Count;

        /// <summary>입자 하나. 예산이 안 주면 false(그리지 않는다).</summary>
        public bool Spawn(string category, Vector2 simPos, Vector2 velCellsPerSec, float life, Color color, float sizePx, int sortingOrder,
            Sprite sprite = null, bool additive = false, Color? outline = null, float outlinePx = 0f)
        {
            if (_s == null || _s.Budget == null || life <= 0f) return false;
            // 프레임당 입자 상한(우선순위 낮은 분류부터 버린다)
            if (_s.Governor != null && !_s.Governor.AllowParticles(FxGovernor.PriorityOf(category))) return false;
            int token;
            if (!_s.Budget.TryAcquire(category, _s.Time, life, out token)) return false;
            var vc = _s.Vc;
            if (vc == null || vc.Pool == null) { _s.Budget.Release(token); return false; }
            var art = vc.Game != null ? vc.Game.Art : null;
            int r = Mathf.Max(1, Mathf.RoundToInt(sizePx * 0.5f));
            var spr = sprite != null ? sprite : (art != null ? art.Circle(r) : null);
            int addMax = _s.Data.Tuning.I("additive_max_px");
            var mat = EffectOptionRules.MaterialFor(Mathf.RoundToInt(sizePx), Mathf.RoundToInt(sizePx), additive, addMax);
            var sr = vc.Pool.Rent(spr, sortingOrder, mat);
            if (sr == null) { _s.Budget.Release(token); return false; }
            if (sprite != null && spr != null)
            {
                // 그림 자신의 PPU로 칸 크기를 구한다(WhitePixel은 PPU 1, 효과 그림은 64).
                float nativeUnits = Mathf.Max(spr.rect.width, spr.rect.height) / Mathf.Max(1e-3f, spr.pixelsPerUnit);
                float k = nativeUnits > 0f ? (sizePx / SimConst.PixelsPerUnit) / nativeUnits : 1f;
                sr.transform.localScale = new Vector3(k, k, 1f);
            }
            if (outline.HasValue && outlinePx > 0f)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(SpriteShaderProps.OutlineColor, outline.Value);
                mpb.SetFloat(SpriteShaderProps.OutlineSize, outlinePx);
                sr.SetPropertyBlock(mpb);
            }
            // 환경 입자 밖은 내 이펙트 → '내 이펙트 불투명도'
            float alpha0 = category == ParticleBudgetConfig.CatEnvironment ? color.a : ReadabilityOptions.MyFxAlpha(color.a, _s.MyFxAlpha);
            var p = new P { Sr = sr, Token = token, Pos = simPos, Vel = velCellsPerSec, Life = life, Color = color, Alpha = alpha0 };
            var c0 = color; c0.a = alpha0;
            sr.color = c0;
            sr.transform.localPosition = vc.ToView(simPos);
            _live.Add(p);
            _byToken[token] = p;
            return true;
        }

        /// <summary>여러 개를 둥글게 흩뿌린다. 받은 수를 돌려준다.</summary>
        public int Burst(string category, int n, Vector2 simPos, float speedCells, float life, Color color, float sizePx, int sortingOrder,
            Sprite sprite = null, bool additive = false, Color? outline = null, float outlinePx = 0f)
        {
            int got = 0;
            for (int i = 0; i < n; i++)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float sp = speedCells * UnityEngine.Random.Range(0.5f, 1f);
                if (!Spawn(category, simPos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, life, color, sizePx, sortingOrder, sprite, additive, outline, outlinePx)) break;
                got++;
            }
            return got;
        }

        public void Update(float dt)
        {
            if (_s == null || _s.Budget == null) return;
            _evicted.Clear();
            _s.Budget.DrainEvicted(_evicted);
            float fade = _s.Data.Budget.OneShotFadeSeconds;
            for (int i = 0; i < _evicted.Count; i++)
            {
                P p;
                if (_byToken.TryGetValue(_evicted[i], out p) && p.Fade < 0f) p.Fade = fade > 0f ? fade : dt;
            }
            var vc = _s.Vc;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.Age += dt;
                p.Pos += p.Vel * dt;
                float a = p.Alpha * Mathf.Clamp01(1f - p.Age / p.Life);
                bool done = p.Age >= p.Life;
                if (p.Fade >= 0f)
                {
                    p.Fade -= dt;
                    a *= fade > 0f ? Mathf.Clamp01(p.Fade / fade) : 0f;
                    if (p.Fade <= 0f) done = true;
                }
                if (done) { Kill(i); continue; }
                if (p.Sr != null)
                {
                    var c = p.Color;
                    c.a = a;
                    p.Sr.color = c;
                    if (vc != null) p.Sr.transform.localPosition = vc.ToView(p.Pos);
                }
            }
        }

        void Kill(int index)
        {
            var p = _live[index];
            _live.RemoveAt(index);
            _byToken.Remove(p.Token);
            if (_s.Budget != null) _s.Budget.Release(p.Token);
            if (p.Sr != null && _s.Vc != null && _s.Vc.Pool != null) _s.Vc.Pool.Return(p.Sr);
        }

        public void DisposeAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--) Kill(i);
            _live.Clear();
            _byToken.Clear();
        }
    }
}
