using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Arts
{
    // 플레이어 지대: 불가마, 사본 마법진, 얼음 병풍. 주인 Arts(w.PlayerZones). 04 요약서 9.3절, 02 요약서 8.3절, 03 요약서 12절.
    // 종류마다 동시 1개(새로 깔면 이전 것은 사라진다 — 남은 흡수량·횟수를 이어받지 않고, 병풍은 파편 없이).
    public sealed partial class ArtSystem
    {
        // ------------------------------------------------------------ 만들기·걷기

        PlayerZoneEntity FindZone(SimWorld w, int slot, PlayerZoneKind kind)
        {
            for (int i = 0; i < w.PlayerZones.Count; i++)
            {
                var z = w.PlayerZones[i];
                if (!z.Dead && z.OwnerSlot == slot && z.Kind == kind) return z;
            }
            return null;
        }

        PlayerZoneEntity FindZoneById(SimWorld w, int zoneId)
        {
            for (int i = 0; i < w.PlayerZones.Count; i++)
                if (w.PlayerZones[i].Id == zoneId && !w.PlayerZones[i].Dead) return w.PlayerZones[i];
            return null;
        }

        /// <summary>지대를 걷는다. broken = 부서짐(병풍). 불가마면 남은 흡수량도 사라진다.</summary>
        void RemoveZone(SimWorld w, PlayerZoneEntity z, bool broken)
        {
            if (z.Dead) return;
            z.Dead = true;
            if (z.Kind == PlayerZoneKind.Kiln && z.OwnerSlot >= 0 && z.OwnerSlot < w.Players.Length)
            {
                var owner = w.Players[z.OwnerSlot];
                owner.KilnAbsorbLeftHu = 0;
                owner.InKilnZone = false;
            }
            w.Emit(SimEventType.PlayerZoneExpired, z.OwnerSlot, entityId: z.Id, a: (int)z.Kind, b: broken ? 1 : 0, pos: z.Pos);
        }

        PlayerZoneEntity NewZone(SimWorld w, PlayerState p, ArtCast c, PlayerZoneKind kind, Vector2 pos)
        {
            // 동시 개수 상한(max_concurrent 1): 이전 것을 먼저 걷는다.
            int max = Mathf.Max(1, c.Def.Effect.MaxConcurrent);
            while (true)
            {
                int count = 0;
                PlayerZoneEntity oldest = null;
                for (int i = 0; i < w.PlayerZones.Count; i++)
                {
                    var o = w.PlayerZones[i];
                    if (o.Dead || o.OwnerSlot != p.Slot || o.Kind != kind) continue;
                    if (oldest == null) oldest = o;
                    count++;
                }
                if (count < max || oldest == null) break;
                RemoveZone(w, oldest, false);
            }
            var z = new PlayerZoneEntity
            {
                Id = w.NewEntityId(),
                Kind = kind,
                OwnerSlot = p.Slot,
                Pos = pos,
                Facing = p.Facing,
                StartTick = w.Tick,
                UntilTick = w.Tick + SimConst.Ticks(c.Def.Effect.DurationSeconds),
                SnapshotT = w.Mods.Spells.StandardT(w, p),
                SnapshotCharMul = c.CharMul
            };
            w.PlayerZones.Add(z);
            return z;
        }

        /// <summary>불가마: 시전 순간 발밑에 고정. 0.5초마다 지대 안 적에게 0.25T, 감속, 임계 발사의 열을 합계 16까지 먹는다.</summary>
        void SpawnKiln(SimWorld w, PlayerState p, ArtCast c)
        {
            var e = c.Def.Effect;
            var z = NewZone(w, p, c, PlayerZoneKind.Kiln, p.Pos);
            z.Radius = e.Radius;
            z.NextPulseTick = z.StartTick + SimConst.Ticks(e.IntervalSeconds);
            p.KilnAbsorbLeftHu = SimConst.HeatToHu(e.HeatAbsorb);
            p.InKilnZone = true;
            w.Emit(SimEventType.PlayerZoneSpawned, p.Slot, entityId: z.Id, a: (int)z.Kind, pos: z.Pos, f: z.Radius);
        }

        /// <summary>사본 마법진: 시전 자리에 고정. 내가 J로 쏠 때마다 따라 쏜다(0.3초에 1번, 최대 12번).</summary>
        void SpawnCopyCircle(SimWorld w, PlayerState p, ArtCast c)
        {
            var e = c.Def.Effect;
            var z = NewZone(w, p, c, PlayerZoneKind.CopyCircle, p.Pos);
            z.ChargesLeft = e.MaxCopies;
            z.ReadyTick = w.Tick;
            w.Emit(SimEventType.PlayerZoneSpawned, p.Slot, entityId: z.Id, a: (int)z.Kind, pos: z.Pos);
        }

        /// <summary>얼음 병풍: 시야 정면 offset칸 앞(벽 두께 중심선), 시야에 수직인 폭 × 두께의 벽.</summary>
        void SpawnIceScreen(SimWorld w, PlayerState p, ArtCast c)
        {
            var e = c.Def.Effect;
            Vector2 center = p.Pos + Dir8Util.ToSimVector(p.Facing) * e.Offset;
            var z = NewZone(w, p, c, PlayerZoneKind.IceScreen, center);
            z.Width = e.Width;
            z.Thickness = e.Thickness;
            w.Emit(SimEventType.PlayerZoneSpawned, p.Slot, entityId: z.Id, a: (int)z.Kind, pos: z.Pos, f: z.Width);
        }

        public override void ClearZones(SimWorld w)
        {
            for (int i = 0; i < w.PlayerZones.Count; i++) RemoveZone(w, w.PlayerZones[i], false);
            for (int i = 0; i < w.Players.Length; i++)
            {
                w.Players[i].KilnAbsorbLeftHu = 0;
                w.Players[i].InKilnZone = false;
            }
        }

        // ------------------------------------------------------------ 5단계 갱신

        void UpdateZones(SimWorld w)
        {
            int n = w.PlayerZones.Count;
            for (int i = 0; i < n; i++)
            {
                var z = w.PlayerZones[i];
                if (z.Dead) continue;
                var owner = z.OwnerSlot >= 0 && z.OwnerSlot < w.Players.Length ? w.Players[z.OwnerSlot] : null;
                switch (z.Kind)
                {
                    case PlayerZoneKind.Kiln:
                        UpdateKiln(w, z, owner);
                        break;
                    case PlayerZoneKind.CopyCircle:
                        if (w.Tick >= z.UntilTick || z.ChargesLeft <= 0) RemoveZone(w, z, false);
                        break;
                    case PlayerZoneKind.IceScreen:
                        if (w.Tick >= z.UntilTick) ShatterScreen(w, z, false);
                        break;
                }
            }
            // 불가마 지대 안에 있는가(발사 틱의 판정 중심이 원 안, 경계 포함). 6단계의 발사가 읽는다.
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (p == null || !p.Active) continue;
                var k = FindZone(w, p.Slot, PlayerZoneKind.Kiln);
                p.InKilnZone = k != null && (p.Pos - k.Pos).magnitude <= k.Radius;
            }
        }

        void UpdateKiln(SimWorld w, PlayerZoneEntity z, PlayerState owner)
        {
            var def = w.Content.Art(Ids.Art.Kiln);
            if (def == null || owner == null) { RemoveZone(w, z, false); return; }
            var e = def.Effect;
            bool pulse = w.Tick >= z.NextPulseTick;
            if (pulse) z.NextPulseTick += Mathf.Max(1, SimConst.Ticks(e.IntervalSeconds));
            float amount = e.Damage * z.SnapshotT * z.SnapshotCharMul;
            float slow = e.SlowPercent / 100f;
            string element = CharElement(owner);
            int count = w.Enemies.Count;
            for (int i = 0; i < count; i++)
            {
                var en = w.Enemies[i];
                if (!en.Targetable) continue;
                if ((en.Pos - z.Pos).magnitude > z.Radius) continue;
                // 감속은 지대 안에 있는 틱에만(1틱씩 다시 건다 → 나가면 바로 풀린다). 보스 절반·뿌리 예외는 Enemies가 정한다.
                w.Mods.Enemies.ApplySlow(w, en, SlowSlot.Kiln, slow, 1, CcSource.Kiln);
                if (pulse) Direct(w, owner, en, amount, element, DamageOrigin.Zone, false, def.Id);
            }
            if (pulse) w.Emit(SimEventType.ArtPulse, owner.Slot, id: def.Id, pos: z.Pos, f: z.Radius);
            if (w.Tick >= z.UntilTick) RemoveZone(w, z, false);
        }

        // ------------------------------------------------------------ 불가마 열 흡수

        public override int AbsorbKilnHeat(SimWorld w, PlayerState p, int heatHu)
        {
            if (p == null || heatHu <= 0 || p.KilnAbsorbLeftHu <= 0) return 0;
            var z = FindZone(w, p.Slot, PlayerZoneKind.Kiln);
            if (z == null || (p.Pos - z.Pos).magnitude > z.Radius) return 0;
            int a = Mathf.Min(p.KilnAbsorbLeftHu, heatHu);
            p.KilnAbsorbLeftHu -= a;
            w.Emit(SimEventType.KilnAbsorbed, p.Slot, a: a);
            return a;
        }

        // ------------------------------------------------------------ 사본 마법진

        public override void OnPlayerFired(SimWorld w, PlayerState p, int shotId)
        {
            if (p == null) return;
            var z = FindZone(w, p.Slot, PlayerZoneKind.CopyCircle);
            if (z == null || z.ChargesLeft <= 0 || w.Tick < z.ReadyTick || w.Tick >= z.UntilTick) return;
            var s = StateOf(p);
            var def = w.Content.Art(Ids.Art.CopyCircle);
            if (s == null || def == null) return;
            var e = def.Effect;
            if (p.Spell != null && p.Spell.IsValid)
            {
                // 내 시야와 같은 방향, 피해 50%, 열 없음, 임계 없음, 거울상·흔들림 없음. 실행당 탄 상한은 Spells가 따로 센다.
                var req = new ExecuteRequest
                {
                    OwnerSlot = p.Slot,
                    Tree = p.Spell,
                    Pos = z.Pos,
                    DirDegrees = FacingDegrees(p),
                    DamageMul = e.Damage,
                    HeatExempt = true,
                    IsCrit = false,
                    Source = ShotSource.CopyCircle,
                    ShotId = NextShot(s),
                    CharacterMul = w.Mods.Traits.CharacterMul(w, p, p.Crystals),
                    HitEffect = ShotHitEffect.None,
                    NoSpread = true
                };
                w.Mods.Spells.Execute(w, in req);
            }
            z.ChargesLeft--;
            z.ReadyTick = w.Tick + SimConst.Ticks(e.IntervalSeconds);
            w.Emit(SimEventType.ArtPulse, p.Slot, id: def.Id, a: e.MaxCopies - z.ChargesLeft, pos: z.Pos);
            if (z.ChargesLeft <= 0) RemoveZone(w, z, false);
        }

        // ------------------------------------------------------------ 얼음 병풍

        /// <summary>점을 병풍의 자리표(u = 벽을 따라, v = 시야 쪽)로 바꾼다.</summary>
        static Vector2 ToScreenLocal(PlayerZoneEntity z, Vector2 q)
        {
            Vector2 n = Dir8Util.ToSimVector(z.Facing);
            Vector2 t = new Vector2(-n.y, n.x);
            Vector2 d = q - z.Pos;
            return new Vector2(Vector2.Dot(d, t), Vector2.Dot(d, n));
        }

        static bool SegmentHitsBox(Vector2 a, Vector2 b, float halfU, float halfV)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            if (!Slab(a.x, d.x, halfU, ref t0, ref t1)) return false;
            if (!Slab(a.y, d.y, halfV, ref t0, ref t1)) return false;
            return t0 <= t1;
        }

        static bool Slab(float start, float delta, float half, ref float t0, ref float t1)
        {
            if (delta == 0f) return start >= -half && start <= half;
            float ta = (-half - start) / delta;
            float tb = (half - start) / delta;
            if (ta > tb) { float tmp = ta; ta = tb; tb = tmp; }
            if (ta > t0) t0 = ta;
            if (tb < t1) t1 = tb;
            return t0 <= t1;
        }

        public override bool SegmentHitsScreen(SimWorld w, Vector2 a, Vector2 b, out int zoneId)
        {
            zoneId = -1;
            for (int i = 0; i < w.PlayerZones.Count; i++)
            {
                var z = w.PlayerZones[i];
                if (z.Dead || z.Kind != PlayerZoneKind.IceScreen) continue;
                if (SegmentHitsBox(ToScreenLocal(z, a), ToScreenLocal(z, b), z.Width * Half, z.Thickness * Half))
                {
                    zoneId = z.Id;
                    return true;
                }
            }
            return false;
        }

        /// <summary>적이 병풍에 닿았다. 중량·보스는 닿으면 부순다(경직 없음). 돌진하던 적이 부딪히면 깨지고 그 적이 경직된다
        /// (엘리트 절반은 Enemies가 정한다). 그 밖(걷는 일반·엘리트)은 못 지나갈 뿐 깨지지 않는다.</summary>
        public override bool OnEnemyTouchedScreen(SimWorld w, int zoneId, EnemyState e, bool dash)
        {
            var z = FindZoneById(w, zoneId);
            if (z == null || z.Kind != PlayerZoneKind.IceScreen || e == null) return false;
            bool heavy = e.IsBoss || e.Has(EnemyTags.Boss) || e.Has(EnemyTags.Heavy);
            if (!heavy && !dash) return false;
            if (!heavy)
            {
                var def = w.Content.Art(Ids.Art.IceScreen);
                if (def != null)
                    w.Mods.Enemies.ApplyCc(w, e, CcKind.Stagger, SimConst.Ticks(def.Effect.StaggerSeconds), CcSource.IceScreen,
                        -Dir8Util.ToSimVector(z.Facing));
            }
            ShatterScreen(w, z, true);
            return true;
        }

        /// <summary>병풍이 사라진다(만료·부서짐): 벽 앞쪽(시전자 반대편) 폭 × 길이 직사각형에 파편 3T(냉기) 1회.</summary>
        void ShatterScreen(SimWorld w, PlayerZoneEntity z, bool broken)
        {
            if (z.Dead) return;
            var owner = z.OwnerSlot >= 0 && z.OwnerSlot < w.Players.Length ? w.Players[z.OwnerSlot] : null;
            var def = w.Content.Art(Ids.Art.IceScreen);
            RemoveZone(w, z, broken);
            if (owner == null || def == null) return;
            var e = def.Effect;
            float amount = e.Damage * z.SnapshotT * z.SnapshotCharMul;
            string element = CharElement(owner);
            float near = z.Thickness * Half;
            float far = near + e.ShardLength;
            Vector2 n = Dir8Util.ToSimVector(z.Facing);
            w.Emit(SimEventType.ArtPulse, owner.Slot, id: def.Id, b: broken ? 1 : 0, pos: z.Pos + n * ((near + far) * Half), f: e.ShardLength);
            int count = w.Enemies.Count;
            for (int i = 0; i < count; i++)
            {
                var en = w.Enemies[i];
                if (!en.Targetable) continue;
                Vector2 q = ToScreenLocal(z, en.Pos);
                if (Mathf.Abs(q.x) > z.Width * Half + en.BodyRadius) continue;
                if (q.y < near - en.BodyRadius || q.y > far + en.BodyRadius) continue;
                Direct(w, owner, en, amount, element, DamageOrigin.Zone, false, def.Id);
            }
        }
    }
}
