using UnityEngine;
using Wizard.Solo.App;
using Wizard.Solo.Art;
using Wizard.Solo.Tests.Art;
using Wizard.Solo.View;

namespace Wizard.Solo.Tests.ViewWorld
{
    /// <summary>디스크에서 읽는 실제 그림 창고(임포터 없이)와 실제 SpritePool로 표시 층을 세우는 도우미.</summary>
    public static class TestArt
    {
        static ArtLibrary _lib;

        /// <summary>Assets/Resources/Art를 직접 읽는 그림 창고(처음 쓸 때 한 번).</summary>
        public static ArtLibrary Library
        {
            get
            {
                if (_lib == null) _lib = new ArtLibrary(new DiskArtSource());
                return _lib;
            }
        }
    }

    /// <summary>표시 문맥 하나(WorldRoot + SpritePool). 시험이 끝나면 Destroy로 걷는다.</summary>
    public sealed class TestViewRig
    {
        public readonly GameObject Root;
        public readonly SpritePool Pool;
        public readonly ViewContext Vc;

        public TestViewRig(Wizard.Solo.Sim.SimWorld w, ViewOptions options = null)
        {
            Root = new GameObject("G14TestRoot");
            var game = new GameContext { Art = TestArt.Library, Content = TestContent.Db };
            Pool = new SpritePool(game.Art, Root.transform);
            Vc = new ViewContext
            {
                Game = game,
                World = w,
                WorldRoot = Root.transform,
                Pool = Pool,
                Options = options ?? new ViewOptions()
            };
        }

        public void Destroy()
        {
            Pool.DestroyAll();
            Object.DestroyImmediate(Root);
        }
    }
}
