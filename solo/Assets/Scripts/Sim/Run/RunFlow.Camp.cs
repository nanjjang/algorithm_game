using UnityEngine;

namespace Wizard.Solo.Sim.Run
{
    // 야영(Camp와 출발 야영 StartCamp 공통). 06 요약서 4.3절, 00 문서 5.5절(D-49·D-52).
    // 하는 일은 조립·회복·비기 선택뿐이다.
    public sealed partial class RunFlow
    {
        void EnterCamp(SimWorld w, RunScratch s, bool startCamp)
        {
            var run = w.Run;
            var camp = w.Content.Camp;
            int justPassed = startCamp ? run.Config.StartWave - 1 : run.LastClearedWave;
            int next = justPassed + 1;

            // 다음 웨이브를 미리 만든다(야영 화면의 미리보기). 야영 동안 Run.Wave는 다음에 싸울 웨이브다.
            run.NextPlan = w.Mods.Waves.BuildPlan(w, next);
            run.Wave = next;
            run.Loot = null;
            run.HealsThisCamp = 0;
            run.DawnCamp = DayCycle.IsBossWave(justPassed);
            run.CampSub = CampSubState.FireIdle;
            run.DepartHoldStartTick = -1;

            // 모닥불 = 전장 가운데, 출발 표식 = 모닥불 + 오프셋(camp.json). 표시는 이 값만 읽는다.
            run.CampFirePos = new Vector2(w.ArenaWidth * 0.5f, w.ArenaHeight * 0.5f);
            run.DepartMarkerPos = run.CampFirePos + new Vector2(camp.DepartMarkerOffsetX, camp.DepartMarkerOffsetY);
            run.CampInteractRadius = camp.FireInteractRadius;
            run.DepartMarkerRadius = camp.DepartMarkerRadius;

            // 새벽 야영(보스 뒤): 체력 1칸 무료(가득이면 효과 없음).
            if (run.DawnCamp && camp.DawnFreeHealCells > 0)
            {
                for (int i = 0; i < w.Players.Length; i++)
                {
                    var p = w.Players[i];
                    if (p.Active && p.Hp < p.MaxHp) w.Mods.Player.Heal(w, p, camp.DawnFreeHealCells);
                }
            }

            SetPhase(w, startCamp ? RunPhase.StartCamp : RunPhase.Camp);
            w.Emit(SimEventType.CampEntered, a: run.DawnCamp ? 1 : 0);
            AdvanceArtOffer(w);
        }

        /// <summary>회복 값 = heal_base + heal_per_wave × (방금 넘긴 웨이브), 같은 야영의 두 번째부터 × 3/2(누적 아님).</summary>
        public override int HealCost(SimWorld w)
        {
            var run = w.Run;
            var camp = w.Content.Camp;
            int justPassed = run.Phase == RunPhase.StartCamp && run.Config != null ? run.Config.StartWave - 1 : run.LastClearedWave;
            int cost = camp.HealBase + camp.HealPerWave * justPassed;
            if (run.HealsThisCamp > 0 && camp.HealRepeatMulDen > 0) cost = cost * camp.HealRepeatMulNum / camp.HealRepeatMulDen;
            return cost;
        }

        static PlayerState FirstActive(SimWorld w)
        {
            for (int i = 0; i < w.Players.Length; i++) if (w.Players[i].Active) return w.Players[i];
            return null;
        }

        /// <summary>
        /// 해금 차례를 하나 꺼내 후보를 만든다(04 요약서 11.3절). 후보가 없는 차례는 보여 주지 않고 쓴다.
        /// 고르는 중인 차례는 PendingArtTurns의 맨 앞에 남아 있다 — "남은 차례가 있는가" = PendingArtTurns.Count &gt; 0.
        /// </summary>
        void AdvanceArtOffer(SimWorld w)
        {
            var run = w.Run;
            if (run.CurrentArtOffer != null) return;
            var p = FirstActive(w);
            while (run.PendingArtTurns.Count > 0)
            {
                int turn = run.PendingArtTurns.Peek();
                ArtOffer offer = p != null ? w.Mods.Arts.BuildOffer(w, p, turn) : null;
                if (offer != null && offer.Candidates.Count > 0)
                {
                    run.CurrentArtOffer = offer;
                    w.Emit(SimEventType.ArtOffered, slot: p.Slot, a: turn);
                    return;
                }
                run.PendingArtTurns.Dequeue();
            }
        }

