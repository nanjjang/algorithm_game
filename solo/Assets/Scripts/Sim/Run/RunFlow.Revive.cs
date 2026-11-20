using UnityEngine;

namespace Wizard.Solo.Sim.Run
{
    // 협동 부활(Docs/11-online.md 5절, 솔로 임시 값 = DataSolo/Coop.json "revive"). 솔로 판에는 쓰러진 "동료"가 없으므로 아무 일도 하지 않는다.
    // - 쓰러진 동료 곁(revive_radius 칸)에 서 있는 동료가 있으면 그 동료의 ReviveTicks가 쌓인다. 곁에 아무도 없으면 0으로 돌아간다.
    // - revive_seconds를 채우면 일어난다: 체력 revive_hp_cells칸, 무적 revive_invuln_seconds. PlayerRevived 이벤트(Slot = 일어난 사람, A = 살린 사람).
    // - 웨이브를 깨면 쓰러져 있던 동료는 같은 체력으로 일어난다(wave_clear_revive).
    // - 전원이 쓰러지면 판이 끝나는 것은 OnPlayerDowned 그대로(게임 오버).
    public sealed partial class RunFlow
    {
        static ReviveTuning Revive(SimWorld w) => CoopRules.Revive(w.Content);

        static bool RevivePhase(RunPhase ph) =>
            ph == RunPhase.WavePrelude || ph == RunPhase.WaveCombat || ph == RunPhase.WaveClearing;

        /// <summary>11단계 머리: 쓰러진 동료 곁에 선 시간을 센다.</summary>
        void TickRevive(SimWorld w)
        {
            if (!RevivePhase(w.Run.Phase)) { ClearReviveProgress(w); return; }
            ReviveTuning t = default;
            bool loaded = false;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var down = w.Players[i];
                if (!down.Active || !down.Downed || down.Left) { if (down.ReviveTicks != 0) down.ReviveTicks = 0; continue; }
                if (!loaded) { t = Revive(w); loaded = true; }
                PlayerState helper = null;
                for (int k = 0; k < w.Players.Length; k++)
                {
                    var h = w.Players[k];
                    if (h == down || !h.Active || h.Downed || h.Left) continue;
                    if ((h.Pos - down.Pos).sqrMagnitude <= t.Radius * t.Radius) { helper = h; break; }
                }
                if (helper == null) { down.ReviveTicks = 0; continue; }
                down.ReviveTicks++;
                if (down.ReviveTicks >= t.Ticks) StandUp(w, down, t, helper.Slot);
            }
        }

        static void ClearReviveProgress(SimWorld w)
        {
            for (int i = 0; i < w.Players.Length; i++) w.Players[i].ReviveTicks = 0;
        }

        /// <summary>웨이브를 깼을 때: 쓰러져 있던 동료를 일으킨다(서 있는 사람이 있을 때만 — 전원이 쓰러졌으면 판이 이미 끝났다).</summary>
        void ReviveAllOnWaveClear(SimWorld w)
        {
            ReviveTuning t = Revive(w);
            if (!t.WaveClearRevive) return;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (p.Active && p.Downed && !p.Left) StandUp(w, p, t, -1); // 판 도중 나간 사람은 일으키지 않는다
            }
        }

        static void StandUp(SimWorld w, PlayerState p, ReviveTuning t, int helperSlot)
        {
            w.Mods.Player.Rise(w, p);
            p.Hp = Mathf.Clamp(t.HpCells * SimConst.HpPerCell, 1, p.MaxHp);
            p.InvulnUntilTick = w.Tick + t.InvulnTicks;
            p.ReviveTicks = 0;
            w.Emit(SimEventType.PlayerRevived, p.Slot, a: helperSlot);
        }
    }
}
