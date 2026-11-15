using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Effects
{
    // 기본 공격 유형 15종의 그림(Docs/12-attack-styles.md 2·3절). 수치는 Juice.json styles.
    public sealed partial class StyleFxLayer
    {
        // ------------------------------------------------------------ 화로 포탄: 포물선 비행(상태로)

        sealed class LobView
        {
            public Bit Shell, Shadow, Marker;
            public float TrailAcc;
            public int Stamp;
        }

        readonly Dictionary<int, LobView> _lobs = new Dictionary<int, LobView>();
        readonly List<int> _drop = new List<int>();
        int _stamp;

        void SyncLobs(SimWorld w, float alpha, float dt)
        {
            _stamp++;
            var list = w.Projectiles;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p.Dead || p.Block == null || p.Block.Style == null || p.Block.Style.Kind != AttackBehavior.Lob || p.StylePhase != 0) continue;
                LobView v;
                if (!_lobs.TryGetValue(p.Id, out v)) { v = NewLob(p); if (v == null) continue; _lobs[p.Id] = v; }
                v.Stamp = _stamp;
                var st = p.Block.Style;
                float leg = Mathf.Max(1, p.TimerTick - p.ArmTick);
                float k = Mathf.Clamp01((TickNow(alpha) - p.ArmTick) / leg);
                Vector2 o = p.Origin, tgt = p.TargetPos;
                Vector2 ground = Vector2.Lerp(o, tgt, k);
                float dist = (tgt - o).magnitude;
                float arc = Or(S("lob", "arc_cells"), 1.4f) + dist * S("lob", "arc_per_cell");
                if (p.StepIndex > 0) arc *= Or(S("lob", "bounce_arc_mul"), 0.55f);
                float h = 4f * arc * k * (1f - k);
                Vector2 gv = SimConst.DirToView(tgt - o);
                float dy = gv.y + 4f * arc * (1f - 2f * k);
                float ang = Mathf.Atan2(dy, gv.x) * Mathf.Rad2Deg;
                float area = Mathf.Sqrt(Mathf.Max(1f, p.AreaScale));
                if (v.Shell != null)
                {
                    v.Shell.Pos = ground;
                    v.Shell.Lift = h + Or(S("lob", "hand_lift_cells"), 0.35f) * (1f - k);
                    v.Shell.Rot = ang;
                    float sc = Or(S("lob", "shell_scale"), 1.3f) * area;
                    v.Shell.Sx0 = v.Shell.Sy0 = sc;
                }
                if (v.Shadow != null)
                {
                    v.Shadow.Pos = ground;
                    float hk = arc > 0f ? Mathf.Clamp01(h / arc) : 0f;
                    float sc = Or(S("lob", "shadow_cells"), 0.5f) * (1f - 0.45f * hk) * area / 0.5f;
                    v.Shadow.Sx0 = sc;
                    v.Shadow.Sy0 = sc * 0.5f;
                    v.Shadow.Alpha = Or(S("lob", "shadow_alpha"), 0.38f) * (1f - 0.5f * hk);
                }
                if (v.Marker != null)
                {
                    v.Marker.Pos = tgt;
                    float r = st.BlastRadius * p.AreaScale * Or(S("lob", "marker_radius_mul"), 0.75f);
                    float pulse = 1f + 0.08f * Mathf.Sin(_clock * 18f);
                    float sc = r / 0.5f * pulse * Mathf.Lerp(1.35f, 1f, k);
                    v.Marker.Sx0 = sc;
                    v.Marker.Sy0 = sc * 0.55f;
                    v.Marker.Alpha = Or(S("lob", "marker_alpha"), 0.45f) * Mathf.Clamp01(k * 3f);
                }
                // 불씨 꼬리
                v.TrailAcc += dt;
                float every = Or(S("lob", "trail_every"), 0.03f);
                Color c0 = ElPrimary(Ids.Element.Fire), c1 = ElSecondary(Ids.Element.Fire);
                while (v.TrailAcc >= every)
                {
                    v.TrailAcc -= every;
                    if (Count(1f, FxPriority.Decor) <= 0) continue;
                    float sz = Or(S("lob", "trail_cells"), 0.14f) / 0.5f;
                    var b = Spawn(null, ProcSprites.SoftDot, ground + Random.insideUnitCircle * 0.06f, Or(S("lob", "trail_seconds"), 0.28f), sz, sz,
                        Color.Lerp(c0, c1, Random.value), SortBands.PlayerShots + 1, false, ParticleBudgetConfig.CatShot, FxPriority.Decor, true);
                    if (b == null) break;
                    b.Lift = v.Shell != null ? v.Shell.Lift : h;
                    b.LiftVel = Random.Range(-0.4f, 0.6f);
                    b.Sx1 = b.Sy1 = sz * 0.3f;
                    ApplyBit(b);
                }
            }
            _drop.Clear();
            foreach (var kv in _lobs) if (kv.Value.Stamp != _stamp) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++) { ReturnLob(_lobs[_drop[i]]); _lobs.Remove(_drop[i]); }
        }

        LobView NewLob(ProjectileEntity p)
        {
            var v = new LobView();
            var shell = A(G1, "mortar_shell");
            v.Shell = Hold(shell, null, p.Pos, 1f, 1f, Color.white, SortBands.PlayerShots + 4, false);
            if (_art != null)
            {
                v.Shadow = Hold(null, _art.Circle(32), p.Pos, 1f, 0.5f, Col("shadow", new Color(0.06f, 0.07f, 0.1f)), SortBands.Shadows + 5, false);
                var ring = _art.Circle(32, true, 3);
                v.Marker = Hold(null, ring, p.TargetPos, 1f, 0.55f, WithA(ElSecondary(Ids.Element.Fire), 0.45f), SortBands.PlayerZones + 14, true);
            }
            Note("lob");
            return v;
        }

        void ReturnLob(LobView v)
        {
            Release(v.Shell, 0.03f);
            Release(v.Shadow, 0.05f);
            Release(v.Marker, 0.08f);
        }

        // ------------------------------------------------------------ 바닥에 남는 것: 불바닥·룬 지뢰

        sealed class GroundView
        {
            public int Kind;          // 0 불바닥, 1 룬
            public Bit Main;
            public float Radius, Scale;
            public bool Armed;
            public float EmberAcc;
            public int ShotId;
        }

        readonly Dictionary<int, GroundView> _grounds = new Dictionary<int, GroundView>();

        void OnGroundSpawned(SimWorld w, in SimEvent e)
        {
            GroundView old;
            if (_grounds.TryGetValue(e.EntityId, out old)) { Release(old.Main, 0.06f); _grounds.Remove(e.EntityId); }
            var v = new GroundView { Kind = e.B, Radius = e.F, ShotId = e.EntityId };
            if (e.B == 0)
            {
                var floor = Retime(A(G1, "fire_floor"), Or(S("floor", "fps"), 10f), true);
                float native = NativeW(floor) * Or(S("floor", "fill"), 0.88f);
                v.Scale = e.F * 2f / Mathf.Max(0.1f, native);
                v.Main = Hold(floor, null, e.Pos, v.Scale, v.Scale, WithA(Color.white, Or(S("floor", "alpha"), 0.9f)), SortBands.PlayerZones + 10, true);
                if (v.Main != null) { v.Main.FadeIn = 0.08f; v.Main.Sx0 = v.Scale * 0.6f; }
                Note("fire_floor");
            }
            else
            {
                var rune = A(G3, "rune_trap");
                float cells = Or(S("rune", "cells"), 1.6f) * Mathf.Sqrt(Mathf.Max(1f, e.F / Mathf.Max(0.1f, Or(S("rune", "base_trigger"), 0.6f))));
                v.Scale = cells / Mathf.Max(0.1f, NativeW(rune));
                v.Main = Hold(rune, null, e.Pos, v.Scale, v.Scale, WithA(Color.white, Or(S("rune", "idle_alpha"), 0.55f)), SortBands.PlayerZones + 12, true);
                // 각인: 크게 찍혔다가 자리로(흰 덮개가 빠르게 빠진다)
                var stamp = Spawn(rune, null, e.Pos, Or(S("rune", "engrave_seconds"), 0.16f), v.Scale * 1.7f, v.Scale * 1.7f,
                    WithA(Col("rune_flash", new Color(0.85f, 1f, 0.95f)), 0.9f), SortBands.PlayerZones + 13, false);
                if (stamp != null) { stamp.Sx1 = stamp.Sy1 = v.Scale; ApplyBit(stamp); }
                Sparks(ParticleBudgetConfig.CatShot, FxPriority.Decor, Count(Or(S("rune", "engrave_sparks"), 6f), FxPriority.Decor), e.Pos, Vector2.zero, 180f, 1.2f, 2.6f,
                    0.25f, 0.08f, ElSecondary(Ids.Element.Arcane), Color.white, 6f);
                Note("rune_engrave");
            }
            _grounds[e.EntityId] = v;
        }

        void OnGroundExpired(in SimEvent e)
        {
            GroundView v;
            if (!_grounds.TryGetValue(e.EntityId, out v)) return;
            Release(v.Main, v.Kind == 0 ? Or(S("floor", "fade"), 0.2f) : 0.05f);
            _grounds.Remove(e.EntityId);
        }

        void OnMineArmed(in SimEvent e)
        {
            GroundView v;
            if (_grounds.TryGetValue(e.EntityId, out v)) { v.Armed = true; if (v.Main != null) v.Main.Alpha = 1f; }
            Ring(e.Pos, 0.15f, e.F + Or(S("rune", "arm_ring_pad"), 0.5f), 0.22f, WithA(ElSecondary(Ids.Element.Arcane), 0.8f));
            Glow(e.Pos, 0.3f, 1.2f, 0.18f, WithA(ElSecondary(Ids.Element.Arcane), 0.7f), SortBands.PlayerZones + 13);
            Note("rune_armed");
        }

        void SyncGrounds(SimWorld w, float dt)
        {
            _drop.Clear();
            foreach (var kv in _grounds)
            {
                var v = kv.Value;
                var p = FindShot(v.ShotId);
                if (p == null) { _drop.Add(kv.Key); continue; }
                if (v.Main == null) continue;
                if (v.Kind == 0)
                {
                    // 펼쳐지기 + 일렁임 + 불씨
                    v.Main.Sx0 = Mathf.MoveTowards(v.Main.Sx0, v.Scale, v.Scale * 6f * dt);
                    v.EmberAcc += dt;
                    float every = Or(S("floor", "ember_every"), 0.09f);
                    while (v.EmberAcc >= every)
                    {
                        v.EmberAcc -= every;
                        Vector2 at = p.Pos + new Vector2(Random.Range(-1f, 1f) * v.Radius * 0.8f, Random.Range(-1f, 1f) * v.Radius * 0.4f);
                        Ember(at, 0.12f, Or(S("floor", "ember_lift"), 1.6f));
                    }
                }
                else
                {
                    // 무장되면 맥박, 터지기 0.5초 전부터 빨리 깜빡(속성색 — 위험색 아님)
                    float now = TickNow(_alpha);
                    float left = (p.EndTick - now) * SimConst.TickSeconds;
                    float a = v.Armed ? 0.85f + 0.15f * Mathf.Sin(_clock * 6f) : Or(S("rune", "idle_alpha"), 0.55f);
                    if (v.Armed && left < Or(S("rune", "blink_seconds"), 0.5f)) a = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(_clock * 24f));
                    v.Main.Alpha = a;
                }
            }
            for (int i = 0; i < _drop.Count; i++)
            {
                GroundView v = _grounds[_drop[i]];
                Release(v.Main, v.Kind == 0 ? Or(S("floor", "fade"), 0.2f) : 0.05f);
                _grounds.Remove(_drop[i]);
            }
        }

        /// <summary>위로 올라가는 불씨 하나(장식).</summary>
        void Ember(Vector2 at, float cells, float lift)
        {
            if (Count(1f, FxPriority.Decor) <= 0) return;
            float sz = cells / 0.5f;
            var b = Spawn(null, ProcSprites.SoftDot, at, Random.Range(0.35f, 0.6f), sz, sz, Color.Lerp(ElPrimary(Ids.Element.Fire), ElSecondary(Ids.Element.Fire), Random.value),
                SortBands.Bursts + 8, false, ParticleBudgetConfig.CatEnvironment, FxPriority.Decor, true);
            if (b == null) return;
            b.LiftVel = lift * Random.Range(0.6f, 1.2f);
            b.Vel = new Vector2(Random.Range(-0.3f, 0.3f), 0f);
            b.Sx1 = b.Sy1 = sz * 0.2f;
            ApplyBit(b);
        }

        // ------------------------------------------------------------ 불꽃 방사·눈보라: 쏘는 동안 이어지는 부채꼴(상태로)

        sealed class Stream
        {
            public int Slot;
            public string Block;
            public Bit[] Fan;
            public float LastSeen, Start, Angle, Radius, Half;
            public Vector2 Origin;
            public float PartAcc;
            public int Stamp;
        }

        readonly Dictionary<string, Stream> _streams = new Dictionary<string, Stream>();
        readonly List<string> _dropS = new List<string>();

        static bool IsStream(AttackStyleParams st) => st != null && st.Kind == AttackBehavior.Cone && st.Attached && !st.AngularSweep;

        void SyncStreams(SimWorld w, float alpha, float dt)
        {
            var list = w.Projectiles;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p.Dead || p.Block == null || !IsStream(p.Block.Style)) continue;
                string key = p.OwnerSlot + "|" + p.Block.Id;
                Stream s;
                if (!_streams.TryGetValue(key, out s))
                {
                    s = NewStream(p, key);
                    if (s == null) continue;
                }
                s.LastSeen = _clock;
                var owner = Player(w, p.OwnerSlot);
                s.Origin = owner != null ? ViewMath.Lerp(owner.PrevPos, owner.Pos, alpha) : p.Origin;
                float target = FxUtil.ViewAngle(p.AimDir);
                s.Angle = Mathf.MoveTowardsAngle(s.Angle, target, Or(S("cone", "turn_deg_per_second"), 1100f) * dt);
                s.Radius = p.Block.Style.ConeRadius * p.AreaScale;
                s.Half = p.Block.Style.ConeHalfDegrees;
            }
            _dropS.Clear();
            float grace = Or(S("cone", "grace_seconds"), 0.14f);
            foreach (var kv in _streams)
            {
                var s = kv.Value;
                if (_clock - s.LastSeen > grace) { _dropS.Add(kv.Key); continue; }
                UpdateStream(s, dt);
            }
            for (int i = 0; i < _dropS.Count; i++)
            {
                var s = _streams[_dropS[i]];
                for (int j = 0; j < s.Fan.Length; j++) Release(s.Fan[j], Or(S("cone", "fade_seconds"), 0.1f));
                _streams.Remove(_dropS[i]);
            }
        }

        Stream NewStream(ProjectileEntity p, string key)
        {
            bool fire = p.Block.Id == "block.core.flame_jet";
            var anim = fire ? A(G1, "flame_jet") : A(G4, "blizzard");
            if (anim == null) return null;
            var s = new Stream { Slot = p.OwnerSlot, Block = p.Block.Id, Start = _clock, LastSeen = _clock, Angle = FxUtil.ViewAngle(p.AimDir), Origin = p.Origin };
            int n = Mathf.Max(1, Mathf.RoundToInt(Or(S("cone", fire ? "flame_copies" : "blizzard_copies"), 3f)));
            s.Fan = new Bit[n];
            for (int j = 0; j < n; j++)
            {
                var b = Hold(anim, null, p.Origin, 0.1f, 0.1f, Color.white, SortBands.Bursts + 2 + (j == 0 ? 1 : 0), true);
                if (b != null) { b.FadeIn = 0.04f; b.Age = j * 0.11f; }
                s.Fan[j] = b;
            }
            _streams[key] = s;
            Note(fire ? "flame_jet" : "blizzard");
            return s;
        }

        void UpdateStream(Stream s, float dt)
        {
            bool fire = s.Block == "block.core.flame_jet";
            string prefix = fire ? "flame_" : "blizzard_";
            var info = _art != null ? _art.EffectInfo(fire ? G1 : G4, fire ? "flame_jet" : "blizzard") : null;
            float lenCells = info != null ? (info.Size.X - (info.HasPivot ? info.Pivot.X : 0)) / 64f : 1.5f;
            float grow = Mathf.Clamp01((_clock - s.Start) / Or(S("cone", "grow_seconds"), 0.07f));
            float sx = s.Radius * Or(S("cone", prefix + "reach"), 1f) / Mathf.Max(0.1f, lenCells) * Mathf.Lerp(0.35f, 1f, grow);
            float sy = sx * Or(S("cone", prefix + "height_mul"), 1f);
            float tip = Or(S("cone", "tip_cells"), 0.3f);
            float lift = J("muzzle", "lift_cells");
            Vector2 dirSim = SimDir(s.Angle);
            for (int j = 0; j < s.Fan.Length; j++)
            {
                var b = s.Fan[j];
                if (b == null) continue;
                float off = j == 0 ? 0f : ((j % 2 == 1 ? 1f : -1f) * s.Half * Or(S("cone", "side_deg_mul"), 0.55f) * ((j + 1) / 2));
                float k = j == 0 ? 1f : Or(S("cone", "side_len_mul"), 0.85f);
                b.Pos = s.Origin + dirSim * tip;
                b.Lift = lift;
                b.Rot = s.Angle + off;
                b.Sx0 = sx * k;
                b.Sy0 = sy * k;
                b.FlipX = false;
                b.Alpha = j == 0 ? 1f : Or(S("cone", "side_alpha"), 0.6f);
            }
            // 입자: 불꽃은 불씨가 앞으로 튄다, 눈보라는 눈송이가 흩날린다
            s.PartAcc += dt;
            float every = Or(S("cone", prefix + "particle_every"), 0.03f);
            while (s.PartAcc >= every)
            {
                s.PartAcc -= every;
                float rel = Random.Range(-s.Half, s.Half);
                Vector2 d = SimDir(s.Angle + rel);
                float sp = s.Radius * Random.Range(2.2f, 3.6f);
                Color a = fire ? ElPrimary(Ids.Element.Fire) : Color.white;
                Color b2 = fire ? ElSecondary(Ids.Element.Fire) : ElSecondary(Ids.Element.Ice);
                Sparks(ParticleBudgetConfig.CatShot, FxPriority.Decor, Count(1f, FxPriority.Decor), s.Origin + dirSim * tip, d, 4f, sp * 0.8f, sp,
                    s.Radius / sp * 1.1f, fire ? 0.12f : 0.09f, a, b2, 1.5f, fire ? null : ProcSprites.HardDot, null, 0f, 0f, 0f, false, 0f, fire);
            }
        }

        /// <summary>화면 각도(도) → 시뮬레이션 방향(단위).</summary>
        static Vector2 SimDir(float viewDeg)
        {
            float r = viewDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), -Mathf.Sin(r));
        }

        // ------------------------------------------------------------ 그림자 파동·낫: 휩쓸기 한 번(이벤트로)

        sealed class Sweep
        {
            public int ShotId, Slot;
            public Vector2 Origin;
            public float Base, From, To, Start, Dur, Radius, GhostAcc;
            public bool Attached;
        }

        readonly List<Sweep> _sweeps = new List<Sweep>();

        void OnConeStarted(SimWorld w, in SimEvent e)
        {
            var b = BlockOf(e.Id);
            var st = b != null ? b.Style : null;
            if (st == null) return;
            if (IsStream(st)) { Note("cone_start"); return; } // 불꽃·눈보라는 상태로(SyncStreams)
            float half = e.A;
            float baseAng = FxUtil.ViewAngle(e.Pos2);
            if (!st.AngularSweep)
            {
                // 그림자 파동: 어둠 물결이 반지름으로 퍼진다(5장을 휩쓰는 시간 + 꼬리에 맞춘다)
                var wave = A(G5, "shadow_wave");
                if (wave == null) return;
                int ticks = e.B > 0 ? e.B : 12;
                float life = ticks * SimConst.TickSeconds + Or(S("wave", "tail_seconds"), 0.16f);
                var anim = Retime(wave, wave.Frames.Length / life, false);
                var info = _art.EffectInfo(G5, "shadow_wave");
                float len = info != null ? (info.Size.X - (info.HasPivot ? info.Pivot.X : 0)) / 64f : 1.9f;
                float sx = e.F / Mathf.Max(0.1f, len) * Or(S("wave", "reach"), 1.05f);
                float sy = sx * Or(S("wave", "height_mul"), 1.35f);
                var bit = Spawn(anim, null, e.Pos, life, sx * 0.55f, sy * 0.55f, Color.white, SortBands.Bursts + 2, true);
                if (bit != null) { bit.Sx1 = sx; bit.Sy1 = sy; bit.Rot = baseAng; bit.FadeOut = 0.1f; ApplyBit(bit); }
                // 바닥을 쓸고 가는 어둠 입자
                Vector2 dir = SimDir(baseAng);
                Sparks(ParticleBudgetConfig.CatShot, FxPriority.Decor, Count(Or(S("wave", "motes"), 7f), FxPriority.Decor), e.Pos + dir * 0.3f, dir, half, e.F * 2f, e.F * 3.2f,
                    0.32f, 0.12f, ElPrimary(Ids.Element.Dark), ElSecondary(Ids.Element.Dark), 3f);
                Note("shadow_wave");
                return;
            }
            // 그림자 낫: 반각 → 반대쪽 반각으로 초승달 날이 돈다(잔상을 남긴다)
            int n = e.B <= -1000 ? -(e.B + 1000) : -e.B;
            bool reverse = e.B <= -1000;
            _sweeps.Add(new Sweep
            {
                ShotId = e.EntityId, Slot = e.Slot, Origin = e.Pos, Base = baseAng, From = reverse ? half : -half, To = reverse ? -half : half,
                Start = _clock, Dur = Mathf.Max(1, n) * SimConst.TickSeconds * Or(S("scythe", "time_mul"), 1.6f), Radius = e.F, Attached = st.Attached
            });
            Note("scythe");
        }

        void SyncSweeps(SimWorld w, float alpha, float dt)
        {
            var anim = A("special", "bloom_dark");
            for (int i = _sweeps.Count - 1; i >= 0; i--)
            {
                var s = _sweeps[i];
                float t = Mathf.Clamp01((_clock - s.Start) / Mathf.Max(1e-3f, s.Dur));
                if (s.Attached)
                {
                    var owner = Player(w, s.Slot);
                    if (owner != null) s.Origin = ViewMath.Lerp(owner.PrevPos, owner.Pos, alpha);
                }
                if (anim != null)
                {
                    s.GhostAcc += dt;
                    float every = Or(S("scythe", "ghost_every"), 0.012f);
                    bool first = s.GhostAcc >= every;
                    while (s.GhostAcc >= every || first)
                    {
                        first = false;
                        s.GhostAcc = Mathf.Max(0f, s.GhostAcc - every);
                        float e2 = 1f - (1f - t) * (1f - t);
                        float ang = s.Base + Mathf.Lerp(s.From, s.To, e2);
                        Vector2 d = SimDir(ang);
                        float bladeCells = s.Radius * Or(S("scythe", "blade_cells_mul"), 1.05f);
                        float sc = bladeCells / Mathf.Max(0.1f, NativeH(anim));
                        float life = Or(S("scythe", "ghost_seconds"), 0.16f);
                        var ghost = Retime(anim, anim.Frames.Length / life, false);
                        var b = Spawn(ghost, null, s.Origin + d * s.Radius * Or(S("scythe", "blade_dist_mul"), 0.5f), life, sc, sc, Color.white,
                            SortBands.Bursts + 3, true);
                        if (b == null) break;
                        b.Rot = ang + 180f;
                        b.Lift = J("muzzle", "lift_cells") * 0.5f;
                        b.FadeOut = life * 0.7f;
                        ApplyBit(b);
                        if (Random.value < 0.5f)
                            Sparks(ParticleBudgetConfig.CatShot, FxPriority.Decor, Count(1f, FxPriority.Decor), s.Origin + d * s.Radius, SimDir(ang + (s.To > s.From ? 90f : -90f)), 25f,
                                2f, 4f, 0.2f, 0.09f, ElSecondary(Ids.Element.Dark), Color.white, 5f);
                    }
                }
                if (t >= 1f) _sweeps.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------ 범위 터짐(포탄·불바닥·낙뢰·고드름·룬)

        void OnBlast(SimWorld w, in SimEvent e)
        {
            Vector2 down = new Vector2(0f, -1f);
            switch ((StyleBlastKind)e.B)
            {
                case StyleBlastKind.MortarLand:
                {
                    var blast = A(G1, "mortar_blast");
                    float sc = e.F * 2f / Mathf.Max(0.1f, NativeW(blast)) * Or(S("mortar", "fit"), 1.15f);
                    Play(A(GCommon, "dust_skirt"), e.Pos, e.F * 2f / Mathf.Max(0.1f, NativeW(A(GCommon, "dust_skirt"))) * 1.1f, SortBands.PlayerZones + 16,
                        WithA(Color.white, Or(S("mortar", "dust_alpha"), 0.75f)));
                    Play(blast, e.Pos, sc, SortBands.Bursts + 4, Color.white);
                    Glow(e.Pos, e.F * 0.6f, e.F * Or(S("mortar", "glow_mul"), 2.2f), 0.2f, WithA(ElSecondary(Ids.Element.Fire), Or(S("mortar", "glow_alpha"), 0.6f)), SortBands.Bursts + 3);
                    Sparks(ParticleBudgetConfig.CatHit, FxPriority.NormalHit, Count(Or(S("mortar", "sparks"), 10f), FxPriority.NormalHit), e.Pos, Vector2.zero, 180f, e.F * 1.5f, e.F * 3.5f,
                        0.45f, 0.1f, ElPrimary(Ids.Element.Fire), ElSecondary(Ids.Element.Fire), 3f, null, null, 1.5f, 4f, 14f);
                    Ring(e.Pos, e.F * 0.3f, e.F * 1.15f, 0.22f, WithA(ElSecondary(Ids.Element.Fire), 0.8f));
                    Impact(Or(S("mortar", "shake_px"), 4f), down, FxPriority.NormalHit);
                    Punch(S("mortar", "punch"));
                    Note("mortar_blast");
                    break;
                }
                case StyleBlastKind.MortarFloorTick:
                    for (int i = 0; i < 2; i++) Ember(e.Pos + Random.insideUnitCircle * e.F * 0.7f, 0.16f, 2.2f);
                    break;
                case StyleBlastKind.SkyStrike:
                {
                    var bolt = A(G2, "strike_bolt");
                    float bs = Or(S("strike", "bolt_scale"), 1.1f) * Mathf.Sqrt(Mathf.Max(1f, e.F / 1.3f));
                    var bb = Play(bolt, e.Pos, bs, SortBands.Bursts + 6, Color.white, 0f, false);
                    if (bb != null) { bb.Sx0 = bs * 1.4f; bb.Sx1 = bs * 0.8f; ApplyBit(bb); }
                    var flat = A(G2, "strike_blast");
                    Play(flat, e.Pos, e.F * 2f / Mathf.Max(0.1f, NativeW(flat)) * Or(S("strike", "fit"), 1.1f), SortBands.Bursts + 4, Color.white);
                    Glow(e.Pos, e.F * 0.5f, e.F * Or(S("strike", "glow_mul"), 2.4f), 0.16f, WithA(ElSecondary(Ids.Element.Lightning), Or(S("strike", "glow_alpha"), 0.7f)), SortBands.Bursts + 3);
                    Sparks(ParticleBudgetConfig.CatHit, FxPriority.NormalHit, Count(Or(S("strike", "sparks"), 9f), FxPriority.NormalHit), e.Pos, Vector2.zero, 180f, e.F * 2f, e.F * 4.5f,
                        0.22f, 0.08f, Color.white, ElSecondary(Ids.Element.Lightning), 6f, null, null, 0.5f, 3f, 10f, true);
                    Impact(Or(S("strike", "shake_px"), 5f), down, FxPriority.NormalHit);
                    Punch(S("strike", "punch"));
                    Note("sky_strike");
                    break;
                }
                case StyleBlastKind.IcicleDrop:
                {
                    var sh = A("status", "frozen_shatter");
                    Play(sh, e.Pos, e.F * 2f / Mathf.Max(0.1f, NativeW(sh)) * Or(S("icicle", "shatter_fit"), 1.5f), SortBands.Bursts + 4, Color.white, 0.24f);
                    Play(A("player", "hit_ice"), e.Pos, 1.1f, SortBands.Bursts + 5, Color.white, 0f, false);
                    Sparks(ParticleBudgetConfig.CatHit, FxPriority.NormalHit, Count(Or(S("icicle", "shards"), 4f), FxPriority.NormalHit), e.Pos, Vector2.zero, 180f, 1.5f, 3.2f,
                        0.4f, 0.16f, Color.white, Color.white, 2f, null, A("special", "ice_screen_shard"), 1.5f, 3.5f, 14f, false, 0f, false);
                    Impact(Or(S("icicle", "shake_px"), 1.5f), down, FxPriority.Decor);
                    Note("icicle_land");
                    break;
                }
                case StyleBlastKind.RuneBurst:
                {
                    var rb = A(G3, "rune_burst");
                    Play(rb, e.Pos, e.F * 2f / Mathf.Max(0.1f, NativeW(rb)) * Or(S("rune", "burst_fit"), 1.1f), SortBands.Bursts + 4, Color.white);
                    Glow(e.Pos, e.F * 0.5f, e.F * Or(S("rune", "glow_mul"), 2.1f), 0.2f, WithA(ElSecondary(Ids.Element.Arcane), Or(S("rune", "glow_alpha"), 0.6f)), SortBands.Bursts + 3);
                    Ring(e.Pos, e.F * 0.25f, e.F * 1.1f, 0.25f, WithA(ElSecondary(Ids.Element.Arcane), 0.85f));
                    Sparks(ParticleBudgetConfig.CatHit, FxPriority.NormalHit, Count(Or(S("rune", "sparks"), 8f), FxPriority.NormalHit), e.Pos, Vector2.zero, 180f, e.F * 1.6f, e.F * 3.2f,
                        0.35f, 0.09f, ElSecondary(Ids.Element.Arcane), Color.white, 4f, null, null, 0.5f, 2.5f, 8f);
                    Impact(Or(S("rune", "shake_px"), 3.5f), down, FxPriority.NormalHit);
                    Punch(S("rune", "punch"));
                    Note("rune_burst");
                    break;
                }
                default:
                    Glow(e.Pos, 0.2f, e.F, 0.12f, WithA(ElSecondary(Ids.Element.Lightning), 0.6f), SortBands.Bursts + 3);
                    break;
            }
        }

        // ------------------------------------------------------------ 예고(낙뢰·고드름 — 플레이어 쪽, 속성색만)

        void OnStrikeWarned(SimWorld w, in SimEvent e)
        {
            float wait = Mathf.Max(SimConst.TickSeconds, e.A * SimConst.TickSeconds);
            if (e.Id == "block.core.icicle_fall")
            {
                var mk = A(G4, "icicle_marker");
                float sc = e.F * 2f / Mathf.Max(0.1f, NativeW(mk)) * Or(S("icicle", "marker_fit"), 1.25f);
                var m = Spawn(mk, null, e.Pos, wait + 0.04f, sc * 1.3f, sc * 1.3f, WithA(Color.white, Or(S("icicle", "marker_alpha"), 0.8f)), SortBands.PlayerZones + 15, true);
                if (m != null) { m.Sx1 = m.Sy1 = sc; m.FadeIn = 0.05f; m.FadeOut = 0.06f; ApplyBit(m); }
                // 떨어지는 고드름: 착지 순간에 땅에 닿게 위에서 곧게 내려온다
                var ic = A(G4, "icicle");
                float fall = Or(S("icicle", "fall_seconds"), 0.14f);
                float height = Or(S("icicle", "fall_cells"), 3.2f);
                float isc = Or(S("icicle", "scale"), 1.35f) * Mathf.Sqrt(Mathf.Max(1f, e.F / 0.6f));
                var b = Spawn(ic, null, e.Pos, fall, isc, isc, Color.white, SortBands.Bursts + 5, false);
                if (b != null)
                {
                    b.Delay = Mathf.Max(0f, wait - fall);
                    float f = Mathf.Min(fall, wait);
                    b.Life = f;
                    b.Lift = height * (f / fall);
                    b.LiftVel = -height / fall;
                    b.FadeOut = 0.001f;
                    b.FadeIn = 0.03f;
                    ApplyBit(b);
                }
                Note("icicle_warn");
                return;
            }
            // 낙뢰 예고: 노랑·흰 고리(위험색 없음)가 좁혀 들어온다 + 하늘에서 모이는 빛
            var mark = A(G2, "strike_marker");
            float ms = e.F * 2f / Mathf.Max(0.1f, NativeW(mark)) * Or(S("strike", "marker_fit"), 1.2f);
            var mb = Spawn(mark, null, e.Pos, wait + 0.03f, ms * 1.45f, ms * 1.45f, WithA(Color.white, Or(S("strike", "marker_alpha"), 0.85f)), SortBands.PlayerZones + 15, true);
            if (mb != null) { mb.Sx1 = mb.Sy1 = ms; mb.FadeIn = 0.04f; mb.FadeOut = 0.03f; ApplyBit(mb); }
            var g = Glow(e.Pos, 0.2f, 0.9f, wait, WithA(ElSecondary(Ids.Element.Lightning), 0.5f), SortBands.Bursts + 1);
            if (g != null) { g.Lift = Or(S("strike", "charge_lift_cells"), 2.2f); g.LiftVel = -g.Lift / wait; ApplyBit(g); }
            Note("strike_warn");
        }

        // ------------------------------------------------------------ 튐·지짐·사슬·깨짐·갈라짐

        void OnRicochet(in SimEvent e)
        {
            Play(A("player", "hit_fire"), e.Pos, 1f, SortBands.Bursts + 5, Color.white, 0f, false, Random.Range(0f, 360f));
            Line(A("wand", "trail_fire"), e.Pos, Vector2.MoveTowards(e.Pos, e.Pos2, Or(S("ricochet", "streak_cells"), 1.2f)), 0.2f, Color.white, 0.12f, SortBands.Bursts + 3);
            Sparks(ParticleBudgetConfig.CatHit, FxPriority.NormalHit, Count(4f, FxPriority.NormalHit), e.Pos, e.Pos2 - e.Pos, 30f, 4f, 8f, 0.18f, 0.08f,
                ElPrimary(Ids.Element.Fire), ElSecondary(Ids.Element.Fire), 6f, null, null, 0f, 0f, 0f, true);
            Note("ricochet");
        }

        void OnZap(in SimEvent e)
        {
            var link = A(G2, "chain_link");
            Line(link, e.Pos, e.Pos2, Or(S("zap", "height_cells"), 0.26f), Color.white, Or(S("zap", "seconds"), 0.09f), SortBands.Bursts + 4);
            Play(A("player", "hit_lightning"), e.Pos2, 0.8f, SortBands.Bursts + 5, Color.white, 0f, false);
            Note("zap");
        }

        void OnChainArc(SimWorld w, in SimEvent e)
        {
            string blk;
            if (!_shotBlock.TryGetValue(e.EntityId, out blk) || blk != "block.core.chain_bolt") return;
            var link = A(G2, "chain_link");
            if (link == null) return;
            float h = Or(S("chain", "height_cells"), 0.375f) * Mathf.Max(0.5f, 1f - e.A * 0.15f);
            float life = Or(S("chain", "seconds"), 0.14f);
            Line(link, e.Pos, e.Pos2, h, Color.white, life, SortBands.Bursts + 4);
            var thin = Line(link, e.Pos, e.Pos2, h * 0.55f, WithA(Color.white, 0.8f), life * 0.7f, SortBands.Bursts + 5);
            if (thin != null) thin.AnimSpeed = 1.7f;
            Play(A("player", "hit_lightning"), e.Pos2, 1f, SortBands.Bursts + 6, Color.white, 0f, false);
            Glow(e.Pos2, 0.3f, 1.1f, 0.12f, WithA(ElSecondary(Ids.Element.Lightning), 0.75f), SortBands.Bursts + 3);
            if (e.A == 0) Glow(e.Pos, 0.2f, 0.8f, 0.1f, WithA(Color.white, 0.7f), SortBands.Bursts + 3);
            Note("chain");
        }

        void OnShatter(in SimEvent e)
        {
            Play(A("player", "hit_ice"), e.Pos, 1.2f, SortBands.Bursts + 5, Color.white, 0f, false);
            var sh = A("status", "frozen_shatter");
            Play(sh, e.Pos, Or(S("shatter", "cells"), 1.2f) / Mathf.Max(0.1f, NativeW(sh)), SortBands.Bursts + 4, Color.white, 0.22f);
            Sparks(ParticleBudgetConfig.CatHit, FxPriority.NormalHit, Count(Or(S("shatter", "shards"), 5f), FxPriority.NormalHit), e.Pos, Vector2.zero, 180f, 2f, 4.5f,
                0.3f, 0.14f, Color.white, Color.white, 3f, null, A("special", "ice_screen_shard"), 0.5f, 2f, 10f, false, 0f, false);
            Glow(e.Pos, 0.3f, 1.2f, 0.14f, WithA(ElSecondary(Ids.Element.Ice), 0.7f), SortBands.Bursts + 3);
            Impact(Or(S("shatter", "shake_px"), 2f), Vector2.zero, FxPriority.Decor);
            Note("shatter");
        }

        void OnSplit(in SimEvent e)
        {
            Play(A("player", "shadow_orb_fade"), e.Pos, 1.1f, SortBands.Bursts + 4, Color.white, 0f, false);
            Play(A("player", "hit_dark"), e.Pos, 1.2f, SortBands.Bursts + 5, Color.white, 0f, false);
            Ring(e.Pos, 0.1f, 0.9f, 0.18f, WithA(ElSecondary(Ids.Element.Dark), 0.85f));
            Sparks(ParticleBudgetConfig.CatHit, FxPriority.NormalHit, Count(6f, FxPriority.NormalHit), e.Pos, Vector2.zero, 180f, 2f, 4f, 0.3f, 0.09f,
                ElSecondary(Ids.Element.Dark), Color.white, 5f);
            Note("split");
        }

        // ------------------------------------------------------------ 추적탄 둘레(상태로)

        readonly Dictionary<int, Bit> _auras = new Dictionary<int, Bit>();
        readonly List<int> _dropA = new List<int>();

        void SyncAuras(SimWorld w, float alpha, float dt)
        {
            _dropA.Clear();
            foreach (var kv in _auras) _dropA.Add(kv.Key);
            var list = w.Projectiles;
            var orb = A("player", "shadow_orb");
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p.Dead || p.State == ProjectileState.Pending || p.Block == null || p.Block.Style == null || p.Block.Style.Kind != AttackBehavior.Seeker) continue;
                _dropA.Remove(p.Id);
                Bit b;
                if (!_auras.TryGetValue(p.Id, out b) || !Alive(b))
                {
                    if (orb == null) continue;
                    float sc = Or(S("seeker", "aura_scale"), 0.8f) * (p.Generation > 0 ? 0.75f : 1f);
                    b = Hold(orb, null, p.Pos, sc, sc, WithA(Color.white, Or(S("seeker", "aura_alpha"), 0.55f)), SortBands.PlayerShots + 1, false);
                    if (b == null) continue;
                    b.FadeIn = 0.06f;
                    _auras[p.Id] = b;
                }
                b.Pos = ViewMath.Lerp(p.PrevPos, p.Pos, alpha);
            }
            for (int i = 0; i < _dropA.Count; i++) { Release(_auras[_dropA[i]], 0.08f); _auras.Remove(_dropA[i]); }
        }

        // ------------------------------------------------------------ 화상·냉기 겹(상태로)

        readonly Dictionary<int, float> _statusNext = new Dictionary<int, float>();

        void OnStatus(in SimEvent e)
        {
            if (e.A <= 0) return;
            if ((StyleStatusKind)e.B == StyleStatusKind.Burn)
                Sparks(ParticleBudgetConfig.CatHit, FxPriority.Decor, Count(2f, FxPriority.Decor), e.Pos, Vector2.zero, 180f, 0.5f, 1.2f, 0.4f, 0.1f,
                    ElPrimary(Ids.Element.Fire), ElSecondary(Ids.Element.Fire), 2f, null, null, 1.5f, 2.5f, 0f);
            else
                Sparks(ParticleBudgetConfig.CatHit, FxPriority.Decor, Count(2f, FxPriority.Decor), e.Pos, Vector2.zero, 180f, 0.4f, 1f, 0.45f, 0.07f,
                    Color.white, ElSecondary(Ids.Element.Ice), 2f, ProcSprites.HardDot, null, 0.8f, 1.6f, 3f, false, 0.25f, false);
        }

        void SyncStatuses(SimWorld w, float dt)
        {
            if (Level <= 0f) return;
            var list = w.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                var en = list[i];
                if (en == null || en.Hp <= 0f || (en.BurnStacks <= 0 && en.ChillStacks <= 0)) continue;
                float next;
                if (_statusNext.TryGetValue(en.Id, out next) && _clock < next) continue;
                bool burn = en.BurnStacks > 0;
                int stacks = Mathf.Max(1, burn ? en.BurnStacks : en.ChillStacks);
                float every = Or(S(burn ? "burn" : "chill", "every"), burn ? 0.22f : 0.3f) / Mathf.Sqrt(stacks);
                _statusNext[en.Id] = _clock + every;
                float r = Mathf.Max(0.2f, en.BodyRadius);
                if (burn) Ember(en.Pos + new Vector2(Random.Range(-r, r) * 0.7f, Random.Range(-0.1f, 0.1f)), 0.13f, 1.4f);
                else
                {
                    float sz = 0.07f / (ProcSprites.HardDot.rect.width / 64f);
                    var b = Spawn(null, ProcSprites.HardDot, en.Pos + new Vector2(Random.Range(-r, r), 0f), 0.6f, sz, sz,
                        Color.Lerp(Color.white, ElSecondary(Ids.Element.Ice), Random.value), SortBands.Bursts + 8, false, ParticleBudgetConfig.CatEnvironment, FxPriority.Decor);
                    if (b == null) continue;
                    b.Lift = r * 2.2f;
                    b.LiftVel = -1.2f;
                    b.Vel = new Vector2(Random.Range(-0.4f, 0.4f), 0f);
                    ApplyBit(b);
                }
            }
            if (_statusNext.Count > 256) _statusNext.Clear();
        }
    }
}
