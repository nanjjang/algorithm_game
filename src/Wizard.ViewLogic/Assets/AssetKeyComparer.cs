using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Presentation;

namespace Wizard.ViewLogic.Assets
{
    // AssetKey 는 계약 타입이라 Equals 를 더하지 않는다(CONTRACTS 13). 레지스트리 표는 이 비교자로 서수 비교한다
    public sealed class AssetKeyComparer : IEqualityComparer<AssetKey>
    {
        public static readonly AssetKeyComparer Instance = new AssetKeyComparer();

        private AssetKeyComparer()
        {
        }

        public bool Equals(AssetKey a, AssetKey b) =>
            a.Pack == b.Pack
            && string.Equals(a.Id, b.Id, StringComparison.Ordinal)
            && string.Equals(a.Variant, b.Variant, StringComparison.Ordinal)
            && string.Equals(a.State, b.State, StringComparison.Ordinal);

        public int GetHashCode(AssetKey k)
        {
            // 조회 전용 표의 해시(나열 순서에 쓰지 않는다)
            int h = (int)k.Pack;
            h = h * 31 + (k.Id == null ? 0 : StringComparer.Ordinal.GetHashCode(k.Id));
            h = h * 31 + (k.Variant == null ? 0 : StringComparer.Ordinal.GetHashCode(k.Variant));
            h = h * 31 + (k.State == null ? 0 : StringComparer.Ordinal.GetHashCode(k.State));
            return h;
        }

        public static string Format(AssetKey k) =>
            k.Pack.ToString() + ":" + k.Id + (k.Variant != null ? "/" + k.Variant : "") + (k.State != null ? "#" + k.State : "");
    }
}
