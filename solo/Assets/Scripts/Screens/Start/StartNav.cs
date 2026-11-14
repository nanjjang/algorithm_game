using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    public enum StartNavKind { None = 0, Push = 1, Replace = 2, PopTo = 3, Close = 4 }

    /// <summary>
    /// 화면 모델이 정한 화면 전이 하나(엔진 없음 — 테스트가 그대로 단언한다). 뷰는 Apply로 관리자에 넘기기만 한다.
    /// Close = 이 화면만 닫는다(들어온 곳으로).
    /// </summary>
    public struct StartNav
    {
        public StartNavKind Kind;
        public ScreenId Target;
        public ScreenArgs Args;

        public static StartNav None => new StartNav { Kind = StartNavKind.None };
        public static StartNav Push(ScreenId id, ScreenArgs args) => new StartNav { Kind = StartNavKind.Push, Target = id, Args = args };
        public static StartNav Replace(ScreenId id, ScreenArgs args) => new StartNav { Kind = StartNavKind.Replace, Target = id, Args = args };
        public static StartNav PopTo(ScreenId id, ScreenArgs args) => new StartNav { Kind = StartNavKind.PopTo, Target = id, Args = args };
        public static StartNav Close() => new StartNav { Kind = StartNavKind.Close };

        /// <summary>관리자에 넘긴다. self는 Close에 쓴다.</summary>
        public void Apply(IScreenManager m, IScreen self)
        {
            if (m == null) return;
            switch (Kind)
            {
                case StartNavKind.Push: m.Push(Target, Args); break;
                case StartNavKind.Replace: m.Replace(Target, Args); break;
                case StartNavKind.PopTo: m.PopTo(Target, Args); break;
                case StartNavKind.Close: if (self != null) m.Close(self); else m.Pop(); break;
            }
        }

        public override string ToString() => Kind + " " + Target;
    }
}
