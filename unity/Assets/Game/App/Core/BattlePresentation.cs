#nullable enable
using System;
using UnityEngine;
using Wizard.Content;
using Wizard.Unity.Presentation.Core;
using Wizard.ViewLogic.Convert;

namespace Wizard.Unity.App.Core
{
    // Battle 장면의 표시기 묶음(ARCHITECTURE 5·6.1). 조립 루트가 세션을 꽂을 때 같이 만들고, 장면을 떠나거나 앱이 끝날 때 치운다.
    // 지금 든 것은 회색 도형의 세계 표시기(WorldPresenter, M1-UE-02) 하나다. 표시기는 ISimView 와 표시 이벤트만 읽는다
    // (IFramePresenter, CONTRACTS 13, T-54). HUD·연출 표시기가 생기면 여기에 더한다.
    public sealed class BattlePresentation : IDisposable
    {
        private readonly FixedTickDriver _driver;
        private readonly WorldPresenter _world;
        private bool _disposed;

        public BattlePresentation(FixedTickDriver driver, WorldPresenterSetup setup)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _world = new WorldPresenter(setup ?? throw new ArgumentNullException(nameof(setup)));
            _driver.AddPresenter(_world);
        }

        public WorldPresenter World => _world;

        // 팩에서 전장과 몸 반지름을 읽어 만든다. parent 에 도형 뿌리를 매단다(조립 루트의 개체: 장면이 바뀌어도 치울 때까지 산다)
        public static BattlePresentation For(FixedTickDriver driver, ContentDb db, SimMode mode, Transform? parent)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            var setup = new WorldPresenterSetup(PlayerShapeSampler.BodyRadiusFrom(db))
            {
                Arena = ArenaOutline.From(db.Arena(mode)),
                Parent = parent,
            };
            return new BattlePresentation(driver, setup);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _driver.RemovePresenter(_world);
            _world.Dispose();
        }
    }
}
