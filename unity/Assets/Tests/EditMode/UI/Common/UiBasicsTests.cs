#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Wizard.Client;
using Wizard.Unity.UI.Common;

namespace Wizard.Unity.Tests.EditMode.UI.Common
{
    // 자리 찾기, 키 배치, 화면 틱, 대체 색, 소스 규칙
    public sealed class UiBasicsTests
    {
        [Test]
        public void Navigator_PicksTheNearestInDirection()
        {
            var from = new PixelRect(100, 100, 100, 50);
            var cands = new List<PixelRect>
            {
                new PixelRect(400, 100, 100, 50),   // 0 오른쪽 멀리
                new PixelRect(250, 100, 100, 50),   // 1 오른쪽 가까이
                new PixelRect(100, 200, 100, 50),   // 2 아래
                new PixelRect(250, 300, 100, 50),   // 3 오른쪽 아래(대각)
            };
            Assert.That(UiNavigator.FindNeighbor(from, UiDirection.Right, cands), Is.EqualTo(1));
            Assert.That(UiNavigator.FindNeighbor(from, UiDirection.Down, cands), Is.EqualTo(2));
            Assert.That(UiNavigator.FindNeighbor(from, UiDirection.Left, cands), Is.EqualTo(-1));
            Assert.That(UiNavigator.FindNeighbor(from, UiDirection.Up, cands), Is.EqualTo(-1));
        }

        [Test]
        public void Navigator_PrefersOverlappingColumnOverCloserDiagonal()
        {
            var from = new PixelRect(100, 100, 100, 48);
            var cands = new List<PixelRect>
            {
                new PixelRect(260, 160, 100, 48),   // 0 대각: 가운데 거리 60, 옆 어긋남 60
                new PixelRect(120, 240, 100, 48),   // 1 같은 열(겹침): 가운데 거리 140
            };
            Assert.That(UiNavigator.FindNeighbor(from, UiDirection.Down, cands), Is.EqualTo(1));
        }

        [Test]
        public void Navigator_TiesGoToTheEarlierCandidate()
        {
            var from = new PixelRect(100, 100, 100, 48);
            var cands = new List<PixelRect>
            {
                new PixelRect(100, 200, 100, 48),
                new PixelRect(100, 200, 100, 48),
            };
            Assert.That(UiNavigator.FindNeighbor(from, UiDirection.Down, cands), Is.EqualTo(0));
            Assert.That(UiNavigator.FindNeighbor(from, UiDirection.Down, new List<PixelRect> { from }), Is.EqualTo(-1));
        }

        [Test]
        public void KeyMap_DefaultFollowsSpec13MenuKeys()
        {
            // spec 13: 방향키 칸 이동, J 넣기, K 빼기, Esc 나가기
            UiKeyMap k = UiKeyMap.Default;
            Assert.That(k.ActionOf(PhysicalKey.UpArrow), Is.EqualTo(UiKeyAction.Up));
            Assert.That(k.ActionOf(PhysicalKey.DownArrow), Is.EqualTo(UiKeyAction.Down));
            Assert.That(k.ActionOf(PhysicalKey.LeftArrow), Is.EqualTo(UiKeyAction.Left));
            Assert.That(k.ActionOf(PhysicalKey.RightArrow), Is.EqualTo(UiKeyAction.Right));
            Assert.That(k.ActionOf(PhysicalKey.J), Is.EqualTo(UiKeyAction.Confirm));
            Assert.That(k.ActionOf(PhysicalKey.K), Is.EqualTo(UiKeyAction.Secondary));
            Assert.That(k.ActionOf(PhysicalKey.Escape), Is.EqualTo(UiKeyAction.Back));
            Assert.That(k.ActionOf(PhysicalKey.Q), Is.EqualTo(UiKeyAction.None));
            Assert.That(k.ActionOf(PhysicalKey.None), Is.EqualTo(UiKeyAction.None));
            Assert.That(UiKeyMap.Alternate.ActionOf(PhysicalKey.Z), Is.EqualTo(UiKeyAction.Confirm));
            Assert.That(UiKeyMap.Alternate.ActionOf(PhysicalKey.X), Is.EqualTo(UiKeyAction.Secondary));
        }

        [Test]
        public void KeyMap_RejectsAKeyBoundTwice()
        {
            Assert.Throws<ArgumentException>(() => UiKeyMap.Build(
                new[] { PhysicalKey.W }, new[] { PhysicalKey.W }, new PhysicalKey[0], new PhysicalKey[0],
                new PhysicalKey[0], new PhysicalKey[0], new PhysicalKey[0]));
        }

