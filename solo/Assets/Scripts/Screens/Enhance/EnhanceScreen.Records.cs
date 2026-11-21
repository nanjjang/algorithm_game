using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Enhance
{
    // 강화 화면의 기록 칸(오른쪽 카드 가운데: 이 지팡이의 시도·성공·유지·하락·대성공·변이, 지금 도전 단계의 표기 평균 대비 실제,
    // "다섯 지팡이의 확률·비용·실패 규칙은 같다" 고정 한 줄, 단계별 표 툴팁)과 경고 줄(왼쪽 카드: 다음 시도 비용 경고, 하락 뒤 회복 안내).
    // 의식 중에는 글을 바꾸지 않는다(결과가 미리 새지 않게 — 결과는 이미 저장돼 있다). 규칙은 EnhanceRecordModel.
    public sealed partial class EnhanceScreen
    {
        RectTransform _recordRoot, _warnRoot;
        UiLabel _recordTitle, _recordSummary, _recordStage, _recordRules, _warnFunds, _warnDrop;
        UnityEngine.UI.Image _warnFundsBar, _warnDropBar;

        void BuildRecordPanel(RectTransform content)
        {
            // 오른쪽 카드(1440,80)의 힌트 상자(…468)와 기댓값 표(616…) 사이.
            _recordRoot = UiKit.Node(content, "EnhanceRecord", SideW + MidW + 40f, PanelTop + 476f, 400f, 132f);
            var hit = UiKit.Rect(_recordRoot, 0f, 0f, 400f, 132f, EnhanceFxStyle.WithA(Color.black, 0.3f), true, "RecordFill");
            UiKit.AttachTooltip(hit.gameObject, RecordTooltip);
            _recordTitle = UiKit.Label(Ctx, _recordRoot, T(EnhanceRecordModel.TitleKey), UiFontId.F16, 8f, 4f, 384f, 0f, UiTheme.Gold);
            _recordSummary = UiKit.Label(Ctx, _recordRoot, string.Empty, UiFontId.F12, 8f, 32f, 384f, 0f, UiTheme.Chalk);
            _recordStage = UiKit.Label(Ctx, _recordRoot, string.Empty, UiFontId.F12, 8f, 56f, 384f, 0f, UiTheme.Chalk);
            _recordRules = UiKit.Label(Ctx, _recordRoot, T(EnhanceRecordModel.SameRulesKey), UiFontId.F12, 8f, 84f, 384f, 40f, UiTheme.SlateBlue,
                TextAnchor.UpperLeft, true);

            // 왼쪽 카드(0,80)의 버튼 줄(…468)과 초상(572…) 사이.
            _warnRoot = UiKit.Node(content, "EnhanceWarnings", 40f, PanelTop + 476f, 400f, 92f);
            _warnFundsBar = UiKit.Rect(_warnRoot, 0f, 0f, 4f, 40f, UiTheme.Amber, false, "FundsBar");
            _warnFunds = UiKit.Label(Ctx, _warnRoot, string.Empty, UiFontId.F12, 12f, 0f, 388f, 40f, UiTheme.Amber, TextAnchor.MiddleLeft, true);
            _warnDropBar = UiKit.Rect(_warnRoot, 0f, 46f, 4f, 44f, UiTheme.Good, false, "DropBar");
            _warnDrop = UiKit.Label(Ctx, _warnRoot, string.Empty, UiFontId.F12, 12f, 46f, 388f, 44f, UiTheme.Chalk, TextAnchor.MiddleLeft, true);
        }

        void RefreshRecordPanel()
        {
            if (_recordRoot == null) return;
            // 의식·변이 결정 중에는 그대로 둔다(결과가 미리 보이지 않게).
            if (_model.Busy || _model.Phase == EnhancePhase.MutationChoice) return;
            var names = Ctx != null ? Ctx.Names : null;
            bool has = _wand != null;
            _recordRoot.gameObject.SetActive(has);
            _warnRoot.gameObject.SetActive(has);
            if (!has) return;

            _recordSummary.SetText(EnhanceRecordModel.SummaryText(names, _wand.EnhanceStats));
            int target = _wand.Stage + 1;
            _recordStage.SetText(_wand.Stage >= _cfg.MaxStage ? string.Empty : EnhanceRecordModel.StageText(names, _wand.EnhanceStats, target));

            var m = _prob;
            FundsWarning fw = m == null ? FundsWarning.None
                : EnhanceRecordModel.Funds(Ctx.Profile.Currencies.Gold, m.StoneId != null ? Qty(m.StoneId) : 0, m.CostGold, m.StoneCount, m.AtMax);
            string fk = EnhanceRecordModel.FundsKey(fw);
            bool showFunds = fk != null;
            _warnFunds.gameObject.SetActive(showFunds);
            _warnFundsBar.gameObject.SetActive(showFunds);
            if (showFunds)
                _warnFunds.SetText(F(fk, "gold", N(Ctx.Profile.Currencies.Gold), "cost", N(m.CostGold),
                    "stone", m.StoneId != null ? T(StoneShortKey(m.StoneId)) : string.Empty, "have", m.StoneId != null ? Qty(m.StoneId) : 0, "need", m.StoneCount));

            var last = _model.Shown;
            bool settled = _model.Phase == EnhancePhase.ResultCard || _model.Phase == EnhancePhase.Idle;
            bool drop = EnhanceRecordModel.ShowAfterDrop(last, settled) && m != null && !m.AtMax;
            _warnDrop.gameObject.SetActive(drop);
            _warnDropBar.gameObject.SetActive(drop);
            if (drop)
            {
                string key = m.AuraPct > 0 ? EnhanceRecordModel.AfterDropAuraKey : EnhanceRecordModel.AfterDropKey;
                _warnDrop.SetText(F(key, "floor", EnhanceNameKeys.Stage(names, m.FloorStage), "pct", m.TotalPct, "aura", m.AuraPct));
            }
        }

        string RecordTooltip() =>
            _wand == null ? string.Empty : EnhanceRecordModel.TableText(Ctx != null ? Ctx.Names : null, _wand.EnhanceStats, _cfg.MaxStage);
    }
}
