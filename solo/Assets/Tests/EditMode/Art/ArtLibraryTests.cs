using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.Tests.Art
{
    /// <summary>
    /// ArtLibrary를 디스크 원천(DiskArtSource)으로 돌린다: 늦게 읽기, 자르기 rect·pivot, 캐시, Release, 대체 그림, 머티리얼.
    /// 기대 좌표는 manifest 값(09 요약서)을 RectMath 공식(00 문서 10절)으로 바꾼 것.
    /// </summary>
    public class ArtLibraryTests
    {
        DiskArtSource _src;
        ArtLibrary _art;

        [SetUp]
        public void SetUp()
        {
            _src = new DiskArtSource();
            _art = new ArtLibrary(_src);
        }

        [TearDown]
        public void TearDown()
        {
            _art.Dispose();
        }

        [Test]
        public void Boot_ParsesManifestsOnly_NoTextureLoaded()
        {
            Assert.AreEqual(0, _src.TextureLoads.Count);
            Assert.AreEqual(0, _art.LoadedTextureCount);
            Assert.AreEqual(0, _art.CachedSpriteCount);
            Assert.AreEqual(5, _art.Players.Characters.Count);
            Assert.AreEqual(9, _art.Effects.Groups.Count, "효과 7묶음 + 보강 2묶음(enhance, layers)");
            Assert.IsEmpty(_art.Warnings, string.Join("\n", _art.Warnings));
        }

        [Test]
        public void CharacterClip_CutsOnlyRequestedSheet_WithFeetPivot()
        {
            var a = _art.CharacterClip(Ids.Char.C1, "idle", Facing3.Down);
            Assert.AreEqual(4, a.Frames.Length);
            Assert.AreEqual(7f, a.Fps);
            Assert.IsTrue(a.Loop);
            Assert.AreEqual(1, _src.TextureLoads.Count);
            var s = a.Frames[0];
            Assert.AreEqual(new Rect(0, 832 - 64, 64, 64), s.rect);
            Assert.AreEqual(30f, s.pivot.x, 1e-4f);
            Assert.AreEqual(4f, s.pivot.y, 1e-4f);
            Assert.AreEqual(64f, s.pixelsPerUnit);
            // 칸 1 = (64, 0)
            Assert.AreEqual(new Rect(64, 768, 64, 64), a.Frames[1].rect);
            // 캐시: 다시 불러도 같은 것, 텍스처를 다시 읽지 않는다
            Assert.AreSame(a, _art.CharacterClip(Ids.Char.C1, "idle", Facing3.Down));
            var right = _art.CharacterClip(Ids.Char.C1, "idle", Facing3.Right);
            Assert.AreEqual(new Rect(256, 768, 64, 64), right.Frames[0].rect); // 칸 4
            Assert.AreEqual(1, _src.TextureLoads.Count);
            // 아래 방향만 있는 클립은 위를 물어도 아래 클립
            Assert.AreEqual(6, _art.CharacterClip(Ids.Char.C1, "death", Facing3.Up).Frames.Length);
            Assert.IsNotNull(_art.CharacterClipInfo(Ids.Char.C1, "fire", Facing3.Right));
        }

        [Test]
        public void CharacterPortraitAndSelection()
        {
            var p = _art.CharacterPortrait(Ids.Char.C3, 2);
            Assert.AreEqual(new Rect(128, 0, 64, 64), p.rect);
            Assert.AreEqual(new Rect(192, 0, 64, 64), _art.CharacterPortrait(Ids.Char.C3, 9).rect);
            Assert.AreEqual(192f, _art.CharacterSelection(Ids.Char.C3).rect.width);
        }

        [Test]
        public void WandCells_RectAndGripPivot()
        {
            var h = _art.WandHand(Ids.Char.C2, 5, Facing3.Right, true);
            // manifest (160, 192, 32, 64) in 448×384 → Unity y = 384 − 192 − 64 = 128
            Assert.AreEqual(new Rect(160, 128, 32, 64), h.rect);
            Assert.AreEqual(16f, h.pivot.x, 1e-4f);
            Assert.AreEqual(16f, h.pivot.y, 1e-4f); // 64 − 48
            var i8 = _art.WandInspection(Ids.Char.C2, 8);
            Assert.AreEqual(new Rect(192, 0, 192, 192), i8.rect);
            var i6 = _art.WandInspection(Ids.Char.C2, 6);
            Assert.AreEqual(new Rect(1152, 192, 192, 192), i6.rect);
            Assert.AreEqual(new Rect(832, 0, 64, 64), _art.WandIcon(Ids.Char.C2, 13).rect);
            Assert.AreEqual(3, _art.WandStageInfo(Ids.Char.C2, 0).Mutations.Count);
            Assert.IsNotNull(_art.WandDetachedPart(Ids.Char.C2));
            Assert.IsNull(_art.WandDetachedPart(Ids.Char.C1));
        }

        [Test]
        public void MonsterClip_PerFrameOriginAndD37Fps()
        {
            var d = _art.MonsterClip(Ids.Enemy.BatDusk, "death");
            Assert.AreEqual(2, d.Frames.Length);
            Assert.AreEqual(8f, d.Fps);
            Assert.IsFalse(d.Loop);
            Assert.AreEqual(32f, d.Frames[0].pivot.x, 1e-4f);
            Assert.AreEqual(10f, d.Frames[0].pivot.y, 1e-4f); // 64 − 54
            Assert.AreEqual(6f, d.Frames[1].pivot.y, 1e-4f);  // 64 − 58
            Assert.IsTrue(_art.MonsterClip(Ids.Enemy.BatDusk, "fly").Loop);
            // 없는 클립 → 첫 클립, 엘리트 id → 바탕 그림
            Assert.AreEqual(3, _art.MonsterClip(Ids.Enemy.BatDusk, "nope").Frames.Length);
            Assert.AreEqual(4, _art.MonsterClip("elite.wolf_bramble", "move").Frames.Length);
            var missing = _art.MonsterClip("enemy.unknown", "move");
            Assert.IsNotNull(missing);
            Assert.AreEqual(1, missing.Frames.Length);
            Assert.IsNotNull(missing.Frames[0]);
            Assert.AreEqual(96f, _art.BossPortrait(Ids.Boss.KilnBoar).rect.width);
            Assert.AreEqual(32f, _art.BossHudIcon(Ids.Boss.KilnBoar).rect.width);
        }

        [Test]
        public void Effect_MultiRowGridAndRelease()
        {
            var crack = _art.Effect("ritual", "screen_crack");
            Assert.AreEqual(3, crack.Frames.Length);
            Assert.AreEqual(new Rect(0, 2160, 1920, 1080), crack.Frames[0].rect);
            Assert.AreEqual(new Rect(0, 0, 1920, 1080), crack.Frames[2].rect);
            Assert.IsTrue(_art.IsTextureLoaded("effects/ritual/screen_crack_1920x1080_f3.png"));
            var hit = _art.Effect("player", "hit_fire");
            Assert.AreEqual(12f, hit.Fps);

            _art.Release("ritual");
            Assert.IsFalse(_art.IsTextureLoaded("effects/ritual/screen_crack_1920x1080_f3.png"));
            Assert.IsTrue(_art.IsTextureLoaded("effects/player/hit_fire_32x32_f3.png"), "다른 묶음은 남는다");
            Assert.AreEqual(1, _src.TextureUnloads.Count);
            var again = _art.Effect("ritual", "screen_crack");
            Assert.AreNotSame(crack, again);
            Assert.IsNotNull(again.Frames[0]);
            Assert.AreEqual(2, _src.TextureLoads.FindAll(p => p.EndsWith("screen_crack_1920x1080_f3")).Count);
        }

        [Test]
        public void Effect_MissingIsSingleFramePlaceholder()
        {
            var e = _art.Effect("player", "no_such_effect");
            Assert.AreEqual(1, e.Frames.Length);
            Assert.IsNotNull(e.Frames[0]);
        }

        [Test]
        public void Effect_SupplementGroups()
        {
            var steam = _art.Effect(ArtLibrary.LayersGroup, "reactive/char.1/steam:threshold");
            Assert.AreEqual(4, steam.Frames.Length);
            var stamp = _art.Effect(ArtLibrary.EnhanceGroup, "protected_stamp");
            Assert.AreEqual(3, stamp.Frames.Length);
            Assert.IsNotNull(_art.LayerInfo("reactive/char.1/steam").Raw["attachment"]);
        }

        [Test]
        public void Ui_NineSliceBorderInUnityOrder()
        {
            var b = _art.Ui("K-20", "button_large", "hover");
            Assert.IsTrue(b.IsValid);
            Assert.IsTrue(b.NineSlice);
            Assert.AreEqual(48, b.Width);
            Assert.AreEqual(64, b.Height);
            Assert.AreEqual(new Vector4(12, 12, 12, 12), LogicalBorder(b.Sprite));
            Assert.That(b.Sprite.texture.width, Is.GreaterThan(b.Width), "solo skin uses the higher-resolution texture");
            var h = _art.Ui("K-04");
            Assert.AreEqual(new Vector4(24, 12, 24, 12), LogicalBorder(h.Sprite));
            var slider = _art.Ui("K-30", "slider_fill");
            Assert.AreEqual(new Vector4(4, 2, 4, 2), slider.Sprite.border);
            // 없는 상태 → normal, 없는 id → 대체
            Assert.IsTrue(_art.Ui("K-20", "button_large", "on").IsValid);
            Assert.IsTrue(_art.Ui("K-999").IsValid);
            var loading = _art.UiAnim("K-44");
            Assert.AreEqual(6, loading.Frames.Length);
            Assert.AreEqual(10f, loading.Fps, 1e-4f);
            Assert.AreEqual(new Rect(160, 0, 32, 32), loading.Frames[5].rect);
        }

        static Vector4 LogicalBorder(Sprite sprite) => sprite.border * (RectMath.PixelsPerUnit / sprite.pixelsPerUnit);

        [Test]
        public void Icon_ContentIdsAndSizes()
        {
            Assert.AreEqual(24f, _art.Icon(Ids.Block.Fireball, 24).rect.width);
            Assert.AreEqual(48f, _art.Icon(Ids.Block.Fireball, 48).rect.width);
            Assert.AreEqual(16f, _art.Icon(Ids.Element.Ice, 16).rect.width);
            Assert.AreEqual(64f, _art.Icon(Ids.Element.Fire, 64).rect.width);
            var mut = _art.Icon(Ids.Mutation.Gluttony, 32);
            Assert.AreEqual(new Rect(96, 0, 32, 32), mut.rect); // 코드 4번째 = 칸 3
            Assert.AreEqual(32f, _art.Icon(Ids.Boss.FrostGoat, 32).rect.width);
            Assert.AreEqual(96f, _art.Icon(Ids.Boss.FrostGoat, 96).rect.width);
            Assert.IsNotNull(_art.Icon(Ids.Tod.Day, 32));
            Assert.AreEqual(32f, _art.Icon(Ids.Tod.Day, 32).rect.width);
            Assert.AreEqual(_art.Placeholder(24, 24), _art.Icon("currency.starlight", 24));
        }

        [Test]
        public void World_TodTilesPosesAndBackgrounds()
        {
            var night = _art.World("field.tile.grass.01", Ids.Tod.Night);
            Assert.AreEqual(1, night.Frames.Length);
            Assert.IsTrue(_art.IsTextureLoaded("world/tiles/field.night.png"));
            Assert.IsFalse(_art.IsTextureLoaded("world/tiles/field.day.png"));
            var wake = _art.World("facility.master_chair:wake");
            Assert.AreEqual(2, wake.Frames.Length);
            Assert.AreEqual(new Rect(384, 96, 96, 96), wake.Frames[0].rect); // 칸 4 = (384, 0) in 768×192
            Assert.AreEqual(6f, wake.Fps);
            Assert.AreEqual(4, _art.World("field.weather.rain").Frames.Length);
            Assert.AreEqual(new Rect(0, 0, 1920, 1080), _art.Background(ArtLibrary.TitleKey).rect);
            Assert.AreEqual(1920f, _art.Background(ArtLibrary.WorkbenchKey).rect.width);
            Assert.AreEqual(1920f, _art.Background("S-0501").rect.width);
            Assert.IsNotNull(_art.WorldMapJson("field.01"));
        }

        [Test]
        public void Generated_PlaceholderWhitePixelCircle()
        {
            var p = _art.Placeholder(10, 20);
            Assert.AreEqual(new Rect(0, 0, 10, 20), p.rect);
            Assert.AreSame(p, _art.Placeholder(10, 20));
            Assert.AreEqual(1f, _art.WhitePixel.pixelsPerUnit);
            Assert.AreEqual(new Rect(0, 0, 1, 1), _art.WhitePixel.rect);
            var c = _art.Circle(8);
            Assert.AreEqual(new Rect(0, 0, 16, 16), c.rect);
            Assert.AreNotSame(c, _art.Circle(8, true, 2));
        }

        [Test]
        public void Material_NeverNull_Cached_HasProps()
        {
            foreach (SpriteMaterialKind k in System.Enum.GetValues(typeof(SpriteMaterialKind)))
            {
                var m = _art.Material(k);
                Assert.IsNotNull(m, k.ToString());
                Assert.AreSame(m, _art.Material(k));
            }
            // 실제 셰이더(Resources/Shaders)를 읽으면 SpriteShaderProps 속성이 있다
            var real = new ArtLibrary(new DiskArtSource { ProvideShaders = true });
            var sm = real.Material(SpriteMaterialKind.Sprite);
            if (sm.shader.name == "Solo/Sprite")
            {
                Assert.IsTrue(sm.HasProperty(SpriteShaderProps.FlashColor));
                Assert.IsTrue(sm.HasProperty(SpriteShaderProps.FlashAmount));
                Assert.IsTrue(sm.HasProperty(SpriteShaderProps.OutlineColor));
                Assert.IsTrue(sm.HasProperty(SpriteShaderProps.OutlineSize));
            }
            else Assert.Fail("Resources/Shaders/SoloSprite를 읽지 못했다: " + sm.shader.name);
            Assert.AreEqual("Solo/SpriteAdditive", real.Material(SpriteMaterialKind.SpriteAdditive).shader.name);
            Assert.AreEqual("Solo/Shape", real.Material(SpriteMaterialKind.Shape).shader.name);
            real.Dispose();
        }

        [Test]
        public void Font_NeverNull()
        {
            Assert.IsNotNull(_art.Font(UiFontId.F12));
            Assert.AreEqual("Art/ui/font/Galmuri11", ArtLibrary.FontPathFor(UiFontId.F24));
            Assert.AreEqual("Art/ui/font/neodgm", ArtLibrary.FontPathFor(UiFontId.F16));
            Assert.AreEqual("Art/ui/font/neodgm", ArtLibrary.FontPathFor(UiFontId.F64));
        }
    }
}
