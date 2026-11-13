using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13절 UiEventKeys 의 구역. 코어 밖 사건의 열쇠는 "<구역>.<마디>[.<마디>…]" 이고 마디는 snake_case(a-z, 0-9, _)다
    public static class UiEventZones
    {
        public const string Ui = "ui";             // 화면 조작, 강화 의식, HUD
        public const string Net = "net";           // 연결, 대기열, 회선, 계정 인증
        public const string Duel = "duel";         // 매치·점수·입력 버려짐·여유 띠(코어 밖)
        public const string Plaza = "plaza";
        public const string Notice = "notice";     // 서버 전체 알림
        public const string Trade = "trade";
        public const string Season = "season";
        public const string Run = "run";           // 제출·검증 상태

        public static IReadOnlyList<string> All { get; } = new[] { Ui, Net, Duel, Plaza, Notice, Trade, Season, Run };

        public static bool IsValidKey(string key)
        {
            int dot = key.IndexOf('.');
            if (dot <= 0 || !IsZone(key.Substring(0, dot)))
            {
                return false;
            }
            return IsSegments(key, dot + 1);
        }

        public static bool IsZone(string zone)
        {
            foreach (string z in All)
            {
                if (string.Equals(z, zone, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // start 부터 끝까지가 "마디(.마디)*" 인가. 빈 마디는 안 된다
        private static bool IsSegments(string key, int start)
        {
            int len = 0;
            for (int i = start; i < key.Length; i++)
            {
                char c = key[i];
                if (c == '.')
                {
                    if (len == 0)
                    {
                        return false;
                    }
                    len = 0;
                    continue;
                }
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                {
                    return false;
                }
                len++;
            }
            return len > 0;
        }
    }
}
