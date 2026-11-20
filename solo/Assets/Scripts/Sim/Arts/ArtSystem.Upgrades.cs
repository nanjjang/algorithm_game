using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Arts
{
    /// <summary>
    /// 고유 비기 강화 하나의 진행(판 안의 상태, ArtPlayerState.Sequences). 주인: Arts.
    /// 비기의 비용·커맨드·원래 효과는 그대로이고, 이 단계들이 덧피해를 더한다(원래 총량의 +20% 이내 — Docs/12-attack-styles.md 4절).
    /// </summary>
    public sealed class ArtSequence
    {
        public string ArtId;
        /// <summary>단계 하나가 떨어지는 틱 = FirstTick + Index × GapTicks(소용돌이는 PulseTicks마다, UntilTick까지).</summary>
        public int FirstTick;
        public int GapTicks;
        public int Index;
        public int Count;
        public int UntilTick;
        public Vector2 Center;
        public Vector2 Dir;
        public Vector2 Start, End;
        public float Radius;
        /// <summary>단계 하나가 적 하나에 주는 피해(시전 순간 n × T × 캐릭터 배율).</summary>
        public float Amount;
        public string Element;
        public int StunTicks, FreezeTicks;
        public float Pull;
        /// <summary>소용돌이가 붙은 사본 마법진 지대 id(지대가 사라지면 같이 끝난다).</summary>
        public int ZoneId = -1;
        /// <summary>이 강화에서 이미 맞은 적(적마다 한 번 — 소용돌이 제외).</summary>
        public readonly List<int> HitSet = new List<int>();
        public bool Done;
    }

    // 고유 비기 강화(게임 규칙). 손잡이 = AttackStyles.json art_upgrades.enabled(명세 값 기본 꺼짐, FunPass가 켠다).
    // 피해는 Direct(→ Spells.DealDirect) 하나로: 비기 피해(DamageOrigin.Art), 캐릭터 속성, 콤보·임계 없음. 단계마다 ArtStage 이벤트.
    public sealed partial class ArtSystem
    {
        static float F(JObject o, string key, float fallback = 0f)
        {
            var v = o != null ? o[key] : null;
            return v != null && (v.Type == JTokenType.Float || v.Type == JTokenType.Integer) ? (float)v : fallback;
        }

        static int UpTicks(float seconds) => seconds > 0f ? Mathf.Max(1, SimConst.Ticks(seconds)) : 0;

        ArtSequence NewSequence(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c, JObject up)
        {
            float t = w.Mods.Spells.StandardT(w, p);
            var q = new ArtSequence
            {
                ArtId = c.Def.Id,
                Amount = F(up, "damage") * t * c.CharMul,
                Element = CharElement(p),
                Radius = F(up, "radius"),
                FirstTick = w.Tick + UpTicks(F(up, "first_delay_seconds")),
                GapTicks = UpTicks(F(up, "gap_seconds")),
                Dir = Dir8Util.ToSimVector(p.Facing)
            };
            s.Sequences.Add(q);
            return q;
        }

        /// <summary>불가마: 내리치는 순간 둘레에 불기둥 고리가 바깥으로 연달아 솟는다(고리 k = 반경 radii[k-1] ~ radii[k], 적마다 한 번).</summary>
        void StartKilnPillars(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c)
        {
            var up = AttackStyles.ArtUpgrade(w.Content, c.Def.Id);
            if (up == null || !(up["ring_radii"] is JArray radii) || radii.Count == 0) return;
            var q = NewSequence(w, p, s, c, up);
            q.Center = p.Pos;
            q.Count = radii.Count;
        }

        /// <summary>벼락 질주: 지나간 길을 n칸으로 나눈 가운데마다 예고 뒤 낙뢰(반경 안 적마다 한 번).</summary>
        void StartBoltTrail(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c, Vector2 start, Vector2 end)
        {
            var up = AttackStyles.ArtUpgrade(w.Content, c.Def.Id);
            if (up == null) return;
            int n = (int)F(up, "strikes");
            if (n <= 0 || (end - start).sqrMagnitude <= 0f) return;
            var q = NewSequence(w, p, s, c, up);
            q.Start = start;
            q.End = end;
            q.Count = n;
            for (int i = 0; i < n; i++)
                w.Emit(SimEventType.ArtStage, p.Slot, id: q.ArtId, a: (int)ArtStageKind.Warn, b: i, pos: TrailPoint(q, i), f: q.Radius,
                    pos2: new Vector2(q.FirstTick + i * q.GapTicks - w.Tick, 0f));
        }

        static Vector2 TrailPoint(ArtSequence q, int i) => Vector2.Lerp(q.Start, q.End, (i + 0.5f) / Mathf.Max(1, q.Count));

        /// <summary>베껴 쓰기: 사본 마법진 둘레의 책장 소용돌이가 간격마다 적을 가운데로 끌어당기며 지속 피해(지대가 사라지면 끝).</summary>
        void StartPageVortex(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c)
        {
            var up = AttackStyles.ArtUpgrade(w.Content, c.Def.Id);
            if (up == null) return;
            var z = FindZone(w, p.Slot, PlayerZoneKind.CopyCircle);
            if (z == null) return;
            var q = NewSequence(w, p, s, c, up);
            q.Center = z.Pos;
            q.ZoneId = z.Id;
            q.UntilTick = z.UntilTick;
            q.Pull = F(up, "pull_per_pulse");
            q.GapTicks = Mathf.Max(1, UpTicks(F(up, "pulse_seconds")));
            q.FirstTick = w.Tick + q.GapTicks;
            q.Count = int.MaxValue;
        }

        /// <summary>얼음 병풍: 병풍 자리에서 시야 쪽으로 얼음 가시가 줄지어 솟아 띄우고(짧은 기절) 얼린다(적마다 한 번).</summary>
        void StartIceSpikes(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c)
        {
            var up = AttackStyles.ArtUpgrade(w.Content, c.Def.Id);
            if (up == null) return;
            var z = FindZone(w, p.Slot, PlayerZoneKind.IceScreen);
            Vector2 origin = z != null ? z.Pos : p.Pos;
            var q = NewSequence(w, p, s, c, up);
            q.Center = origin;
            q.Count = (int)F(up, "spikes");
            q.Start = origin + q.Dir * F(up, "start");
            q.End = q.Start + q.Dir * (F(up, "step") * Mathf.Max(0, q.Count - 1));
            q.StunTicks = UpTicks(F(up, "stun_seconds"));
            q.FreezeTicks = UpTicks(F(up, "freeze_seconds"));
        }

        static Vector2 SpikePoint(ArtSequence q, int i) =>
            q.Count <= 1 ? q.Start : Vector2.Lerp(q.Start, q.End, i / (float)(q.Count - 1));

        /// <summary>그림자 손: 움켜쥔(지금) 자리를 정해진 틱 뒤 짓눌러 반경 안 범위 피해 한 번.</summary>
        void StartShadowCrush(SimWorld w, PlayerState p, ArtPlayerState s, ArtCast c, Vector2 center)
        {
            var up = AttackStyles.ArtUpgrade(w.Content, c.Def.Id);
            if (up == null) return;
            var q = NewSequence(w, p, s, c, up);
            q.Center = center;
            q.Count = 1;
            q.FirstTick = w.Tick + UpTicks(F(up, "crush_delay_seconds"));
            w.Emit(SimEventType.ArtStage, p.Slot, id: q.ArtId, a: (int)ArtStageKind.Grip, b: 0, pos: center, f: q.Radius);
        }

        // ------------------------------------------------------------ 5단계 진행

        void RunSequences(SimWorld w, PlayerState p, ArtPlayerState s)
        {
            for (int i = 0; i < s.Sequences.Count; i++)
            {
                var q = s.Sequences[i];
                if (!q.Done) StepSequence(w, p, q);
            }
            s.Sequences.RemoveAll(q => q.Done);
        }

        void StepSequence(SimWorld w, PlayerState p, ArtSequence q)
        {
            switch (q.ArtId)
            {
                case Ids.Art.Kiln:
                    while (q.Index < q.Count && w.Tick >= q.FirstTick + q.Index * q.GapTicks)
                    {
                        var up = AttackStyles.ArtUpgrade(w.Content, q.ArtId);
                        var radii = up != null ? up["ring_radii"] as JArray : null;
                        float inner = q.Index > 0 && radii != null ? (float)radii[q.Index - 1] : 0f;
                        float outer = radii != null && q.Index < radii.Count ? (float)radii[q.Index] : inner;
                        w.Emit(SimEventType.ArtStage, p.Slot, id: q.ArtId, a: (int)ArtStageKind.Rise, b: q.Index, pos: q.Center, f: outer,
                            pos2: new Vector2(inner, 0f));
                        HitRing(w, p, q, inner, outer);
                        q.Index++;
                    }
                    if (q.Index >= q.Count) q.Done = true;
                    break;

                case Ids.Art.BoltDash:
                    while (q.Index < q.Count && w.Tick >= q.FirstTick + q.Index * q.GapTicks)
                    {
                        Vector2 at = TrailPoint(q, q.Index);
                        w.Emit(SimEventType.ArtStage, p.Slot, id: q.ArtId, a: (int)ArtStageKind.Land, b: q.Index, pos: at, f: q.Radius);
                        HitCircle(w, p, q, at, q.Radius, true, 0, 0);
                        q.Index++;
                    }
                    if (q.Index >= q.Count) q.Done = true;
                    break;

                case Ids.Art.CopyCircle:
                    {
                        var z = FindZoneById(w, q.ZoneId);
                        if (z == null || w.Tick >= q.UntilTick)
                        {
                            w.Emit(SimEventType.ArtStage, p.Slot, id: q.ArtId, a: (int)ArtStageKind.End, b: q.Index, pos: q.Center, f: q.Radius);
                            q.Done = true;
                            break;
                        }
                        if (w.Tick < q.FirstTick + q.Index * q.GapTicks) break;
                        w.Emit(SimEventType.ArtStage, p.Slot, id: q.ArtId, a: (int)ArtStageKind.Pulse, b: q.Index, pos: q.Center, f: q.Radius);
                        int n = w.Enemies.Count;
                        for (int i = 0; i < n; i++)
                        {
                            var en = w.Enemies[i];
                            if (!en.Targetable) continue;
                            Vector2 to = q.Center - en.Pos;
                            float dist = to.magnitude;
                            if (dist > q.Radius + en.BodyRadius) continue;
                            float pull = Mathf.Min(q.Pull, dist);
                            if (pull > 0f && dist > 0f) w.Mods.Enemies.ApplyCc(w, en, CcKind.Pull, pull, CcSource.ArtStage, to / dist);
                            if (en.Targetable) Direct(w, p, en, q.Amount, q.Element, DamageOrigin.Art, false, q.ArtId);
                        }
                        q.Index++;
                        break;
                    }

                case Ids.Art.IceScreen:
                    while (q.Index < q.Count && w.Tick >= q.FirstTick + q.Index * q.GapTicks)
                    {
                        Vector2 at = w.ClampToArena(SpikePoint(q, q.Index), 0f);
                        w.Emit(SimEventType.ArtStage, p.Slot, id: q.ArtId, a: (int)ArtStageKind.Rise, b: q.Index, pos: at, f: q.Radius, pos2: q.Dir);
                        HitCircle(w, p, q, at, q.Radius, true, q.StunTicks, q.FreezeTicks);
                        q.Index++;
                    }
                    if (q.Index >= q.Count) q.Done = true;
                    break;

                case Ids.Art.ShadowHand:
                    if (w.Tick < q.FirstTick) break;
                    w.Emit(SimEventType.ArtStage, p.Slot, id: q.ArtId, a: (int)ArtStageKind.Crush, b: 0, pos: q.Center, f: q.Radius);
                    HitCircle(w, p, q, q.Center, q.Radius, true, 0, 0);
                    q.Done = true;
                    break;

                default:
                    q.Done = true;
                    break;
            }
        }

        /// <summary>고리(inner, outer] 안의 적(몸 겉면 기준)을 적마다 한 번 때린다.</summary>
        void HitRing(SimWorld w, PlayerState p, ArtSequence q, float inner, float outer)
        {
            int n = w.Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var en = w.Enemies[i];
                if (!en.Targetable || q.HitSet.Contains(en.Id)) continue;
                float d = (en.Pos - q.Center).magnitude;
                if (d - en.BodyRadius > outer || d + en.BodyRadius < inner) continue;
                q.HitSet.Add(en.Id);
                Direct(w, p, en, q.Amount, q.Element, DamageOrigin.Art, false, q.ArtId);
            }
        }

        /// <summary>원(중심, 반경 + 몸) 안의 적을 때린다. once = 이 강화에서 적마다 한 번. 기절 → 빙결 순(띄웠다가 얼린다).</summary>
        void HitCircle(SimWorld w, PlayerState p, ArtSequence q, Vector2 center, float radius, bool once, int stunTicks, int freezeTicks)
        {
            int n = w.Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var en = w.Enemies[i];
                if (!en.Targetable) continue;
                if (once && q.HitSet.Contains(en.Id)) continue;
                if ((en.Pos - center).magnitude > radius + en.BodyRadius) continue;
                if (once) q.HitSet.Add(en.Id);
                var res = Direct(w, p, en, q.Amount, q.Element, DamageOrigin.Art, false, q.ArtId);
                if (res.Killed || !en.Targetable) continue;
                if (stunTicks > 0) w.Mods.Enemies.ApplyCc(w, en, CcKind.Stun, stunTicks, CcSource.ArtStage, q.Dir);
                if (freezeTicks > 0) w.Mods.Enemies.ApplyCc(w, en, CcKind.Freeze, freezeTicks, CcSource.ArtStage, q.Dir);
            }
        }
    }
}
