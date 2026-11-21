using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.World
{
    /// <summary>
    /// 캐릭터 층(00 문서 7절 AddCharacters): 활동 중인 플레이어마다 PlayerFigure 하나(몸 37클립 3방향, 지팡이, 그림자,
    /// 무적 깜빡임, 스침 잔상, 피격 번쩍, 림, 지팡이 열 반응·표식, 덧그림). 상태를 읽기만 한다.
    /// Build는 진행 중인 세계에도 불린다 — 쓰러짐·자세·이동·무적 같은 것은 이벤트 없이 상태에서 고른다.
    /// </summary>
    public sealed class CharacterLayer : IWorldViewLayer
    {
        ViewContext _vc;
        WorldTuning _t;
        float _time;
        readonly PlayerFigure[] _figures = new PlayerFigure[SimConst.MaxPlayers];

        /// <summary>시험용: 슬롯의 그림(없으면 null).</summary>
        public PlayerFigure Figure(int slot) => slot >= 0 && slot < _figures.Length ? _figures[slot] : null;
        public WorldTuning Tuning => _t;

        public void Build(ViewContext vc, SimWorld w)
        {
            Dispose();
            _vc = vc;
            _t = WorldTuning.From(w != null ? w.Content : vc != null && vc.Game != null ? vc.Game.Content : null);
            _time = 0f;
            if (w == null) return;
            SyncFigures(w);
            Render(w, 1f, 0f);
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (e.Slot < 0 || e.Slot >= _figures.Length) return;
            var f = _figures[e.Slot];
            if (f == null && w != null) { SyncFigures(w); f = _figures[e.Slot]; }
            if (f != null) f.OnEvent(w, e, _time);
        }

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (w == null) return;
            _time += dt;
            SyncFigures(w);
            for (int i = 0; i < _figures.Length; i++)
                if (_figures[i] != null) _figures[i].Render(w, w.Players[i], alpha, _time);
        }

        public void Dispose()
        {
            for (int i = 0; i < _figures.Length; i++)
            {
                if (_figures[i] != null) _figures[i].Dispose();
                _figures[i] = null;
            }
        }

        void SyncFigures(SimWorld w)
        {
            for (int i = 0; i < _figures.Length && i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                bool want = p != null && p.Active && !string.IsNullOrEmpty(p.CharId);
                if (want && (_figures[i] == null || _figures[i].CharId != p.CharId))
                {
                    if (_figures[i] != null) _figures[i].Dispose();
                    _figures[i] = new PlayerFigure(_vc, _t, p, w);
                }
                else if (!want && _figures[i] != null)
                {
                    _figures[i].Dispose();
                    _figures[i] = null;
                }
            }
        }
    }
}
