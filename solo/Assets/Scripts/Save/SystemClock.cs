using System;
using System.Globalization;

namespace Wizard.Solo.Save
{
    /// <summary>기기 시계(07 요약서 9.3절 "주간 한도 등은 기기 시계"). 주 경계 도우미도 여기 둔다(00 문서 D-33).</summary>
    public sealed class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;

        /// <summary>그 시각이 속한 주의 시작 = 기기 현지 시간 월요일 00:00(D-33 [임시]).</summary>
        public static DateTime WeekStart(DateTime now)
        {
            DateTime day = now.Date;
            int sinceMonday = ((int)day.DayOfWeek + 6) % 7;   // 월 0 … 일 6
            return day.AddDays(-sinceMonday);
        }

        /// <summary>주 시작 날짜(ISO "yyyy-MM-dd"). profile.shop_week.week_start에 적는 꼴.</summary>
        public static string WeekStartIso(DateTime now) =>
            WeekStart(now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>두 시각이 같은 주(월요일 00:00 경계)에 있나.</summary>
        public static bool SameWeek(DateTime a, DateTime b) => WeekStart(a) == WeekStart(b);

        /// <summary>ISO 8601 주 번호 글자("2026-W46"). profile.weekly.iso_week에 적는 꼴.</summary>
        public static string IsoWeek(DateTime now)
        {
            DateTime monday = WeekStart(now);
            DateTime thursday = monday.AddDays(3);           // ISO 주의 해는 그 주 목요일의 해
            int year = thursday.Year;
            int week = (thursday.DayOfYear - 1) / 7 + 1;
            return year.ToString("D4", CultureInfo.InvariantCulture) + "-W" + week.ToString("D2", CultureInfo.InvariantCulture);
        }

        /// <summary>저장 파일 시각 꼴(기기 현지, ISO 8601 + 오프셋).</summary>
        public static string Iso(DateTime t) => t.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
    }
}
