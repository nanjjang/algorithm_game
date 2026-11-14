using System;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Story
{
    /// <summary>
    /// 튜토리얼 화면(ScreenId.Tutorial)의 흐름. 그리기가 없어 EditMode에서 가짜 서비스로 시험한다(TutorialScreen이 이것을 부른다).
    /// 08 요약서 6.4절, 00 문서 4.1·5.5·8.1절, D-59.
    ///   들어올 때: 받은 진짜 판의 설정(realCfg)은 들고만 있는다. 튜토리얼 방 설정은 BuildPracticeConfig(…, Tutorial, null, …)로 따로 만든다
    ///             (프로필을 바꾸지 않는다). 못 만들면(서비스 없음 포함) 튜토리얼 없이 진짜 판으로.
    ///   단계: 완료 판정은 시뮬레이션(TutorialStepDone) → 잠깐 뒤 TutorialAdvance(A = 단계 + 1).
    ///   ⑤: [조립 열기] = Camp(CampMode.Tutorial). TutorialStepDone(5)이면 Camp를 PopTo(Tutorial)로 닫고 끝낸다.
    ///   Esc: 건너뛰기 확인 창(일시정지 화면을 쓰지 않는다. PauseRequested는 듣지 않는다).
    ///   끝: flags.tutorial_dummy_done = true + 저장 → EndRun → InputScreen 되돌림 → Loading을 거쳐 Battle(realCfg).
    /// </summary>
    public sealed class TutorialController
    {
        public const string SkipTitleKey = "str.tutorial.skip_confirm.title";
        public const string SkipBodyKey = "str.tutorial.skip_confirm.body";
        public const string StepSound = "ui.hud.tutorial_step";

        readonly GameContext _ctx;
        readonly StoryTuning _tuning;
        TutorialFlowModel _model;
        RunConfig _realCfg;
        RunConfig _tutorialCfg;
        IGameSession _subscribed;
        ScreenId _prevInputScreen = ScreenId.None;
        bool _inputScreenTaken;
        bool _running;
        bool _left;
        bool _confirmOpen;

        public TutorialController(GameContext ctx, StoryTuning tuning = null)
        {
            _ctx = ctx;
            _tuning = tuning ?? StoryTuning.From(ctx != null ? ctx.Content : null);
        }

        public TutorialFlowModel Model => _model;
        public RunConfig RealConfig => _realCfg;
        public RunConfig TutorialConfig => _tutorialCfg;
        /// <summary>튜토리얼 방이 돌고 있다(StartRun 했고 아직 끝나지 않았다).</summary>
        public bool Running => _running;
        /// <summary>진짜 판(Loading → Battle)으로 넘어갔다.</summary>
        public bool Left => _left;
        public bool ConfirmOpen => _confirmOpen;

        IGameSession Session => _ctx != null ? _ctx.Session : null;
        IScreenManager Screens => _ctx != null ? _ctx.Screens : null;

        /// <summary>지금 튜토리얼 세계(세션의 세계. 없으면 null).</summary>
        public SimWorld World => Session != null ? Session.World : null;

        /// <summary>지금 단계(시뮬레이션 상태가 정본).</summary>
        public int CurrentStep
        {
            get
            {
                var w = World;
                return w != null && w.Run != null ? w.Run.TutorialStep : 0;
            }
        }

        public TutorialBalloon Balloon() =>
            _model != null ? _model.Balloon(CurrentStep, _ctx != null ? _ctx.Names : null) : new TutorialBalloon();

        /// <summary>화면 OnEnter. 튜토리얼 방을 열었으면 true, 열지 못해 진짜 판으로 바로 넘어갔으면 false.</summary>
        public bool Enter(ScreenArgs args)
        {
            _left = false;
            _running = false;
            _confirmOpen = false;
            _realCfg = args != null ? args.Get<RunConfig>(ScreenArgKeys.RunConfig) : null;
            _model = new TutorialFlowModel(_tuning.TutorialStepCount, _tuning.TutorialAdvanceDelayTicks);
            _tutorialCfg = BuildTutorialConfig(_realCfg);
            var session = Session;
            if (_tutorialCfg == null || session == null)
            {
                GoToRealRun();
                return false;
            }

            session.StartRun(_tutorialCfg);
            _prevInputScreen = session.InputScreen;
            session.InputScreen = ScreenId.Tutorial;
            _inputScreenTaken = true;
            session.SimEventRaised += OnSimEvent;
            _subscribed = session;
            _running = true;
            return true;
        }

        RunConfig BuildTutorialConfig(RunConfig real)
        {
            if (real == null || _ctx == null || _ctx.Wands == null) return null;
            int character = Ids.Char.NumberOf(real.CharId);
            if (character <= 0) return null;
            try
            {
                var r = _ctx.Wands.BuildPracticeConfig(character, real.WandId, RunMode.Tutorial, null, real.Seed);
                if (!r.Ok || r.Value == null) return null;
                // 진짜 판 설정과 같은 객체를 쓰지 않는다(Mode가 바뀌면 진짜 판이 튜토리얼이 된다).
                if (ReferenceEquals(r.Value, real)) return null;
                return r.Value;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("TutorialController: BuildPracticeConfig failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>화면 OnExit(다른 화면으로 바뀌어 닫힐 때). 구독과 입력 문을 되돌린다(이미 했으면 아무 일 없다).</summary>
        public void Exit()
        {
            Unsubscribe();
            RestoreInputScreen();
        }

        void Unsubscribe()
        {
            if (_subscribed != null) _subscribed.SimEventRaised -= OnSimEvent;
            _subscribed = null;
        }

        void RestoreInputScreen()
        {
            if (!_inputScreenTaken) return;
            _inputScreenTaken = false;
            var s = Session;
            if (s != null && s.InputScreen == ScreenId.Tutorial) s.InputScreen = _prevInputScreen;
        }

        /// <summary>IGameSession.SimEventRaised. 튜토리얼 세계의 TutorialStepDone만 본다.</summary>
        public void OnSimEvent(SimWorld world, SimEvent e)
        {
            if (!_running || _model == null || e.Type != SimEventType.TutorialStepDone) return;
            var w = World;
            if (w != null && world != null && !ReferenceEquals(world, w)) return;
            int current = world != null && world.Run != null ? world.Run.TutorialStep : CurrentStep;
            bool last;
            if (!_model.OnStepDone(e.A, current, e.Tick, out last)) return;
            PlaySound(StepSound);
            if (last) CloseCampIfOpen();
        }

        void CloseCampIfOpen()
        {
            var m = Screens;
            if (m == null) return;
            if (m.Top != ScreenId.Tutorial && m.IsOpen(ScreenId.Tutorial)) m.PopTo(ScreenId.Tutorial);
        }

        /// <summary>매 프레임(화면 Tick). 예약한 넘김을 세계에 넣고, 마지막이면 끝낸다.</summary>
        public void Tick()
        {
            if (!_running || _model == null) return;
            var w = World;
            if (w == null) return;
            var act = _model.Poll(w.Tick);
            if (act.Kind == TutorialActionKind.None) return;
            w.Enqueue(new RunCommand(RunCommandKind.TutorialAdvance, act.Next));
            if (act.Kind == TutorialActionKind.AdvanceAndFinish) FinishNow();
        }

        /// <summary>⑤ [조립 열기](J). 되면 true.</summary>
        public bool OpenLoadout()
        {
            if (!_running || _model == null || !_model.CanOpenLoadout(CurrentStep)) return false;
            var m = Screens;
            if (m == null) return false;
            m.Push(ScreenId.Camp, ScreenArgs.From(ScreenId.Tutorial).With(ScreenArgKeys.CampMode, CampMode.Tutorial));
            return true;
        }

        /// <summary>Esc = 건너뛰기 확인 창. 확인하면 끝낸다.</summary>
        public void RequestSkip()
        {
            if (!_running || _model == null || _model.Finished || _confirmOpen) return;
            var m = Screens;
            if (m == null) { Skip(); return; }
            _confirmOpen = true;
            m.Confirm(SkipTitleKey, SkipBodyKey, false, ok =>
            {
                _confirmOpen = false;
                if (ok) Skip();
            });
        }

        void Skip()
        {
            if (_model == null || !_model.RequestFinish()) return;
            FinishNow();
        }

        /// <summary>끝(완료·건너뛰기 공통).</summary>
        void FinishNow()
        {
            if (!_running) return;
            _running = false;
            Unsubscribe();
            var profile = _ctx != null ? _ctx.Profile : null;
            if (profile != null && profile.Flags != null)
            {
                profile.Flags.TutorialDummyDone = true;
                try { _ctx.Profiles.Save(); }
                catch (Exception ex) { Debug.LogWarning("TutorialController: profile save failed: " + ex.Message); }
            }
            var s = Session;
            if (s != null) s.EndRun();
            RestoreInputScreen();
            CloseCampIfOpen();
            GoToRealRun();
        }

        /// <summary>Loading을 거쳐 Battle(realCfg 그대로). realCfg가 없으면 이 화면만 닫는다.</summary>
        void GoToRealRun()
        {
            _left = true;
            var m = Screens;
            if (m == null) return;
            if (_realCfg == null)
            {
                Debug.LogWarning("TutorialController: no RunConfig argument; closing the tutorial.");
                if (m.Top == ScreenId.Tutorial) m.Pop();
                return;
            }
            var battleArgs = ScreenArgs.From(ScreenId.Tutorial).With(ScreenArgKeys.RunConfig, _realCfg);
            m.Replace(ScreenId.Loading, ScreenArgs.ViaLoading(ScreenId.Tutorial, ScreenId.Battle, battleArgs));
        }

        void PlaySound(string id)
        {
            var snd = _ctx != null ? _ctx.Sound : null;
            if (snd == null) return;
            try { snd.Play(id, SoundBus.Ui); } catch (Exception) { }
        }
    }
}
