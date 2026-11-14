using System;
using System.Globalization;
using System.Text;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>
    /// 이 묶음 화면들이 같이 쓰는 숫자·문구 규칙(엔진 없음). 숫자는 문화권과 무관하게 점(.)으로 적는다.
    /// 화면 글자는 이름 표 열쇠만 돌려주고, 글 자체는 화면이 ctx.Names로 만든다.
    /// </summary>
    public static class CampFormat
    {
        /// <summary>소수 한 자리(발사당 열 26.8, 03 요약서 13.3절 "소수 한 자리 표시").</summary>
        public static string OneDecimal(double v) => Round(v, 1).ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>소수 둘째 자리(반올림). 깊이 감쇠 꼬리표("×0.85", 08 요약서 8.3절 TV-073·074 — 반올림 쪽을 따른다, 03 요약서 17절 M5).</summary>
        public static string TwoDecimals(double v) => Round(v, 2).ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>간격 표시(08 8.4절 "0.18초", 04 11.4절 "0.2초", "0.25초"): 소수 둘째 자리까지, 끝의 0은 뺀다.</summary>
        public static string ShortSeconds(double v) => Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

        static double Round(double v, int digits) => Math.Round(v, digits, MidpointRounding.AwayFromZero);

        /// <summary>
        /// 늦음 초(04 요약서 6.1절): ceil(LateBy × 100 / 15360) / 100 — 소수 둘째 자리 **올림**. 153u → 0.01, 307u → 0.02, 1,075u → 0.07, 1u → 0.01.
        /// 0 이하는 0.
        /// </summary>
        public static double LateSecondsFromUnits(long lateU)
        {
            if (lateU <= 0) return 0.0;
            long hundredths = (lateU * 100 + SimConst.UnitsPerSecond - 1) / SimConst.UnitsPerSecond;
            return hundredths / 100.0;
        }

        /// <summary>이미 초로 받은 늦음(ArtFizzled.F). 시뮬레이션이 올림한 값이면 그대로, 날 값이면 둘째 자리로 올린다(부동소수 오차 1e-6 허용).</summary>
        public static double LateSecondsCeil(double seconds)
        {
            if (seconds <= 0) return 0.0;
            double h = Math.Ceiling(seconds * 100.0 - 1e-6);
            if (h < 1) h = 1;
            return h / 100.0;
        }

        /// <summary>늦음 초 글자: 소수 둘째 자리 고정("0.02", "0.03", TV-081).</summary>
        public static string LateText(double seconds) => seconds.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>오류 코드 → 이름 표 열쇠("error." + snake_case, 07 요약서 11절 / MetaContracts ErrorCode 주석).</summary>
        public static string ErrorKey(ErrorCode code)
        {
            if (code == ErrorCode.None) return null;
            string name = code.ToString();
            var sb = new StringBuilder("error.");
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
