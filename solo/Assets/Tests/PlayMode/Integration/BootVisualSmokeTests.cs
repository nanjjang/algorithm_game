using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.App;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.UI;
using Wizard.Solo.View;

namespace Wizard.Solo.Tests.PlayMode.Integration
{
    /// <summary>
    /// 통합 연기 테스트(00 문서 12절). Bootstrap.Boot → 월드(솔로 셰이더로 그린 스프라이트) → 타이틀 자리 화면 → 메인 메뉴 자리 화면이
    /// 오류 로그 없이 몇 초 돌고, 실제로 임포트된 그림·한글 글꼴·이름 표가 읽히는지 본다. 장면마다 스크린숏을 Logs/shots/에 남긴다
    /// (사람이 눈으로 확인하는 용도. 그래픽 장치가 있어야 하므로 -nographics 없이 돌린다. 갈무리하는 길은 SmokeCapture).
    /// 오류 로그(Debug.LogError·예외)는 테스트 러너가 실패로 잡는다 — 따로 단언하지 않는다.
    /// 저장은 임시 폴더(DataRoot)에만 한다. UnityEditor를 쓰지 않는다.
    /// </summary>
    public class BootVisualSmokeTests
    {
        const float SecondsPerScene = 1.5f;
        const int HangulFirst = 0xAC00, HangulLast = 0xD7A3;

