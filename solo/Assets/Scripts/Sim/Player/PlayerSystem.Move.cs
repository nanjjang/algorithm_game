using UnityEngine;

namespace Wizard.Solo.Sim.Player
{
    // 4단계 이동과 비기의 순간 이동. 02 요약서 4.5·4.6절.
    public sealed partial class PlayerSystem
    {
        /// <summary>지금 이동 속력(칸/초): 자세면 자세 속도로 교체, 스침 가속 ×, 환경 감속 ×(02 요약서 4.5절).</summary>
        public float MoveSpeed(SimWorld w, PlayerState p)
        {
            if (p.Def == null) return 0f;
            float speed = p.InStance ? p.Def.StanceSpeed : p.Def.WalkSpeed;
            // 가속은 겹치지 않는다(시간만 갱신).
            if (w.Tick < p.GrazeBuffUntilTick) speed *= p.Def.GrazeBuffSpeed;
            return speed * p.EnvSpeedMul;
        }

        public override void Stage4_Move(SimWorld w)
        {
            bool control = w.PlayerControlActive;
            for (int s = 0; s < w.Players.Length; s++)
            {
                PlayerState p = w.Players[s];
                if (!p.Active) continue;
                p.PrevPos = p.Pos;
                Dir8 dir;
                if (!control || p.Downed || p.InputLocked || p.Def == null || !PlayerRules.TryMoveDir(p.Keys, out dir))
                {
                    p.Moving = false;
                    continue;
                }
                p.Moving = true;
                p.MoveDir = dir;

                // 대각도 단위 벡터라 속력이 같다.
                Vector2 delta = Dir8Util.ToSimVector(dir) * (MoveSpeed(w, p) * SimConst.TickSeconds);
                Vector2 target = p.Pos + delta;
                ObstacleEntity hit;
                if (w.Mods.Threats.SegmentHitsObstacle(w, p.Pos, target, false, out hit))
                {
                    // 장애물: 축별로 따로 가 본다(미끄러짐). 둘 다 막히면 제자리.
                    Vector2 alongX = new Vector2(p.Pos.x + delta.x, p.Pos.y);
                    Vector2 alongY = new Vector2(p.Pos.x, p.Pos.y + delta.y);
                    if (delta.x != 0f && !w.Mods.Threats.SegmentHitsObstacle(w, p.Pos, alongX, false, out hit)) target = alongX;
                    else if (delta.y != 0f && !w.Mods.Threats.SegmentHitsObstacle(w, p.Pos, alongY, false, out hit)) target = alongY;
                    else target = p.Pos;
                }
                // 벽: 몸 반지름만큼 안쪽으로 축별로 자른다(벽 따라 미끄러짐).
                p.Pos = w.ClampToArena(target, p.Def.BodyRadius);
            }
        }

        /// <summary>
        /// 비기의 순간 이동. 벽 앞에서 멈추고, stopAtEnemies면 살아 있는 적의 몸 앞에서, stopAtObstacles면 장애물 앞에서 멈춘다.
        /// 도착 위치를 돌려준다. 이벤트(PlayerMovedByArt)는 부른 쪽(Arts)이 낸다.
        /// </summary>
        public override Vector2 MoveByArt(SimWorld w, PlayerState p, Vector2 delta, bool stopAtEnemies, bool stopAtObstacles)
        {
            if (p == null || !p.Active) return p != null ? p.Pos : Vector2.zero;
            float body = p.Def != null ? p.Def.BodyRadius : 0f;
            Vector2 from = p.Pos;
            float t = 1f;

            // 벽: 몸 반지름을 뺀 안쪽 상자를 벗어나는 지점까지.
            t = Mathf.Min(t, WallLimit(from.x, delta.x, body, w.ArenaWidth - body));
            t = Mathf.Min(t, WallLimit(from.y, delta.y, body, w.ArenaHeight - body));

            if (stopAtObstacles)
            {
                ObstacleEntity hit;
                if (w.Mods.Threats.SegmentHitsObstacle(w, from, from + delta * t, false, out hit))
                {
                    float th;
                    if (hit != null && (PlayerRules.SegmentEntersCircle(from, delta, hit.Pos, hit.Radius + body, out th)
                                        || PlayerRules.SegmentEntersCircle(from, delta, hit.Pos, hit.Radius, out th)))
                        t = Mathf.Min(t, th);
                    else t = 0f;
                }
            }

            if (stopAtEnemies)
            {
                for (int i = 0; i < w.Enemies.Count; i++)
                {
                    EnemyState e = w.Enemies[i];
                    // 죽어 가는 적은 없는 것으로 본다(공통 규칙 2). 땅속의 적은 몸이 없다.
                    if (!e.Alive || e.Burrowed) continue;
                    float te;
                    if (PlayerRules.SegmentEntersCircle(from, delta, e.Pos, e.BodyRadius + body, out te)) t = Mathf.Min(t, te);
                }
            }

            if (t < 0f) t = 0f;
            p.Pos = w.ClampToArena(from + delta * t, body);
            return p.Pos;
        }

        /// <summary>한 축에서 [lo, hi]를 벗어나지 않는 이동 비율(0~1).</summary>
        static float WallLimit(float pos, float d, float lo, float hi)
        {
            if (d > 0f) return pos >= hi ? 0f : Mathf.Min(1f, (hi - pos) / d);
            if (d < 0f) return pos <= lo ? 0f : Mathf.Min(1f, (lo - pos) / d);
            return 1f;
        }
    }
}
