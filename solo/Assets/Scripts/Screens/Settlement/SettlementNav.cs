using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Settlement
{
    public enum SettlementNavKind { None = 0, Again = 1, ToEnhance = 2, ToMenu = 3 }

    /// <summary>
    /// 정산 끝 버튼이 정한 화면 전이(엔진 없음 — 테스트가 그대로 단언한다). 08 요약서 2·9절, 00 문서 8.1절·D-34.
    /// 정산 화면은 판이 끝난 뒤의 맨 위 화면이라, 먼저 메인 메뉴까지 걷고(PopTo — 없으면 메인 메뉴를 연다) 그 위에 다음 화면을 연다.
    /// 그래서 다음 화면에서 Esc를 누르면 메인 메뉴로 돌아온다.
    /// - Again: Loading을 거쳐 ExpeditionPrep(인자 없음, ScreenArgs.ViaLoading)
    /// - ToEnhance: WandPicker(ReturnTo = MainMenu, Character = 그 판의 캐릭터 번호 int). 강화 첫 안내를 열지는 강화 쪽이 정한다(D-67).
    /// - ToMenu: 메인 메뉴만.
    /// </summary>
    public sealed class SettlementNav
    {
        public SettlementNavKind Kind;
        /// <summary>마지막에 닿는 화면(ExpeditionPrep / WandPicker / MainMenu).</summary>
        public ScreenId Target;
        /// <summary>메인 메뉴 위에 Push 하는 화면(Loading / WandPicker). None이면 메인 메뉴에서 멈춘다.</summary>
        public ScreenId PushId;
        /// <summary>PushId에 넘기는 인자.</summary>
        public ScreenArgs Args;

        public static SettlementNav Again()
        {
            var next = ScreenArgs.From(ScreenId.Settlement);
            return new SettlementNav
            {
                Kind = SettlementNavKind.Again,
                Target = ScreenId.ExpeditionPrep,
                PushId = ScreenId.Loading,
                Args = ScreenArgs.ViaLoading(ScreenId.Settlement, ScreenId.ExpeditionPrep, next)
            };
        }

        /// <param name="character">그 판의 캐릭터 번호 1~5(0이면 인자를 넣지 않는다 — 받는 쪽이 last_character를 쓴다).</param>
        public static SettlementNav ToEnhance(int character)
        {
            var args = ScreenArgs.From(ScreenId.Settlement).With(ScreenArgKeys.ReturnTo, ScreenId.MainMenu);
            if (character >= 1 && character <= Ids.Char.Count) args.With(ScreenArgKeys.Character, character);
            return new SettlementNav { Kind = SettlementNavKind.ToEnhance, Target = ScreenId.WandPicker, PushId = ScreenId.WandPicker, Args = args };
        }

        public static SettlementNav ToMenu() =>
            new SettlementNav { Kind = SettlementNavKind.ToMenu, Target = ScreenId.MainMenu, PushId = ScreenId.None };

        /// <summary>관리자에 넘긴다.</summary>
        public void Apply(IScreenManager m)
        {
            if (m == null || Kind == SettlementNavKind.None) return;
            m.PopTo(ScreenId.MainMenu);
            if (PushId != ScreenId.None) m.Push(PushId, Args);
        }

        public override string ToString() => Kind + " " + Target;
    }
}