        string _dataRoot;
        string _shotDir;
        BootResult _boot;
        readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _made.Clear();
            _dataRoot = Path.Combine(Path.GetTempPath(), "wizard-solo-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dataRoot);
            // 프로젝트 폴더(Assets의 부모)/Logs/shots. 플레이어에서 돌면 dataPath의 부모가 달라도 그 옆에 남는다.
            _shotDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "shots");
            Directory.CreateDirectory(_shotDir);
            _boot = Bootstrap.Boot(new BootOptions { BuildVersion = "smoke", DataRoot = _dataRoot, InputOverride = new BotInputSource() });
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < _made.Count; i++) if (_made[i] != null) UnityEngine.Object.Destroy(_made[i]);
            _made.Clear();
            Bootstrap.Shutdown();
            yield return null;
            try { if (Directory.Exists(_dataRoot)) Directory.Delete(_dataRoot, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        [UnityTest]
        public IEnumerator Boot_World_Title_MainMenu_RenderArtAndHangul()
        {
            var ctx = _boot.Context;
            Assert.IsNotNull(ctx, "context");
            Debug.Log("[smoke] screen " + Screen.width + "x" + Screen.height + ", ui scale " + UiScale.Compute(Screen.width, Screen.height) +
                      ", graphics " + SystemInfo.graphicsDeviceType + ", batch " + Application.isBatchMode);

            // ---- 기반 서비스가 전부 끼워졌다 ----
            Assert.IsNotNull(ctx.Clock, "clock");
            Assert.IsNotNull(ctx.Settings, "settings");
            Assert.IsNotNull(ctx.Content, "content");
            Assert.IsNotNull(ctx.Names, "names");
            Assert.IsNotNull(ctx.Art, "art");
            Assert.IsNotNull(ctx.Sound, "sound");
            Assert.IsNotNull(ctx.Profiles, "profiles");
            Assert.IsNotNull(ctx.Profile, "profile loaded");
            Assert.IsNotNull(ctx.Input, "input");
            Assert.IsNotNull(ctx.Session, "session");
            Assert.IsNotNull(ctx.Screens, "screens");
            Assert.IsTrue(_boot.FirstRun, "temp DataRoot has no settings file → first run");
            Assert.IsTrue(_boot.ProfileCreated, "temp DataRoot has no profile → created");
            Assert.IsTrue(File.Exists(Path.Combine(_dataRoot, "profiles", "offline", "profile.json")), "profile.json written under DataRoot");

            // ---- 데이터와 이름 표(Resources 경로) ----
            var db = ctx.Content as ContentDb;
            Assert.IsNotNull(db, "ContentDb");
            Assert.AreEqual(0, db.LoadErrors.Count, "content load errors: " + string.Join(" / ", db.LoadErrors));
            Assert.AreEqual(Ids.Char.Count, ctx.Content.Characters.Count, "characters");
            Assert.Greater(ctx.Content.Blocks.Count, 0, "blocks");
            Assert.AreEqual(6, ctx.Content.Bosses.Count, "bosses");
            Assert.IsNotNull(ctx.Content.SoloExtra(UiTuning.ExtraName), "DataSolo/F3.json");
            var names = ctx.Names as NameTable;
            Assert.IsNotNull(names, "NameTable");
            Assert.AreEqual(0, names.Errors.Count, "name table errors: " + string.Join(" / ", names.Errors));
            Assert.IsTrue(ctx.Names.Has(SmokeTitleScreen.TitleTextKey), "title key");
            Assert.IsTrue(HasHangul(ctx.Names.Get(SmokeTitleScreen.TitleTextKey)), "title text is Hangul");
            Assert.IsTrue(ctx.Names.Has("str.battle_hud.death_line"), "solo name fragment (NamesSolo/F1) merged");
            Assert.IsTrue(ctx.Names.Has("str.ui.hint.pick"), "solo name fragment (NamesSolo/F3) merged");

            // ---- 그림 창고: 실제 임포트된 그림(대체 그림이 아님) ----
            var art = ctx.Art as ArtLibrary;
            Assert.IsNotNull(art, "ArtLibrary");
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                string id = Ids.Char.Of(c);
                AssertRealSprite(ctx.Art.CharacterSelection(id), 192, 192, "selection " + id);
                AssertRealSprite(ctx.Art.CharacterPortrait(id, 0), 64, 64, "portrait " + id);
                var idle = ctx.Art.CharacterClip(id, "idle", Facing3.Down);
                Assert.IsNotNull(idle, "idle clip " + id);
                Assert.Greater(idle.Frames.Length, 0, "idle frames " + id);
                AssertRealSprite(idle.Frames[0], 0, 0, "idle frame " + id);
                AssertRealSprite(ctx.Art.WandInspection(id, 0), 192, 192, "wand inspection " + id);
                AssertRealSprite(ctx.Art.WandIcon(id, 13), 64, 64, "wand icon " + id);
                AssertRealSprite(ctx.Art.WandHand(id, 7, Facing3.Right, true), 0, 0, "wand hand " + id);
            }
            var slime = ctx.Art.MonsterClip(Ids.Enemy.SlimeMoss, null);
            Assert.Greater(slime.Frames.Length, 0, "monster frames");
            AssertRealSprite(slime.Frames[0], 0, 0, "monster frame");
            AssertRealSprite(ctx.Art.BossPortrait(Ids.Boss.KilnBoar), 0, 0, "boss portrait");
            AssertRealSprite(ctx.Art.Background("title"), 1920, 1080, "title background");
            var board = ctx.Art.Ui("K-01", "board");
            Assert.IsTrue(board.IsValid, "K-01 board");
            Assert.IsTrue(board.NineSlice, "K-01 is nine-slice");
            Assert.AreNotEqual(Vector4.zero, board.Sprite.border, "K-01 border from manifest");
            AssertRealSprite(board.Sprite, 0, 0, "K-01 board");
            AssertRealSprite(ctx.Art.Ui("K-20").Sprite, 0, 0, "K-20 button");
            AssertRealSprite(ctx.Art.Icon("block.fireball", 48), 0, 0, "icon block.fireball");

            // ---- 글꼴: 에셋의 TTF 둘, 동적, 한글 글리프 ----
            foreach (UiFontId size in Enum.GetValues(typeof(UiFontId)))
            {
                var font = ctx.Art.Font(size);
                Assert.IsNotNull(font, "font " + size);
                Assert.IsTrue(font.dynamic, "font is dynamic " + size);
                StringAssert.DoesNotContain("LegacyRuntime", font.name, "font is the project TTF, not the builtin fallback: " + size);
                Assert.IsTrue(font.HasCharacter((char)HangulFirst), "font has Hangul " + size);
            }
            StringAssert.Contains("Galmuri", ctx.Art.Font(UiFontId.F12).name);
            StringAssert.Contains("neodgm", ctx.Art.Font(UiFontId.F16).name);

            // ---- 1. 월드: 솔로 셰이더 셋으로 그린 스프라이트·도형(화면은 아직 없다 — 진짜 타이틀은 G18) ----
            var runner = ctx.Session as GameRunner;
            Assert.IsNotNull(runner, "GameRunner");
            var cam = runner.Host.Camera;
            Assert.IsNotNull(cam, "camera");
            BuildWorldSample(ctx, runner.Host);
            foreach (SpriteMaterialKind kind in Enum.GetValues(typeof(SpriteMaterialKind)))
            {
                var m = ctx.Art.Material(kind);
                Assert.IsNotNull(m, "material " + kind);
                Assert.IsNotNull(m.shader, "shader " + kind);
                Assert.IsTrue(m.shader.isSupported, "shader supported " + kind + " (" + m.shader.name + ")");
                StringAssert.StartsWith("Solo/", m.shader.name, "solo shader, not a fallback: " + kind);
            }
            yield return RunFor(SecondsPerScene);
            yield return Shot("01-world");

            // ---- 2. 타이틀 자리 화면 ----
            var mgr = ctx.Screens as ScreenManager;
            Assert.IsNotNull(mgr, "ScreenManager");
            if (!mgr.IsRegistered(ScreenId.Title)) ctx.Screens.Register(ScreenId.Title, () => new SmokeTitleScreen());
            if (!mgr.IsRegistered(ScreenId.MainMenu)) ctx.Screens.Register(ScreenId.MainMenu, () => new SmokeMainMenuScreen());
            if (ctx.Screens.Top == ScreenId.None) ctx.Screens.Push(ScreenId.Title, Bootstrap.FirstScreenArgs(_boot));
            else ctx.Screens.PopTo(ScreenId.Title, Bootstrap.FirstScreenArgs(_boot));
            yield return null;
            Assert.AreEqual(ScreenId.Title, ctx.Screens.Top, "title on top");
            yield return RunFor(SecondsPerScene);
            yield return Shot("02-title");
            AssertHangulTextRendered("title screen");

            // ---- 3. 메인 메뉴 자리 화면(메뉴 키로 커서를 옮겨 본다) ----
            ctx.Screens.Replace(ScreenId.MainMenu);
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, ctx.Screens.Top, "main menu on top");
            mgr.Dispatch(MenuKey.Down);
            mgr.Dispatch(MenuKey.Down);
            yield return RunFor(SecondsPerScene);
            ctx.Screens.Toast(ctx.Names.Get("str.main_menu.offline_guide"));
            yield return RunFor(0.5f);
            yield return Shot("03-mainmenu");
            AssertHangulTextRendered("main menu screen");

            // ---- 확인 창(Modal)도 한 번 ----
            bool? answer = null;
            ctx.Screens.Confirm(null, "str.main_menu.server_only", true, ok => answer = ok);
            yield return null;
            Assert.AreEqual(ScreenId.ConfirmDialog, ctx.Screens.Top, "confirm dialog on top");
            yield return RunFor(0.5f);
            yield return Shot("04-confirm");
            ctx.Screens.Pop(false);
            yield return null;
            Assert.AreEqual(false, answer, "confirm result delivered");
            Assert.AreEqual(ScreenId.MainMenu, ctx.Screens.Top, "back on main menu");

            Assert.AreEqual(0, art.Warnings.Count, "art warnings: " + string.Join(" / ", art.Warnings));
        }

