using System.Runtime.CompilerServices;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Threats
{
    /// <summary>
    /// 위협 모듈 구현(G05): 예고 표와 대기 줄, 동시 예고 상한, R5, 예고 구역 수명, 적 탄, 남는 구역, 장애물,
    /// 스침 후보, 받는 피해 칸. 05 요약서 0~4절·8.5절, Modules.cs 공통 규칙 4·6.
    /// 세계마다 따로인 속 상태(표, 버퍼)는 세계에 매단 ThreatWorldData에 둔다(모듈 객체 하나를 여러 세계가 써도 섞이지 않는다).
    /// </summary>
    public sealed partial class ThreatSystem : ThreatModule
    {
        /// <summary>보충 수치 조각 이름(Resources/DataSolo/G05.json).</summary>
        public const string SoloExtraName = "G05";

        readonly ConditionalWeakTable<SimWorld, ThreatWorldData> _data = new ConditionalWeakTable<SimWorld, ThreatWorldData>();

        /// <summary>그 세계의 속 상태(처음 쓸 때 만든다).</summary>
        public ThreatWorldData Data(SimWorld w)
        {
            ThreatWorldData d;
            if (!_data.TryGetValue(w, out d))
            {
                d = new ThreatWorldData { Config = ThreatConfig.From(w.Content) };
                _data.Add(w, d);
            }
            return d;
        }

        static float HitRadius(ThreatConfig cfg, PlayerState p) =>
            p.Def != null && p.Def.HitRadius > 0f ? p.Def.HitRadius : cfg.HitRadius;

        static float GrazeRadius(ThreatConfig cfg, PlayerState p) =>
            p.Def != null && p.Def.GrazeRadius > 0f ? p.Def.GrazeRadius : cfg.GrazeRadius;

        static bool CanBeHit(PlayerState p) => p != null && p.Active && !p.Downed;

        /// <summary>겨눌 플레이어: 명세의 슬롯이 서 있으면 그 사람, 아니면 가장 가까운 사람(없으면 null).</summary>
        static PlayerState TargetFor(SimWorld w, int slot, Vector2 from)
        {
            if (slot >= 0 && slot < w.Players.Length && CanBeHit(w.Players[slot])) return w.Players[slot];
            return w.NearestPlayer(from);
        }

        // ------------------------------------------------------------ 받는 피해 칸 (05 요약서 3.1절, 공통 규칙 6)

        public override int DamageCells(SimWorld w, int baseDamage, PlayerDamageSource source, EnemyState owner)
        {
            if (baseDamage <= 0) return 0;
            if (source == PlayerDamageSource.Scripted) return baseDamage;
            var cfg = Data(w).Config;
            int cells = baseDamage;
            if (source == PlayerDamageSource.Telegraph || source == PlayerDamageSource.Dash)
            {
                int wave = w.Wave != null ? w.Wave.Wave : 0;
                if (cfg.AllPlusFrom > 0 && wave >= cfg.AllPlusFrom) cells += cfg.TelegraphPlus;
                else if (cfg.EliteBossPlusFrom > 0 && wave >= cfg.EliteBossPlusFrom && owner != null && (owner.IsElite || owner.IsBoss))
                    cells += cfg.TelegraphPlus;
            }
            if (w.Boss != null && w.Boss.State != BossFightState.Dead && w.Boss.FinalRage) cells += cfg.FinalRageDamagePlus;
            if (cfg.AttackCap > 0 && cells > cfg.AttackCap) cells = cfg.AttackCap;
            return cells;
        }

        // ------------------------------------------------------------ 전부 걷기·빈자리

        public override void ClearAll(SimWorld w, bool keepObstacles)
        {
            var d = Data(w);
            for (int i = 0; i < d.Tickets.Count; i++)
            {
                var t = d.Tickets[i];
                if (t.Status != TicketStatus.Waiting && t.Status != TicketStatus.Active) continue;
                t.Status = TicketStatus.Cancelled;
                t.EndTick = w.Tick;
            }
            for (int i = 0; i < w.Zones.Count; i++) CancelZone(w, w.Zones[i]);
            for (int i = 0; i < w.Hazards.Count; i++) KillHazard(w, d, w.Hazards[i]);
            ClearShots(w, Vector2.zero, 0f);
            if (!keepObstacles)
                for (int i = 0; i < w.Obstacles.Count; i++) KillObstacle(w, w.Obstacles[i], broken: false);
            for (int i = 0; i < w.Players.Length; i++) w.Players[i].EnvSpeedMul = 1f;
        }

        public override bool IsClear(SimWorld w, Vector2 pos, float clearance)
        {
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var e = w.Enemies[i];
                if (!e.Alive) continue;
                if ((e.Pos - pos).magnitude - e.BodyRadius < clearance) return false;
            }
            for (int i = 0; i < w.Zones.Count; i++)
            {
                var z = w.Zones[i];
                if (z.Cancelled || z.Detonated || z.Harmless) continue;
                if (ThreatGeometry.Distance(ShapeGeom.Of(z), pos) < clearance) return false;
            }
            for (int i = 0; i < w.Hazards.Count; i++)
            {
                var h = w.Hazards[i];
                if (h.Dead) continue;
                if (ThreatGeometry.Distance(ShapeGeom.Of(h), pos) < clearance) return false;
            }
            return true;
        }
    }
}
