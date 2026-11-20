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
            run.DawnCamp = DayCycle.IsBossWave(justPassed);
            for (int i = 0; i < run.Rewards.Length; i++)
            {
                var r = run.Rewards[i];
                r.Loot = null;
                r.LootDone = false;
                r.HealsThisCamp = 0;
                r.CampSub = CampSubState.FireIdle;
                r.DepartHoldStartTick = -1;
                r.DepartReady = false;
            }

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
                    if (p.Active && !p.Left && p.Hp < p.MaxHp) w.Mods.Player.Heal(w, p, camp.DawnFreeHealCells);
                }
            }

            SetPhase(w, startCamp ? RunPhase.StartCamp : RunPhase.Camp);
            w.Emit(SimEventType.CampEntered, a: run.DawnCamp ? 1 : 0);
            for (int i = 0; i < w.Players.Length; i++)
                if (Participating(w.Players[i])) AdvanceArtOffer(w, w.Players[i]);
        }

        /// <summary>회복 값 = heal_base + heal_per_wave × (방금 넘긴 웨이브), 같은 야영의 두 번째부터 × 3/2(누적 아님). 횟수는 사람마다 센다.</summary>
        public override int HealCost(SimWorld w, int slot)
        {
            var run = w.Run;
            var camp = w.Content.Camp;
            int justPassed = run.Phase == RunPhase.StartCamp && run.Config != null ? run.Config.StartWave - 1 : run.LastClearedWave;
            int cost = camp.HealBase + camp.HealPerWave * justPassed;
            int heals = slot >= 0 && slot < run.Rewards.Length ? run.Rewards[slot].HealsThisCamp : 0;
            if (heals > 0 && camp.HealRepeatMulDen > 0) cost = cost * camp.HealRepeatMulNum / camp.HealRepeatMulDen;
            return cost;
        }

        /// <summary>
        /// 그 사람의 해금 차례를 하나 꺼내 후보를 만든다(04 요약서 11.3절). 후보가 없는 차례는 보여 주지 않고 쓴다.
        /// 고르는 중인 차례는 ArtTurns의 맨 앞에 남아 있다 — "남은 차례가 있는가" = ArtTurns.Count &gt; 0.
        /// </summary>
        void AdvanceArtOffer(SimWorld w, PlayerState p)
        {
            if (p == null) return;
            var r = w.Run.Rewards[p.Slot];
            if (r.ArtOffer != null) return;
            while (r.ArtTurns.Count > 0)
            {
                int turn = r.ArtTurns.Peek();
                ArtOffer offer = w.Mods.Arts.BuildOffer(w, p, turn);
                if (offer != null && offer.Candidates.Count > 0)
                {
                    r.ArtOffer = offer;
                    w.Emit(SimEventType.ArtOffered, slot: p.Slot, a: turn);
                    return;
                }
                r.ArtTurns.Dequeue();
            }
        }

        bool HasPendingArtTurn(SimWorld w, PlayerState p)
        {
            AdvanceArtOffer(w, p);
            return w.Run.Rewards[p.Slot].ArtOffer != null;
        }

        /// <summary>참여 중인 사람들에게 해금 차례를 하나씩 더한다(웨이브 번호).</summary>
        static void EnqueueArtTurn(SimWorld w, int wave)
        {
            for (int i = 0; i < w.Players.Length; i++)
                if (Participating(w.Players[i])) w.Run.Rewards[i].ArtTurns.Enqueue(wave);
        }

        /// <summary>그 사람의 야영 화면을 연다(출발 준비는 거둔다).</summary>
        void OpenCampScreen(SimWorld w, PlayerState p)
        {
            if (p == null) return;
            var r = w.Run.Rewards[p.Slot];
            if (r.CampSub == CampSubState.CampScreen) return;
            if (r.CampSub == CampSubState.DepartHold)
            {
                r.DepartHoldStartTick = -1;
                w.Emit(SimEventType.DepartHold, p.Slot, a: 0);
            }
            r.DepartReady = false;
            r.CampSub = CampSubState.CampScreen;
            w.Emit(SimEventType.CampChanged, p.Slot);
        }

        /// <summary>MarkReady의 결과.</summary>
        enum DepartResult { Blocked, Waiting, Departed }

        /// <summary>
        /// 그 사람이 출발한다. 남은 해금 차례가 있으면 받지 않는다(Blocked). 참여 중인 사람이 모두 준비되면 웨이브로 떠나고(Departed),
        /// 아니면 동료를 기다린다(Waiting — 솔로에는 없다).
        /// </summary>
        DepartResult MarkReady(SimWorld w, RunScratch s, PlayerState p)
        {
            var run = w.Run;
            var r = run.Rewards[p.Slot];
            if (HasPendingArtTurn(w, p)) return DepartResult.Blocked;
            r.DepartHoldStartTick = -1;
            r.CampSub = CampSubState.FireIdle;
            r.DepartReady = true;
            if (AllReady(w))
            {
                EnterPrelude(w, s, run.Wave);
                return DepartResult.Departed;
            }
            w.Emit(SimEventType.CampChanged, p.Slot);
            return DepartResult.Waiting;
        }

        /// <summary>출발을 기다리는 사람(참여 중이고 서 있는 사람)이 모두 준비됐나. 기다릴 사람이 없으면 false.</summary>
        static bool AllReady(SimWorld w)
        {
            int n = 0;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!Participating(p) || p.Downed) continue;
                if (!w.Run.Rewards[i].DepartReady) return false;
                n++;
            }
            return n > 0;
        }

        static bool Near(PlayerState p, Vector2 pos, float radius, bool needJPress, bool needJDown)
        {
            if ((p.Pos - pos).sqrMagnitude > radius * radius) return false;
            if (needJPress && !p.JPressedThisTick) return false;
            if (needJDown && !p.Keys.IsDown(LogicalKey.Fire)) return false;
            return true;
        }

        /// <summary>FireIdle·DepartHold의 전장 상호작용. 사람마다 따로: 자기 야영 화면이 떠 있는 사람은 읽지 않는다.</summary>
        void TickCamp(SimWorld w, RunScratch s)
        {
            var run = w.Run;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!Participating(p) || p.Downed) continue;
                var r = run.Rewards[i];
                if (r.CampSub == CampSubState.CampScreen) continue;

                // 출발 준비를 마치고 기다리는 사람: 모닥불 옆 J로 다시 야영 화면(준비를 거둔다).
                if (r.DepartReady)
                {
                    if (Near(p, run.CampFirePos, run.CampInteractRadius, true, false)) OpenCampScreen(w, p);
                    continue;
                }

                bool onMarker = Near(p, run.DepartMarkerPos, run.DepartMarkerRadius, false, true);
                if (r.CampSub == CampSubState.DepartHold)
                {
                    if (!onMarker)
                    {
                        // 떼거나 반경을 벗어남 → 취소
                        r.CampSub = CampSubState.FireIdle;
                        r.DepartHoldStartTick = -1;
                        w.Emit(SimEventType.DepartHold, p.Slot, a: 0);
                        continue;
                    }
                    // 누른 틱부터 세어 depart_hold_seconds(60틱)째에 출발한다. 59틱째까지 누르다 떼면 위에서 취소된다.
                    int held = w.Tick - r.DepartHoldStartTick + 1;
                    if (held >= SimConst.Ticks(w.Content.Camp.DepartHoldSeconds))
                    {
                        var res = MarkReady(w, s, p);
                        if (res == DepartResult.Departed) { TickPrelude(w, s); return; }
                        if (res == DepartResult.Blocked) OpenCampScreen(w, p);
                    }
                    continue;
                }

                // FireIdle: 모닥불 옆 J(누르는 순간) = 야영 화면
                if (Near(p, run.CampFirePos, run.CampInteractRadius, true, false))
                {
                    OpenCampScreen(w, p);
                    continue;
                }
                if (onMarker)
                {
                    // 남은 해금 차례가 있으면 출발 누름을 받지 않고 야영 화면(비기 탭)으로 보낸다.
                    if (HasPendingArtTurn(w, p)) { OpenCampScreen(w, p); continue; }
                    r.CampSub = CampSubState.DepartHold;
                    r.DepartHoldStartTick = w.Tick;
                    w.Emit(SimEventType.DepartHold, p.Slot, a: Mathf.Max(1, w.Tick));
                }
            }
        }

        /// <summary>협동: 판 도중 나간 사람. 보상·출발 기다림에서 빼고, 남은 사람이 다 끝냈으면 넘어간다.</summary>
        public override void OnPlayerLeft(SimWorld w, int slot)
        {
            if (slot < 0 || slot >= w.Players.Length) return;
            var p = w.Players[slot];
            var run = w.Run;
            if (!p.Active || p.Left) return;
            p.Left = true;
            var r = run.Rewards[slot];
            r.Loot = null;
            r.LootDone = true;
            r.ArtTurns.Clear();
            r.ArtOffer = null;
            r.CampSub = CampSubState.FireIdle;
            r.DepartHoldStartTick = -1;
            r.DepartReady = false;
            if (run.Phase == RunPhase.NotStarted || run.Phase == RunPhase.RunEnded) return;
            var s = Scratch(w);
            bool anyone = false;
            for (int i = 0; i < w.Players.Length; i++) if (Participating(w.Players[i])) anyone = true;
            if (!anyone) return;
            if (run.Phase == RunPhase.LootPick)
            {
                if (AllLootDone(w)) EnterCamp(w, s, startCamp: false);
                else w.Emit(SimEventType.LootChanged, slot);
            }
            else if (w.InCamp)
            {
                if (AllReady(w)) EnterPrelude(w, s, run.Wave);
                else w.Emit(SimEventType.CampChanged, slot);
            }
        }
    }
}
