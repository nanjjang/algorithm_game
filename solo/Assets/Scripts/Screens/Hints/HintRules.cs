using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Hints
{
    /// <summary>
    /// 판 안의 문맥 힌트를 고른다(순수). 진짜 판(Mode = Endless)의 본판 이벤트만 본다 — 튜토리얼·연습 방은 이미 가르치는 중이다.
    /// 막힘 감지: 과열 잠금 중 J 연타(FireDryWhileLocked)가 창 안에 n번, 결정 부족 불발(ArtFizzled NoCrystal)이 창 안에 n번.
    /// 판마다 막힘 힌트는 한 번씩만 낸다(BeginRun이 비운다).
    /// </summary>
    public sealed class HintRules
    {
        readonly HintTuning _t;
        readonly Queue<int> _dry = new Queue<int>();
        readonly Queue<int> _fizzles = new Queue<int>();
        readonly HashSet<string> _firedThisRun = new HashSet<string>();

        public HintRules(HintTuning tuning) { _t = tuning ?? new HintTuning(); }

        /// <summary>새 판. 막힘 창과 "이번 판에 낸 막힘 힌트"를 비운다.</summary>
        public void BeginRun()
        {
            _dry.Clear();
            _fizzles.Clear();
            _firedThisRun.Clear();
        }

        public static bool IsRealRun(SimWorld w) =>
            w != null && w.Run != null && w.Run.Config != null && w.Run.Config.Mode == RunMode.Endless;

        /// <summary>이벤트 하나가 부르는 힌트 id들을 into에 넣는다(앞에 둔 것이 먼저). 진짜 판이 아니면 아무것도 넣지 않는다.</summary>
        public void OnEvent(SimWorld w, in SimEvent e, List<string> into)
        {
            if (!IsRealRun(w) || into == null) return;
            if (e.Slot > 0) return;
            switch (e.Type)
            {
                case SimEventType.HeatLockEntered: into.Add(HintIds.FirstLock); break;
                case SimEventType.CrystalGained:
                    into.Add(HintIds.FirstCrystal);
                    // 결정이 파이널 어빌리티를 쓸 만큼 모였다 → 파이널 커맨드 안내
                    if (w.Me != null && AttackStyles.FinalMinCrystals(w.Content) > 0 && w.Me.Crystals >= AttackStyles.FinalMinCrystals(w.Content))
                        into.Add(HintIds.FirstFinal);
                    break;
                case SimEventType.GrazeAwarded: into.Add(HintIds.FirstGraze); break;
                case SimEventType.EnemySpawned: if (e.A == 1) into.Add(HintIds.FirstElite); break;
                case SimEventType.BossIntroBegin: into.Add(HintIds.FirstBoss); break;
                case SimEventType.ArtUnlocked: into.Add(HintIds.FirstArtUnlock); break;
                case SimEventType.AffinityDiscovered: into.Add(HintIds.FirstAffinity); break;
                case SimEventType.ArtFizzled:
                    if (e.A != (int)ArtFizzleReason.NoCrystal) break;
                    into.Add(HintIds.FirstFizzle);
                    if (Window(_fizzles, e.Tick, SimConst.Ticks(_t.FizzleWindowSeconds)) >= _t.FizzleCount) Stuck(HintIds.StuckFizzle, into);
                    break;
                case SimEventType.FireDryWhileLocked:
                    if (Window(_dry, e.Tick, SimConst.Ticks(_t.DryFireWindowSeconds)) >= _t.DryFireCount) Stuck(HintIds.StuckDryFire, into);
                    break;
            }
        }

        void Stuck(string id, List<string> into)
        {
            if (_firedThisRun.Add(id)) into.Add(id);
        }

        static int Window(Queue<int> q, int tick, int windowTicks)
        {
            q.Enqueue(tick);
            while (q.Count > 0 && tick - q.Peek() > windowTicks) q.Dequeue();
            return q.Count;
        }
    }
}
