using Wizard.Solo.Data;

namespace Wizard.Solo.Sim
{
    /// <summary>협동 부활 수치(DataSolo/Coop.json "revive", Docs/11-online.md). 규칙(RunFlow.Revive)과 표시(머리 위 부활 막대)가 같이 읽는다.</summary>
    public struct ReviveTuning
    {
        public float Radius;
        public int Ticks;
        public int HpCells;
        public int InvulnTicks;
        public bool WaveClearRevive;
    }

    public static class CoopRules
    {
        public const string ExtraName = "Coop";

        public static ReviveTuning Revive(IContentDb db)
        {
            var t = new ReviveTuning { Radius = 1.5f, Ticks = SimConst.Ticks(3f), HpCells = 1, InvulnTicks = SimConst.Ticks(2f), WaveClearRevive = true };
            var root = db != null ? db.SoloExtra(ExtraName) : null;
            var r = root != null ? root["revive"] as Newtonsoft.Json.Linq.JObject : null;
            if (r == null) return t;
            if (r["revive_radius"] != null) t.Radius = (float)r["revive_radius"];
            if (r["revive_seconds"] != null) t.Ticks = SimConst.Ticks((float)r["revive_seconds"]);
            if (r["revive_hp_cells"] != null) t.HpCells = (int)r["revive_hp_cells"];
            if (r["revive_invuln_seconds"] != null) t.InvulnTicks = SimConst.Ticks((float)r["revive_invuln_seconds"]);
            if (r["wave_clear_revive"] != null) t.WaveClearRevive = (bool)r["wave_clear_revive"];
            return t;
        }

        /// <summary>표시용: 이 판에 서 있는 동료가 있나(쓰러진 사람에게 부활 막대를 보일지).</summary>
        public static bool HasStandingAlly(SimWorld w, PlayerState p)
        {
            for (int i = 0; i < w.Players.Length; i++)
            {
                var q = w.Players[i];
                if (q != p && q.Active && !q.Downed) return true;
            }
            return false;
        }
    }
}
