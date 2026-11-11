using System.Collections.Generic;
using Wizard.Rules.Enhance;

namespace Wizard.Rules.Economy
{
    // CONTRACTS 4.3
    public interface IEconomyRules
    {
        ShopQuote QuoteShop(string sku, int qty, ShopWeekState week);                    // 값, 주간 한도
        long AppraiseWand(WandState wand);                                               // 감정가(spec 08 6.6)
        long AppraiseItem(string itemCode, long marketMedianOrZero);
        TradeFee DirectTradeFee(TradeSide a, TradeSide b);                               // 올림, 양쪽 반씩(spec 11 7절)
        MarketFee MarketFees(string itemRef, long unitPrice, long qty);                  // 보증금, 수수료
        long GiftFee(TradeSide given);
        ReleaseRefund ReleaseWands(IReadOnlyList<WandState> wands);                      // 떠나보내기 환급(내림)
        int LibraryCapacity(int expansions);
        RuleCheck CanTrade(TradeEligibility who);                                        // 웨이브 10, 보류, 갚을 것 등
    }
}
