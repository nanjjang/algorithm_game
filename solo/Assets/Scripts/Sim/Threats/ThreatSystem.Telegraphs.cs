using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Threats
{
    // 예고 표, 대기 줄(상한·R5), 구역 수명. 05 요약서 1.2·1.3·2.1·2.2절, 06 요약서 1.4절(마지막 숨), 공통 규칙 4.
    public sealed partial class ThreatSystem
    {
        // ------------------------------------------------------------ 표

        public override int RequestTelegraph(SimWorld w, TelegraphRequest req)
        {
            if (req == null) return 0;
            var d = Data(w);
            var t = new TelegraphTicket
            {
                Id = d.NextTicketId++,
                OwnerEnemyId = req.OwnerEnemyId,
                CastId = req.CastId,
                CountAsOne = req.CountAsOne,
                Bypass = req.Bypass,
                StaggerTicks = req.StaggerIfBlockedTicks,
                Specs = req.Zones.ToArray(),   // 부른 쪽이 요청 객체를 다시 써도 되게 베껴 둔다
                RequestTick = w.Tick,
                NextCheckTick = w.Tick,
            };
            d.Tickets.Add(t);
            d.ById[t.Id] = t;
            return t.Id;
        }

        public override TicketStatus Status(SimWorld w, int ticket)
        {
            TelegraphTicket t;
            return Data(w).ById.TryGetValue(ticket, out t) ? t.Status : TicketStatus.Unknown;
        }

        public override bool TryGetTiming(SimWorld w, int ticket, out int startTick, out int detonateTick)
        {
            TelegraphTicket t;
            if (Data(w).ById.TryGetValue(ticket, out t) && t.Started)
            {
                startTick = t.StartTick;
                detonateTick = t.DetonateTick;
                return true;
            }
            startTick = 0;
            detonateTick = 0;
            return false;
        }

        public override void GetZones(SimWorld w, int ticket, List<ZoneEntity> into)
        {
            TelegraphTicket t;
            if (into == null || !Data(w).ById.TryGetValue(ticket, out t)) return;
            into.AddRange(t.Zones);
        }

        /// <summary>주인이 표를 내려놓는다: 대기 중이면 줄에서 빼고, 떠 있는 구역은 취소하고, 표를 지운다(그 뒤 Status = Unknown).</summary>
        public override void CancelTicket(SimWorld w, int ticket)
        {
            var d = Data(w);
            TelegraphTicket t;
            if (!d.ById.TryGetValue(ticket, out t)) return;
            for (int i = 0; i < t.Zones.Count; i++) CancelZone(w, t.Zones[i]);
            d.ById.Remove(ticket);
            d.Tickets.Remove(t);
        }

        public override void CancelTelegraphsOf(SimWorld w, int enemyId)
        {
            if (enemyId == 0) return;   // 0 = 주인 없는 요청(마지막 숨의 원 등). 한꺼번에 지우지 않는다
            var d = Data(w);
            for (int i = 0; i < d.Tickets.Count; i++)
            {
                var t = d.Tickets[i];
                if (t.OwnerEnemyId != enemyId) continue;
                if (t.Status != TicketStatus.Waiting && t.Status != TicketStatus.Active) continue;
                bool anyDetonated = false;
                for (int z = 0; z < t.Zones.Count; z++)
                {
                    if (t.Zones[z].Detonated) anyDetonated = true;
                    CancelZone(w, t.Zones[z]);
                }
                t.Status = anyDetonated ? TicketStatus.Detonated : TicketStatus.Cancelled;
                t.EndTick = w.Tick;
            }
        }

        static void CancelZone(SimWorld w, ZoneEntity z)
        {
            if (z.Cancelled || z.Detonated) return;
            z.Cancelled = true;
            w.Emit(SimEventType.TelegraphCancelled, entityId: z.Id, pos: z.Pos);
        }

        // ------------------------------------------------------------ 8단계: 대기 줄

        public override void Stage8_Queue(SimWorld w)
        {
            var d = Data(w);
            var cfg = d.Config;

            // 끝난 표는 정해진 틱이 지나면 지운다.
            for (int i = d.Tickets.Count - 1; i >= 0; i--)
            {
                var t = d.Tickets[i];
                if (t.Status != TicketStatus.Detonated && t.Status != TicketStatus.Cancelled) continue;
                if (w.Tick - t.EndTick < cfg.TicketKeepTicks) continue;
                d.ById.Remove(t.Id);
                d.Tickets.RemoveAt(i);
            }

            // 방향이 아직 고정되지 않은 구역(last_n_ticks)은 매 틱 플레이어를 따라간다.
            for (int i = 0; i < d.Tickets.Count; i++)
            {
                var t = d.Tickets[i];
                if (t.Status != TicketStatus.Active) continue;
                for (int z = 0; z < t.Zones.Count; z++)
                {
                    var zone = t.Zones[z];
                    if (zone.Cancelled || zone.Detonated || zone.DirLocked) continue;
                    TrackZone(w, zone, t.Specs[z]);
                }
            }

            int cap = w.Wave != null ? w.Wave.TelegraphCap : 0;
            int active = 0;
            for (int i = 0; i < w.Zones.Count; i++)
            {
                var z = w.Zones[i];
                if (!z.Cancelled && !z.Detonated && z.CountsToCap) active++;
            }

            // (요청 틱, id) 오름차순 = 목록 순서. 앞이 막혀도 뒤는 본다. 단 오래 굶은 요청이 막혀 있으면 그 뒤는 이 틱에 시작하지 않는다.
            bool starving = false;
            int count = d.Tickets.Count;
            for (int i = 0; i < count && !starving; i++)
            {
                var t = d.Tickets[i];
                if (t.Status != TicketStatus.Waiting || w.Tick < t.NextCheckTick) continue;

                EnemyState owner = null;
                if (t.OwnerEnemyId != 0)
                {
                    owner = w.FindEnemy(t.OwnerEnemyId);
                    if (owner == null || !owner.Alive)
                    {
                        // 요청자가 죽었다(죽어 가는 중 포함) → 폐기
                        t.Status = TicketStatus.Cancelled;
                        t.EndTick = w.Tick;
                        continue;
                    }
                }

                // 구역 위치·방향을 지금 플레이어 기준으로 다시 잡는다. 겨눌 사람이 없으면 이 틱은 건너뛴다.
                if (!ResolveAll(w, d, t, owner)) continue;

                int need = 0;
                for (int s = 0; s < t.Specs.Length; s++) if (!t.Specs[s].Harmless) need++;
                if (t.CountAsOne && need > 1) need = 1;
                bool counted = t.StaggerTicks <= 0;

                bool ok = true;
                if (!t.Bypass)
                {
                    if (counted && cap > 0 && active + need > cap) ok = false;
                    if (ok) ok = EscapeOk(w, d, t.Specs, d.GeomScratch);
                }

                if (ok)
                {
                    StartTicket(w, d, t, counted);
                    if (counted) active += need;
                }
                else if (t.StaggerTicks > 0) t.NextCheckTick = w.Tick + t.StaggerTicks;   // 늦게 청한 것부터 밀린다
                else if (cfg.StarveTicks > 0 && w.Tick - t.RequestTick > cfg.StarveTicks) starving = true;
            }
        }

        /// <summary>표의 구역 명세를 지금 기준의 기하로 바꿔 d.GeomScratch에 채운다(명세와 같은 순서).</summary>
        static bool ResolveAll(SimWorld w, ThreatWorldData d, TelegraphTicket t, EnemyState owner)
        {
            d.GeomScratch.Clear();
            for (int i = 0; i < t.Specs.Length; i++)
            {
                ShapeGeom g;
                if (!Resolve(w, t.Specs[i], owner, out g)) return false;
                d.GeomScratch.Add(g);
            }
            return true;
        }

        static bool Resolve(SimWorld w, in ZoneSpec s, EnemyState owner, out ShapeGeom g)
        {
            g = new ShapeGeom
            {
                Shape = s.Shape, Radius = s.Radius, InnerRadius = s.InnerRadius, Width = s.Width, Length = s.Length, FanDegrees = s.FanDegrees
            };
            Vector2 from = owner != null ? owner.Pos : s.Pos;
            PlayerState target = null;
            if (s.Anchor == ZoneAnchor.TargetPlayer || s.AimAtPlayer)
            {
                target = TargetFor(w, s.TargetSlot, from);
                if (target == null) return false;
            }

            if (s.Anchor == ZoneAnchor.TargetPlayer) g.Pos = target.Pos + s.Pos;
            else if (s.Anchor == ZoneAnchor.Owner && owner != null) g.Pos = owner.Pos + s.Pos;
            else g.Pos = s.Pos;

            g.Dir = ThreatGeometry.NormalizeOr(s.Dir, new Vector2(1, 0));
            if (s.AimAtPlayer) g.Dir = Aim(g.Pos, target.Pos, g.Dir, s.AimOffsetDegrees);
            FitLength(w, s.Length, ref g);
            return true;
        }

        static Vector2 Aim(Vector2 from, Vector2 to, Vector2 fallback, float offsetDegrees)
        {
            Vector2 dir = ThreatGeometry.NormalizeOr(to - from, fallback);
            return ThreatGeometry.Rotate(dir, offsetDegrees);
        }

        /// <summary>길이 0 이하 = 벽까지(시작·고정 때 레이캐스트로 정한다). 띠와 조준선만.</summary>
        static void FitLength(SimWorld w, float specLength, ref ShapeGeom g)
        {
            if (specLength > 0f) return;
            if (g.Shape != ZoneShape.Band && g.Shape != ZoneShape.AimLine) return;
            g.Length = ThreatGeometry.RayToWall(g.Pos, g.Dir, w.ArenaWidth, w.ArenaHeight);
        }

        void StartTicket(SimWorld w, ThreatWorldData d, TelegraphTicket t, bool counted)
        {
            t.Status = TicketStatus.Active;
            t.Started = true;
            t.StartTick = w.Tick;
            t.DetonateTick = w.Tick;
            bool firstCounted = true;
            for (int i = 0; i < t.Specs.Length; i++)
            {
                var s = t.Specs[i];
                var g = d.GeomScratch[i];
                int ticks = s.TelegraphTicks > 0 ? s.TelegraphTicks : 0;
                var z = new ZoneEntity
                {
                    Id = w.NewEntityId(),
                    CastId = t.CastId,
                    OwnerEnemyId = t.OwnerEnemyId,
                    Shape = g.Shape,
                    Pos = g.Pos,
                    Dir = g.Dir,
                    Radius = g.Radius,
                    InnerRadius = g.InnerRadius,
                    Width = g.Width,
                    Length = g.Length,
                    FanDegrees = g.FanDegrees,
                    StartTick = w.Tick,
                    DetonateTick = w.Tick + ticks,
                    BaseDamage = s.BaseDamage,
                    // 구역의 피해 분류에 "접촉"은 없다. 명세를 비워 둔 것(열거형 기본값)으로 보고 예고 공격으로 센다.
                    DamageSource = s.DamageSource == PlayerDamageSource.Contact ? PlayerDamageSource.Telegraph : s.DamageSource,
                    Grazeable = s.Grazeable,
                    Harmless = s.Harmless,
                    LeavesHazard = s.LeavesHazard,
                };
                z.CountsToCap = counted && !s.Harmless && (!t.CountAsOne || firstCounted);
                if (z.CountsToCap) firstCounted = false;
                bool tracks = s.LockLastTicks > 0;
                z.LockTick = tracks ? Mathf.Max(z.StartTick, z.DetonateTick - s.LockLastTicks) : z.StartTick;
                z.DirLocked = !tracks || z.LockTick <= z.StartTick;
                if (z.DetonateTick > t.DetonateTick) t.DetonateTick = z.DetonateTick;
                t.Zones.Add(z);
                w.Zones.Add(z);
                w.Emit(SimEventType.TelegraphStarted, entityId: z.Id, a: (int)z.Shape, b: z.DetonateTick - z.StartTick, pos: z.Pos);
                if (tracks && z.DirLocked) w.Emit(SimEventType.TelegraphLocked, entityId: z.Id, pos: z.Pos);
            }
        }

        /// <summary>last_n_ticks: s..s+N−n 동안 매 틱 플레이어 쪽으로 방향을 다시 잡고, s+N−n에 고정한다.</summary>
        static void TrackZone(SimWorld w, ZoneEntity z, in ZoneSpec s)
        {
            EnemyState owner = z.OwnerEnemyId != 0 ? w.FindEnemy(z.OwnerEnemyId) : null;
            if (s.Anchor == ZoneAnchor.Owner && owner != null) z.Pos = owner.Pos + s.Pos;
            if (s.AimAtPlayer)
            {
                var target = TargetFor(w, s.TargetSlot, owner != null ? owner.Pos : z.Pos);
                if (target != null) z.Dir = Aim(z.Pos, target.Pos, z.Dir, s.AimOffsetDegrees);
            }
            var g = ShapeGeom.Of(z);
            FitLength(w, s.Length, ref g);
            z.Length = g.Length;
            if (w.Tick >= z.LockTick)
            {
                z.DirLocked = true;
                w.Emit(SimEventType.TelegraphLocked, entityId: z.Id, pos: z.Pos);
            }
        }

        // ------------------------------------------------------------ R5

        public override bool CheckEscape(SimWorld w, IReadOnlyList<ZoneSpec> proposed, int ownerEnemyId)
        {
            var d = Data(w);
            EnemyState owner = ownerEnemyId != 0 ? w.FindEnemy(ownerEnemyId) : null;
            var geoms = new List<ShapeGeom>();
            var specs = new ZoneSpec[proposed != null ? proposed.Count : 0];
            for (int i = 0; i < specs.Length; i++)
            {
                specs[i] = proposed[i];
                ShapeGeom g;
                if (!Resolve(w, specs[i], owner, out g)) return true;   // 겨눌 사람이 없다 = 막을 길도 없다
                geoms.Add(g);
            }
            return EscapeOk(w, d, specs, geoms);
        }

        /// <summary>떠 있는 구역 + 남는 구역 + 장애물 + 새 구역을 함께 놓고, 서 있는 플레이어 모두에게 길이 남는지 본다.</summary>
        bool EscapeOk(SimWorld w, ThreatWorldData d, ZoneSpec[] specs, List<ShapeGeom> geoms)
        {
            return EscapeOk(w, d, specs, geoms, d.Config.R5Shrink);
        }

        /// <summary>줄이는 양을 바꿔 검사한다(검산용: 보정 없는 글자 그대로의 방법은 shrink = −0.15).</summary>
        public bool EscapeOk(SimWorld w, ThreatWorldData d, ZoneSpec[] specs, List<ShapeGeom> geoms, float shrink)
        {
            var list = d.EscapeScratch;
            list.Clear();
            for (int i = 0; i < w.Zones.Count; i++)
            {
                var z = w.Zones[i];
                if (z.Cancelled || z.Detonated || z.Harmless) continue;
                var g = ShapeGeom.Of(z);
                list.Add(MakeEscapeZone(g, z.DetonateTick, z.LeavesHazard));
            }
            for (int i = 0; i < specs.Length; i++)
            {
                if (specs[i].Harmless) continue;
                int ticks = specs[i].TelegraphTicks > 0 ? specs[i].TelegraphTicks : 0;
                list.Add(MakeEscapeZone(geoms[i], w.Tick + ticks, specs[i].LeavesHazard));
            }
            if (list.Count == 0) return true;

            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!CanBeHit(p)) continue;
                if (!d.Solver.HasEscape(d.Config, w.ArenaWidth, w.ArenaHeight, p.Pos, w.Tick, list, w.Hazards, w.Obstacles, shrink))
                    return false;
            }
            return true;
        }

        static EscapeZone MakeEscapeZone(in ShapeGeom g, int detonateTick, HazardDef leaves)
        {
            var ez = new EscapeZone { Geom = g, DetonateTick = detonateTick };
            if (leaves != null && ParseHazardKind(leaves.Kind) != HazardKind.FrostField)
            {
                ez.LeavesBlock = true;
                ez.BlockGeom = LeftHazardGeom(g, leaves);
            }
            return ez;
        }

        // ------------------------------------------------------------ 9단계: 번쩍, 판정

        void StepZones(SimWorld w, ThreatWorldData d)
        {
            var cfg = d.Config;
            int count = w.Zones.Count;
            for (int i = 0; i < count; i++)
            {
                var z = w.Zones[i];
                if (z.Cancelled || z.Detonated) continue;

                if (w.Tick < z.DetonateTick)
                {
                    // 번쩍: 판정 2틱 전(예고가 그보다 짧으면 시작 틱)
                    int flashTick = Mathf.Max(z.StartTick, z.DetonateTick - cfg.FlashLeadTicks);
                    if (cfg.FlashLeadTicks > 0 && w.Tick == flashTick && !z.Harmless)
                        w.Emit(SimEventType.TelegraphFlash, entityId: z.Id, a: z.DetonateTick - flashTick, pos: z.Pos);
                    continue;
                }

                // 판정 틱 = s + N. 차오름 끝과 같은 틱이다.
                z.Detonated = true;
                var g = ShapeGeom.Of(z);
                if (!z.Harmless && z.BaseDamage > 0)
                {
                    EnemyState owner = z.OwnerEnemyId != 0 ? w.FindEnemy(z.OwnerEnemyId) : null;
                    for (int s = 0; s < w.Players.Length; s++)
                    {
                        var p = w.Players[s];
                        if (!CanBeHit(p)) continue;
                        if (ThreatGeometry.Distance(g, p.Pos) >= HitRadius(cfg, p)) continue;
                        var hit = new PlayerHitRequest
                        {
                            AttackId = z.Id,
                            CastId = z.CastId,
                            SourceEnemyId = z.OwnerEnemyId,
                            DamageCells = DamageCells(w, z.BaseDamage, z.DamageSource, owner),
                            Source = z.DamageSource,
                            FromPos = z.Pos,
                        };
                        w.Mods.Player.QueueHit(w, p, hit);
                    }
                }
                w.Emit(SimEventType.TelegraphDetonated, entityId: z.Id, pos: z.Pos);
                if (z.LeavesHazard != null) SpawnLeftHazard(w, z, g);
            }

            // 표의 상태: 구역이 전부 끝났으면 Detonated(하나라도 터졌을 때) 또는 Cancelled.
            for (int i = 0; i < d.Tickets.Count; i++)
            {
                var t = d.Tickets[i];
                if (t.Status != TicketStatus.Active) continue;
                bool done = true, anyDetonated = t.Zones.Count == 0;
                for (int z = 0; z < t.Zones.Count; z++)
                {
                    if (t.Zones[z].Detonated) anyDetonated = true;
                    else if (!t.Zones[z].Cancelled) done = false;
                }
                if (!done) continue;
                t.Status = anyDetonated ? TicketStatus.Detonated : TicketStatus.Cancelled;
                t.EndTick = w.Tick;
            }
        }
    }
}