        [Test]
        public void TickClock_Counts60PerSecondWithCarry()
        {
            var c = new UiTickClock();
            Assert.That(c.Advance(10.0), Is.EqualTo(0));
            Assert.That(c.Advance(10.5), Is.EqualTo(30));
            Assert.That(c.Advance(10.5 + 1.0 / 64), Is.EqualTo(0));    // 0.9375틱은 남는다(2진 소수라 정확하다)
            Assert.That(c.Advance(10.5 + 2.0 / 64), Is.EqualTo(1));    // 0.9375 + 0.9375 = 1.875
            Assert.That(c.Advance(20.0), Is.EqualTo(UiTickClock.MaxTicksPerAdvance));
            Assert.That(c.Advance(19.0), Is.EqualTo(0));                // 거꾸로 가면 다시 잡는다
        }

        // spec 14 4.2 위험색 검사(InDangerRange): S ≥ 70, V ≥ 70, H ≥ 330 또는 H ≤ 15. 정수로 센다(H 는 ×d 로 비교)
        private static bool InDangerRange(Color32 c)
        {
            int r = c.r, g = c.g, b = c.b;
            int mx = Math.Max(r, Math.Max(g, b));
            int mn = Math.Min(r, Math.Min(g, b));
            int d = mx - mn;
            if (d == 0 || mx == 0)
            {
                return false;
            }
            bool s70 = d * 100 >= 70 * mx;
            bool v70 = mx * 100 >= 70 * 255;
            if (!s70 || !v70 || mx != r)
            {
                return false;   // 330~15도는 빨강이 가장 큰 쪽에만 있다
            }
            // H = 60 × ((g − b)/d mod 6). g ≥ b 면 0~60, 아니면 300~360
            return g >= b ? 60 * (g - b) <= 15 * d : 60 * (b - g) <= 30 * d;
        }

        [Test]
        public void DangerCheck_SelfTest()
        {
            Assert.That(InDangerRange(new Color32(0xFF, 0x3B, 0x30, 0xFF)), Is.True);    // 빨강 #FF3B30
            Assert.That(InDangerRange(new Color32(0xFF, 0x2E, 0x88, 0xFF)), Is.True);    // 자홍 #FF2E88
            Assert.That(InDangerRange(new Color32(0xF2, 0xA3, 0x3A, 0xFF)), Is.False);   // 호박색
        }

        [Test]
        public void Palette_HasNoDangerGoldLimeOrTeal()
        {
            var gold = new Color32(0xFF, 0xD2, 0x4A, 0xFF);
            var lime = new Color32(0xB8, 0xF0, 0x4A, 0xFF);
            var teal = new Color32(0x4F, 0xE3, 0xC1, 0xFF);
            foreach (Color32 c in UiPalette.All)
            {
                Assert.That(InDangerRange(c), Is.False, c.ToString());
                Assert.That(Same(c, gold) || Same(c, lime) || Same(c, teal), Is.False, c.ToString());
            }
            Assert.That(UiPalette.SelectYellow, Is.EqualTo(new Color32(0xFF, 0xE9, 0x4A, 0xFF)));
            Assert.That(UiPalette.ColorOf(Wizard.ViewLogic.Common.UiColorTokens.AlertAmber), Is.EqualTo(UiPalette.AlertAmber));
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        // 뷰 기반 코드의 정적 확인: 프리팹·장면·uGUI 이벤트 시스템을 쓰지 않고, 키를 프레임 단위로 읽지 않고, Time.timeScale 을 만지지 않는다
        [Test]
        public void UiCommonSource_UsesNoPrefabsScenesOrFramePolledInput()
        {
            var forbidden = new[]
            {
                new Regex(@"\bInstantiate\s*\("),
                new Regex(@"\bResources\s*\.\s*Load\b"),
                new Regex(@"\bAssetDatabase\b"),
                new Regex(@"\bPrefabUtility\b"),
                new Regex(@"\bSceneManager\b"),
                new Regex(@"\bEventSystem\b"),
                new Regex(@"\bGetKey(Down|Up)?\s*\("),
                new Regex(@"\bUnityEngine\.Input\s*\."),
                new Regex(@"\bInput\s*\.\s*(mousePosition|GetMouseButton|GetAxis)"),
                new Regex(@"\btimeScale\b"),
            };
            string root = Path.Combine(Application.dataPath, "Game/UI/Common");
            var problems = new List<string>();
            int files = 0;
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                files++;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    int c = lines[i].IndexOf("//", StringComparison.Ordinal);
                    string code = c < 0 ? lines[i] : lines[i].Substring(0, c);
                    foreach (Regex r in forbidden)
                    {
                        if (r.IsMatch(code))
                        {
                            problems.Add(Path.GetFileName(file) + ":" + (i + 1) + ": " + lines[i].Trim());
                        }
                    }
                }
            }
            Assert.That(files, Is.GreaterThan(10));
            Assert.That(problems, Is.Empty);
        }
    }
}
