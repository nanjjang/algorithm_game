using System;
using System.Numerics;
using Wizard.Content;

namespace Wizard.ViewLogic.Convert
{
    // 전장의 바닥과 기둥을 표시 좌표로 옮긴 것(ArenaDef: 팩의 arena 절). 표시 좌표는 1칸 = 1유닛, y 가 위로 는다.
    // 전장의 (0, 0) 은 왼쪽 위 모서리이므로 표시 좌표의 바닥은 x 0~W, y 0~−H 다.
    public sealed class ArenaOutline
    {
        // 기둥은 1×1칸이고 정의의 값은 그 칸의 작은 쪽 모서리다(ArenaDef, world-characters 2.6)
        public const float PillarSizeCells = 1f;

        private readonly Vector2[] _pillarCenters;

        private ArenaOutline(Vector2 size, Vector2[] pillarCenters)
        {
            Size = size;
            _pillarCenters = pillarCenters;
        }

        public Vector2 Size { get; }

        public Vector2 Center => new Vector2(Size.X / 2f, -Size.Y / 2f);

        public int PillarCount => _pillarCenters.Length;

        public Vector2 PillarCenter(int index)
        {
            if ((uint)index >= (uint)_pillarCenters.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _pillarCenters[index];
        }

        public static ArenaOutline From(ArenaDef def)
        {
            if (def == null)
            {
                throw new ArgumentNullException(nameof(def));
            }
            var size = new Vector2(FixConvert.ToFloat(def.Width), FixConvert.ToFloat(def.Height));
            var half = new Vector2(PillarSizeCells / 2f, -PillarSizeCells / 2f);
            var pillars = new Vector2[def.Pillars.Length];
            for (int i = 0; i < pillars.Length; i++)
            {
                pillars[i] = FixConvert.ToWorld(def.Pillars[i]) + half;
            }
            return new ArenaOutline(size, pillars);
        }
    }
}
