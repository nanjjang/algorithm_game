using System;
using System.Collections.Generic;
using Wizard.Content;

namespace Wizard.Protocol.Http
{
    // CONTRACTS 8.2. JSON 은 snake_case. 두 백엔드가 같은 모양으로 돌려준다
    public sealed class ProfileView
    {
        public long AccountId { get; set; }
        public string Nickname { get; set; } = "";
        public bool EmailVerified { get; set; }
        public List<CharacterView> Characters { get; set; } = new List<CharacterView>();   // 5명
        public List<WandView> Wands { get; set; } = new List<WandView>();
        public List<BalanceView> Balances { get; set; } = new List<BalanceView>();          // currency, bucket, amount, amount_pending
        public List<StackView> Items { get; set; } = new List<StackView>();                 // item_code, qty, qty_pending, qty_socketed, qty_bound
        public List<InstanceView> Instances { get; set; } = new List<InstanceView>();       // 개체 꾸미기
        public List<DebtView> Debts { get; set; } = new List<DebtView>();
        public int LibraryCapacity { get; set; }
        public LeagueId League { get; set; }
        public int HoldingIl { get; set; }
        public bool PhantomMet { get; set; }
        public DateTime? RevengeDoneAt { get; set; }
        public string? TitleId { get; set; }
        public DateTime? TradeHoldUntil { get; set; }
        public long PlaySeconds { get; set; }
        public int MaxWaveReached { get; set; }
        public bool TradeOpen { get; set; }                                                 // 거래·장터가 열렸는가(spec 11 6절)
        public List<string> TradeMissing { get; set; } = new List<string>();                // need_wave10, need_play2h, hold, debt …
    }
}
