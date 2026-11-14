using System.Collections.Generic;

namespace Wizard.Solo.View
{
    /// <summary>전투 HUD와 캐릭터 둘레 표시의 등록(00 문서 2.1절, 7절 표 AddHud = G17).</summary>
    public static partial class ViewRegistry
    {
        static partial void AddHud(List<IWorldViewLayer> layers) { layers.Add(new Hud.HudLayer()); }
    }
}
