namespace Wizard.ViewLogic.Common
{
    // 누를 수 있는 것 하나에 겹쳐 그릴 것(spec 14 4.3 "괄호 겹그림", spec 13 1.3 꺼짐 줄).
    //   Motion/Tone   선택 괄호(K-40)의 꼴과 색. None 이면 괄호 없음
    //   ConfirmCheck  오른쪽 위 확정 체크(K-41)
    //   ReasonKey     꺼진 것의 이유 한 줄(이름 표 열쇠). 보일 때만 null 이 아니다
    //   Size          대상 크기로 고른 괄호 크기
    public readonly struct BracketOverlay
    {
        public readonly BracketMotion Motion;
        public readonly BracketTone Tone;
        public readonly bool ConfirmCheck;
        public readonly string? ReasonKey;
        public readonly BracketSize Size;

        public BracketOverlay(BracketMotion motion, BracketTone tone, bool confirmCheck, string? reasonKey, BracketSize size)
        {
            Motion = motion;
            Tone = tone;
            ConfirmCheck = confirmCheck;
            ReasonKey = reasonKey;
            Size = size;
        }

        public bool HasBracket => Motion != BracketMotion.None;

        public string ToneToken => Tone == BracketTone.Muted ? UiColorTokens.Slate : UiColorTokens.SelectYellow;
    }
}
