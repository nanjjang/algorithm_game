using System.Collections.Generic;
using Wizard.Solo.Screens.Story;
using Wizard.Solo.View.Effects;

namespace Wizard.Solo.View
{
    public static class ViewRegistry
    {
        // 이 순서로 표시 층을 만들고 매 프레임 갱신한다.
        public static List<IWorldViewLayer> CreateAll()
        {
            var shared = new EffectsShared();
            var effects = new EffectLayer(shared);
            return new List<IWorldViewLayer>
            {
                new World.ArenaLayer(),
                new World.CharacterLayer(),
                new Enemies.EnemyLayer(),
                new Enemies.ThreatLayer(),
                new Enemies.OffscreenMarkerLayer(),
                new ShotLayer(shared),
                effects,
                new JuiceLayer(effects.Shared, effects),
                new StyleFxLayer(effects.Shared, effects),
                new DeathSequenceLayer(),
                new Hud.HudLayer(),
                new AudioLayer(),
                new StoryLayer(),
                new TutorialLayer()
            };
        }
    }
}
