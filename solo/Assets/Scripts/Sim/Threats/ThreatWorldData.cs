using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Sim.Threats
{
    /// <summary>예고 표 하나(요청 → 대기 → 구역 → 판정·취소 → 600틱 뒤 지움). Modules.cs 공통 규칙 4.</summary>
    public sealed class TelegraphTicket
    {
        public int Id;
        public int OwnerEnemyId;
        public int CastId;
        public bool CountAsOne;
        public bool Bypass;
        public int StaggerTicks;
        public ZoneSpec[] Specs;
        public int RequestTick;
        /// <summary>이 틱 전에는 다시 검사하지 않는다(마지막 숨의 미루기).</summary>
        public int NextCheckTick;
        public TicketStatus Status = TicketStatus.Waiting;
        /// <summary>허가돼 구역이 생긴 적이 있다(TryGetTiming이 참을 주는 조건).</summary>
        public bool Started;
        public int StartTick;
        public int DetonateTick;
        /// <summary>판정·취소로 끝난 틱(남겨 두는 기한의 기준).</summary>
        public int EndTick;
        public readonly List<ZoneEntity> Zones = new List<ZoneEntity>();
    }

    /// <summary>세계 하나에 딸린 위협 모듈의 속 상태(표, 줄, 검사 버퍼). 세계마다 따로 있다.</summary>
    public sealed class ThreatWorldData
    {
        public ThreatConfig Config;
        public int NextTicketId = 1;
        /// <summary>표 전부(id 오름차순 = 요청 순).</summary>
        public readonly List<TelegraphTicket> Tickets = new List<TelegraphTicket>();
        public readonly Dictionary<int, TelegraphTicket> ById = new Dictionary<int, TelegraphTicket>();
        public readonly EscapeSolver Solver = new EscapeSolver();
        public readonly List<EscapeZone> EscapeScratch = new List<EscapeZone>();
        public readonly List<ShapeGeom> GeomScratch = new List<ShapeGeom>();
        /// <summary>남는 구역이 "사라지기 직전"을 알렸는가(구역 id).</summary>
        public readonly HashSet<int> HazardWarned = new HashSet<int>();
    }
}
