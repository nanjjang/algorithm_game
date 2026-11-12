#nullable enable
using System;
using UnityEngine;
using Wizard.Content;
using Wizard.ViewLogic.Convert;

namespace Wizard.Unity.Presentation.Core
{
    // WorldPresenter 를 만들 때 주는 것. 조립 루트(App)가 채운다:
    //   Arena           = ArenaOutline.From(db.Arena(모드))      없으면 바닥과 카메라 맞추기를 하지 않는다
    //   BodyRadiusCells = PlayerShapeSampler.BodyRadiusFrom(db)  플레이어 몸 반지름(칸)
    //   Parent          = 도형 뿌리를 매달 곳. 조립 루트의 개체에 매달면 장면이 바뀌어도 조립 루트가 치울 때까지 산다
    // 적·탄·포탑은 따로 받지 않는다. ISimView 의 Enemies·Projectiles 에서 읽는다(CONTRACTS 3.3, T-54).
    public sealed class WorldPresenterSetup
    {
        public const int DefaultPlayerCapacity = 4;       // 자료구조 크기(PlayerSlot 0..3)
        public const int DefaultEntityCapacity = 512;     // 자료구조 크기: 한 화면의 적과 탄(플레이어 탄 상한 + 적 탄 상한 150 + 적)보다 크다

        public WorldPresenterSetup(Func<CharacterId, float> bodyRadiusCells)
        {
            BodyRadiusCells = bodyRadiusCells ?? throw new ArgumentNullException(nameof(bodyRadiusCells));
        }

        public Func<CharacterId, float> BodyRadiusCells { get; }

        public ArenaOutline? Arena { get; set; }

        public Transform? Parent { get; set; }

        public int PlayerCapacity { get; set; } = DefaultPlayerCapacity;

        public int EntityCapacity { get; set; } = DefaultEntityCapacity;

        // 주 카메라가 없으면 만든다(장면에는 시작 개체만 있다, T-32)
        public bool EnsureCamera { get; set; } = true;

        public bool ShowDebugText { get; set; } = true;
    }
}
