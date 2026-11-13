using System.Globalization;

namespace Wizard.ViewLogic.Assets.Loaders
{
    // 진단 문구의 숫자(문화권에 기대지 않는다)
    internal static class Inv
    {
        public static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);

        public static string Size(int w, int h) => Int(w) + "x" + Int(h);
    }
}