        bool HasPendingArtTurn(SimWorld w)
        {
            AdvanceArtOffer(w);
            return w.Run.CurrentArtOffer != null;
        }

        void OpenCampScreen(SimWorld w)
        {
            var run = w.Run;
            if (run.CampSub == CampSubState.CampScreen) return;
            if (run.CampSub == CampSubState.DepartHold)
            {
                run.DepartHoldStartTick = -1;
                w.Emit(SimEventType.DepartHold, a: 0);
            }
            run.CampSub = CampSubState.CampScreen;
            w.Emit(SimEventType.CampChanged);
        }

        /// <summary>출발. 남은 해금 차례가 있으면 떠나지 않는다(false).</summary>
        bool TryDepart(SimWorld w, RunScratch s)
        {
            var run = w.Run;
            if (HasPendingArtTurn(w)) return false;
            run.DepartHoldStartTick = -1;
            run.CampSub = CampSubState.FireIdle;
            EnterPrelude(w, s, run.Wave);
            return true;
        }

        static bool AnyPlayerNear(SimWorld w, Vector2 pos, float radius, bool needJPress, bool needJDown)
        {
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!p.Active || p.Downed) continue;
                if ((p.Pos - pos).sqrMagnitude > radius * radius) continue;
                if (needJPress && !p.JPressedThisTick) continue;
                if (needJDown && !p.Keys.IsDown(LogicalKey.Fire)) continue;
                return true;
            }
            return false;
        }

        /// <summary>FireIdle·DepartHold의 전장 상호작용. 야영 화면이 떠 있는 동안에는 아무것도 읽지 않는다.</summary>
        void TickCamp(SimWorld w, RunScratch s)
        {
            var run = w.Run;
            if (run.CampSub == CampSubState.CampScreen) return;

            bool onMarker = AnyPlayerNear(w, run.DepartMarkerPos, run.DepartMarkerRadius, false, true);
            if (run.CampSub == CampSubState.DepartHold)
            {
                if (!onMarker)
                {
                    // 떼거나 반경을 벗어남 → 취소
                    run.CampSub = CampSubState.FireIdle;
                    run.DepartHoldStartTick = -1;
                    w.Emit(SimEventType.DepartHold, a: 0);
                    return;
                }
                // 누른 틱부터 세어 depart_hold_seconds(60틱)째에 출발한다. 59틱째까지 누르다 떼면 위에서 취소된다.
                int held = w.Tick - run.DepartHoldStartTick + 1;
                if (held >= SimConst.Ticks(w.Content.Camp.DepartHoldSeconds))
                {
                    if (TryDepart(w, s)) TickPrelude(w, s);
                    else OpenCampScreen(w);
                }
                return;
            }

            // FireIdle: 모닥불 옆 J(누르는 순간) = 야영 화면
            if (AnyPlayerNear(w, run.CampFirePos, run.CampInteractRadius, true, false))
            {
                OpenCampScreen(w);
                return;
            }
            if (onMarker)
            {
                // 남은 해금 차례가 있으면 출발 누름을 받지 않고 야영 화면(비기 탭)으로 보낸다.
                if (HasPendingArtTurn(w)) { OpenCampScreen(w); return; }
                run.CampSub = CampSubState.DepartHold;
                run.DepartHoldStartTick = w.Tick;
                w.Emit(SimEventType.DepartHold, a: Mathf.Max(1, w.Tick));
            }
        }
    }
}
