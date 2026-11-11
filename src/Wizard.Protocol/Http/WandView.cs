using System;
using System.Collections.Generic;

namespace Wizard.Protocol.Http
{
    // CONTRACTS 8.2. 하락 흔적은 거래할 때 상대에게 항상 보인다(DECISIONS 1.15)
    public sealed class WandView
    {
        public long Id { get; set; }
        public int Character { get; set; }
        public int Stage { get; set; }
        public int FloorPeak { get; set; }
        public int RecordPeak { get; set; }
        public int DropCount { get; set; }
        public int Aura { get; set; }
        public int SlotsExtra { get; set; }
        public int CarveBonus { get; set; }
        public List<string> Mutations { get; set; } = new List<string>();       // 받은 순서
        public MutationOfferDto? PendingOffer { get; set; }
        public LoadoutDto Loadout { get; set; } = new LoadoutDto();
        public string? SkinId { get; set; }
        public bool ShowScars { get; set; }
        public string Origin { get; set; } = "start";
        public int OwnerCount { get; set; }
        public DateTime? TradeLockedUntil { get; set; }
        public string LockState { get; set; } = "none";
        public bool PendingLocked { get; set; }
        public bool DebtLocked { get; set; }
        public DateTime AcquiredAt { get; set; }
        public int Version { get; set; }
        public int Il { get; set; }
    }
}
