using System;
using Wizard.ViewLogic.Presentation;

namespace Wizard.AssetCheck.Check
{
    // 그림이 어느 쪽 그림인가(spec 14 5.7.1절 3번, 4.2절 2번).
    //  - 플레이어 쪽: Character(몸, 초상, 치장 겹그림 cosmetic/…), Wand, Ui, Background, World(배경·소품, 4.2절 2번),
    //    Fx 의 enemy/ 밖 묶음. "FX 의 위험색 전용 그림은 enemy/ 묶음에만 있어야 한다 [임시]"이므로
    //    player/special/wand/ritual 뿐 아니라 loot/status 도 위험색 0픽셀이다.
    //  - 적 쪽: Monster 와 Fx 의 enemy/ — 위험색은 허용되고 Monster 는 적 몸의 색 규칙(CheckEnemyBody)을 따로 받는다
    public enum AssetSide
    {
        PlayerSide = 0,
        Enemy = 1,
    }

    public static class AssetSides
    {
        public const string EnemyFxGroup = "enemy/";
        public const string CosmeticVariantPrefix = "cosmetic/";   // 치장 겹그림의 열쇠 [임시: spec 14 9절 U-10 꼴, char.N/cosmetic/<칸>/<키>]

        public static AssetSide Of(AssetKey key)
        {
            switch (key.Pack)
            {
                case AssetPack.Monster:
                    return AssetSide.Enemy;
                case AssetPack.Fx:
                    return key.Id.StartsWith(EnemyFxGroup, StringComparison.Ordinal) ? AssetSide.Enemy : AssetSide.PlayerSide;
                default:
                    return AssetSide.PlayerSide;
            }
        }

        public static bool IsCosmetic(AssetKey key) =>
            key.Pack == AssetPack.Character && key.Variant != null && key.Variant.StartsWith(CosmeticVariantPrefix, StringComparison.Ordinal);
    }
}
