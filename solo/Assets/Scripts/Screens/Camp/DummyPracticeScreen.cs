using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>
    /// 허수아비 연습(ScreenId.DummyPractice, 08 요약서 8.5절). 메인 메뉴 "연습"과 야영 "조립 시험" 둘 다 이 화면(TV-082).
    /// 세션은 PracticeSession이 다룬다(MainMenu = StartRun/EndRun, Camp = PushSubRun/PopSubRun). 전장 위 표시만 그린다:
    /// 비기 목록·목표 커맨드·입력 간격 막대·저스트 게이지·실패 원인·허수아비 수치·연습 옵션 셋·다시 하기.
    /// 이 화면이 전투 입력을 받는 동안 J·K·방향은 전투 키다 → 메뉴 키로 쓰지 않는다(Q/E = 캐릭터 바꾸기만, Esc = 나가기).
    /// 결투 입력 지연 체험과 [봇 상대 결투 연습]은 넣지 않는다(D-51).
    /// </summary>
    public sealed class DummyPracticeScreen : ScreenBase
    {
        // 전장 위 표시 자리(08 8.5절 표, 화면 좌표). 화면 배치 상수.
        const float ArenaLeft = 192f, ArenaTop = 104f;
        const float GaugeDx = -96f, GaugeDy = 112f, GaugeW = 192f, GaugeH = 24f;
        const float GapAreaX = 560f, GapAreaY = 176f, GapAreaW = 800f, GapAreaH = 48f;
        /// <summary>최근 입력 최대 5개(Arts.GetRecentInputs) → 막대 칸 수. 자료구조 크기.</summary>
        const int MaxRecent = 5;
        /// <summary>흰 별이 떠 있는 길이(초). 표시만.</summary>
        const float StarSeconds = 0.5f;

        public override ScreenId Id => ScreenId.DummyPractice;
        protected override bool HasFrame => false;

        PracticeSession _session;
        SimWorld _world;
        readonly PracticeStats _stats = new PracticeStats();
        readonly List<RecentInput> _recent = new List<RecentInput>();
        readonly List<GapBar> _bars = new List<GapBar>();
        readonly List<UiToggle> _options = new List<UiToggle>();
        readonly List<UiButton> _artButtons = new List<UiButton>();
        string _targetArt;
        List<CmdDir> _targetPath = new List<CmdDir>();
        PracticeFailLine _fail;
        float _starLeft;
        long _lastDirU = -1, _justWindowU;
        Action<SimWorld, SimEvent> _onEvent;
        int _syncHold;
        /// <summary>옵션 명령을 보낸 뒤 세계 값과 맞추기를 미루는 화면 프레임 수(명령 적용 = 다음 틱).</summary>
        const int SyncHoldFrames = 2 + 2;

        UiLabel _error, _title, _target, _failLabel, _dps, _recentDmg, _heatRatio, _perCrystal, _charLabel, _star;
        RectTransform _artList, _targetRoot, _gapRoot, _gauge, _gaugeFill;

        protected override void BuildContent(RectTransform content)
        {
            _title = UiKit.LabelKey(Ctx, content, "str.dummy_practice.title", UiFontId.F24, 16f, 64f, 320f, 32f, UiTheme.Chalk, TextAnchor.MiddleLeft);
            _artList = UiKit.Node(content, "Arts", 16f, 104f, 320f, 760f);
            _targetRoot = UiKit.Node(content, "Target", 560f, 104f, 800f, 64f);
            _gapRoot = UiKit.Node(content, "Gaps", GapAreaX, GapAreaY, GapAreaW, GapAreaH);
            _gauge = UiKit.Node(content, "JustGauge", 0f, 0f, GaugeW, GaugeH);
            UiKit.Rect(_gauge, 0f, 0f, GaugeW, GaugeH, UiTheme.WithAlpha(UiTheme.Ink, 0.7f));
            _gaugeFill = UiKit.Rect(_gauge, 2f, 2f, 0f, GaugeH - 4f, UiTheme.Chalk).rectTransform;
            _star = UiKit.Label(Ctx, _gauge, T("str.dummy_practice.just_star"), UiFontId.F24, GaugeW + 4f, -4f, 32f, 32f, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
            _failLabel = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 560f, 904f, 800f, 40f, UiTheme.Amber, TextAnchor.MiddleCenter, false, true);

            UiKit.Panel(Ctx, content, UiPanelKind.Board, 1584f, 104f, 320f, 360f);
            UiKit.LabelKey(Ctx, content, "str.dummy_practice.stats", UiFontId.F16, 1600f, 112f, 288f, 24f, UiTheme.SlateBlue);
            _dps = StatRow(content, "str.dummy_practice.dps", 148f);
            _recentDmg = StatRow(content, "str.dummy_practice.recent_damage", 220f);
            _heatRatio = StatRow(content, "str.dummy_practice.heat_ratio", 292f);
            _perCrystal = StatRow(content, "str.dummy_practice.damage_per_crystal", 364f);

            UiKit.Panel(Ctx, content, UiPanelKind.Board, 1584f, 480f, 320f, 360f);
            UiKit.LabelKey(Ctx, content, "str.dummy_practice.options", UiFontId.F16, 1600f, 488f, 288f, 24f, UiTheme.SlateBlue);
            for (int i = 0; i < PracticeModel.Options.Length; i++)
            {
                var opt = PracticeModel.Options[i];
                var t = UiKit.Toggle(Ctx, content, T(PracticeModel.OptionKey(opt)), 1600f, 524f + i * 56f, 288f, 48f, false, v => SetOption(opt, v), Focus);
                _options.Add(t);
            }
            UiKit.Button(Ctx, content, T("str.dummy_practice.reset"), 1600f, 524f + 3 * 56f + 8f, 288f, 48f, OnReset, UiButtonStyle.Medium, Focus);
            _charLabel = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, 1600f, 524f + 4 * 56f + 16f, 288f, 24f, UiTheme.SlateBlue);
            _error = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 360f, 480f, 1200f, 40f, UiTheme.Amber, TextAnchor.MiddleCenter, true, true);
        }

        UiLabel StatRow(RectTransform content, string key, float y)
        {
            UiKit.LabelKey(Ctx, content, key, UiFontId.F16, 1600f, y, 288f, 24f, UiTheme.SlateBlue);
            return UiKit.Label(Ctx, content, string.Empty, UiFontId.F32, 1600f, y + 24f, 288f, 40f);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            var entry = Args.Get(ScreenArgKeys.PracticeEntry, PracticeEntry.MainMenu);
            _session = new PracticeSession(Ctx);
            _session.Enter(entry, Args.Get(ScreenArgKeys.Character, 0), NewSeed());
            AttachWorld();
            if (Ctx.Session != null)
            {
                _onEvent = OnSimEvent;
                Ctx.Session.SimEventRaised += _onEvent;
            }
        }

        public override void OnExit()
        {
            if (Ctx.Session != null && _onEvent != null) Ctx.Session.SimEventRaised -= _onEvent;
            _onEvent = null;
            if (_session != null) _session.Exit();
            _session = null;
            _world = null;
            base.OnExit();
        }

        ulong NewSeed()
        {
            long t = Ctx != null && Ctx.Clock != null ? Ctx.Clock.UtcNow.Ticks : 1L;
            return (ulong)t;
        }

        void AttachWorld()
        {
            _world = _session != null && _session.Open && Ctx.Session != null ? Ctx.Session.World : null;
            _stats.Clear();
            _fail = default;
            _starLeft = 0f;
            _error.SetText(_session != null && _session.ErrorKey != null ? T(_session.ErrorKey) : string.Empty);
            _charLabel.SetText(_session != null && _session.Entry == PracticeEntry.MainMenu
                ? T(Ids.Char.Of(_session.Character)) + T("str.ui.hint.sep") + T("str.camp.hint.character") : string.Empty);
            var p = PracticeSession.FirstActive(_world);
            for (int i = 0; i < _options.Count; i++)
                _options[i].Value = _world != null && PracticeModel.OptionValue(_world.Run, PracticeModel.Options[i]);
            BuildArtList(p);
            SelectTarget(_targetArt);
        }

        void BuildArtList(PlayerState p)
        {
            foreach (var b in _artButtons) Focus.Remove(b);
            _artButtons.Clear();
            CampViews.Clear(_artList);
            if (p == null) return;
            var arts = PracticeModel.ArtList(Ctx.Content, p.CharId, p.UnlockedArts);
            const float Row = 48f;
            for (int i = 0; i < arts.Count && (i + 2) * Row <= 760f; i++)
            {
                string art = arts[i];
                var b = UiKit.Button(Ctx, _artList, T(art), 0f, i * Row, 320f, Row - 4f, () => SelectTarget(art), UiButtonStyle.Plain, Focus, null, UiFontId.F16);
                _artButtons.Add(b);
            }
            UiKit.Button(Ctx, _artList, T("str.dummy_practice.view_trie"), 0f, 760f - Row, 320f, Row - 4f, OnViewTrie, UiButtonStyle.Medium, Focus, null, UiFontId.F16);
            if (_targetArt == null || !arts.Contains(_targetArt)) _targetArt = arts.Count > 0 ? arts[0] : null;
        }

        void SelectTarget(string art)
        {
            _targetArt = art;
            _targetPath = PracticeModel.TargetPath(Ctx.Content, art);
            for (int i = 0; i < _artButtons.Count; i++) _artButtons[i].On = _artButtons[i].Label != null && art != null && _artButtons[i].Label.FullText == T(art);
            DrawTarget(0);
        }

        void DrawTarget(int progress)
        {
            CampViews.Clear(_targetRoot);
            UiKit.LabelKey(Ctx, _targetRoot, "str.dummy_practice.target_command", UiFontId.F16, 0f, 0f, 200f, 24f, UiTheme.SlateBlue);
            if (_targetArt == null) return;
            UiKit.Label(Ctx, _targetRoot, T(_targetArt), UiFontId.F16, 200f, 0f, 600f, 24f);
            float x = 0f;
            for (int i = 0; i <= _targetPath.Count; i++)
            {
                string key = i < _targetPath.Count ? ArtTrieModel.GlyphKey(_targetPath[i]) : ArtTrieModel.KGlyphKey;
                bool lit = i < progress;
                UiKit.LabelKey(Ctx, _targetRoot, key, UiFontId.F24, x, 28f, 48f, 32f, lit ? UiTheme.SelectYellow : UiTheme.SlateBlue);
                x += 56f;
            }
        }

        void OnViewTrie()
        {
            int c = _session != null ? _session.Character : 0;
            Screens?.Push(ScreenId.ArtTrie, ScreenArgs.From(ScreenId.DummyPractice).With(ScreenArgKeys.Character, c));
        }

        void SetOption(PracticeOption opt, bool on)
        {
            if (_world == null) return;
            _world.Enqueue(PracticeModel.OptionCommand(opt, on));
            _syncHold = SyncHoldFrames;   // 명령은 다음 틱에 적용된다 — 그동안 단추를 세계 값으로 되돌리지 않는다.
        }

        void OnReset()
        {
            if (_world == null) return;
            _world.Enqueue(PracticeModel.ResetCommand());
            _stats.Clear();
            _fail = default;
        }

        void OnSimEvent(SimWorld w, SimEvent e)
        {
            if (w == null || w != _world) return;
            switch (e.Type)
            {
                case SimEventType.PracticeReport:
                    _stats.Apply(e);
                    break;
                case SimEventType.ArtFizzled:
                    _fail = PracticeModel.FromFizzle(e);
                    if (!_fail.IsEmpty) PlayUi("ui.dummy.late_fail");
                    break;
                case SimEventType.ArtFired:
                    if (e.B == 1)
                    {
                        // 섬 기술: K가 창 안 → 흰 별.
                        _starLeft = StarSeconds;
                        _fail = default;
                        PlayUi("ui.dummy.just_star");
                    }
                    else if (MissedJust(w, e))
                    {
                        long late = PracticeModel.LateKUnits(SimConst.TickStartU(e.Tick), _lastDirU, _justWindowU);
                        _fail = PracticeModel.LateK(late);
                        if (!_fail.IsEmpty) PlayUi("ui.dummy.late_fail");
                    }
                    else _fail = default;
                    break;
            }
        }

        /// <summary>섬 기술이 붙은 마디의 일반 기술이 나갔고 그 섬 기술이 해금돼 있다(= 섬을 놓쳤다).</summary>
        bool MissedJust(SimWorld w, in SimEvent e)
        {
            var p = PracticeSession.FirstActive(w);
            if (p == null || e.Id == null || Ctx.Content == null) return false;
            for (int i = 0; i < Ctx.Content.Arts.Count; i++)
            {
                var a = Ctx.Content.Arts[i];
                if (!a.Just || !p.UnlockedArts.Contains(a.Id)) continue;
                if (SamePaths(a, Ctx.Content.Art(e.Id))) return true;
            }
            return false;
        }

        static bool SamePaths(Wizard.Solo.Data.ArtDef just, Wizard.Solo.Data.ArtDef k)
        {
            if (just == null || k == null || k.Just || just.Paths.Count == 0 || k.Paths.Count == 0) return false;
            var a = just.Paths[0]; var b = k.Paths[0];
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public override void Tick(float dt)
        {
            var w = _world;
            var p = PracticeSession.FirstActive(w);
            if (w != null && p != null)
            {
                var pv = w.Mods.Arts.Preview(w, p, w.NowU);
                if (pv.LastDirTimeU >= 0) _lastDirU = pv.LastDirTimeU;
                if (pv.JustWindowU > 0) _justWindowU = pv.JustWindowU;
                _recent.Clear();
                w.Mods.Arts.GetRecentInputs(p, _recent);
                DrawGaps();
                DrawTarget(PracticeModel.TargetProgress(_targetPath, _recent));
                float fill = PracticeModel.JustGauge(w.NowU, pv.LastDirTimeU, pv.JustWindowU);
                _gaugeFill.sizeDelta = new Vector2((GaugeW - 4f) * fill, _gaugeFill.sizeDelta.y);
                UiKit.Place(_gauge, ArenaLeft + p.Pos.x * SimConst.PixelsPerUnit + GaugeDx, ArenaTop + p.Pos.y * SimConst.PixelsPerUnit + GaugeDy, GaugeW, GaugeH);
                _gauge.gameObject.SetActive(true);
                if (_syncHold > 0) _syncHold--;
                else for (int i = 0; i < _options.Count; i++)
                {
                    bool v = PracticeModel.OptionValue(w.Run, PracticeModel.Options[i]);
                    if (_options[i].Value != v) _options[i].Value = v;
                }
            }
            else _gauge.gameObject.SetActive(false);

            if (_starLeft > 0f) _starLeft -= dt;
            _star.gameObject.SetActive(_starLeft > 0f);
            _failLabel.SetText(_fail.IsEmpty ? string.Empty : F(_fail.Key, "n", _fail.Index, "sec", _fail.Seconds));
            _dps.SetText(CampFormat.OneDecimal(_stats.Dps));
            _recentDmg.SetText(N((long)Math.Round(_stats.RecentDamage)));
            _heatRatio.SetText(F("str.format.percent", "n", (long)Math.Round(_stats.HeatRatio * 100f)));
            _perCrystal.SetText(CampFormat.OneDecimal(_stats.DamagePerCrystal));
        }

        void DrawGaps()
        {
            CampViews.Clear(_gapRoot);
            PracticeModel.GapBars(_recent, _bars);
            float slot = GapAreaW / MaxRecent;
            for (int i = 0; i < _bars.Count && i < MaxRecent; i++)
            {
                var b = _bars[i];
                float x = i * slot;
                float barW = slot * 0.6f;
                UiKit.Rect(_gapRoot, x, 20f, barW, 8f, UiTheme.WithAlpha(UiTheme.Chalk, 0.6f));
                float mark = Mathf.Min(b.MarkRatio, 1.5f) * barW;
                UiKit.Rect(_gapRoot, x + mark - 2f, 12f, 4f, 24f, b.Over ? UiTheme.Amber : UiTheme.Good);
                UiKit.LabelKey(Ctx, _gapRoot, ArtTrieModel.GlyphKey(b.Dir), UiFontId.F12, x, 0f, 24f, 12f, UiTheme.Chalk);
            }
        }

        public override bool OnBack()
        {
            // Esc = 나가기(일시정지 화면을 쓰지 않는다, 00 4.1절).
            Close();
            return true;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if ((key == MenuKey.PrevTab || key == MenuKey.NextTab) && _session != null && _session.Entry == PracticeEntry.MainMenu)
            {
                // 화면 안에서 Q/E로 캐릭터를 바꾸면 EndRun 뒤 다시 만든다(D-56).
                _session.SwitchCharacter(key == MenuKey.PrevTab ? -1 : 1, NewSeed());
                AttachWorld();
                return true;
            }
            // J·K·방향은 전투 키다(전투 입력의 문이 열려 있다). 메뉴로 쓰지 않는다 — 단추는 마우스로.
            return true;
        }
    }
}
