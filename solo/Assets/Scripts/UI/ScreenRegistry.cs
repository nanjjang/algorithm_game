using Wizard.Solo.Screens.Start;

namespace Wizard.Solo.UI
{
    public static class ScreenRegistry
    {
        public static void RegisterAll(IScreenManager m, GameContext ctx)
        {
            // 공용 팝업
            m.Register(ScreenId.ConfirmDialog, () => new UiConfirmDialog());
            m.Register(ScreenId.Tooltip, () => new UiTooltipScreen());
            m.Register(ScreenId.Loading, () => new UiLoadingScreen());
            m.Register(ScreenId.SaveRestored, () => new UiSaveRestoredDialog());

            // 시작과 메인 메뉴
            m.Register(ScreenId.FirstRunPrompt, () => new FirstRunPromptScreen());
            m.Register(ScreenId.Title, () => new TitleScreen());
            m.Register(ScreenId.ProfileSelect, () => new ProfileSelectScreen());
            m.Register(ScreenId.OfflineGuide, () => new OfflineGuideScreen());
            m.Register(ScreenId.MainMenu, () => new MainMenuScreen());

            // 온라인 메뉴
            m.Register(ScreenId.CoopLobby, () => new Screens.Online.CoopLobbyScreen());
            m.Register(ScreenId.Plaza, () => new Screens.Online.PlazaScreen());
            m.Register(ScreenId.OnlineProfile, () => new Screens.Online.OnlineProfileScreen());
            m.Register(ScreenId.OnlineAssembly, () => new Screens.Online.OnlineAssemblyScreen());
            m.Register(ScreenId.OnlineMarket, () => new Screens.Online.OnlineMarketScreen());
            m.Register(ScreenId.OnlineFriends, () => new Screens.Online.OnlineFriendsScreen());
            m.Register(ScreenId.OnlineSeason, () => new Screens.Online.OnlineSeasonScreen());
            m.Register(ScreenId.OnlineLogin, () => new Screens.Online.OnlineLoginScreen());

            // 원정 준비
            m.Register(ScreenId.ExpeditionPrep, () => new Screens.Prep.ExpeditionPrepScreen());

            // 전투와 일시정지
            m.Register(ScreenId.Battle, () => new Screens.Battle.BattleScreen());
            m.Register(ScreenId.Pause, () => new Screens.Battle.PauseScreen());
            m.Register(ScreenId.ControlsView, () => new Screens.Battle.ControlsViewScreen());

            // 야영과 보상
            m.Register(ScreenId.LootPick, () => new Screens.Camp.LootPickScreen());
            m.Register(ScreenId.Camp, () => new Screens.Camp.CampScreen());
            m.Register(ScreenId.ArtTrie, () => new Screens.Camp.ArtTrieScreen());
            m.Register(ScreenId.DummyPractice, () => new Screens.Camp.DummyPracticeScreen());

            // 정산
            // 판 끝 요약용 표시 통계 수집기: 부팅 때 세션에 한 번 붙고, 정산 화면이 그 판의 통계를 읽는다(시뮬레이션 상태를 더하지 않는다).
            var stats = new View.Stats.RunStatsCollector();
            var tuning = Screens.Settlement.RecapTuning.From(ctx != null ? ctx.Content : null);
            stats.PatternWindowTicks = SimConst.Ticks(tuning.BossPatternWindowSeconds);
            if (ctx != null) stats.Attach(ctx.Session);
            m.Register(ScreenId.Settlement, () => new Screens.Settlement.SettlementScreen(stats, tuning));

            // 지팡이 강화
            m.Register(ScreenId.WandPicker, () => new Screens.Enhance.WandPickerScreen());
            m.Register(ScreenId.Enhance, () => new Screens.Enhance.EnhanceScreen());
            m.Register(ScreenId.WandReleaseConfirm, () => new Screens.Enhance.WandReleaseConfirmScreen());
            m.Register(ScreenId.MutationRemove, () => new Screens.Enhance.MutationRemoveScreen());
            m.Register(ScreenId.SlotEngrave, () => new Screens.Enhance.SlotEngraveScreen());
            m.Register(ScreenId.EnhanceTutorial, () => new Screens.Enhance.EnhanceTutorialScreen());

            // 기록과 상점
            m.Register(ScreenId.Records, () => new Screens.Meta.RecordsScreen());
            m.Register(ScreenId.TitlePicker, () => new Screens.Meta.TitlePickerScreen());
            m.Register(ScreenId.Wardrobe, () => new Screens.Meta.WardrobeScreen());
            m.Register(ScreenId.Shop, () => new Screens.Meta.ShopScreen());
            m.Register(ScreenId.ShopBuyConfirm, () => new Screens.Meta.ShopBuyConfirmScreen());

            // 설정
            m.Register(ScreenId.Settings, () => new Screens.Settings.SettingsScreen());
            m.Register(ScreenId.KeyRebind, () => new Screens.Settings.KeyRebindScreen());

            // 튜토리얼과 도감
            m.Register(ScreenId.Tutorial, () => new Screens.Story.TutorialScreen());
            // 조작·도감(일시정지 메뉴에서 배운 것 다시 보기)과 문맥 힌트 쪽지(처음 일어난 일을 한 번 알려 준다).
            m.Register(ScreenId.Codex, () => new Screens.Hints.CodexScreen());
            Screens.Hints.HintHost.Attach(ctx);
        }
    }
}
