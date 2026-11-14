using UnityEngine;
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

        public override ScreenId Id => ScreenId.Battle;
        public override ScreenLayer Layer => ScreenLayer.Fullscreen;
        /// <summary>뼈대(바탕·위 띠·아래 띠)를 그리지 않는다 — 전장이 보여야 한다.</summary>
        protected override bool HasFrame => false;

        public BattleController Controller => _controller;

        protected override void BuildContent(RectTransform content)
        {
            _controller = new BattleController(Ctx);
            float lh = UiTheme.LineHeight(UiFontId.F48);
            _deathLine = UiKit.Label(Ctx, content, T(DeathLineKey), UiFontId.F48,
                (UiScale.RefWidth - DeathLineWidth) * 0.5f, (UiScale.RefHeight - lh) * 0.5f, DeathLineWidth, lh,
                UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
            _deathLine.gameObject.SetActive(false);
            _controller.DeathLineChanged = ShowDeathLine;
        }

        void ShowDeathLine(bool on)
        {
            if (_deathLine == null) return;
            if (on) _deathLine.SetText(T(DeathLineKey));
            _deathLine.gameObject.SetActive(on);
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
            if (_controller != null) _controller.Tick(dt);
        }
    }
}
