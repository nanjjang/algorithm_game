using System;
using System.Text;
using Wizard.Content;

namespace Wizard.Server.Data
{
    // 열거형 → DB 문자열(spec 12 8.1절: 열거값은 text + CHECK). 열거형의 정수 번호는 DB 에 싣지 않는다.
    public static class DbNames
    {
        public static string Currency(CurrencyCode currency)
        {
            switch (currency)
            {
                case CurrencyCode.Gold: return "gold";
                case CurrencyCode.SeasonCoin: return "season_coin";
                case CurrencyCode.Starlight: return "starlight";
                default: throw new ArgumentOutOfRangeException(nameof(currency), currency, null);
            }
        }

        public static string Bucket(CurrencyBucket bucket)
        {
            switch (bucket)
            {
                case CurrencyBucket.Earned: return "earned";
                case CurrencyBucket.Paid: return "paid";
                case CurrencyBucket.Free: return "free";
                default: throw new ArgumentOutOfRangeException(nameof(bucket), bucket, null);
            }
        }

        // currency_ledger.reason, item_ledger.reason: 이름의 snake_case(RunSettle → run_settle, spec 12 10.6절의 'refund').
        // LedgerReason 은 추가만 하므로 새 값도 같은 규칙으로 바뀐다
        public static string Reason(LedgerReason reason)
        {
            if (!Enum.IsDefined(typeof(LedgerReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
            }
            string name = reason.ToString();
            var sb = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                    {
                        sb.Append('_');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
