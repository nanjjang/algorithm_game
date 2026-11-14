using System.Collections.Generic;

namespace Wizard.Solo.View
{
    /// <summary>G15 표시 층 등록(00 문서 2.1·7절): AddEnemies = 적·보스 몸과 표식, AddThreats = 예고·적 탄·남는 구역·장애물·별똥.</summary>
    public static partial class ViewRegistry
    {
        static partial void AddEnemies(List<IWorldViewLayer> layers)
        {
            layers.Add(new Enemies.EnemyLayer());
        }

        static partial void AddThreats(List<IWorldViewLayer> layers)
        {
            layers.Add(new Enemies.ThreatLayer());
            // 화면 밖 적·보스·예고 가장자리 화살(따라가는 줌 카메라 때문에 필요 — 줄일 수 없는 표시)
            layers.Add(new Enemies.OffscreenMarkerLayer());
        }
    }
}
