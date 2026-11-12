using System;
using System.Globalization;

namespace Wizard.Server.Data
{
    // item_ledger.item_ref 의 꼴(spec 12 8.2절 [임시]): 'stack:<item_code>' | 'wand:<id>' | 'inst:<id>'.
    // 겹치는 물건은 item_stack 의 수량이 바뀌고, 지팡이·개체는 그 행의 생성(+1)·소멸(−1)을 원장에 적는다.
    public static class ItemRefs
    {
        public const string StackPrefix = "stack:";
        public const string WandPrefix = "wand:";
        public const string InstancePrefix = "inst:";

        public static string Stack(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode))
            {
                throw new ArgumentException("item code is required", nameof(itemCode));
            }
            return StackPrefix + itemCode;
        }

        public static string Wand(long wandId) => WandPrefix + wandId.ToString(CultureInfo.InvariantCulture);

        public static string Instance(long instanceId) => InstancePrefix + instanceId.ToString(CultureInfo.InvariantCulture);

        // 'stack:' 이면 item_code 를 돌려준다
        public static bool TryGetStackCode(string itemRef, out string itemCode)
        {
            if (itemRef != null && itemRef.Length > StackPrefix.Length && itemRef.StartsWith(StackPrefix, StringComparison.Ordinal))
            {
                itemCode = itemRef.Substring(StackPrefix.Length);
                return true;
            }
            itemCode = "";
            return false;
        }

        // 'wand:<id>' 또는 'inst:<id>' 인가(id 는 0 이상의 십진 정수)
        public static bool IsEntity(string itemRef)
        {
            if (itemRef == null)
            {
                return false;
            }
            string? digits = null;
            if (itemRef.StartsWith(WandPrefix, StringComparison.Ordinal))
            {
                digits = itemRef.Substring(WandPrefix.Length);
            }
            else if (itemRef.StartsWith(InstancePrefix, StringComparison.Ordinal))
            {
                digits = itemRef.Substring(InstancePrefix.Length);
            }
            return digits != null && digits.Length > 0
                && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long id) && id >= 0;
        }
    }
}
