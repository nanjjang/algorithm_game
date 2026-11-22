using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.UI
{
    /// <summary>솔로에 들어가는 화면(08 요약서 0.1절). 번호는 더하기만 한다.</summary>
    public enum ScreenId
    {
        None = 0,
        FirstRunPrompt, Title, ProfileSelect, OfflineGuide,
        MainMenu, ExpeditionPrep, Records, TitlePicker, Settings, KeyRebind, Wardrobe, Shop,
        Loading, Battle, LootPick, Camp, ArtTrie, DummyPractice, Pause, ControlsView, Settlement,
        Enhance, WandPicker, WandReleaseConfirm, MutationRemove, SlotEngrave, EnhanceTutorial,
        ConfirmDialog, Tooltip, SaveRestored, ShopBuyConfirm,
        Tutorial,
        /// <summary>한 줄 알림(IScreenManager.Toast가 직접 올린다. Register 하지 않는다).</summary>
        Toast,
        /// <summary>조작·도감(일시정지 메뉴): 배운 것 다시 보기 — 조작, 과열, 스침, 콤보·결정, 비기, 블록, 변이, 변덕, 약점·저항.</summary>
        Codex,
        /// <summary>협동 방(11-online.md): 방 만들기·동료 목록·시작, 판이 끝나면 결과.</summary>
        CoopLobby,
        /// <summary>광장(좀비고식): 걸어 다니며 말풍선으로 이야기하고, 여기서 방을 만들거나 코드로 들어간다.</summary>
        Plaza,
        /// <summary>내 온라인 기록: 서버 프로필, 서버 지팡이 강화, 결투 순위표.</summary>
        OnlineProfile,
        /// <summary>서버 지팡이 조립(인자 Character 1~5).</summary>
        OnlineAssembly,
        /// <summary>장터(올라온 것 사기, 올리기·내리기).</summary>
        OnlineMarket,
        /// <summary>친구(신청·받기·목록).</summary>
        OnlineFriends,
        /// <summary>온라인 로그인·가입(이메일, Google). 로그인하면 닫히고 부른 화면으로 돌아간다.</summary>
        OnlineLogin,
        /// <summary>시즌(11-online.md 6.14절): 시즌·리그·후계 점수, 자동 매치 대기, 관전, 순위표, 우편함, 차단·신고.</summary>
        OnlineSeason
    }

    public enum ScreenLayer { Fullscreen = 0, Panel = 1, HudOverlay = 2, Modal = 3, Toast = 4 }

    /// <summary>메뉴 공통 키(08 요약서 1.2절). 방향은 방향키와 이동 키 배치 둘 다 받는다.</summary>
    public enum MenuKey { Up, Down, Left, Right, Confirm, Cancel, Back, PrevTab, NextTab }

    /// <summary>야영 화면(ScreenId.Camp)의 모드. 08 요약서 8.2절, 03 요약서 13.1절.</summary>
    public enum CampMode
    {
        /// <summary>판 중 야영: 조립(보관함) + 비기 탭, 회복, 출발. 조작은 RunCommand.</summary>
        Camp = 0,
        /// <summary>메인 메뉴 "조립과 서고": 조립 + 서고 탭, 지팡이는 Q/E로 바꾼다. 저장은 IWandService.SetLoadout.</summary>
        Library = 1,
        /// <summary>원정 준비의 [조립 바꾸기]: 조립 탭만, 인자의 지팡이 하나(제목 str.camp.title.loadout_edit). 저장은 SetLoadout.</summary>
        LoadoutEdit = 2,
        /// <summary>튜토리얼 5단계: 조립 탭만(회복·출발·비기·조립 시험 없음). 조작은 RunCommand(연습 방의 보관함).</summary>
        Tutorial = 3
    }

    /// <summary>허수아비 연습(ScreenId.DummyPractice)의 진입점. MainMenu = 세션 StartRun / Camp = 세션 PushSubRun(본판은 멈춘다).</summary>
    public enum PracticeEntry { MainMenu = 0, Camp = 1 }

    /// <summary>
    /// 화면 인자의 열쇠(ScreenArgs.Values). 보내는 쪽과 받는 쪽은 이 상수와 적힌 형만 쓴다(문자열을 직접 쓰지 않는다).
    /// 화면별로 받는 열쇠는 00-architecture.md 8.1절 표가 정본이다. 형이 다르면 Get이 fallback을 돌려주므로 형을 꼭 맞춘다
    /// (character는 int 1~5, wandId는 long).
    /// </summary>
    public static class ScreenArgKeys
    {
        /// <summary>Wizard.Solo.Sim.RunConfig — Battle(시작할 판), Tutorial(튜토리얼 뒤에 시작할 진짜 판).</summary>
        public const string RunConfig = "run_config";
        /// <summary>Wizard.Solo.Sim.RunSummary — Settlement.</summary>
        public const string RunSummary = "run_summary";
        /// <summary>CampMode — Camp.</summary>
        public const string CampMode = "camp_mode";
        /// <summary>PracticeEntry — DummyPractice.</summary>
        public const string PracticeEntry = "practice_entry";
        /// <summary>long — Camp(Library·LoadoutEdit), Enhance, MutationRemove, SlotEngrave.</summary>
        public const string WandId = "wand_id";
        /// <summary>List&lt;long&gt; — WandReleaseConfirm.</summary>
        public const string WandIds = "wand_ids";
        /// <summary>int 1~5 — WandPicker, Camp(Library), DummyPractice, ShopBuyConfirm(지팡이 상자).</summary>
        public const string Character = "character";
        /// <summary>ScreenId — Esc로 나갈 때 돌아갈 화면(WandPicker·Enhance가 정산에서 열렸으면 MainMenu). 없으면 Pop.</summary>
        public const string ReturnTo = "return_to";
        /// <summary>ScreenId, ScreenArgs — Loading: 준비가 끝나면 자신을 NextScreen(NextArgs)으로 Replace 한다.</summary>
        public const string NextScreen = "next_screen", NextArgs = "next_args";
        /// <summary>string, int — ShopBuyConfirm.</summary>
        public const string Sku = "sku", Qty = "qty";
        /// <summary>string — MutationRemove.</summary>
        public const string MutationId = "mutation_id";
        /// <summary>bool — ArtTrie: 해금 차례 고르기로 열렸다(판 중 야영).</summary>
        public const string ArtPick = "art_pick";
        /// <summary>Wizard.Solo.Meta.SettlementResult — Title·MainMenu: 지난 실행의 자동 정산 결과를 한 번 알린다.</summary>
        public const string PendingSettled = "pending_settled";
        /// <summary>bool — Title: 프로필을 이전 세대로 되돌렸다(SaveRestored 창을 띄운다).</summary>
        public const string ProfileRestored = "profile_restored";
        /// <summary>string, string, bool — ConfirmDialog(IScreenManager.Confirm이 채운다).</summary>
        public const string TitleKey = "title_key", BodyKey = "body_key", Caution = "caution";
        /// <summary>string — Tooltip, Toast의 글자(이미 이름 표를 거친 것).</summary>
        public const string Text = "text";
        /// <summary>int, int — CoopLobby: 방금 끝난 협동 판의 마지막 클리어 웨이브·점수(Battle이 넘긴다).</summary>
        public const string CoopResultWave = "coop_result_wave", CoopResultScore = "coop_result_score";
        /// <summary>int — CoopLobby: 끝난 결투의 이긴 슬롯(−1 무승부), 내 슬롯, 승수. RoomMode — CoopLobby의 처음 방 종류.</summary>
        public const string DuelWinner = "duel_winner", DuelMySlot = "duel_my_slot", DuelWins0 = "duel_wins0", DuelWins1 = "duel_wins1", RoomMode = "room_mode";
    }

    /// <summary>화면에 넘기는 인자. 진입점이 여럿인 화면은 Entry로만 구분한다. 열쇠는 ScreenArgKeys.</summary>
    public sealed class ScreenArgs
    {
        /// <summary>어디서 들어왔나(돌아갈 곳 판단용).</summary>
        public ScreenId Entry;
        public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        /// <summary>창이 닫힐 때 결과를 받는다(ConfirmDialog의 예/아니오 등).</summary>
        public Action<object> OnResult;

        public ScreenArgs With(string key, object value) { Values[key] = value; return this; }
        public T Get<T>(string key, T fallback = default)
        {
            return Values.TryGetValue(key, out var v) && v is T t ? t : fallback;
        }
        public static ScreenArgs From(ScreenId entry) => new ScreenArgs { Entry = entry };
        /// <summary>Loading을 거쳐 next 화면으로 가는 인자를 만든다: Push/Replace(ScreenId.Loading, ScreenArgs.ViaLoading(entry, next, nextArgs)).</summary>
        public static ScreenArgs ViaLoading(ScreenId entry, ScreenId next, ScreenArgs nextArgs) =>
            From(entry).With(ScreenArgKeys.NextScreen, next).With(ScreenArgKeys.NextArgs, nextArgs ?? From(entry));
    }

    /// <summary>
    /// 화면 하나. 뷰는 코드로 조립한다(프리팹 없음). 화면에 보이는 글자는 전부 ctx.Names에서 가져온다.
    /// 수명: Build(한 번) → OnEnter → (Tick, OnMenuKey …) → OnExit. 다시 열리면 OnEnter부터.
    /// </summary>
    public interface IScreen
    {
        ScreenId Id { get; }
        ScreenLayer Layer { get; }
        /// <summary>true면 아래 화면이 입력을 받지 않는다(Modal은 true).</summary>
        bool BlocksInput { get; }
        /// <summary>root 아래에 UI를 만든다(1920×1080 기준 좌표). 처음 열릴 때 한 번.</summary>
        void Build(GameContext ctx, RectTransform root);
        void OnEnter(ScreenArgs args);
        void OnExit();
        /// <summary>Esc. true면 소비(스스로 처리), false면 관리자가 Pop 한다.</summary>
        bool OnBack();
        /// <summary>메뉴 키 누름. true면 소비.</summary>
        bool OnMenuKey(MenuKey key);
        /// <summary>매 프레임(맨 위 화면과 그 아래 BlocksInput이 아닌 화면).</summary>
        void Tick(float dt);
    }

    /// <summary>화면 상태 기계. 구현: UI/ScreenManager(F3).</summary>
    public interface IScreenManager
    {
        void Register(ScreenId id, Func<IScreen> factory);
        void Push(ScreenId id, ScreenArgs args = null);
        /// <summary>맨 위 화면을 닫고 새 화면을 연다.</summary>
        void Replace(ScreenId id, ScreenArgs args = null);
        /// <summary>맨 위 화면을 닫는다(들어온 곳으로). result는 닫힌 화면의 args.OnResult로 간다.</summary>
        void Pop(object result = null);
        /// <summary>id 화면이 맨 위가 될 때까지 닫는다. 쌓여 있지 않으면 전부 닫고 id를 연다.</summary>
        void PopTo(ScreenId id, ScreenArgs args = null);
        /// <summary>맨 위의 화면(없으면 None). Toast·Tooltip 층은 세지 않는다 — 전투 입력의 문(IGameSession.InputScreen)이 이 값으로 열리고 닫힌다.</summary>
        ScreenId Top { get; }
        bool IsOpen(ScreenId id);
        /// <summary>그 화면을 닫는다(맨 위가 아니어도 된다. 맨 위면 Pop과 같고, 아니면 그 칸만 뺀다). result는 그 화면의 args.OnResult로 간다.</summary>
        void Close(IScreen screen, object result = null);
        /// <summary>예/아니오 확인 창. caution = 조심 버튼(호박색 테 + 경고 삼각).</summary>
        void Confirm(string titleKey, string bodyKey, bool caution, Action<bool> onResult);
        /// <summary>Confirm과 같고 본문은 이미 만든 글(틀을 채운 글 — 예: "웨이브 15까지 정산"). 글은 이름 표를 거친 것이어야 한다.</summary>
        void ConfirmText(string titleKey, string bodyText, bool caution, Action<bool> onResult);
        /// <summary>한 줄 알림(Toast).</summary>
        void Toast(string text, float seconds = 3f);
        /// <summary>툴팁을 띄운다(anchor 옆, 최대 폭 400). 이미 떠 있으면 글과 자리만 바꾼다. text가 비면 내린다.</summary>
        void ShowTooltip(string text, RectTransform anchor);
        /// <summary>툴팁을 내린다. anchor를 주면 그 칸의 툴팁일 때만.</summary>
        void HideTooltip(RectTransform anchor = null);
        /// <summary>true인 동안 메뉴 키(Esc 포함)를 화면에 보내지 않는다 — 키 재설정의 "키를 누르세요" 동안 그 화면이 키보드를 직접 읽는다.</summary>
        bool CaptureKeys { get; set; }
    }
}
