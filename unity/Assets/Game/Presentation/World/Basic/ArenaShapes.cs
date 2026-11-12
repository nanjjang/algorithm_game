#nullable enable
using UnityEngine;
using Wizard.Unity.Presentation.Core;
using Wizard.ViewLogic.Convert;

namespace Wizard.Unity.Presentation.World.Basic
{
    // 전장의 바닥(사각)과 기둥(1×1칸 사각). 값은 ArenaOutline(팩의 arena 절을 표시 좌표로 옮긴 것)에서만 온다
    public static class ArenaShapes
    {
        public static void Draw(ShapeCanvas canvas, ArenaOutline arena)
        {
            canvas.Box(UnityConvert.ToUnity(arena.Center), UnityConvert.ToUnity(arena.Size), 0f, ShapePalette.Floor, ShapeStyle.OrderFloor);
            var pillar = new Vector2(ArenaOutline.PillarSizeCells, ArenaOutline.PillarSizeCells);
            for (int i = 0; i < arena.PillarCount; i++)
            {
                canvas.Box(UnityConvert.ToUnity(arena.PillarCenter(i)), pillar, 0f, ShapePalette.Pillar, ShapeStyle.OrderPillar);
            }
        }
    }
}
