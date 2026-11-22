using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.UI
{
    /// <summary>UiLabel.SetText 같은 글 건너뛰기(최적화)와 UiTextFit.Fit 이어 세기가 결과를 바꾸지 않는지.</summary>
    public class UiLabelSetTextTests
    {
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestRoot", typeof(RectTransform));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(1920f, 1080f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        RectTransform Root => (RectTransform)_root.transform;

        [Test]
        public void SetText_SameText_SkipsRefit_ChangedText_Updates()
        {
            var l = UiKit.Label(new GameContext(), Root, "가나다", UiFontId.F16, 0f, 0f, 128f, 24f);
            Assert.AreEqual("가나다", l.Text.text);
            // 밖에서 글 칸을 바꿔 두면, 같은 글을 다시 넣는 것은 아무것도 하지 않아야 한다(다시 자르지 않음)
            l.Text.text = "sentinel";
            l.SetText("가나다");
            Assert.AreEqual("sentinel", l.Text.text, "같은 글 + 같은 폭 = 건너뛴다");
            l.SetText("라마");
            Assert.AreEqual("라마", l.Text.text, "다른 글은 바로 바뀐다");
            Assert.AreEqual("라마", l.FullText);
            l.SetText(null);
            Assert.AreEqual(string.Empty, l.Text.text);
            Assert.AreEqual(string.Empty, l.FullText);
        }

        [Test]
        public void SetText_SameText_WidthChanged_Refits()
        {
            var l = UiKit.Label(new GameContext(), Root, "가나다라마바사아자", UiFontId.F16, 0f, 0f, 400f, 24f);
            Assert.IsFalse(l.Truncated);
            l.Rect.sizeDelta = new Vector2(128f, l.Rect.sizeDelta.y);
            l.SetText("가나다라마바사아자");
            Assert.IsTrue(l.Truncated, "폭이 줄었으면 같은 글이라도 다시 자른다");
            StringAssert.EndsWith(UiTextFit.Ellipsis, l.Text.text);
        }

        [Test]
        public void Fit_IncrementalWidth_MatchesNaive()
        {
            string[] samples = { "가나다라마바사아자", "abcdefghijklmnop", "가a나b다c라d마e", "ab\U0001F600cd\U0001F600ef", "\uD800가나", "가나\uDC00다라" };
            foreach (var s in samples)
                for (int px = 12; px <= 32; px += 4)
                    for (int max = 0; max <= 200; max += 7)
                        Assert.AreEqual(NaiveFit(s, px, max), UiTextFit.Fit(s, px, max), s + " px" + px + " max" + max);
        }

        /// <summary>예전 Fit(자를 때마다 Substring으로 다시 센다).</summary>
        static string NaiveFit(string text, int fontPx, int maxWidthPx)
        {
            if (string.IsNullOrEmpty(text) || UiTextFit.WidthPx(text, fontPx) <= maxWidthPx) return text ?? string.Empty;
            int e = UiTextFit.WidthPx(UiTextFit.Ellipsis, fontPx);
            int end = text.Length;
            while (end > 0 && UiTextFit.WidthPx(text.Substring(0, end), fontPx) + e > maxWidthPx)
                end = end >= 2 && char.IsLowSurrogate(text[end - 1]) && char.IsHighSurrogate(text[end - 2]) ? end - 2 : end - 1;
            return text.Substring(0, end) + UiTextFit.Ellipsis;
        }
    }
}
