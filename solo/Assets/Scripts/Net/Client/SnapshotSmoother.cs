using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 스냅숏 사이 보간(11-online.md 3절). 스냅숏을 덮어쓰기 전의 표시 자리를 id(플레이어는 슬롯)로 기억했다가, 덮어쓴 뒤 PrevPos에 넣는다.
    /// 그러면 뷰의 Lerp(PrevPos, Pos, alpha)가 "앞 스냅숏 → 지금 스냅숏"을 스냅숏 간격(3틱) 동안 잇는다(alpha = CoopClient.InterpAlpha).
    /// 새로 생긴 것은 스냅숏의 PrevPos 그대로(서버의 한 틱 전).
    /// </summary>
    public sealed class SnapshotSmoother
    {
        readonly Vector2[] _players = new Vector2[SimConst.MaxPlayers];
        readonly bool[] _hasPlayer = new bool[SimConst.MaxPlayers];
        readonly Dictionary<int, Vector2> _enemies = new Dictionary<int, Vector2>();
        readonly Dictionary<int, Vector2> _shots = new Dictionary<int, Vector2>();
        readonly Dictionary<int, Vector2> _enemyShots = new Dictionary<int, Vector2>();

        public void Capture(SimWorld w)
        {
            for (int i = 0; i < _players.Length && i < w.Players.Length; i++)
            {
                _hasPlayer[i] = w.Players[i].Active;
                _players[i] = w.Players[i].Pos;
            }
            _enemies.Clear();
            foreach (var e in w.Enemies) _enemies[e.Id] = e.Pos;
            _shots.Clear();
            foreach (var p in w.Projectiles) _shots[p.Id] = p.Pos;
            _enemyShots.Clear();
            foreach (var s in w.EnemyShots) _enemyShots[s.Id] = s.Pos;
        }

        public void Restore(SimWorld w)
        {
            for (int i = 0; i < _players.Length && i < w.Players.Length; i++)
                if (_hasPlayer[i] && w.Players[i].Active) w.Players[i].PrevPos = _players[i];
            foreach (var e in w.Enemies) if (_enemies.TryGetValue(e.Id, out var v)) e.PrevPos = v;
            foreach (var p in w.Projectiles) if (_shots.TryGetValue(p.Id, out var v)) p.PrevPos = v;
            foreach (var s in w.EnemyShots) if (_enemyShots.TryGetValue(s.Id, out var v)) s.PrevPos = v;
        }
    }
}
