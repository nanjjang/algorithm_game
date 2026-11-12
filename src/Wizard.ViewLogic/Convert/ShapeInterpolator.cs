using System;
using System.Collections.Generic;
using System.Numerics;

namespace Wizard.ViewLogic.Convert
{
    // 틱 사이 보간(ARCHITECTURE 6.1 "Views.Render(session.View, alpha) — 위치 보간"). 표시 전용이고 세계를 모른다.
    //
    // 프레임마다 마지막으로 돈 틱(Tick)의 묶음을 하나 받는다. 한 프레임에 틱이 k 개 돌았으면 앞 묶음은 k 틱 전의 것이다.
    // 그림은 한 틱 늦게 그린다: 그리는 시각 = 지금 틱 − 1 + alpha. 앞 묶음(틱 T−k)에서 지금 묶음(틱 T)으로 가는 직선 위의
    // 비율 t = (k − 1 + alpha) ÷ k 의 자리다(k = 1 이면 t = alpha). 짝은 id 로 찾고(플레이어는 슬롯), 이렇게 둔다:
    //   - 앞 묶음에 없는 개체(새로 생김)는 지금 자리에 그린다.
    //   - 종류가 다르거나 한 틱에 SnapCellsPerTick 칸보다 많이 움직인 개체(순간 이동, 밀려남)는 지금 자리에 그린다.
    //   - 틱이 뒤로 가면(새 세계, 다시 시작) 앞 묶음을 버린다.
    public sealed class ShapeInterpolator
    {
        // [임시] 한 틱에 이보다 많이 움직이면 보간하지 않는다. 시제품의 가장 빠른 것(탄)은 한 틱에 1칸이 안 된다.
        // 반동 사격의 넉백(2칸)처럼 한 틱에 옮겨지는 것은 끊어 그린다. OPEN-QUESTIONS(표시 조정값)
        public const float SnapCellsPerTick = 1f;

        private ShapeFrame _prev;
        private ShapeFrame _curr;
        private readonly Dictionary<int, int> _prevIndex;   // 조회만 한다(순회하지 않는다)

        public ShapeInterpolator(int playerCapacity, int entityCapacity)
        {
            _prev = new ShapeFrame(playerCapacity, entityCapacity);
            _curr = new ShapeFrame(playerCapacity, entityCapacity);
            _prevIndex = new Dictionary<int, int>(entityCapacity);
        }

        public ShapeFrame Current => _curr;

        public ShapeFrame Previous => _prev;

        // 이 틱의 묶음을 이미 받았는가
        public bool Has(uint tick) => _curr.HasTick && _curr.Tick == tick;

        // 새 틱의 묶음을 채우기 시작한다. 지금 묶음이 앞 묶음이 된다. 돌려준 묶음을 채운 뒤 End 를 부른다
        public ShapeFrame Begin(uint tick)
        {
            ShapeFrame old = _prev;
            _prev = _curr;
            _curr = old;
            if (_prev.HasTick && unchecked((int)(tick - _prev.Tick)) <= 0)
            {
                _prev.Clear();                              // 틱이 뒤로 갔거나 같다: 이어지는 세계가 아니다
            }
            _curr.Reset(tick);
            return _curr;
        }

        public void End()
        {
            _prevIndex.Clear();
            for (int i = 0; i < _prev.EntityCount; i++)
            {
                _prevIndex[_prev.Entity(i).Id] = i;
            }
        }

        public void Clear()
        {
            _prev.Clear();
            _curr.Clear();
            _prevIndex.Clear();
        }

        // 앞 묶음과 지금 묶음 사이의 틱 수(앞 묶음이 없으면 0)
        public int TickSpan => _prev.HasTick && _curr.HasTick ? unchecked((int)(_curr.Tick - _prev.Tick)) : 0;

        // 앞 묶음 → 지금 묶음 직선 위의 비율 0~1
        public float Progress(double alpha)
        {
            int k = TickSpan;
            if (k <= 0)
            {
                return 1f;
            }
            double a = alpha < 0 ? 0 : (alpha > 1 ? 1 : alpha);
            return (float)((k - 1 + a) / k);
        }

        public ShapeSample Player(int index, double alpha)
        {
            ref readonly ShapeSample c = ref _curr.Player(index);
            if (index >= _prev.PlayerCount)
            {
                return c;
            }
            return Blend(in _prev.Player(index), in c, alpha);
        }

        public ShapeSample Entity(int index, double alpha)
        {
            ref readonly ShapeSample c = ref _curr.Entity(index);
            if (!_prevIndex.TryGetValue(c.Id, out int p))
            {
                return c;
            }
            return Blend(in _prev.Entity(p), in c, alpha);
        }

        private ShapeSample Blend(in ShapeSample prev, in ShapeSample curr, double alpha)
        {
            int k = TickSpan;
            if (k <= 0 || prev.Kind != curr.Kind)
            {
                return curr;
            }
            float limit = SnapCellsPerTick * k;
            if (Vector2.DistanceSquared(prev.Pos, curr.Pos) > limit * limit)
            {
                return curr;
            }
            return curr.WithPos(Vector2.Lerp(prev.Pos, curr.Pos, Progress(alpha)));
        }
    }
}