        // ------------------------------------------------------------------ 도우미

        static bool HasHangul(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++) if (s[i] >= HangulFirst && s[i] <= HangulLast) return true;
            return false;
        }

        /// <summary>임포트된 그림에서 자른 스프라이트인가: 대체 그림이 아니고, Point 필터, PPU 64. w·h가 0보다 크면 크기도 본다.</summary>
        static void AssertRealSprite(Sprite s, int w, int h, string what)
        {
            Assert.IsNotNull(s, what);
            Assert.IsNotNull(s.texture, what + " texture");
            StringAssert.DoesNotStartWith("placeholder_", s.texture.name, what + " is a placeholder");
            Assert.AreEqual(FilterMode.Point, s.texture.filterMode, what + " filter");
            // 교체본 중 2배 해상도(제자 1 pixelScale 2)는 PPU가 64의 정수배다 — 화면 크기는 같고 세부만 촘촘하다.
            float ppuMul = s.pixelsPerUnit / SimConst.PixelsPerUnit;
            Assert.That(ppuMul >= 1f && Mathf.Abs(ppuMul - Mathf.Round(ppuMul)) < 1e-4f, what + " PPU " + s.pixelsPerUnit);
            Assert.AreEqual(1, s.texture.mipmapCount, what + " has no mipmaps");
            if (w > 0) Assert.AreEqual(w, Mathf.RoundToInt(s.rect.width / ppuMul), what + " width (논리 px)");
            if (h > 0) Assert.AreEqual(h, Mathf.RoundToInt(s.rect.height / ppuMul), what + " height (논리 px)");
        }

        /// <summary>
        /// 한글이 든 Text가 켜져 있고, uGUI가 그 글의 메시를 만들었고(갈무리 뒤에 부른다 — 배치 모드에서는 갈무리가 캔버스를 그리게 한다),
        /// 그 글꼴이 그 크기로 한글 글리프를 찍어 낸다. 글자 모양이 맞는지는 스크린숏을 사람이 본다.
        /// </summary>
        static void AssertHangulTextRendered(string what)
        {
            int hangulTexts = 0;
            var texts = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (!t.isActiveAndEnabled || !HasHangul(t.text)) continue;
                hangulTexts++;
                Assert.IsNotNull(t.font, what + ": text has a font");
                Assert.Greater(t.cachedTextGenerator.vertexCount, 0, what + ": text mesh was generated");
                t.font.RequestCharactersInTexture(t.text, t.fontSize, t.fontStyle);
                for (int k = 0; k < t.text.Length; k++)
                {
                    char ch = t.text[k];
                    if (ch < HangulFirst || ch > HangulLast) continue;
                    CharacterInfo info;
                    Assert.IsTrue(t.font.GetCharacterInfo(ch, out info, t.fontSize, t.fontStyle),
                        what + ": glyph U+" + ((int)ch).ToString("X4") + " not in the font texture (" + t.font.name + " " + t.fontSize + ")");
                    Assert.Greater(info.glyphWidth, 0, what + ": glyph has width");
                    break;
                }
            }
            Assert.Greater(hangulTexts, 0, what + ": no visible Hangul text");
        }

        /// <summary>월드에 본보기를 놓는다: 캐릭터 다섯(몸 + 지팡이), 몬스터, 보스, 외곽선·번쩍, 가산 효과, 정점 색 도형.</summary>
        void BuildWorldSample(GameContext ctx, WorldViewHost host)
        {
            var pool = host.Pool;
            Assert.IsNotNull(pool, "sprite pool");
            var block = new MaterialPropertyBlock();
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                string id = Ids.Char.Of(c);
                var body = pool.Rent(ctx.Art.CharacterClip(id, "idle", Facing3.Down).Frames[0], SortBands.Bodies + c);
                body.transform.position = SimConst.ToView(new Vector2(4f + c * 2.5f, 9f), 14f);
                var wand = pool.Rent(ctx.Art.WandHand(id, c * 2 + 3, Facing3.Down, false), SortBands.Bodies + c + 10);
                wand.transform.position = body.transform.position + new Vector3(0.6f, 0f, 0f);
            }
            string[] monsters = { Ids.Enemy.SlimeMoss, Ids.Enemy.WolfBramble, Ids.Enemy.GolemMenhir, Ids.Enemy.BatDusk, Ids.Boss.KilnBoar };
            for (int i = 0; i < monsters.Length; i++)
            {
                var r = pool.Rent(ctx.Art.MonsterClip(monsters[i], null).Frames[0], SortBands.Bodies + 30 + i);
                r.transform.position = SimConst.ToView(new Vector2(4f + i * 4f, 4f), 14f);
                if (i == 1)
                {
                    // 엘리트 금색 2px 외곽선(SpriteShaderProps)
                    r.GetPropertyBlock(block);
                    block.SetColor(SpriteShaderProps.OutlineColor, UiTheme.Gold);
                    block.SetFloat(SpriteShaderProps.OutlineSize, 2f);
                    r.SetPropertyBlock(block);
                }
                if (i == 2)
                {
                    // 피격 흰 번쩍
                    r.GetPropertyBlock(block);
                    block.SetColor(SpriteShaderProps.FlashColor, Color.white);
                    block.SetFloat(SpriteShaderProps.FlashAmount, 0.7f);
                    r.SetPropertyBlock(block);
                }
            }
            // 가산 혼합(32px 이하 그림만): 흰 원을 청록으로
            var glow = pool.Rent(ctx.Art.Circle(16), SortBands.PlayerShots, SpriteMaterialKind.SpriteAdditive);
            glow.color = UiTheme.ArcaneTeal;
            glow.transform.position = SimConst.ToView(new Vector2(12f, 6.5f), 14f);
            // 정점 색 도형(예고 구역 자리): 네모 한 장
            var mr = pool.RentMesh(SortBands.TelegraphFill);
            var mesh = new Mesh { name = "smoke-quad" };
            _made.Add(mesh);
            mesh.vertices = new[] { new Vector3(-2f, -1f), new Vector3(2f, -1f), new Vector3(2f, 1f), new Vector3(-2f, 1f) };
            var tint = new Color(UiTheme.Amber.r, UiTheme.Amber.g, UiTheme.Amber.b, 0.5f);
            mesh.colors = new[] { tint, tint, tint, tint };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mr.GetComponent<MeshFilter>().sharedMesh = mesh;
            mr.transform.position = SimConst.ToView(new Vector2(12f, 11.5f), 14f);
            // 바닥: 대체 바둑판(그림이 없을 때의 대체 도형이 위험색을 쓰지 않는지 눈으로 본다)
            var floor = pool.Rent(ctx.Art.Placeholder(128, 64), SortBands.Floor);
            floor.transform.position = SimConst.ToView(new Vector2(21f, 11.5f), 14f);
        }

        static IEnumerator RunFor(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            int frames = 0;
            while (Time.realtimeSinceStartup < end || frames < 5)
            {
                frames++;
                yield return null;
            }
        }

        /// <summary>스크린숏을 Logs/shots/&lt;name&gt;.png로 남긴다. 파일이 실제로 생기고 비어 있지 않은지까지 본다.</summary>
        IEnumerator Shot(string name)
        {
            string path = Path.Combine(_shotDir, name + ".png");
            string how = null;
            var runner = (GameRunner)_boot.Context.Session;
            yield return SmokeCapture.Capture(runner.Host.Camera, path, h => how = h);
            Assert.IsTrue(File.Exists(path), "screenshot not written: " + path);
            Assert.Greater(new FileInfo(path).Length, 1024L, "screenshot is empty: " + path);
            Debug.Log("[smoke] screenshot (" + how + ") " + path);
        }
    }
}
