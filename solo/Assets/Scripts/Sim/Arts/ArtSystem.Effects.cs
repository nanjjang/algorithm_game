using System;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Arts
{
    // 비기 15종의 효과. 04 요약서 8·9절, 02 요약서 8.3절.
    // 공통: 열을 올리지 않는다(HeatExempt), 임계 없음, 콤보 없음(Spells·Player가 출처로 거른다),
    // 직접 피해 = n × 표준 T × 캐릭터 배율(쓴 뒤 결정 수). 대상은 EnemyState.Targetable인 적만(공통 규칙 2).
    public sealed partial class ArtSystem
    {
        const float Half = 1f / 2f;

        /// <summary>예약된 비기 하나를 이 틱만큼 진행한다. 준비 시간이 안 끝났으면 기다린다.</summary>
        void StepCast(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c)
        {
            if (w.Tick < c.StartTick) return;
            var e = c.Def.Effect;
            switch (c.Def.Id)
            {
                case Ids.Art.Burst:
                    // ① 시야 방향으로 트리 1회, 2T.
                    Exec(w, p, c, NextShot(s), p.Pos, 0f, e.Damage, ShotSource.ManaDischarge, ShotHitEffect.None, false);
                    c.Done = true;
                    break;
                case Ids.Art.Recoil:
                    CastRecoil(w, p, s, c);
                    c.Done = true;
                    break;
                case Ids.Art.Fan:
                case Ids.Art.Octastar:
                    // ③ 세 갈래(가운데 100%, 좌우 70%) / ⑧ 여덟 방향(정면 100%, 나머지 60%).
                    Spread(w, p, c, NextShot(s), e.Damage, e.SideDamage);
                    c.Done = true;
                    break;
                case Ids.Art.Wings:
                    CastWings(w, p, s, c);
                    c.Done = true;
                    break;
                case Ids.Art.Afterimage:
                    CastAfterimage(w, p, c);
                    c.Done = true;
                    break;
                case Ids.Art.Spear:
                    CastSpear(w, p, c, false);
                    c.Done = true;
                    break;
                case Ids.Art.ThunderSpear:
                    CastSpear(w, p, c, true);
                    c.Done = true;
                    break;
                case Ids.Art.Vent:
                    CastVent(w, p, c);
                    c.Done = true;
                    break;
                case Ids.Art.Bloom:
                    StepBloom(w, p, s, c);
                    break;
                case Ids.Art.Kiln:
                    SpawnKiln(w, p, c);
                    StartKilnPillars(w, p, s, c);
                    c.Done = true;
                    break;
                case Ids.Art.BoltDash:
                    CastBoltDash(w, p, s, c);
                    c.Done = true;
                    break;
                case Ids.Art.CopyCircle:
                    SpawnCopyCircle(w, p, c);
                    StartPageVortex(w, p, s, c);
                    c.Done = true;
                    break;
                case Ids.Art.IceScreen:
                    SpawnIceScreen(w, p, c);
                    StartIceSpikes(w, p, s, c);
                    c.Done = true;
                    break;
                case Ids.Art.ShadowHand:
                    CastShadowHand(w, p, s, c);
                    c.Done = true;
                    break;
                default:
                    c.Done = true;
                    break;
            }
        }

        // ------------------------------------------------------------ 공용 도우미

        static int NextShot(ArtPlayerState s) => s.NextShotId--;

        static int RoundTicks(float ticks) => (int)Math.Round(ticks, MidpointRounding.AwayFromZero);

        static float FacingDegrees(PlayerState p) => Dir8Util.ToDegrees(p.Facing);

        static string CoreElement(PlayerState p)
        {
            if (p.Spell != null && p.Spell.Core != null && !string.IsNullOrEmpty(p.Spell.Core.Element)) return p.Spell.Core.Element;
            return Ids.Element.Neutral;
        }

        static string CharElement(PlayerState p) =>
            p.Def != null && !string.IsNullOrEmpty(p.Def.Element) ? p.Def.Element : Ids.Element.Neutral;

        /// <summary>트리 1회 실행(시야 + offset도). 비기 한 번의 탄 예산을 넘으면 건너뛴다(A-08).</summary>
        void Exec(SimWorld w, PlayerState p, ArtCast c, int shotId, Vector2 pos, float offsetDegrees, float mul,
            ShotSource source, ShotHitEffect hitEffect, bool noSpread)
        {
            if (p.Spell == null || !p.Spell.IsValid) return;
            if (c.Capped && c.BudgetLeft <= 0) return;
            var req = new ExecuteRequest
            {
                OwnerSlot = p.Slot,
                Tree = p.Spell,
                Pos = pos,
                DirDegrees = FacingDegrees(p) + offsetDegrees,
                DamageMul = mul,
                HeatExempt = true,
                IsCrit = false,
                Source = source,
                ShotId = shotId,
                ProjectileBudget = c.Capped ? c.BudgetLeft : 0,
                CharacterMul = c.CharMul,
                HitEffect = hitEffect,
                NoSpread = noSpread
            };
            var r = w.Mods.Spells.Execute(w, in req);
            if (c.Capped) c.BudgetLeft -= r.Spawned;
        }

        /// <summary>정면 1회 + 좌우로 번갈아 갈래를 낸다(정면 → ±1 → ±2 …). 예산이 모자라면 뒤쪽 갈래부터 빠진다.</summary>
        void Spread(SimWorld w, PlayerState p, ArtCast c, int shotId, float frontMul, float sideMul)
        {
            var e = c.Def.Effect;
            Exec(w, p, c, shotId, p.Pos, 0f, frontMul, ShotSource.ArtOther, ShotHitEffect.None, false);
            for (int j = 1; j < e.Branches; j++)
            {
                int step = (j + 1) / 2;
                float sign = (j & 1) == 1 ? 1f : -1f;
                Exec(w, p, c, shotId, p.Pos, sign * step * e.BranchAngleDegrees, sideMul, ShotSource.ArtOther, ShotHitEffect.None, true);
            }
        }

        DamageResult Direct(SimWorld w, PlayerState p, EnemyState target, float amount, string element, DamageOrigin origin,
            bool ignoresArmor, string sourceId)
        {
            var dmg = new DirectDamage
            {
                AttackerSlot = p.Slot,
                TargetEnemyId = target.Id,
                Amount = amount,
                Element = element,
                Origin = origin,
                IgnoresArmor = ignoresArmor,
                SourceId = sourceId
            };
            return w.Mods.Spells.DealDirect(w, in dmg);
        }

        static float DistToSegment(Vector2 q, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 <= 0f) return (q - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2);
            return (q - (a + ab * t)).magnitude;
        }

        /// <summary>pos에서 dir로 벽까지의 거리(최대 maxLen).</summary>
        static float RayToWall(SimWorld w, Vector2 pos, Vector2 dir, float maxLen)
        {
            float len = maxLen;
            if (dir.x > 0f) len = Mathf.Min(len, (w.ArenaWidth - pos.x) / dir.x);
            else if (dir.x < 0f) len = Mathf.Min(len, -pos.x / dir.x);
            if (dir.y > 0f) len = Mathf.Min(len, (w.ArenaHeight - pos.y) / dir.y);
            else if (dir.y < 0f) len = Mathf.Min(len, -pos.y / dir.y);
            return Mathf.Max(0f, len);
        }

        // ------------------------------------------------------------ 공용 비기

        /// <summary>② 트리 1회(맞은 적 넉백·돌진 취소는 탄의 HitEffect로 Spells가 건다) + 시야 반대쪽으로 물러남(벽 앞 멈춤).</summary>
        void CastRecoil(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c)
        {
            var e = c.Def.Effect;
            Exec(w, p, c, NextShot(s), p.Pos, 0f, e.Damage, ShotSource.ArtOther, ShotHitEffect.RecoilKnockback, false);
            Vector2 from = p.Pos;
            Vector2 back = -Dir8Util.ToSimVector(p.Facing) * e.SelfMove;
            Vector2 to = w.Mods.Player.MoveByArt(w, p, back, false, true);
            w.Emit(SimEventType.PlayerMovedByArt, p.Slot, id: c.Def.Id, pos: from, pos2: to);
        }

        /// <summary>④ 시야 −90도·+90도로 트리 1회씩, 각 100%.</summary>
        void CastWings(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c)
        {
            var e = c.Def.Effect;
            int shot = NextShot(s);
            Exec(w, p, c, shot, p.Pos, e.BranchAngleDegrees, e.Damage, ShotSource.ArtOther, ShotHitEffect.None, true);
            Exec(w, p, c, shot, p.Pos, -e.BranchAngleDegrees, e.Damage, ShotSource.ArtOther, ShotHitEffect.None, true);
        }

        /// <summary>⑤ 마지막으로 누른 물리 방향으로 순간 이동(벽·적 앞 멈춤) + 무적, 그동안 스침 1회만. 피해 없음.</summary>
        void CastAfterimage(SimWorld w, PlayerState p, ArtCast c)
        {
            var e = c.Def.Effect;
            Dir8 dir = c.HasPhysical ? c.Physical : p.Facing;
            Vector2 from = p.Pos;
            Vector2 to = w.Mods.Player.MoveByArt(w, p, Dir8Util.ToSimVector(dir) * e.SelfMove, true, true);
            w.Mods.Player.GrantInvuln(w, p, SimConst.Ticks(e.InvulnSeconds), true);
            w.Emit(SimEventType.PlayerMovedByArt, p.Slot, id: c.Def.Id, pos: from, pos2: to);
        }

        /// <summary>⑥ 3T 한 줄 관통(즉발 선분, 폭·사거리는 데이터, 철갑 무시, 코어 블록 속성). ⑨는 같은 모양에 6T + 기절(보스는 동작 멈춤).</summary>
        void CastSpear(SimWorld w, PlayerState p, ArtCast c, bool thunder)
        {
            var e = c.Def.Effect;
            Vector2 dir = Dir8Util.ToSimVector(p.Facing);
            Vector2 start = p.Pos;
            Vector2 end = start + dir * RayToWall(w, start, dir, e.Range);
            w.Emit(SimEventType.ArtLine, p.Slot, id: c.Def.Id, pos: start, pos2: end, f: e.Width);

            float amount = e.Damage * w.Mods.Spells.StandardT(w, p) * c.CharMul;
            string element = CoreElement(p);
            int n = w.Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var en = w.Enemies[i];
                if (!en.Targetable) continue;
                if (DistToSegment(en.Pos, start, end) > e.Width * Half + en.BodyRadius) continue;
                Direct(w, p, en, amount, element, DamageOrigin.Art, e.IgnoresArmor, c.Def.Id);
                if (!thunder || !en.Targetable) continue;
                if (en.IsBoss || en.Has(EnemyTags.Boss)) w.Mods.Bosses.ActionHalt(w, en, SimConst.Ticks(e.BossHaltSeconds));
                else w.Mods.Enemies.ApplyCc(w, en, CcKind.Stun, SimConst.Ticks(e.StunSeconds), CcSource.ThunderSpear, dir);
            }
        }

        /// <summary>⑦ 시전자 중심 폭발: 피해 = T × 열 ÷ 25(잠금 중이면 열 100), 적 탄 소거, 열 0, 잠금 해제. 코어 블록 속성.</summary>
        void CastVent(SimWorld w, PlayerState p, ArtCast c)
        {
            var e = c.Def.Effect;
            float heat = w.Mods.Player.ConsumeHeatForVent(w, p);
            float amount = e.HeatPerT > 0f ? w.Mods.Spells.StandardT(w, p) * (heat / e.HeatPerT) * c.CharMul : 0f;
            w.Emit(SimEventType.ArtPulse, p.Slot, id: c.Def.Id, pos: p.Pos, f: e.Radius);
            if (amount > 0f)
            {
                string element = CoreElement(p);
                int n = w.Enemies.Count;
                for (int i = 0; i < n; i++)
                {
                    var en = w.Enemies[i];
                    if (!en.Targetable) continue;
                    if ((en.Pos - p.Pos).magnitude > e.Radius) continue;
                    Direct(w, p, en, amount, element, DamageOrigin.Art, false, c.Def.Id);
                }
            }
            w.Mods.Threats.ClearShots(w, p.Pos, e.ClearRadius);
        }

        /// <summary>⑩ 팔방 전개를 결정 수만큼 반복(각 2배). i번째 파동 틱 = 효과 시작 틱 + round(i × 7.2). 마지막 파동에 적 탄 소거.
        /// 탄 예산(160)은 앞 파동부터 쓰므로 모자라면 마지막 반복부터 빠진다.</summary>
        void StepBloom(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c)
        {
            var e = c.Def.Effect;
            while (c.NextIndex < c.Repeats)
            {
                int due = c.StartTick + RoundTicks(c.NextIndex * e.RepeatIntervalSeconds * SimConst.TicksPerSecond);
                if (w.Tick < due) break;
                bool last = c.NextIndex == c.Repeats - 1;
                Spread(w, p, c, NextShot(s), e.Damage, e.SideDamage);
                w.Emit(SimEventType.ArtPulse, p.Slot, id: c.Def.Id, a: c.NextIndex, b: last ? 1 : 0, pos: p.Pos, f: last ? e.ClearRadius : 0f);
                if (last) w.Mods.Threats.ClearShots(w, p.Pos, e.ClearRadius);
                c.NextIndex++;
            }
            if (c.NextIndex >= c.Repeats) c.Done = true;
        }

        // ------------------------------------------------------------ 고유 비기(즉발)

        /// <summary>벼락 질주: 시야 정면으로 적을 뚫고 이동(벽·기둥 앞 멈춤), 길 위 적마다 1회 3T(번개) + 경직, 무적, 길 위 적 탄은 스침 1회.</summary>
        void CastBoltDash(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c)
        {
            var e = c.Def.Effect;
            Vector2 dir = Dir8Util.ToSimVector(p.Facing);
            Vector2 start = p.Pos;
            Vector2 end = w.Mods.Player.MoveByArt(w, p, dir * e.SelfMove, false, true);
            w.Mods.Player.GrantInvuln(w, p, SimConst.Ticks(e.InvulnSeconds), false);
            w.Emit(SimEventType.PlayerMovedByArt, p.Slot, id: c.Def.Id, pos: start, pos2: end);
            w.Emit(SimEventType.ArtLine, p.Slot, id: c.Def.Id, pos: start, pos2: end, f: e.Width);
            // 강화(Docs/12 4절): 지나간 궤적에 낙뢰가 차례로 떨어진다(예약만 — 피해는 다음 틱부터 5단계에서).
            StartBoltTrail(w, p, s, c, start, end);

            float amount = e.Damage * w.Mods.Spells.StandardT(w, p) * c.CharMul;
            string element = CharElement(p);
            int stagger = SimConst.Ticks(e.StaggerSeconds);
            int n = w.Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var en = w.Enemies[i];
                if (!en.Targetable) continue;
                if (DistToSegment(en.Pos, start, end) > e.Width * Half + en.BodyRadius) continue;
                Direct(w, p, en, amount, element, DamageOrigin.Art, false, c.Def.Id);
                if (en.Targetable) w.Mods.Enemies.ApplyCc(w, en, CcKind.Stagger, stagger, CcSource.BoltDash, dir);
            }

            // 길 위의 적 탄: 스침 1회로 인정(탄 수와 무관하게 한 번. 피뢰침도 1회).
            for (int i = 0; i < w.EnemyShots.Count; i++)
            {
                var shot = w.EnemyShots[i];
                if (shot.Dead || shot.Blocked) continue;
                if (DistToSegment(shot.Pos, start, end) > e.Width * Half + shot.Radius) continue;
                w.Mods.Player.AwardGraze(w, p, shot.CastId, GrazeKind.BoltDash, shot.Pos);
                break;
            }
        }

        /// <summary>그림자 손: 시야 정면 지점(벽 안쪽으로 자름) 둘레의 적을 중심 쪽으로 당김(중심을 넘지 않음) + 3T(암흑) + 경직.</summary>
        void CastShadowHand(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c)
        {
            var e = c.Def.Effect;
            Vector2 center = w.ClampToArena(p.Pos + Dir8Util.ToSimVector(p.Facing) * e.Offset, 0f);
            w.Emit(SimEventType.ArtPulse, p.Slot, id: c.Def.Id, pos: center, f: e.Radius);
            // 강화(Docs/12 4절): 땅에서 솟은 손이 움켜쥔 뒤(지금의 당김·피해) 짓눌러 범위 피해(예약).
            StartShadowCrush(w, p, s, c, center);

            float amount = e.Damage * w.Mods.Spells.StandardT(w, p) * c.CharMul;
            string element = CharElement(p);
            int stagger = SimConst.Ticks(e.StaggerSeconds);
            int n = w.Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var en = w.Enemies[i];
                if (!en.Targetable) continue;
                Vector2 to = center - en.Pos;
                float dist = to.magnitude;
                if (dist > e.Radius) continue;
                Vector2 dir = dist > 0f ? to / dist : Vector2.zero;
                float pull = Mathf.Min(e.PullDistance, dist);
                if (pull > 0f) w.Mods.Enemies.ApplyCc(w, en, CcKind.Pull, pull, CcSource.ShadowHand, dir);
                Direct(w, p, en, amount, element, DamageOrigin.Art, false, c.Def.Id);
                if (en.Targetable) w.Mods.Enemies.ApplyCc(w, en, CcKind.Stagger, stagger, CcSource.ShadowHand, dir);
            }
        }
    }
}
