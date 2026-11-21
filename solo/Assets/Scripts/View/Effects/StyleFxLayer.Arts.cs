using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Effects
{
    // 고유 비기 강화의 단계(ArtStage, Docs/12-attack-styles.md 4절)와 큰 순간의 짧은 화면 어둡힘.
    // 불가마: 땅 내리침 + 불기둥 고리 시간차 / 벼락 질주: 궤적 위 예고 → 낙뢰 / 베껴 쓰기: 책이 촤라락 펼쳐지고 책장 소용돌이 /
    // 얼음 병풍: 내리침 + 얼음 가시가 줄지어 솟음·버팀·깨짐 / 그림자 손: 그림자 웅덩이 → 거대한 손이 솟아 움켜쥠 → 짓누름.
    public sealed partial class StyleFxLayer
    {
        sealed class Vortex
        {
            public int Slot;
            public Vector2 Center;
            public float Radius, LastPulse;
            public Bit Book, Swirl, Under;
        }

        sealed class Hand
        {
            public int Slot;
            public Vector2 Center;
            public float Radius, Since, PoolScale;
            public Bit Pool, Grip;
        }

        readonly Dictionary<int, Vortex> _vortex = new Dictionary<int, Vortex>();
        readonly Dictionary<int, Hand> _hands = new Dictionary<int, Hand>();

        void ClearArtHolds()
        {
            _vortex.Clear();
            _hands.Clear();
        }

        void OnArtStage(SimWorld w, in SimEvent e)
        {
            var kind = (ArtStageKind)e.A;
            switch (e.Id)
            {
                case Ids.Art.Kiln: if (kind == ArtStageKind.Rise) KilnRing(w, e); break;
                case Ids.Art.BoltDash:
                    if (kind == ArtStageKind.Warn) BoltWarn(e);
                    else if (kind == ArtStageKind.Land) BoltLand(e);
                    break;
                case Ids.Art.CopyCircle:
                    if (kind == ArtStageKind.Pulse) CopyPulse(w, e);
                    else if (kind == ArtStageKind.End) CopyEnd(e);
                    break;
                case Ids.Art.IceScreen: if (kind == ArtStageKind.Rise) IceSpike(w, e); break;
                case Ids.Art.ShadowHand:
                    if (kind == ArtStageKind.Grip) HandGrip(e);
                    else if (kind == ArtStageKind.Crush) HandCrush(e);
                    break;
            }
        }

        // ------------------------------------------------------------ 땅 내리침(공용: 충격 고리·바닥 금·먼지 치마·흙 파편)

        void Slam(Vector2 at, float radius, Color tint, float debrisMul)
        {
            var ring = A(GCommon, "ground_slam");
            Play(ring, at, radius * 2f / Mathf.Max(0.1f, NativeW(ring)) * Or(S("slam", "ring_fit"), 1.1f), SortBands.TelegraphFill - 3, tint);
            var crack = A(GCommon, "ground_crack");
            if (crack != null)
            {
                float cs = radius * 2f / Mathf.Max(0.1f, NativeW(crack)) * Or(S("slam", "crack_fit"), 0.9f);
                float hold = Or(S("slam", "crack_hold_seconds"), 0.6f);
                var c = Spawn(crack, null, at, crack.Duration + hold, cs, cs, WithA(Color.Lerp(Color.white, tint, 0.3f), Or(S("slam", "crack_alpha"), 0.9f)), SortBands.TelegraphFill - 4, false);
                if (c != null) { c.FadeOut = hold * 0.8f; ApplyBit(c); }
            }
            var dust = A(GCommon, "dust_skirt");
            Play(dust, at, radius * 2f / Mathf.Max(0.1f, NativeW(dust)) * Or(S("slam", "dust_fit"), 1.25f), SortBands.TelegraphFill - 2, WithA(tint, 0.9f));
            var debris = A(GCommon, "debris_burst");
            int n = Mathf.Max(0, Mathf.RoundToInt(Or(S("slam", "debris"), 6f) * debrisMul * Mathf.Max(Level, 0.34f)));
            for (int i = 0; i < n; i++)
            {
                float ang = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / Mathf.Max(1, n);
                Vector2 p = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius * Random.Range(0.45f, 0.95f);
                var d = Play(debris, p, Random.Range(0.8f, 1.15f), SortBands.Bursts + 2, tint, 0f, true);
                if (d != null) { d.Delay = Random.Range(0f, 0.06f); ApplyBit(d); }
            }
            Sparks(ParticleBudgetConfig.CatArt, FxPriority.Crit, Count(Or(S("slam", "chips"), 14f), FxPriority.Crit), at, Vector2.zero, 180f, radius * 1.2f, radius * 3f,
                0.55f, 0.1f, Color.Lerp(Col("earth", new Color(0.62f, 0.5f, 0.38f)), tint, 0.3f), Col("earth_dark", new Color(0.36f, 0.29f, 0.22f)), 2.5f,
                ProcSprites.HardDot, null, 2f, 5f, 18f, false, radius * 0.4f, false);
        }

        /// <summary>큰 순간(비기 절정): 방향 흔들림 + 그림 정지 + 줌 펀치 + 짧은 어둡힘.</summary>
        void BigMoment(string art, Vector2 viewDir)
        {
            Impact(Or(S("art", art, "shake_px"), 9f), viewDir, FxPriority.Crit);
            HitStop(S("art", art, "hitstop"), FxPriority.Crit);
            Punch(S("art", art, "punch"));
            Dim(Or(S("art", art, "dim_alpha"), S("dim", "alpha")), Or(S("dim", "seconds"), 0.3f));
        }

        // ------------------------------------------------------------ 불가마: 내리침 + 불기둥 고리(시간차)

        void KilnRing(SimWorld w, in SimEvent e)
        {
            float inner = e.Pos2.x, outer = e.F;
            Color fireTint = Col("kiln_tint", new Color(1f, 0.82f, 0.62f));
            if (e.B == 0)
            {
                Slam(e.Pos, Mathf.Max(1.2f, Or(S("art", "kiln", "slam_radius"), 2.4f)), fireTint, 1f);
                Glow(e.Pos, 1f, Or(S("art", "kiln", "glow_cells"), 5.5f), 0.3f, WithA(ElSecondary(Ids.Element.Fire), 0.65f), SortBands.Bursts + 1);
                Ring(e.Pos, 0.4f, 3.2f, 0.4f, WithA(ElSecondary(Ids.Element.Fire), 0.9f));
                BigMoment("kiln", new Vector2(0f, -1f));
                Note("kiln_slam");
            }
            var pillar = A(G1, "fire_pillar");
            if (pillar == null) return;
            float mid = (inner + outer) * 0.5f;
            // 첫 고리(반경 0~1)의 기둥이 내 캐릭터를 가리지 않게 고리 바깥 가장자리 쪽에 세운다
            float minMid = Or(S("art", "kiln", "inner_pillar_cells"), 0.9f);
            if (mid < minMid) mid = minMid;
            float spacing = Or(S("art", "kiln", "pillar_spacing"), 0.85f);
            int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * mid / spacing), 5, 16);
            float sc = Or(S("art", "kiln", "pillar_scale"), 0.8f) * (1f + 0.15f * e.B);
            float stagger = Or(S("art", "kiln", "pillar_stagger"), 0.012f);
            float phase = e.B * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float ang = (i + phase) * Mathf.PI * 2f / n;
                Vector2 p = e.Pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * mid;
                var b = Spawn(pillar, null, p, pillar.Duration, sc, sc, Color.white, SortBands.Bodies + SortBands.YOrder(p.y) + 1, false);
                if (b == null) continue;
                b.Delay = i * stagger * Random.Range(0.6f, 1.4f);
                b.Sy0 = sc * 0.4f;
                b.Sy1 = sc * Random.Range(0.95f, 1.15f);
                b.FlipX = (i & 1) == 1;
                b.FadeOut = 0.1f;
                ApplyBit(b);
                if ((i & 1) == 0) Ember(p, 0.16f, 3.5f);
            }
            Impact(Or(S("art", "kiln", "ring_shake_px"), 4f), new Vector2(0f, 1f), FxPriority.Crit);
            Note("kiln_ring" + e.B);
        }

        // ------------------------------------------------------------ 벼락 질주: 궤적 위 예고 → 낙뢰

        void BoltWarn(in SimEvent e)
        {
            var mark = A(G2, "strike_marker");
            float wait = Mathf.Max(SimConst.TickSeconds, e.Pos2.x * SimConst.TickSeconds);
            float ms = e.F * 2f / Mathf.Max(0.1f, NativeW(mark)) * 1.2f;
            var mb = Spawn(mark, null, e.Pos, wait + 0.03f, ms * 1.6f, ms * 1.6f, WithA(Color.white, 0.85f), SortBands.PlayerZones + 15, true);
            if (mb != null) { mb.Sx1 = mb.Sy1 = ms; mb.FadeIn = 0.03f; ApplyBit(mb); }
            Note("bolt_warn");
        }

        void BoltLand(in SimEvent e)
        {
            var bolt = A(G2, "strike_bolt");
            var bb = Play(bolt, e.Pos, Or(S("art", "bolt_dash", "bolt_scale"), 1.25f), SortBands.Bursts + 6, Color.white, 0f, false);
            if (bb != null) { bb.Sx0 *= 1.5f; ApplyBit(bb); }
            var flat = A(G2, "strike_blast");
            Play(flat, e.Pos, e.F * 2f / Mathf.Max(0.1f, NativeW(flat)) * 1.4f, SortBands.Bursts + 4, Color.white);
            Glow(e.Pos, 0.5f, e.F * 3f, 0.18f, WithA(ElSecondary(Ids.Element.Lightning), 0.75f), SortBands.Bursts + 3);
            Sparks(ParticleBudgetConfig.CatArt, FxPriority.Crit, Count(8f, FxPriority.Crit), e.Pos, Vector2.zero, 180f, 3f, 7f, 0.25f, 0.08f, Color.white,
                ElSecondary(Ids.Element.Lightning), 6f, null, null, 1f, 4f, 10f, true);
            Impact(Or(S("art", "bolt_dash", "land_shake_px"), 6f), new Vector2(0f, -1f), FxPriority.Crit);
            if (e.B >= 4)
            {
                HitStop(S("art", "bolt_dash", "hitstop"), FxPriority.Crit);
                Punch(S("art", "bolt_dash", "punch"));
                Dim(S("art", "bolt_dash", "dim_alpha"), Or(S("dim", "seconds"), 0.3f) * 0.7f);
            }
            else Punch(S("art", "bolt_dash", "punch") * 0.3f);
            Note("bolt_land" + e.B);
        }

        // ------------------------------------------------------------ 베껴 쓰기: 책 촤라락 → 책장 소용돌이

        void CopyPulse(SimWorld w, in SimEvent e)
        {
            Vortex v;
            if (!_vortex.TryGetValue(e.Slot, out v))
            {
                v = new Vortex { Slot = e.Slot, Center = e.Pos, Radius = e.F };
                // 책이 펼쳐진다(마지막 장 = 펼친 책) → 책장 넘김 반복. 마법진 위에 떠 있다.
                var open = A(G3, "book_open");
                var flip = Retime(A(G3, "book_flip"), Or(S("art", "copy", "flip_fps"), 14f), true);
                float bs = Or(S("art", "copy", "book_scale"), 1.6f);
                float lift = Or(S("art", "copy", "book_lift_cells"), 0.55f);
                var ob = Spawn(open, null, e.Pos, open != null ? open.Duration : 0.4f, bs * 0.6f, bs * 0.6f, Color.white, SortBands.Bursts + 6, false);
                if (ob != null) { ob.Sx1 = ob.Sy1 = bs; ob.Lift = lift; ob.FadeOut = 0.001f; ApplyBit(ob); }
                v.Book = Hold(flip, null, e.Pos, bs, bs, Color.white, SortBands.Bursts + 6, false);
                if (v.Book != null) { v.Book.Delay = open != null ? open.Duration : 0f; v.Book.Lift = lift; ApplyBit(v.Book); }
                var swirl = Retime(A(G3, "page_vortex"), Or(S("art", "copy", "vortex_fps"), 14f), true);
                float vs = e.F * 2f / Mathf.Max(0.1f, NativeW(swirl)) * Or(S("art", "copy", "vortex_fit"), 1.1f);
                // 소용돌이는 땅(바닥 띠 맨 위)에 깐다 — 적·내 캐릭터는 그 위로 보이고, 책장 입자만 위로 흩날린다.
                v.Swirl = Hold(swirl, null, e.Pos, vs, vs * 0.85f, WithA(Color.white, Or(S("art", "copy", "vortex_alpha"), 0.8f)), SortBands.TelegraphFill - 2, true);
                if (v.Swirl != null) { v.Swirl.FadeIn = 0.2f; v.Swirl.RotVel = Or(S("art", "copy", "vortex_spin"), -40f); }
                _vortex[e.Slot] = v;
                // 펼치는 순간: 책장이 바깥으로 촤라락
                PageBurst(e.Pos, e.F * 0.5f, Mathf.RoundToInt(Or(S("art", "copy", "open_pages"), 14f)), false);
                Glow(e.Pos, 0.6f, 3.2f, 0.3f, WithA(ElSecondary(Ids.Element.Arcane), 0.6f), SortBands.Bursts + 2);
                BigMoment("copy", new Vector2(0f, 1f));
                Note("copy_open");
            }
            v.Center = e.Pos;
            v.Radius = e.F;
            v.LastPulse = _clock;
            // 맥박: 둘레에서 책장이 소용돌이치며 가운데로 빨려 든다 + 고리가 오므라든다
            PageBurst(e.Pos, e.F, Mathf.RoundToInt(Or(S("art", "copy", "pages_per_pulse"), 10f)), true);
            Ring(e.Pos, e.F * 1.05f, e.F * 0.25f, 0.35f, WithA(ElSecondary(Ids.Element.Arcane), 0.75f));
            Impact(Or(S("art", "copy", "pulse_shake_px"), 2f), Vector2.zero, FxPriority.Decor);
            Punch(S("art", "copy", "pulse_punch"));
            Note("copy_pulse");
        }

        /// <summary>책장 n장: inward = 둘레(radius)에서 소용돌이치며 가운데로 / 아니면 가운데에서 바깥으로 흩날림.</summary>
        void PageBurst(Vector2 center, float radius, int n, bool inward)
        {
            var big = A(GReuse, "page_large");
            var small = A(GReuse, "page_small");
            var scattered = A("special", "scattered_pages");
            n = Mathf.Max(0, Mathf.RoundToInt(n * Mathf.Max(Level, 0.4f)));
            float spin = Or(S("art", "copy", "swirl_deg"), 70f);
            for (int i = 0; i < n; i++)
            {
                var anim = (i % 3) == 0 ? big : (i % 3) == 1 ? small : scattered;
                if (anim == null) anim = big ?? small ?? scattered;
                if (anim == null) return;
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector2 radial = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 tang = new Vector2(-radial.y, radial.x);
                float life = Random.Range(0.45f, 0.7f);
                Vector2 at = inward ? center + radial * radius * Random.Range(0.95f, 1.2f) : center + radial * Random.Range(0f, 0.3f);
                float sc = Random.Range(1.1f, 1.6f);
                var b = Spawn(Retime(anim, Random.Range(10f, 18f), true), null, at, life, sc, sc, Color.white, SortBands.Bursts + 7, false,
                    ParticleBudgetConfig.CatArt, FxPriority.Crit);
                if (b == null) break;
                float sp = inward ? radius / life * 1.4f : Random.Range(3f, 6f);
                Vector2 dir = inward ? (-radial * Mathf.Cos(spin * Mathf.Deg2Rad) + tang * Mathf.Sin(spin * Mathf.Deg2Rad)) : (radial * 0.85f + tang * 0.5f);
                b.Vel = dir.normalized * sp;
                b.Drag = inward ? 0.6f : 2.2f;
                b.Lift = Random.Range(0.1f, 0.9f);
                b.LiftVel = inward ? Random.Range(-0.3f, 0.6f) : Random.Range(1.5f, 3.5f);
                b.LiftGrav = inward ? 0f : 6f;
                b.RotVel = Random.Range(-540f, 540f);
                b.Sx1 = b.Sy1 = sc * (inward ? 0.35f : 0.7f);
                b.FadeOut = life * 0.4f;
                ApplyBit(b);
            }
        }

        void CopyEnd(in SimEvent e)
        {
            Vortex v;
            if (_vortex.TryGetValue(e.Slot, out v))
            {
                Release(v.Book, 0.25f);
                Release(v.Swirl, 0.3f);
                Release(v.Under, 0.3f);
                _vortex.Remove(e.Slot);
            }
            PageBurst(e.Pos, e.F, Mathf.RoundToInt(Or(S("art", "copy", "end_pages"), 12f)), false);
            Note("copy_end");
        }

        // ------------------------------------------------------------ 얼음 병풍: 내리침 + 얼음 가시 줄(솟음·버팀·깨짐)

        void IceSpike(SimWorld w, in SimEvent e)
        {
            Color ice = Col("ice_tint", new Color(0.82f, 0.94f, 1f));
            Vector2 dir = e.Pos2.sqrMagnitude > 1e-6f ? e.Pos2.normalized : Vector2.right;
            if (e.B == 0)
            {
                Slam(e.Pos - dir * Or(S("art", "ice", "slam_back_cells"), 0.9f), Or(S("art", "ice", "slam_radius"), 1.6f), ice, 0.7f);
                Glow(e.Pos, 0.6f, 3.5f, 0.25f, WithA(ElSecondary(Ids.Element.Ice), 0.6f), SortBands.Bursts + 1);
                BigMoment("ice", SimConst.DirToView(dir) * 0.6f + new Vector2(0f, -0.8f));
                Note("ice_slam");
            }
            float sc = Or(S("art", "ice", "spike_scale"), 1.35f) * Mathf.Sqrt(Mathf.Max(1f, e.F / 0.8f));
            var rise = Clip(G4, "ice_spike", "rise", Or(S("art", "ice", "rise_fps"), 20f));
            var hold = Clip(G4, "ice_spike", "hold", Or(S("art", "ice", "hold_fps"), 8f), true);
            var brk = Clip(G4, "ice_spike", "break", Or(S("art", "ice", "break_fps"), 14f));
            float tRise = rise != null ? rise.Duration : 0.15f;
            float tHold = Or(S("art", "ice", "hold_seconds"), 0.5f);
            int order = SortBands.Bodies + SortBands.YOrder(e.Pos.y) + 2;
            bool flip = (e.B & 1) == 1;
            var r = Spawn(rise, null, e.Pos, tRise, sc, sc * 0.6f, Color.white, order, false);
            if (r != null) { r.Sy1 = sc; r.FlipX = flip; r.FadeOut = 0.001f; ApplyBit(r); }
            var h = Spawn(hold, null, e.Pos, tHold, sc, sc, Color.white, order, false);
            if (h != null) { h.Delay = tRise; h.FlipX = flip; h.FadeOut = 0.001f; ApplyBit(h); }
            var b = Spawn(brk, null, e.Pos, brk != null ? brk.Duration + 0.05f : 0.2f, sc, sc, Color.white, order, false);
            if (b != null) { b.Delay = tRise + tHold; b.FlipX = flip; b.FadeOut = 0.08f; ApplyBit(b); }
            var debris = A(GCommon, "debris_burst");
            Play(debris, e.Pos, 0.9f, SortBands.Bursts + 2, ice, 0f, true);
            Sparks(ParticleBudgetConfig.CatArt, FxPriority.Crit, Count(6f, FxPriority.Crit), e.Pos, Vector2.zero, 180f, 1.5f, 3.5f, 0.45f, 0.14f, Color.white, Color.white, 2.5f,
                null, A("special", "ice_screen_shard"), 2f, 4.5f, 14f, false, 0.2f, false);
            Ring(e.Pos, 0.1f, e.F + 0.3f, 0.2f, WithA(ElSecondary(Ids.Element.Ice), 0.8f));
            if (e.B > 0) Impact(Or(S("art", "ice", "spike_shake_px"), 3f), new Vector2(0f, 1f), FxPriority.Crit);
            Note("ice_spike" + e.B);
        }

        // ------------------------------------------------------------ 그림자 손: 웅덩이 → 솟아 움켜쥠 → 짓누름

        void HandGrip(in SimEvent e)
        {
            Hand old;
            if (_hands.TryGetValue(e.Slot, out old)) { Release(old.Pool, 0.1f); Release(old.Grip, 0.05f); }
            var h = new Hand { Slot = e.Slot, Center = e.Pos, Radius = e.F, Since = _clock };
            var pool = Retime(A("special", "shadow_pool"), 10f, true);
            // 움켜쥠의 반경(e.F)은 원래 비기의 끌어당김 범위라 크다 → 웅덩이는 짓누름 반경에 맞춘 고정 지름(pool_cells)
            float ps = Or(S("art", "hand", "pool_cells"), 4.4f) / Mathf.Max(0.1f, NativeW(pool));
            h.PoolScale = ps;
            h.Pool = Hold(pool, null, e.Pos, ps * 0.4f, ps * 0.22f, WithA(Color.white, Or(S("art", "hand", "pool_alpha"), 0.85f)), SortBands.TelegraphFill - 6, false);
            if (h.Pool != null) h.Pool.FadeIn = 0.1f;
            float hs = Or(S("art", "hand", "hand_scale"), 1.35f);
            var rise = Clip(G5, "giant_hand", "rise", Or(S("art", "hand", "rise_fps"), 14f));
            var grip = Clip(G5, "giant_hand", "grip", Or(S("art", "hand", "grip_fps"), 9f));
            float tRise = rise != null ? rise.Duration : 0.14f;
            int order = SortBands.Bursts + 5;
            // 손은 이 비기의 주인공 그림 — 누적 가림 셈으로 버려지지 않게 셈 밖(불투명한 몸 그림이고 0.5초 남짓)
            var r = Spawn(rise, null, e.Pos, tRise, hs, hs * 0.3f, Color.white, order, false);
            if (r != null) { r.Sy1 = hs; r.FadeOut = 0.001f; ApplyBit(r); }
            h.Grip = Hold(grip, null, e.Pos, hs, hs, Color.white, order, false);
            if (h.Grip != null) { h.Grip.Delay = tRise; ApplyBit(h.Grip); }
            _hands[e.Slot] = h;
            Sparks(ParticleBudgetConfig.CatArt, FxPriority.Crit, Count(10f, FxPriority.Crit), e.Pos, Vector2.zero, 180f, 1f, 2.5f, 0.5f, 0.12f, ElPrimary(Ids.Element.Dark),
                ElSecondary(Ids.Element.Dark), 2f, null, null, 1.5f, 3.5f, 4f, false, e.F * 0.6f);
            Impact(Or(S("art", "hand", "grip_shake_px"), 3f), new Vector2(0f, 1f), FxPriority.Crit);
            Note("hand_grip");
        }

        void HandCrush(in SimEvent e)
        {
            Hand h;
            float hs = Or(S("art", "hand", "hand_scale"), 1.35f) * Mathf.Sqrt(Mathf.Max(1f, e.F / 2.2f));
            if (_hands.TryGetValue(e.Slot, out h))
            {
                Release(h.Grip, 0.02f);
                Release(h.Pool, 0.45f);
                _hands.Remove(e.Slot);
            }
            var slam = Clip(G5, "giant_hand", "slam", Or(S("art", "hand", "slam_fps"), 16f));
            var fade = Clip(G5, "giant_hand", "fade", 8f);
            float tSlam = slam != null ? slam.Duration : 0.12f;
            var s = Spawn(slam, null, e.Pos, tSlam, hs * 1.08f, hs * 1.08f, Color.white, SortBands.Bursts + 5, false);
            if (s != null) { s.Sy0 = hs * 1.2f; s.Sy1 = hs * 0.95f; s.FadeOut = 0.001f; ApplyBit(s); }
            var f = Spawn(fade, null, e.Pos, Or(S("art", "hand", "fade_seconds"), 0.3f), hs, hs * 0.95f, Color.white, SortBands.Bursts + 5, false);
            if (f != null) { f.Delay = tSlam; f.FadeOut = 0.25f; ApplyBit(f); }
            Color dark = Col("hand_tint", new Color(0.72f, 0.55f, 1f));
            Slam(e.Pos, e.F, dark, 1f);
            Glow(e.Pos, 1f, e.F * 2.6f, 0.3f, WithA(ElSecondary(Ids.Element.Dark), 0.65f), SortBands.Bursts + 1);
            Ring(e.Pos, e.F * 0.2f, e.F * 1.3f, 0.35f, WithA(ElSecondary(Ids.Element.Dark), 0.9f));
            Sparks(ParticleBudgetConfig.CatArt, FxPriority.Crit, Count(14f, FxPriority.Crit), e.Pos, Vector2.zero, 180f, e.F * 1.5f, e.F * 3.5f, 0.4f, 0.12f,
                ElSecondary(Ids.Element.Dark), Color.white, 3f, null, null, 1f, 3f, 12f);
            BigMoment("hand", new Vector2(0f, -1f));
            Note("hand_crush");
        }

        /// <summary>남은 것 정리(소용돌이가 끝 이벤트 없이 사라진 경우 — 판 끝·보조 판).</summary>
        void SyncArtHolds(SimWorld w, float dt)
        {
            if (_vortex.Count > 0)
            {
                int gone = -1;
                foreach (var kv in _vortex)
                {
                    var v = kv.Value;
                    if (v.Swirl != null) { v.Swirl.Pos = v.Center; }
                    if (_clock - v.LastPulse > Or(S("art", "copy", "orphan_seconds"), 1.5f)) gone = kv.Key;
                }
                if (gone >= 0)
                {
                    var v = _vortex[gone];
                    Release(v.Book, 0.2f); Release(v.Swirl, 0.25f); Release(v.Under, 0.25f);
                    _vortex.Remove(gone);
                }
            }
            if (_hands.Count > 0)
            {
                int gone = -1;
                foreach (var kv in _hands)
                {
                    var h = kv.Value;
                    if (_clock - h.Since > 2f) gone = kv.Key;
                    if (h.Pool != null && h.Pool.Persistent)
                    {
                        // 웅덩이가 0.15초에 걸쳐 펼쳐진다
                        h.Pool.Sx0 = Mathf.MoveTowards(h.Pool.Sx0, h.PoolScale, h.PoolScale * 4f * dt);
                        h.Pool.Sy0 = h.Pool.Sx0 * 0.55f;
                    }
                }
                if (gone >= 0) { var h = _hands[gone]; Release(h.Pool, 0.2f); Release(h.Grip, 0.1f); _hands.Remove(gone); }
            }
        }

        // ------------------------------------------------------------ 짧은 화면 어둡힘(땅·내 장판만 — 적·예고·탄은 그대로 밝다)

        SpriteRenderer _dim;
        float _dimAge = 10f, _dimLife, _dimAlpha;

        void BuildDim() { _dimAge = 10f; }

        void DisposeDim()
        {
            if (_dim != null && _pool != null) _pool.Return(_dim);
            _dim = null;
        }

        /// <summary>
        /// 바닥 띠(예고 채움 아래)만 덮는 어두운 막. 적 몸·적 탄·적 예고·내 캐릭터는 덮지 않아 가독성이 떨어지지 않는다.
        /// 연출 감도 최소 = 없음, 줄임 = 절반, 섬광 줄이기 = 절반, 섬광 배율을 곱한다.
        /// </summary>
        void Dim(float alpha, float seconds)
        {
            if (alpha <= 0f || seconds <= 0f || _s.EffectsLevel >= 2) return;
            float a = alpha * (_s.EffectsLevel == 1 ? 0.5f : 1f) * Mathf.Clamp01(_s.Options.FlashScale);
            if (_s.Options.ReduceFlash) a *= 0.5f;
            if (a <= 0f) return;
            if (_dimAge < _dimLife && _dimAlpha >= a) return;
            _dimAlpha = a;
            _dimLife = seconds;
            _dimAge = 0f;
        }

        public float DimAlpha => _dim != null && _dim.enabled ? _dim.color.a : 0f;

        void UpdateDim(float dt)
        {
            _dimAge += Mathf.Max(dt, Time.unscaledDeltaTime);
            float a = 0f;
            if (_dimAge < _dimLife)
            {
                float rise = Mathf.Min(0.04f, _dimLife * 0.2f);
                a = _dimAge < rise ? _dimAlpha * (_dimAge / rise) : _dimAlpha * (1f - (_dimAge - rise) / Mathf.Max(1e-3f, _dimLife - rise));
            }
            if (a <= 0.001f)
            {
                if (_dim != null) _dim.enabled = false;
                return;
            }
            var cam = _vc != null ? _vc.Camera : null;
            if (cam == null || _art == null) return;
            if (_dim == null)
            {
                _dim = _pool.Rent(_art.WhitePixel, SortBands.TelegraphFill - 8);
                if (_dim == null) return;
            }
            _dim.enabled = true;
            float h = cam.orthographicSize * 2f + 2f, w = h * cam.aspect + 2f;
            Vector3 c = cam.transform.position;
            if (_vc.WorldRoot != null) c = _vc.WorldRoot.InverseTransformPoint(c);
            var t = _dim.transform;
            t.localPosition = new Vector3(c.x, c.y, 0f);
            t.localRotation = Quaternion.identity;
            var sp = _dim.sprite;
            float sw = sp != null ? sp.bounds.size.x : 1f, sh = sp != null ? sp.bounds.size.y : 1f;
            t.localScale = new Vector3(w / Mathf.Max(1e-4f, sw), h / Mathf.Max(1e-4f, sh), 1f);
            var col = Col("dim", new Color(0.04f, 0.05f, 0.09f));
            col.a = a;
            _dim.color = col;
        }
    }
}
