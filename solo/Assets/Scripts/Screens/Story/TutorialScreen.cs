using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Run;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Story
{
    /// <summary>
    /// 튜토리얼(첫 판 전 허수아비 방, 해 보며 배우는 관문 — DataSolo/Tutorial.json 단계 표). 판을 연 화면이라 전투 입력의 문(InputScreen)을 자기 id로 연다.
    /// 전장이 보여야 하므로 뼈대 없는 Fullscreen 층(전투 화면과 같음)이고, 그리는 것은
    ///   (가) 풍선: 단계 번호, 제목, 본문 1~2문장, 화면 안 목표(진척 n/m + 막대), 누를 키, 막혔을 때 힌트 한 줄, 완료 칭찬,
    ///   (나) 도입 대사 판(스승의 말, J로 넘김), (다) 단계 눈금.
    /// 전장 위 표시(이동 목표 고리, 칭찬 반짝)는 TutorialLayer가 한다. 흐름은 TutorialController가 갖는다.
    /// 위험색을 쓰지 않는다(칭찬 = 좋음 초록, 힌트 = 놋쇠, 실수 = 호박색 테).
    /// </summary>
    public sealed class TutorialScreen : ScreenBase
    {
        public const string OpenLoadoutKey = "str.tutorial.open_loadout";
        public const string SkipActionKey = "str.tutorial.skip";
        public const string StepCounterKey = "str.tutorial.step_counter";
        public const string NextLineActionKey = "str.tutorial.intro.next";
        public const string IntroCounterKey = "str.tutorial.intro.counter";
        public const string MasterNameKey = "npc.master";
        public const string HintMarkKey = "str.tutorial.hint_mark";

        public override ScreenId Id => ScreenId.Tutorial;
        // 전장이 보여야 하므로 뼈대 없이 그린다. 층은 전투 화면(Battle)·연습 화면과 같이 Fullscreen이다 —
        // HudOverlay면 아래의 전체 화면(원정 준비가 바뀐 뒤 드러나는 메인 메뉴)이 꺼지지 않아 전장을 가린다(통합 단계 3에서 화면으로 확인).
        public override ScreenLayer Layer => ScreenLayer.Fullscreen;
        protected override bool HasFrame => false;
        /// <summary>메뉴 키가 아래 화면으로 새지 않게 막는다(아래 화면이 남아 있어도 조작되지 않는다).</summary>
        public override bool BlocksInput => true;

        const float Pad = 20f, ChipW = 64f, KeyCapPx = 32f, PipW = 28f, PipH = 6f, PipGap = 6f;

        TutorialController _ctl;
        TutorialPlan _plan;
        TutorialOpening _opening;
        AsyncOperation _openingLoad;
        UImage _openingVeil;
        float _openingReveal;
        RectTransform _balloon, _dialog, _pips, _keys;
        UImage _praiseFrame, _setbackFrame;
        UiLabel _chip, _title, _body, _goal, _hint, _praise, _skip;
        UiLabel _dialogName, _dialogLine, _dialogFoot;
        UiProgressBar _bar;
        UiButton _open;
        readonly List<UImage> _pipImages = new List<UImage>();
        int _shownStep = -1, _shownLine = -1, _shownProgress = -1, _shownGoal = -1;
        string _shownHint, _shownBody, _shownKeys;
        bool _shownDone;
        float _praiseAge = -1f, _setbackAge = -1f, _stepAge = 1f;
        Vector2 _balloonBase;

        public TutorialController Controller => _ctl;

        protected override void BuildContent(RectTransform content)
        {
            var tuning = StoryTuning.From(Ctx != null ? Ctx.Content : null);
            _ctl = new TutorialController(Ctx, tuning);
            _plan = _ctl.Plan;
            _ctl.StepCompleted += OnStepCompleted;
            _ctl.SetbackRaised += OnSetback;
            BuildBalloon(content);
            BuildDialog(content);
        }

        float BW => _plan.BalloonW > 0f ? _plan.BalloonW : 960f;
        float BH => _plan.BalloonH > 0f ? _plan.BalloonH : 176f;

        void BuildBalloon(RectTransform content)
        {
            float w = BW, h = BH;
            _balloon = UiKit.Node(content, "TutorialBalloon", _plan.BalloonX, _plan.BalloonY, w, h);
            _balloonBase = _balloon.anchoredPosition;
            UiKit.OrnatePanel(Ctx, _balloon, 0f, 0f, w, h).raycastTarget = false;
            _praiseFrame = Frame(_balloon, w, h, UiTheme.Good, "PraiseFrame");
            _setbackFrame = Frame(_balloon, w, h, UiTheme.Amber, "SetbackFrame");

            float l24 = UiTheme.LineHeight(UiFontId.F24), l16 = UiTheme.LineHeight(UiFontId.F16);
            var chipBg = UiKit.Panel(Ctx, _balloon, UiPanelKind.Heading, Pad, Pad, ChipW, ChipW);
            chipBg.raycastTarget = false;
            _chip = UiKit.Label(Ctx, chipBg.rectTransform, string.Empty, UiFontId.F16, 0f, 0f, ChipW, ChipW, UiTheme.Chalk, TextAnchor.MiddleCenter);
            float x = Pad + ChipW + Pad;
            float tw = w - x - Pad;
            _title = UiKit.Label(Ctx, _balloon, string.Empty, UiFontId.F24, x, Pad - 4f, tw * 0.62f, l24 + 4f, UiTheme.Chalk);
            UiKit.DropShadow(_title, 2f);
            _praise = UiKit.Label(Ctx, _balloon, string.Empty, UiFontId.F24, x + tw * 0.4f, Pad - 4f, tw * 0.6f, l24 + 4f, UiTheme.Good, TextAnchor.MiddleRight);
            UiKit.DropShadow(_praise, 2f);
            _body = UiKit.Label(Ctx, _balloon, string.Empty, UiFontId.F16, x, Pad + l24 + 4f, tw, l16 * 2f + 4f, UiTheme.Chalk, TextAnchor.UpperLeft, true);

            float gy = h - Pad - l16 - 8f - 36f;
            _goal = UiKit.Label(Ctx, _balloon, string.Empty, UiFontId.F16, x, gy, 360f, 32f, UiTheme.Chalk);
            _bar = UiKit.ProgressBar(Ctx, _balloon, x + 368f, gy + 10f, 240f, 12f, 0f);
            _bar.FillColor = UiTheme.Good;
            _keys = UiKit.Node(_balloon, "Keys", x + 624f, gy, w - x - 624f - Pad, 32f);

            float bw = 280f, bh = 40f;
            string openText = Ctx != null && Ctx.Names != null
                ? Ctx.Names.Format(OpenLoadoutKey, "key", UiKeyLabels.Of(Ctx.Names, Key.J))
                : OpenLoadoutKey;
            _open = UiKit.Button(Ctx, _balloon, openText, w - Pad - bw, gy - 4f, bw, bh, OnOpenLoadoutClicked,
                UiButtonStyle.Primary, null, null, UiFontId.F16);

            float hy = h - Pad - l16;
            _hint = UiKit.Label(Ctx, _balloon, string.Empty, UiFontId.F16, x, hy, tw - 200f, l16, UiTheme.Brass);
            _skip = UiKit.Label(Ctx, _balloon,
                UiKeyHint.Compose(Ctx != null ? Ctx.Names : null, new[] { new UiKeyHint(Key.Escape, SkipActionKey) }),
                UiFontId.F16, w - Pad - 200f, hy, 200f, l16, UiTheme.SlateBlue, TextAnchor.MiddleRight);

            int n = _plan.Count;
            float pw = n * PipW + Mathf.Max(0, n - 1) * PipGap;
            _pips = UiKit.Node(content, "TutorialPips", _plan.BalloonX + (w - pw) * 0.5f, _plan.BalloonY + h + 10f, pw, PipH);
            for (int i = 0; i < n; i++) _pipImages.Add(UiKit.Rect(_pips, i * (PipW + PipGap), 0f, PipW, PipH, UiTheme.WoodLight, false, "Pip" + (i + 1)));

            _balloon.gameObject.SetActive(false);
            _pips.gameObject.SetActive(false);
        }

        static UImage Frame(RectTransform parent, float w, float h, Color c, string name)
        {
            var node = UiKit.Node(parent, name, 0f, 0f, w, h);
            const float t = 3f;
            UiKit.Rect(node, 0f, 0f, w, t, c);
            UiKit.Rect(node, 0f, h - t, w, t, c);
            UiKit.Rect(node, 0f, 0f, t, h, c);
            var last = UiKit.Rect(node, w - t, 0f, t, h, c);
            node.gameObject.SetActive(false);
            return last;
        }

        void BuildDialog(RectTransform content)
        {
            float w = _plan.DialogW > 0f ? _plan.DialogW : 1120f, h = _plan.DialogH > 0f ? _plan.DialogH : 200f;
            _dialog = UiKit.Node(content, "MasterDialog", _plan.DialogX, _plan.DialogY, w, h);
            UiKit.OrnatePanel(Ctx, _dialog, 0f, 0f, w, h).raycastTarget = false;
            float l24 = UiTheme.LineHeight(UiFontId.F24), l16 = UiTheme.LineHeight(UiFontId.F16);
            var plate = UiKit.Panel(Ctx, _dialog, UiPanelKind.Heading, 28f, -l24 * 0.5f, 200f, l24 + 12f);
            plate.raycastTarget = false;
            _dialogName = UiKit.Label(Ctx, plate.rectTransform, T(MasterNameKey), UiFontId.F24, 0f, 0f, 200f, l24 + 12f, UiTheme.Brass, TextAnchor.MiddleCenter);
            UiKit.DropShadow(_dialogName, 2f);
            _dialogLine = UiKit.Label(Ctx, _dialog, string.Empty, UiFontId.F24, 40f, 40f, w - 80f, h - 40f - l16 - 28f, UiTheme.Chalk, TextAnchor.UpperLeft, true);
            UiKit.DropShadow(_dialogLine, 2f);
            _dialogFoot = UiKit.Label(Ctx, _dialog, string.Empty, UiFontId.F16, 40f, h - 20f - l16, w - 80f, l16, UiTheme.SlateBlue, TextAnchor.MiddleRight);
            _dialog.gameObject.SetActive(false);
        }

        void OnOpenLoadoutClicked()
        {
            if (_ctl != null) _ctl.OpenLoadout();
        }

        void OnStepCompleted(int step)
        {
            _praiseAge = 0f;
        }

        void OnSetback(TutorialSetback why)
        {
            _setbackAge = 0f;
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _shownStep = -1;
            _shownLine = -1;
            if (_ctl == null) _ctl = new TutorialController(Ctx);
            var run = Args.Get<RunConfig>(ScreenArgKeys.RunConfig);
            if (run != null && Ctx?.Art != null && Ctx.Session != null
                && Application.CanStreamedLevelBeLoaded(TutorialOpening.ScenePath))
            {
                _openingVeil = UiKit.Rect(Content, 0f, 0f, 1920f, 1080f, Color.black, true, "Opening transition");
                UiFullBleed.Fill(_openingVeil.rectTransform);
                _openingLoad = SceneManager.LoadSceneAsync(TutorialOpening.ScenePath, LoadSceneMode.Additive);
                if (_openingLoad != null) return;
            }
            StartTutorial(false);
        }

        public override void OnExit()
        {
            ReleaseOpening();
            ClearOpeningVeil();
            if (_ctl != null) _ctl.Exit();
            base.OnExit();
        }

        /// <summary>Esc = 건너뛰기 확인 창(일시정지 화면을 쓰지 않는다 — 00 4.1절).</summary>
        public override bool OnBack()
        {
            if (_openingLoad != null || _opening != null)
            {
                StartTutorial(true);
                return true;
            }
            if (_ctl != null) _ctl.RequestSkip();
            return true;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            // 메뉴 키(J·방향)는 같은 물리 키로 전투에도 들어간다. 도입 대사의 J(다음 줄)와 블록 단계의 J([조립 열기])만 쓴다.
            if (_openingLoad != null || _opening != null) return true;
            if (key != MenuKey.Confirm || _ctl == null || _ctl.Model == null) return true;
            if (_ctl.NextIntroLine()) return true;
            if (_ctl.Model.CanOpenLoadout(_ctl.CurrentStep)) _ctl.OpenLoadout();
            return true;
        }

        public override void Tick(float dt)
        {
            if (_openingLoad != null)
            {
                if (!_openingLoad.isDone) return;
                _openingLoad = null;
                var scene = SceneManager.GetSceneByPath(TutorialOpening.ScenePath);
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out _opening)) break;
                }
                int effects = SettingsOptions.EffectsLevel(Ctx.Settings?.Current);
                float motion = effects == 0 ? 1f : effects == 1 ? 0.5f : 0f;
                if (_opening == null || !_opening.Build(Content, Ctx, Args.Get<RunConfig>(ScreenArgKeys.RunConfig), motion))
                    StartTutorial(false);
                return;
            }
            if (_opening != null)
            {
                if (!_opening.Advance(dt)) StartTutorial(true);
                return;
            }
            if (_openingVeil != null)
            {
                _openingReveal = Mathf.MoveTowards(_openingReveal, 0f, dt / 0.55f);
                _openingVeil.color = new Color(0f, 0f, 0f, _openingReveal);
                if (_openingReveal <= 0f) ClearOpeningVeil();
            }
            if (_ctl == null) return;
            _ctl.Tick();
            if (_praiseAge >= 0f) _praiseAge += dt;
            _stepAge += dt;
            if (_setbackAge >= 0f) _setbackAge += dt;
            Refresh();
        }

        void StartTutorial(bool openingShown)
        {
            ReleaseOpening();
            if (_openingVeil != null)
            {
                _openingVeil.raycastTarget = false;
                _openingReveal = 1f;
            }
            _ctl.Enter(Args, openingShown);
            Refresh();
        }

        void ClearOpeningVeil()
        {
            if (_openingVeil == null) return;
            _openingVeil.gameObject.SetActive(false);
            Destroy(_openingVeil.gameObject);
            _openingVeil = null;
        }

        void ReleaseOpening()
        {
            var pending = _openingLoad;
            _openingLoad = null;
            if (_opening != null) _opening.Hide();
            _opening = null;
            if (pending != null && !pending.isDone)
                pending.completed += _ => UnloadOpeningScene();
            else
                UnloadOpeningScene();
        }

        static void UnloadOpeningScene()
        {
            var scene = SceneManager.GetSceneByPath(TutorialOpening.ScenePath);
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }

        void Refresh()
        {
            if (_balloon == null || _ctl == null) return;
            var b = _ctl.Running ? _ctl.Balloon() : new TutorialBalloon();
            bool showBalloon = b.Visible && !b.IsIntro;
            if (_balloon.gameObject.activeSelf != showBalloon) _balloon.gameObject.SetActive(showBalloon);
            if (_pips.gameObject.activeSelf != showBalloon) _pips.gameObject.SetActive(showBalloon);
            if (_dialog.gameObject.activeSelf != (b.Visible && b.IsIntro)) _dialog.gameObject.SetActive(b.Visible && b.IsIntro);
            if (!b.Visible) { _shownStep = -1; return; }
            RefreshPips(b);
            if (b.IsIntro) { RefreshDialog(); _shownStep = b.Step; return; }

            if (b.Step != _shownStep)
            {
                _shownStep = b.Step;
                _shownBody = null; _shownHint = null; _shownKeys = null; _shownProgress = -1; _shownGoal = -1; _shownDone = false;
                _praiseAge = -1f; _setbackAge = -1f;
                _chip.SetText(F(StepCounterKey, "n", b.Step, "total", b.Total));
                _title.SetText(TK(b.TitleKey));
                _stepAge = 0f;
                _hint.SetText(string.Empty);
                _praise.SetText(string.Empty);
            }
            if (b.BodyKey != _shownBody)
            {
                _shownBody = b.BodyKey;
                _body.SetText(b.BodyKey != null ? TK(b.BodyKey) : string.Empty);
                _body.gameObject.SetActive(b.BodyKey != null);
            }
            if (b.Progress != _shownProgress || b.Goal != _shownGoal)
            {
                bool rose = _shownProgress >= 0 && b.Progress > _shownProgress;
                _shownProgress = b.Progress;
                _shownGoal = b.Goal;
                _goal.SetText(b.GoalKey != null ? TK(b.GoalKey, "n", b.Progress, "max", b.Goal) : string.Empty);
                _goal.gameObject.SetActive(b.GoalKey != null);
                _bar.gameObject.SetActive(b.Goal > 0);
                _bar.Value01 = b.Goal > 0 ? Mathf.Clamp01((float)b.Progress / b.Goal) : 0f;
                if (rose) UiMotion.Pop(_goal, 1.12f, 0.18f);
            }
            if (b.HintKey != _shownHint)
            {
                _shownHint = b.HintKey;
                _hint.SetText(b.HintKey != null ? T(HintMarkKey) + " " + TK(b.HintKey) : string.Empty);
            }
            if (b.Done != _shownDone)
            {
                _shownDone = b.Done;
                _praise.SetText(b.PraiseKey != null ? TK(b.PraiseKey) : string.Empty);
                if (b.Done) UiMotion.Pop(_praise, 1.25f, 0.3f);
            }
            _praise.gameObject.SetActive(b.Done);
            bool showOpen = b.ShowOpenLoadout;
            if (_open.gameObject.activeSelf != showOpen) _open.gameObject.SetActive(showOpen);
            _keys.gameObject.SetActive(!showOpen);
            RefreshKeys(b);

            float praiseSeconds = _plan.PraiseSeconds > 0f ? _plan.PraiseSeconds : 1f;
            bool pf = _praiseAge >= 0f && _praiseAge < praiseSeconds;
            SetFrame(_praiseFrame, pf, pf ? 1f - _praiseAge / praiseSeconds : 0f);
            bool sf = _setbackAge >= 0f && _setbackAge < praiseSeconds && !pf;
            SetFrame(_setbackFrame, sf, sf ? 1f - _setbackAge / praiseSeconds : 0f);
            Animate();
        }

        /// <summary>
        /// 풍선의 움직임(표시만): 새 단계 = 위에서 살짝 내려오며 나타남, 완료 = 살짝 부풀었다 돌아옴, 실수 = 좌우로 짧게 흔들림.
        /// 기준 자리에서 매 프레임 다시 계산한다(움직임이 겹쳐도 자리가 밀리지 않는다).
        /// </summary>
        void Animate()
        {
            const float slideSeconds = 0.18f, popSeconds = 0.25f, shakeSeconds = 0.3f, shakePx = 6f, slidePx = 12f, popScale = 0.03f;
            Vector2 off = Vector2.zero;
            if (_stepAge < slideSeconds) off.y += (1f - UiMotion.EaseOutCubic(_stepAge / slideSeconds)) * slidePx;
            if (_setbackAge >= 0f && _setbackAge < shakeSeconds)
                off.x += Mathf.Sin(_setbackAge * Mathf.PI * 2f * 8f) * shakePx * (1f - _setbackAge / shakeSeconds);
            _balloon.anchoredPosition = _balloonBase + off;
            float sc = 1f;
            if (_praiseAge >= 0f && _praiseAge < popSeconds) sc += Mathf.Sin(_praiseAge / popSeconds * Mathf.PI) * popScale;
            _balloon.localScale = new Vector3(sc, sc, 1f);
        }

        static void SetFrame(UImage anyChild, bool on, float alpha)
        {
            if (anyChild == null) return;
            var node = anyChild.transform.parent.gameObject;
            if (node.activeSelf != on) node.SetActive(on);
            if (!on) return;
            foreach (Transform c in node.transform)
            {
                var img = c.GetComponent<UImage>();
                if (img != null) img.color = UiTheme.WithAlpha(img.color, Mathf.Clamp01(alpha));
            }
        }

        void RefreshPips(TutorialBalloon b)
        {
            for (int i = 0; i < _pipImages.Count; i++)
            {
                int step = i + 1;
                Color c = step < b.Step || (step == b.Step && b.Done) ? UiTheme.Good : step == b.Step ? UiTheme.Chalk : UiTheme.WoodLight;
                if (_pipImages[i].color != c) _pipImages[i].color = c;
            }
        }

        void RefreshDialog()
        {
            var m = _ctl.Model;
            if (m == null || m.IntroLine == _shownLine) return;
            _shownLine = m.IntroLine;
            _dialogLine.SetText(TK(TutorialFlowModel.IntroLineKey(m.IntroLine)));
            string next = UiKeyHint.Compose(Ctx != null ? Ctx.Names : null, new[] { new UiKeyHint(KeyOf(LogicalKey.Fire), NextLineActionKey), new UiKeyHint(Key.Escape, SkipActionKey) });
            _dialogFoot.SetText(F(IntroCounterKey, "n", m.IntroLine, "total", m.IntroLineCount) + "   " + next);
        }

        /// <summary>풍선 글: 키 이름({fire} {art} {stance} {reverse} {up} {left} {down} {right})은 지금 키 배치 그대로 넣는다.</summary>
        string TK(string key, params object[] extra)
        {
            var names = Ctx != null ? Ctx.Names : null;
            if (names == null) return key;
            var args = new List<object>
            {
                "fire", KeyName(LogicalKey.Fire), "art", KeyName(LogicalKey.Skill), "stance", KeyName(LogicalKey.Stance),
                "reverse", KeyName(LogicalKey.Reverse), "up", KeyName(LogicalKey.Up), "left", KeyName(LogicalKey.Left),
                "down", KeyName(LogicalKey.Down), "right", KeyName(LogicalKey.Right)
            };
            if (extra != null) args.AddRange(extra);
            return names.Format(key, args.ToArray());
        }

        string KeyName(LogicalKey k) => UiKeyLabels.Of(Ctx != null ? Ctx.Names : null, KeyOf(k));

        // ---------------------------------------------------------------- 누를 키

        Key KeyOf(LogicalKey k)
        {
            var s = Ctx != null && Ctx.Settings != null ? Ctx.Settings.Current : null;
            int bound = s != null ? KeyLayouts.PrimaryOf(s, k) : 0;
            if (bound > 0) return (Key)bound;
            var table = KeyLayouts.DefaultPrimary;
            return (int)k < table.Count ? (Key)table[(int)k] : Key.None;
        }

        /// <summary>그 단계에서 누를 키(지금 키 배치 그대로). 커맨드 단계의 "뒤"는 지금 바라보는 쪽의 반대 키.</summary>
        List<Key> KeysFor(string id)
        {
            var list = new List<Key>();
            switch (id)
            {
                case TutorialStepIds.Move:
                case TutorialStepIds.Graze:
                    list.Add(KeyOf(LogicalKey.Up)); list.Add(KeyOf(LogicalKey.Left)); list.Add(KeyOf(LogicalKey.Down)); list.Add(KeyOf(LogicalKey.Right));
                    break;
                case TutorialStepIds.Fire:
                case TutorialStepIds.Overheat:
                case TutorialStepIds.Crit:
                case TutorialStepIds.Combo:
                    list.Add(KeyOf(LogicalKey.Fire));
                    break;
                case TutorialStepIds.Art:
                    list.Add(KeyOf(LogicalKey.Skill));
                    break;
                case TutorialStepIds.Stance:
                    list.Add(KeyOf(LogicalKey.Stance));
                    list.Add(KeyOf(LogicalKey.Up)); list.Add(KeyOf(LogicalKey.Left)); list.Add(KeyOf(LogicalKey.Down)); list.Add(KeyOf(LogicalKey.Right));
                    break;
                case TutorialStepIds.Reverse:
                    list.Add(KeyOf(LogicalKey.Reverse));
                    list.Add(KeyOf(LogicalKey.Fire));
                    break;
                case TutorialStepIds.Command:
                    list.Add(KeyOf(LogicalKey.Stance));
                    list.Add(KeyOf(BackKey()));
                    list.Add(KeyOf(LogicalKey.Skill));
                    break;
            }
            return list;
        }

        LogicalKey BackKey()
        {
            var w = _ctl != null ? _ctl.World : null;
            var p = w != null && w.Players != null && w.Players.Length > 0 ? w.Players[0] : null;
            Dir8 facing = p == null ? Dir8.S : p.InStance ? p.LockedFacing : p.Facing;
            if (((int)facing & 1) == 1 && p != null) facing = p.LastPressedCardinal;
            switch (facing)
            {
                case Dir8.N: return LogicalKey.Down;
                case Dir8.S: return LogicalKey.Up;
                case Dir8.E: return LogicalKey.Left;
                default: return LogicalKey.Right;
            }
        }

        void RefreshKeys(TutorialBalloon b)
        {
            var keys = KeysFor(b.StepId);
            string sig = string.Join(",", keys);
            if (sig == _shownKeys) return;
            _shownKeys = sig;
            for (int i = _keys.childCount - 1; i >= 0; i--) Destroy(_keys.GetChild(i).gameObject);
            float x = _keys.sizeDelta.x;
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                x -= KeyCapPx;
                UiKit.KeyCap(Ctx, _keys, keys[i], x, 0f, KeyCapPx);
                x -= 6f;
            }
        }

        static void Destroy(GameObject go) => Object.Destroy(go);
    }
}
