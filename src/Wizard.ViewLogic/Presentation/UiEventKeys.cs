using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13. 코어 밖에서 생기는 표시 사건의 문자열 열쇠 카탈로그. 열쇠는 추가만 한다
    public static class UiEventKeys
    {
        // 구역: "ui.*"(화면 조작, 강화 의식), "net.*"(연결, 대기열, 회선), "duel.*"(매치·점수·입력 버려짐·여유 띠),
        //       "plaza.*", "notice.*"(서버 전체 알림), "trade.*", "season.*", "run.*"(제출·검증 상태)
        // 아래 다섯은 spec 10 23절의 예. 나머지(spec 09 22, 10 23, 11 17, 12 17, 13 21절의 이름)는 M9-PRS-01 이 채운다.
        public const string DuelMatchFound = "duel.match_found";
        public const string DuelQueueEnter = "duel.queue_enter";
        public const string DuelRatingChanged = "duel.rating_changed";
        public const string DuelInputDropped = "duel.input_dropped";
        public const string DuelMarginBandChanged = "duel.margin_band_changed";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            DuelMatchFound, DuelQueueEnter, DuelRatingChanged, DuelInputDropped, DuelMarginBandChanged,
        };
    }
}
