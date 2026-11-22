using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Battle
{
    /// <summary>
    /// 판 진행 화면(ScreenId.Battle). 전체 화면이지만 그리는 것이 없다 — 월드와 HUD는 표시 층이 그린다(00 문서 7절).
    /// 바탕·띠 뼈대를 만들지 않고, 사망 문구(str.battle_hud.death_line)만 판이 체력 0으로 끝났을 때 가운데에 보인다.
    /// 규칙은 BattleController·BattleFlowModel(엔진 없음)에 있다.
    /// </summary>
    public sealed class BattleScreen : ScreenBase
    {
        public const string DeathLineKey = "str.battle_hud.death_line";
        const float DeathLineWidth = 1200f;

        BattleController _controller;
        UiLabel _deathLine;
        // 사망 글 페이드인(Juice.json death 절: text_delay·text_fade_seconds·text_scale_from). 쓰러짐 연출(DeathSequenceLayer)과 같은 표.
        float _deathT = -1f, _deathDelay, _deathFade, _deathScaleFrom = 1f;

        /// <summary>시험용: 사망 글이 보이는가와 지금 불투명도·배율.</summary>
        public bool DeathLineShown => _deathLine != null && _deathLine.gameObject.activeSelf;
        public float DeathLineAlpha => _deathLine != null ? _deathLine.Color.a : 0f;
        public float DeathLineScale => _deathLine != null ? _deathLine.transform.localScale.x : 1f;

        public override ScreenId Id => ScreenId.Battle;
        public override ScreenLayer Layer => ScreenLayer.Fullscreen;
        /// <summary>뼈대(바탕·위 띠·아래 띠)를 그리지 않는다 — 전장이 보여야 한다.</summary>
        protected override bool HasFrame => false;

        public BattleController Controller => _controller;

        protected override void BuildContent(RectTransform content)
        {
            _controller = new BattleController(Ctx);
            float lh = UiTheme.LineHeight(UiFontId.F64);
            _deathLine = UiKit.Label(Ctx, content, T(DeathLineKey), UiFontId.F64,
                (UiScale.RefWidth - DeathLineWidth) * 0.5f, (UiScale.RefHeight - lh) * 0.5f, DeathLineWidth, lh,
                UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
            UiKit.DropShadow(_deathLine, 3f);
            // 커지며 나타날 때 가운데를 축으로(Place는 왼쪽 위 축)
            var drt = _deathLine.Rect;
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition += new Vector2(DeathLineWidth * 0.5f, -lh * 0.5f);
            _deathLine.gameObject.SetActive(false);
            _controller.DeathLineChanged = ShowDeathLine;
            ReadDeathTuning();

            // 결투 점수판(결투 판에서만): 위 가운데 "라운드 n · 남은 시간 · 승수", 가운데 큰 글자(카운트다운·라운드 결과).
            float sh = UiTheme.LineHeight(UiFontId.F32);
            _duelBar = UiKit.Label(Ctx, content, string.Empty, UiFontId.F32, (UiScale.RefWidth - 900f) * 0.5f, 120f, 900f, sh,
                UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
            UiKit.DropShadow(_duelBar, 2f);
            _duelBig = UiKit.Label(Ctx, content, string.Empty, UiFontId.F64, (UiScale.RefWidth - 1200f) * 0.5f, 380f, 1200f, lh,
                UiTheme.SelectYellow, TextAnchor.MiddleCenter, false, true);
            UiKit.DropShadow(_duelBig, 3f);
            _duelBar.gameObject.SetActive(false);
            _duelBig.gameObject.SetActive(false);
            _duelBarKey = long.MinValue; // 새 점수판 칸은 첫 프레임에 반드시 글을 쓴다
            _deathCameraFx = null;
        }

        UiLabel _duelBar, _duelBig;
        long _duelBarKey = long.MinValue;
        Wizard.Solo.View.CameraFx _deathCameraFx;
        public const string DuelBarKey = "str.duel.bar", DuelCountKey = "str.duel.countdown", DuelFightKey = "str.duel.fight",
            DuelRoundWinKey = "str.duel.round_win", DuelRoundLoseKey = "str.duel.round_lose", DuelRoundDrawKey = "str.duel.round_draw",
            DuelOvertimeKey = "str.duel.overtime";

        /// <summary>결투 점수판. 결투 판이 아니면 숨긴다. 시간·카운트다운 값은 DuelState와 판 상태에서 읽는다(Duel.json의 기본값과 같은 90초·3초).</summary>
        void RenderDuel()
        {
            var w = Ctx != null && Ctx.Session != null ? Ctx.Session.MainWorld : null;
            var d = w != null && w.Run != null ? w.Run.Duel : null;
            bool on = d != null;
            if (_duelBar.gameObject.activeSelf != on) _duelBar.gameObject.SetActive(on);
            if (!on) { if (_duelBig.gameObject.activeSelf) _duelBig.gameObject.SetActive(false); return; }
            int me = w.ViewSlot == 1 ? 1 : 0, them = 1 - me;
            int roundTicks = d.Overtime ? SimConst.Ticks(30f) : SimConst.Ticks(90f);
            int left = w.Run.Phase == RunPhase.DuelFight ? Mathf.Max(0, roundTicks - (w.Tick - d.RoundStartTick)) : roundTicks;
            int secs = (left + SimConst.TicksPerSecond - 1) / SimConst.TicksPerSecond;
            // 점수판 글은 초·라운드·승수가 바뀔 때만 다시 만든다(프레임마다 문자열 조립·할당을 하지 않게)
            long barKey = ((long)d.Round << 40) ^ ((long)secs << 20) ^ ((long)(d.Wins[me] & 0xFF) << 12) ^ ((long)(d.Wins[them] & 0xFF) << 4) ^ (d.Overtime ? 1L : 0L);
            if (barKey != _duelBarKey)
            {
                _duelBarKey = barKey;
                _duelBar.SetText(F(DuelBarKey, "round", d.Round, "time", (secs / 60) + ":" + (secs % 60).ToString("00"),
                    "me", d.Wins[me], "them", d.Wins[them]) + (d.Overtime ? "  " + T(DuelOvertimeKey) : string.Empty));
            }
            string big = null;
            int since = w.Tick - w.Run.PhaseStartTick;
            if (w.Run.Phase == RunPhase.DuelCountdown)
                big = F(DuelCountKey, "n", Mathf.Max(1, 3 - since / SimConst.TicksPerSecond));
            else if (w.Run.Phase == RunPhase.DuelFight && since < SimConst.TicksPerSecond && !d.Overtime)
                big = T(DuelFightKey);
            else if (w.Run.Phase == RunPhase.DuelRoundEnd || w.Run.Phase == RunPhase.RunEnded)
                big = d.LastRoundWinner < 0 ? T(DuelRoundDrawKey) : T(d.LastRoundWinner == me ? DuelRoundWinKey : DuelRoundLoseKey);
            bool show = big != null;
            if (_duelBig.gameObject.activeSelf != show) _duelBig.gameObject.SetActive(show);
            if (show) _duelBig.SetText(big);
        }

        void ReadDeathTuning()
        {
            Newtonsoft.Json.Linq.JObject root = null;
            try { root = Ctx != null && Ctx.Content != null ? Ctx.Content.SoloExtra(Wizard.Solo.View.Effects.JuiceLayer.JuiceName) : null; }
            catch (System.Exception) { root = null; }
            var j = new Wizard.Solo.View.Effects.EffectsTuning(root);
            string sec = Wizard.Solo.View.Effects.DeathSequenceLayer.Section;
            _deathDelay = Mathf.Max(0f, j.F(sec, "text_delay"));
            _deathFade = Mathf.Max(0f, j.F(sec, "text_fade_seconds"));
            float from = j.F(sec, "text_scale_from");
            _deathScaleFrom = from > 0f ? from : 1f;
            if (_deathLine != null)
            {
                var rt = _deathLine.Rect;
                var p = rt.anchoredPosition;
                rt.anchoredPosition = new Vector2(p.x, p.y - j.F(sec, "text_offset_y_px"));
            }
        }

        void ShowDeathLine(bool on)
        {
            if (_deathLine == null) return;
            if (on)
            {
                _deathLine.SetText(T(DeathLineKey));
                PlaceDeathLineAwayFromSubject();
            }
            _deathLine.gameObject.SetActive(on);
            _deathT = on ? 0f : -1f;
            ApplyDeathLine(0f);
        }

        /// <summary>쓰러진 캐릭터가 화면 위쪽 반에 있으면 글을 아래쪽 1/3 높이에, 아래쪽 반이면 위쪽 1/3 높이에 둔다(QA: 위쪽 벽 근처에서 쓰러지면 글이 캐릭터를 덮었다).</summary>
        void PlaceDeathLineAwayFromSubject()
        {
            // 글이 보이기 전까지 프레임마다 불리므로 장면 찾기는 한 번만(카메라가 바뀌어 부서졌으면 다시 찾는다)
            if (_deathCameraFx == null) _deathCameraFx = Object.FindFirstObjectByType<Wizard.Solo.View.CameraFx>();
            var cf = _deathCameraFx;
            float vy = cf != null ? cf.SubjectViewportY : -1f;
            if (vy < 0f) return;   // 모르면 기본 자리(가운데에서 조금 위)
            float centerFromTop = vy > 0.5f ? UiScale.RefHeight * 0.68f : UiScale.RefHeight * 0.30f;
            var rt = _deathLine.Rect;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -centerFromTop);
        }

        /// <summary>쓰러짐 연출 뒤 글이 크게 나타난다: text_delay초 기다렸다가 text_fade_seconds에 걸쳐 불투명 + text_scale_from배 → 1배.
        /// 연출 감도 최소면 커지기 없이 페이드만. 실제 시간으로 센다(표시 층의 슬로모션과 무관).</summary>
        void ApplyDeathLine(float dt)
        {
            if (_deathLine == null || _deathT < 0f) return;
            _deathT += dt;
            if (_deathT <= _deathDelay) PlaceDeathLineAwayFromSubject();   // 글이 보이기 전까지는 쓰러짐 층이 잰 자리를 따라간다
            float k = _deathFade > 0f ? Mathf.Clamp01((_deathT - _deathDelay) / _deathFade) : (_deathT >= _deathDelay ? 1f : 0f);
            float e = 1f - (1f - k) * (1f - k) * (1f - k);
            var c = _deathLine.Color;
            c.a = e;
            _deathLine.Color = c;
            bool motion = Ctx == null || Ctx.Settings == null || Ctx.Settings.Current == null
                || Wizard.Solo.Meta.SettingsOptions.EffectsLevel(Ctx.Settings.Current) < 2;
            float sc = motion ? Mathf.Lerp(_deathScaleFrom, 1f, e) : 1f;
            _deathLine.transform.localScale = new Vector3(sc, sc, 1f);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            if (_controller == null) _controller = new BattleController(Ctx);
            _controller.Enter(Args);
        }

        public override void OnExit()
        {
            if (_controller != null) _controller.Exit();
            ShowDeathLine(false);
            base.OnExit();
        }

        /// <summary>Esc = 일시정지(00 문서 4.1절). 늘 소비한다 — 관리자가 Battle을 Pop 하지 않게.</summary>
        public override bool OnBack()
        {
            if (_controller != null) _controller.RequestPause();
            return true;
        }

        /// <summary>전투 중 메뉴 키(J/K/방향)는 전투 입력과 같은 물리 키다. 화면은 아무것도 하지 않고 소비한다.</summary>
        public override bool OnMenuKey(MenuKey key) => true;

        public override void Tick(float dt)
        {
            ApplyDeathLine(dt);
            RenderDuel();
            if (_controller != null) _controller.Tick(dt);
        }
    }
}
