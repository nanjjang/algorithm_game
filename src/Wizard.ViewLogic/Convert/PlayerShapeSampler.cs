using System;
using System.Numerics;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;

namespace Wizard.ViewLogic.Convert
{
    // ISimView 의 플레이어(슬롯 순)를 도형으로 옮긴다. 위치는 PlayerView.Pos, 방향은 시야(Facing)다. 도형의 Id 는 슬롯 번호다.
    // 몸 반지름은 PlayerView 에 없어 캐릭터 정의(ContentDb, 팩의 characters 절)에서 읽는다(BodyRadiusFrom).
    // 시작 상태가 서기 전(첫 Step 전)의 플레이어는 옮기지 않는다(CONTRACTS 3.3: 그때의 PlayerView 는 MaxHpPoints 가 0 이다, T-54).
    public sealed class PlayerShapeSampler
    {
        private const byte NoOwner = 255;

        private readonly Func<CharacterId, float> _bodyRadiusCells;

        public PlayerShapeSampler(Func<CharacterId, float> bodyRadiusCells)
        {
            _bodyRadiusCells = bodyRadiusCells ?? throw new ArgumentNullException(nameof(bodyRadiusCells));
        }

        // 팩의 캐릭터 정의에서 몸 반지름(spec 01 5.6 판정 중심의 몸)을 읽는 조회. 캐릭터마다 한 번만 읽는다
        public static Func<CharacterId, float> BodyRadiusFrom(ContentDb db)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            var cache = new float[byte.MaxValue + 1];
            var known = new bool[byte.MaxValue + 1];
            return id =>
            {
                int k = (int)id;
                if (!known[k])
                {
                    cache[k] = FixConvert.ToFloat(db.Character(id).BodyRadius);
                    known[k] = true;
                }
                return cache[k];
            };
        }

        public void Fill(ISimView view, ShapeFrame frame)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }
            int n = view.PlayerCount;
            for (int slot = 0; slot < n; slot++)
            {
                PlayerView p = view.Player(new PlayerSlot((byte)slot));
                if (!IsPresent(in p))
                {
                    continue;
                }
                frame.AddPlayer(new ShapeSample(slot, ShapeKind.Player, FixConvert.ToWorld(p.Pos), _bodyRadiusCells(p.Character),
                                                FacingVector(p.Facing), HpRatio(p.HpPoints, p.MaxHpPoints), 0U, 0, NoOwner,
                                                (int)p.Character));
            }
        }

        // 이 플레이어가 세계에 서 있는가(시작 상태가 선 뒤). 쓰러진 플레이어(체력 0)도 서 있는 것이다
        public static bool IsPresent(in PlayerView p) => p.MaxHpPoints > 0;

        // 시야 방향의 표시 좌표 단위 벡터(반시계 +, N = 화면 위)
        public static Vector2 FacingVector(Dir8 facing)
        {
            double rad = FacingArrowSample.DegreesOf(facing) * Math.PI / FacingArrowSample.HalfTurnDegrees;
            return new Vector2((float)Math.Cos(rad), (float)Math.Sin(rad));
        }

        private static float HpRatio(int hp, int max)
        {
            if (max <= 0)
            {
                return 1f;
            }
            float r = (float)hp / max;
            return r < 0f ? 0f : (r > 1f ? 1f : r);
        }
    }
}
