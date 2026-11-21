using System;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Battle
{
    /// <summary>일시정지 메뉴의 항목(08 요약서 7절, 무한 웨이브). 순서가 화면의 위→아래 순서다.</summary>
    public enum PauseItem { Resume = 0, Settings = 1, Controls = 2, GiveUp = 3, ToMenu = 4, Codex = 5 }

    /// <summary>일시정지 메뉴의 구성(순수).</summary>
    public static class PauseMenuModel
    {
        public const string TitleKey = "str.pause.title";
        public const string GiveUpConfirmKey = "str.pause.give_up_confirm";

        static readonly PauseItem[] ItemTable = { PauseItem.Resume, PauseItem.Settings, PauseItem.Controls, PauseItem.Codex, PauseItem.GiveUp, PauseItem.ToMenu };

        /// <summary>무한 웨이브의 버튼 여섯: 계속, 설정, 조작 보기, 조작·도감(배운 것 다시 보기), 판 포기, 메인 메뉴로.</summary>
        public static PauseItem[] Items => (PauseItem[])ItemTable.Clone();

        public static string LabelKey(PauseItem item)
        {
            switch (item)
            {
                case PauseItem.Resume: return "str.pause.resume";
                case PauseItem.Settings: return "str.pause.settings";
                case PauseItem.Controls: return "str.pause.controls";
                case PauseItem.GiveUp: return "str.pause.give_up";
                case PauseItem.ToMenu: return "str.pause.to_menu";
                case PauseItem.Codex: return "str.pause.codex";
            }
            return string.Empty;
        }

        /// <summary>조심 버튼(호박색 테 + 경고 삼각)인가. 08 7절: [판 포기]가 조심 버튼.</summary>
        public static bool IsCaution(PauseItem item) => item == PauseItem.GiveUp;

        /// <summary>확인 창을 거치는가(판 포기, 메인 메뉴로 = 판 포기와 같은 길 D-19).</summary>
        public static bool NeedsConfirm(PauseItem item) => item == PauseItem.GiveUp || item == PauseItem.ToMenu;
    }

    /// <summary>
    /// 일시정지 화면의 동작(엔진 없음). close = 일시정지 화면 자신을 닫는다(화면은 ScreenBase.Close, 테스트는 가짜 Pop).
    /// </summary>
    public sealed class PauseController
    {
        readonly GameContext _ctx;
        readonly Action _close;

        public PauseController(GameContext ctx, Action close)
        {
            _ctx = ctx;
            _close = close;
        }

        IGameSession Session => _ctx != null ? _ctx.Session : null;
        IScreenManager Screens => _ctx != null ? _ctx.Screens : null;

        public void Activate(PauseItem item)
        {
            switch (item)
            {
                case PauseItem.Resume: Resume(); break;
                case PauseItem.Settings: Screens?.Push(ScreenId.Settings, ScreenArgs.From(ScreenId.Pause)); break;
                case PauseItem.Controls: Screens?.Push(ScreenId.ControlsView, ScreenArgs.From(ScreenId.Pause)); break;
                case PauseItem.Codex: Screens?.Push(ScreenId.Codex, ScreenArgs.From(ScreenId.Pause)); break;
                case PauseItem.GiveUp:
                case PauseItem.ToMenu:
                    AskGiveUp(item);
                    break;
            }
        }

        /// <summary>[계속]과 Esc: Paused = false(세션이 Resync 한다) + 닫기.</summary>
        public void Resume()
        {
            var s = Session;
            if (s != null) s.Paused = false;
            _close?.Invoke();
        }

        /// <summary>확인 창(조심 버튼) → 예면 GiveUp 명령 + 일시정지 풀기 + 닫기. 정산 화면은 RunEnded를 본 Battle이 연다.</summary>
        public void AskGiveUp(PauseItem from)
        {
            var m = Screens;
            if (m == null) { GiveUp(); return; }
            m.Confirm(PauseMenuModel.LabelKey(from), PauseMenuModel.GiveUpConfirmKey, true, ok => { if (ok) GiveUp(); });
        }

        public void GiveUp()
        {
            var s = Session;
            if (s != null)
            {
                var w = s.MainWorld;
                if (w != null) w.Enqueue(new RunCommand(RunCommandKind.GiveUp));
                s.Paused = false;
            }
            _close?.Invoke();
        }
    }
}
