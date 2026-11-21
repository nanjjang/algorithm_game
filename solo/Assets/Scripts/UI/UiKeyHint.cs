using System.Text;
using UnityEngine.InputSystem;
using Wizard.Solo.Data;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 아래 조작 안내 띠의 한 마디 "J 고르기"(K-06). Key = 물리 키(표시 글자는 UiKeyLabels), ActionKey = 이름 표 열쇠.
    /// KeyText가 있으면 Key 대신 그 글(이미 이름 표를 거친 것, 예: "Q/E").
    /// </summary>
    public struct UiKeyHint
    {
        public Key Key;
        public string KeyText;
        public string ActionKey;

        public UiKeyHint(Key key, string actionKey) { Key = key; KeyText = null; ActionKey = actionKey; }
        public UiKeyHint(string keyText, string actionKey) { Key = Key.None; KeyText = keyText; ActionKey = actionKey; }

        /// <summary>기본 메뉴 안내: "J 고르기 · K 뒤로 · Esc 나가기"(08 1.1절).</summary>
        public static UiKeyHint[] Defaults => new[]
        {
            new UiKeyHint(Key.J, UiNameKeys.HintPick),
            new UiKeyHint(Key.K, UiNameKeys.CommonBack),
            new UiKeyHint(Key.Escape, UiNameKeys.CommonExit),
        };

        /// <summary>안내 줄 글을 만든다(열쇠 str.ui.hint.item "{key} {action}", 사이 str.ui.hint.sep).</summary>
        public static string Compose(INameTable names, UiKeyHint[] hints)
        {
            if (hints == null || hints.Length == 0) return string.Empty;
            var sb = new StringBuilder();
            string sep = names != null ? names.Get(UiNameKeys.HintSep) : " ";
            for (int i = 0; i < hints.Length; i++)
            {
                if (i > 0) sb.Append(sep);
                var h = hints[i];
                string key = h.KeyText ?? UiKeyLabels.Of(names, h.Key);
                string action = names != null ? names.Get(h.ActionKey) : h.ActionKey;
                sb.Append(names != null ? names.Format(UiNameKeys.HintItem, "key", key, "action", action) : key + " " + action);
            }
            return sb.ToString();
        }
    }

    /// <summary>물리 키의 표시 글자. 이름 표 str.ui.key.&lt;Key 이름 소문자&gt;가 있으면 그것, 없으면 글쇠 글자(A~Z, 0~9) 또는 Key 이름.</summary>
    public static class UiKeyLabels
    {
        public static string NameKey(Key key) => "str.ui.key." + key.ToString().ToLowerInvariant();

        public static string Of(INameTable names, Key key)
        {
            string nk = NameKey(key);
            if (names != null && names.Has(nk)) return names.Get(nk);
            if (key >= Key.A && key <= Key.Z) return ((char)('A' + (key - Key.A))).ToString();
            if (key >= Key.Digit1 && key <= Key.Digit9) return ((char)('1' + (key - Key.Digit1))).ToString();
            if (key == Key.Digit0) return "0";
            return key.ToString();
        }
    }

    /// <summary>F3이 쓰는 이름 표 열쇠(원본 names.ko.json + NamesSolo/F3.ko.json).</summary>
    public static class UiNameKeys
    {
        public const string HintItem = "str.ui.hint.item";
        public const string HintSep = "str.ui.hint.sep";
        public const string HintPick = "str.ui.hint.pick";
        public const string CommonBack = "str.common.back";
        public const string CommonExit = "str.common.exit";
        public const string CommonYes = "str.common.yes";
        public const string CommonNo = "str.common.no";
        public const string CommonOk = "str.common.ok";
        public const string ConfirmTitle = "str.confirm_dialog.title";
        public const string LoadingTitle = "str.loading.title";
        public const string SaveRestoredTitle = "str.save_restored.title";
        public const string SaveRestoredMessage = "str.save_restored.message";
    }
}
