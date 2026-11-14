using System.Collections.Generic;

namespace Wizard.Solo.View
{
    /// <summary>G14 등록(00 문서 2.1절): 전장 층과 캐릭터 층. 더한 순서대로 Build·Render된다(전장이 먼저).</summary>
    public static partial class ViewRegistry
    {
        static partial void AddArena(List<IWorldViewLayer> layers) { layers.Add(new World.ArenaLayer()); }
        static partial void AddCharacters(List<IWorldViewLayer> layers) { layers.Add(new World.CharacterLayer()); }
    }
}
