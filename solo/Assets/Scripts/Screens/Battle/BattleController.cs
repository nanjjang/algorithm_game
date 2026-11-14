using System;
using System.Collections.Generic;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Battle
{
    /// <summary>
    /// 판 진행 화면의 동작(엔진 없음). BattleScreen이 OnEnter/OnExit/OnBack/Tick을 여기로 넘긴다. 테스트는 가짜 세션·화면 관리자로 직접 돌린다.
    /// - Enter: RunConfig로 세션 StartRun, InputScreen = Battle, SimEventRaised·PauseRequested 구독.
    /// - 일시정지 길은 RequestPause 하나(Esc = OnBack, 재설정한 일시정지 키 = PauseRequested). 커맨드 초기화는 세션이 한다.
    /// - 보조 판(야영의 조립 시험)의 이벤트는 무시한다.
    /// </summary>
    public sealed class BattleController
    {
        readonly GameContext _ctx;
        readonly List<BattleFlowAction> _actions = new List<BattleFlowAction>();
        IGameSession _subscribed;

        public readonly BattleFlowModel Flow = new BattleFlowModel();

        /// <summary>사망 문구를 보이거나 숨길 때(화면이 글자 칸을 켜고 끈다).</summary>
        public Action<bool> DeathLineChanged;

        public BattleController(GameContext ctx)
        {
            _ctx = ctx;
            Flow.Configure(ctx != null ? ctx.Content : null);
        }

        IGameSession Session => _ctx != null ? _ctx.Session : null;
        IScreenManager Screens => _ctx != null ? _ctx.Screens : null;

        public void Enter(ScreenArgs args)
        {
            Flow.Reset();
            DeathLineChanged?.Invoke(false);
            var s = Session;
            if (s == null) return;
            Unsubscribe();
            var cfg = args != null ? args.Get<RunConfig>(ScreenArgKeys.RunConfig) : null;
            if (cfg != null) s.StartRun(cfg);
            s.InputScreen = ScreenId.Battle;
            s.SimEventRaised += OnSimEvent;
            s.PauseRequested += OnPauseRequested;
            _subscribed = s;
        }

        public void Exit()
        {
            Unsubscribe();
            var s = Session;
            if (s != null) s.InputScreen = ScreenId.None;
        }

        void Unsubscribe()
        {
            if (_subscribed == null) return;
            _subscribed.SimEventRaised -= OnSimEvent;
            _subscribed.PauseRequested -= OnPauseRequested;
            _subscribed = null;
        }

        void OnPauseRequested() => RequestPause();

        /// <summary>
        /// 일시정지(00 문서 4.1절 — 길은 하나). 이미 Paused, 판 없음, 되비침·쓰러짐 중, 판이 끝남, Battle이 맨 위가 아님이면 무시.
        /// 아니면 Paused = true(세션이 커맨드 초기화를 한 번 한다) + Push(Pause). 열었으면 true.
        /// </summary>
        public bool RequestPause()
        {
            var s = Session;
            var m = Screens;
            if (s == null || m == null) return false;
            if (m.Top != ScreenId.Battle) return false;
            if (!Flow.CanRequestPause(s.MainWorld, s.HasRun, s.Paused)) return false;
            s.Paused = true;
            m.Push(ScreenId.Pause, ScreenArgs.From(ScreenId.Battle));
            return true;
        }

        /// <summary>세션의 틱 이벤트. 본판 세계의 것만 본다.</summary>
        public void OnSimEvent(SimWorld world, SimEvent e)
        {
            var s = Session;
            if (s == null || world == null || !ReferenceEquals(world, s.MainWorld)) return;
            var m = Screens;
            _actions.Clear();
            Flow.OnEvent(world, e, m != null ? (Func<ScreenId, bool>)m.IsOpen : null, _actions);
            for (int i = 0; i < _actions.Count; i++) Execute(world, _actions[i]);
            _actions.Clear();
        }

        void Execute(SimWorld world, BattleFlowAction a)
        {
            var m = Screens;
            switch (a.Kind)
            {
                case BattleFlowActionKind.PushLootPick:
                    m?.Push(ScreenId.LootPick, ScreenArgs.From(ScreenId.Battle));
                    break;
                case BattleFlowActionKind.CloseLootPick:
                case BattleFlowActionKind.CloseCamp:
                    m?.PopTo(ScreenId.Battle);
                    break;
                case BattleFlowActionKind.PushCamp:
                    m?.Push(ScreenId.Camp, ScreenArgs.From(ScreenId.Battle).With(ScreenArgKeys.CampMode, CampMode.Camp));
                    break;
                case BattleFlowActionKind.SaveCheckpoint:
                    if (_ctx.Settlement != null && world.Mods != null && world.Mods.Run != null)
                        _ctx.Settlement.SaveCheckpoint(world.Mods.Run.BuildSummary(world));
                    break;
                case BattleFlowActionKind.RecordArtUnlock:
                    _ctx.Settlement?.RecordArtUnlock(a.Character, a.ArtId);
                    break;
                case BattleFlowActionKind.BeginSettlement:
                    DeathLineChanged?.Invoke(Flow.DeathLineVisible);
                    break;
            }
        }

        /// <summary>매 프레임. 정산 대기가 끝나면 Settlement로 Replace.</summary>
        public void Tick(float dt)
        {
            if (!Flow.Advance(dt)) return;
            GoToSettlement();
        }

        void GoToSettlement()
        {
            var s = Session;
            var m = Screens;
            if (m == null) return;
            RunSummary summary = s != null ? s.LastSummary : null;
            if (summary == null && s != null)
            {
                var w = s.MainWorld;
                if (w != null && w.Mods != null && w.Mods.Run != null) summary = w.Mods.Run.BuildSummary(w);
            }
            if (m.Top != ScreenId.Battle && m.IsOpen(ScreenId.Battle)) m.PopTo(ScreenId.Battle);
            m.Replace(ScreenId.Settlement, ScreenArgs.From(ScreenId.Battle).With(ScreenArgKeys.RunSummary, summary));
        }
    }
}
