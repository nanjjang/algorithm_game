using System;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2·12.2절 wand 표의 열 그대로(spec 12 8.2절 + CONTRACTS 12.1절의 pending_offer, acquired_at, debt_locked, pending_run_id uuid).
    // IRowLocks.LockWandAsync 가 FOR UPDATE 로 읽은 한 줄이다. 열거값 열(origin, lock_state)은 DB 문자열 그대로 싣는다(spec 12 8.1절).
    // 표 자체는 M10-INV-01 의 마이그레이션이 만든다. 열을 더하면 여기와 PgRowLocks 의 SELECT 를 같이 바꾼다.
    public sealed class WandRow
    {
        public long Id { get; set; }
        public long OwnerId { get; set; }
        public short Character { get; set; }               // 1~5 (char.1~char.5)
        public short Stage { get; set; }                   // 0~13
        public short FloorPeak { get; set; }               // 바닥 계산용
        public short RecordPeak { get; set; }              // 흔적용
        public int DropCount { get; set; }
        public int Aura { get; set; }                      // 장인의 기운(단위는 강화 명세)
        public short SlotsExtra { get; set; }              // 0~3
        public int CarveBonus { get; set; }                // 슬롯 새기기 누적 확률(만분율 [임시])
        public string Mutations { get; set; } = "[]";      // jsonb 원문
        public string? PendingOffer { get; set; }          // jsonb 원문, 결정 대기 변이(spec 08 2.1절)
        public string Loadout { get; set; } = "";          // jsonb 원문, 출발 조립
        public string? SkinId { get; set; }
        public bool ShowScars { get; set; }                // 흔적 공개(기본 끔, DECISIONS 1.15). 거래 상대에게는 항상 보인다
        public string Origin { get; set; } = "";           // 'start' | 'shop' | 'trade'
        public int OwnerCount { get; set; }
        public DateTime? TradeLockedUntil { get; set; }
        public string LockState { get; set; } = "";        // 'none' | 'expedition' | 'duel' | 'market' | 'mail' | 'trade'
        public Guid? PendingRunId { get; set; }
        public bool DebtLocked { get; set; }
        public DateTime? AcquiredAt { get; set; }
        public int Version { get; set; }                   // 낙관적 동시성 + 원장 참조
    }
}
