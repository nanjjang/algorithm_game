using System.IO;
using NUnit.Framework;
using Wizard.Solo.Editor;

namespace Wizard.Solo.Tests.Art
{
    /// <summary>
    /// 두 TTF(D-42)의 글자 확인. cmap 읽기가 맞는지(한글·ASCII가 있다)를 단언하고, 화면이 쓰는 특수 글자 중 없는 것은
    /// 테스트 출력에 적는다(없으면 OS 글꼴로 대체된다 — 결과 보고 항목. 글꼴을 고치는 것은 이 테스트의 일이 아니다).
    /// </summary>
    public class FontGlyphTests
    {
        [TestCaseSource(nameof(Fonts))]
        public void Fonts_HaveHangulAndAscii_ReportSpecialGlyphs(string assetPath)
        {
            string p = Path.Combine(ArtTestUtil.ProjectRoot, assetPath);
            FileAssert.Exists(p);
            var cps = TtfCmap.ReadCodepoints(File.ReadAllBytes(p));
            Assert.IsTrue(cps.Contains(0xAC00), "가");
            Assert.IsTrue(cps.Contains(0xD7A3), "힣");
            Assert.IsTrue(cps.Contains('A'));
            Assert.IsTrue(cps.Contains('0'));
            string missing = FontGlyphCheck.Missing(p);
            TestContext.WriteLine(assetPath + " 없는 특수 글자: " + (missing.Length == 0 ? "없음" : FontGlyphCheck.Describe(missing)));
        }

        static string[] Fonts => FontGlyphCheck.FontFiles;
    }
}
