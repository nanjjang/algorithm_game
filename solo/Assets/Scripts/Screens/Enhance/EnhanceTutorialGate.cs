using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 강화 첫 안내를 여는 때(00 D-67, 08 9.3절, 07 5.5절): WandPicker·Enhance에 들어갈 때
    /// flags.crow_bundle_given &amp;&amp; !flags.enhance_tutorial_shown 이면 EnhanceTutorial을 먼저 연다(닫으면 원래 화면이 그대로 있다).
    /// 정산이 넘기는 값에 기대지 않는다 — 메인 메뉴를 거쳐 나중에 와도 뜬다.
    /// </summary>
    public static class EnhanceTutorialGate
    {
        public static bool ShouldOpen(ProfileData p) =>
            p != null && p.Flags != null && p.Flags.CrowBundleGiven && !p.Flags.EnhanceTutorialShown;

        /// <summary>열어야 하면 안내를 위에 쌓고(인자 NextScreen·NextArgs 없이) true. 이미 떠 있으면 다시 쌓지 않는다.</summary>
        public static bool TryOpen(GameContext ctx, ScreenId from)
        {
            if (ctx == null || ctx.Screens == null || !ShouldOpen(ctx.Profile)) return false;
            if (ctx.Screens.IsOpen(ScreenId.EnhanceTutorial)) return false;
            ctx.Screens.Push(ScreenId.EnhanceTutorial, ScreenArgs.From(from));
            return true;
        }

        /// <summary>본 것으로 적고 한 번 저장한다. 이미 적혀 있으면 아무것도 하지 않는다(false).</summary>
        public static bool MarkShown(ProfileData p, IProfileStore store)
        {
            if (p == null || p.Flags == null || p.Flags.EnhanceTutorialShown) return false;
            p.Flags.EnhanceTutorialShown = true;
            store?.Save();
            return true;
        }
    }

    /// <summary>강화 안내 4쪽(08 10.11절). 본문 열쇠 str.enhance_tutorial.page.N.body — 값이 이름 표에 없으면 그 줄을 숨긴다(N-05).</summary>
    public sealed class EnhanceTutorialModel
    {
        public int Pages;
        public int Page { get; private set; } = 1;
        public bool Closed { get; private set; }

        public EnhanceTutorialModel(int pages) { Pages = pages < 1 ? 1 : pages; }

        public bool IsLast => Page >= Pages;
        public bool IsFirst => Page <= 1;
        public bool Next() { if (IsLast) return false; Page++; return true; }
        public bool Prev() { if (IsFirst) return false; Page--; return true; }
        public void Reset() { Page = 1; Closed = false; }

        public string PageLabel(INameTable names) =>
            names != null ? names.Format(EnhanceNameKeys.TutorialPage, "n", Page, "total", Pages) : EnhanceNameKeys.TutorialPage;

        /// <summary>그 쪽의 본문(없으면 null = 줄을 숨긴다).</summary>
        public string Body(INameTable names) => EnhanceNameKeys.IfPresent(names, EnhanceNameKeys.TutorialBody(Page));

        /// <summary>그 쪽의 본문을 값(enhance.json의 수치)으로 채운다. 열쇠가 없으면 null.</summary>
        public string Body(INameTable names, object[] args)
        {
            string key = EnhanceNameKeys.TutorialBody(Page);
            if (names == null || !names.Has(key)) return null;
            return names.Format(key, args ?? new object[0]);
        }

        /// <summary>닫는다: 플래그를 한 번만 적고 저장한다.</summary>
        public bool Close(ProfileData p, IProfileStore store)
        {
            if (Closed) return false;
            Closed = true;
            return EnhanceTutorialGate.MarkShown(p, store);
        }
    }

    /// <summary>Esc의 갈 곳(08 10.1절, 00 8.1절): ReturnTo가 있으면 PopTo(그 화면), 없으면 Pop.</summary>
    public static class EnhanceNav
    {
        /// <summary>돌아갈 곳. ReturnTo가 없어도 정산에서 열렸으면 메인 메뉴([임시] D-34).</summary>
        public static ScreenId ReturnTo(ScreenArgs args)
        {
            if (args == null) return ScreenId.None;
            var rt = args.Get(ScreenArgKeys.ReturnTo, ScreenId.None);
            if (rt == ScreenId.None && args.Entry == ScreenId.Settlement) rt = ScreenId.MainMenu;
            return rt;
        }

        /// <summary>나간다. 소비했으면 true(OnBack이 그대로 돌려준다).</summary>
        public static bool Back(IScreenManager screens, ScreenArgs args)
        {
            if (screens == null) return false;
            var rt = ReturnTo(args);
            if (rt != ScreenId.None) screens.PopTo(rt);
            else screens.Pop();
            return true;
        }

        /// <summary>강화 화면으로 넘길 인자: WandId(long) + ReturnTo(고르기 화면의 ReturnTo, 없으면 고르기 화면을 연 곳). 열린 곳도 없으면 ReturnTo를 넣지 않아 Esc가 고르기 화면으로 돌아간다.</summary>
        public static ScreenArgs EnhanceArgs(long wandId, ScreenArgs pickerArgs)
        {
            var a = ScreenArgs.From(ScreenId.WandPicker).With(ScreenArgKeys.WandId, wandId);
            var rt = ReturnTo(pickerArgs);
            if (rt == ScreenId.None && pickerArgs != null) rt = pickerArgs.Entry;
            if (rt != ScreenId.None && rt != ScreenId.WandPicker) a.With(ScreenArgKeys.ReturnTo, rt);
            return a;
        }

        /// <summary>떠나보내기 확인 창 인자: WandIds(List&lt;long&gt;).</summary>
        public static ScreenArgs ReleaseArgs(IEnumerable<long> ids) =>
            ScreenArgs.From(ScreenId.WandPicker).With(ScreenArgKeys.WandIds, new List<long>(ids));

        public static ScreenArgs EngraveArgs(long wandId) => ScreenArgs.From(ScreenId.Enhance).With(ScreenArgKeys.WandId, wandId);

        public static ScreenArgs MutationRemoveArgs(long wandId, string mutationId) =>
            ScreenArgs.From(ScreenId.Enhance).With(ScreenArgKeys.WandId, wandId).With(ScreenArgKeys.MutationId, mutationId);
    }
}
