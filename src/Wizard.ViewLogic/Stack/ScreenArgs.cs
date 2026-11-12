namespace Wizard.ViewLogic.Stack
{
    // spec 13 1.1절. 화면을 열 때 넘기는 값.
    //   Entry         진입점(같은 화면, 다른 진입점)
    //   ReturnTarget  나갈 때 돌아갈 화면을 따로 정할 때만 준다. None 이면 바로 아래 화면으로 돌아간다.
    //                 예: 정산 3쪽 [강화 화면으로] → Enhance(entry=MainMenu), 돌아올 곳 = MainMenu(spec 13 10절 [임시])
    //   Variant       화면마다 뜻이 다른 보조 번호(Camp(mode=Library), Shop(kind=Gold) 따위). 뜻은 그 화면의 명세가 정한다
    //   Payload       화면마다의 요청 자료(확인 창의 물음 따위). 화면이 자기 형식으로 읽는다
    public readonly struct ScreenArgs
    {
        public readonly ScreenEntry Entry;
        public readonly ScreenId ReturnTarget;
        public readonly int Variant;
        public readonly object? Payload;

        public ScreenArgs(ScreenEntry entry, ScreenId returnTarget = ScreenId.None, int variant = 0, object? payload = null)
        {
            Entry = entry;
            ReturnTarget = returnTarget;
            Variant = variant;
            Payload = payload;
        }

        public static ScreenArgs None => default;

        public static ScreenArgs From(ScreenEntry entry) => new ScreenArgs(entry);

        public static ScreenArgs WithPayload(object payload) => new ScreenArgs(ScreenEntry.None, ScreenId.None, 0, payload);

        public ScreenArgs WithReturnTarget(ScreenId target) => new ScreenArgs(Entry, target, Variant, Payload);
    }
}
