using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>메인 메뉴 항목이 하는 일(08 요약서 4.2절 순서). 봇 상대 결투 연습은 없다(D-51).</summary>
    public enum MainMenuAction
    {
        Expedition = 0, Enhance = 1, AssemblyLibrary = 2, Practice = 3, Records = 4, Wardrobe = 5, Shop = 6, Settings = 7,
        /// <summary>서버 전용 묶음 상자 → OfflineGuide 다시 보기.</summary>
        ServerOnly = 8,
        /// <summary>게임 종료(D-36 [임시], 확인 창 한 번 — N-02).</summary>
        Exit = 9
    }

    /// <summary>메인 메뉴의 한 칸(모드 카드, 목록 줄, 묶음 상자, 나가기).</summary>
    public sealed class MainMenuEntry
    {
        public MainMenuAction Action;
        /// <summary>08 4.2절의 항목 id(enhance, assembly_library …).</summary>
        public string Id;
        public string NameKey;
        /// <summary>여는 화면(나가기는 None).</summary>
        public ScreenId Target;
        public bool Enabled = true;
        public string DisabledReasonKey;
    }

    /// <summary>메인 메뉴 배경의 시간대(08 4.1절 [임시] U-14).</summary>
    public enum MenuTimeOfDay { Day = 0, Dusk = 1, Night = 2 }

    /// <summary>
    /// 메인 메뉴(08 요약서 4절, 01 요약서 2.2절) — 프로필 → 항목 목록·활성 여부·부제·추적기 한 줄.
    /// - 원정 카드: 최고 웨이브 = 전 캐릭터의 최고(str.main_menu.best_wave). 복수 중이면 추적기 한 줄(revenge.tracker.short).
    ///   추적기 값은 RevengeTracker.Build(profile, content)의 Visible·Progress·ProgressMax로만 만든다(직접 계산하지 않는다).
    /// - 목록(7줄): 강화, 조립과 서고, 연습, 기록, 옷장, 상점, 설정. 서버 전용(결투·광장 카드, 우편함·친구·순위표·시즌·장터)은
    ///   흐리게 두지 않고 묶음 상자 하나 + str.main_menu.server_only → OfflineGuide 다시 보기(TV-026).
    /// - 프로필 전환 수단은 없다(타이틀에서만, TV-005). 판 중에는 강화 항목이 없다(TV-133). 봇 상대 결투 연습 없음(D-51).
    /// </summary>
    public sealed class MainMenuModel
    {
        /// <summary>묶음 상자 안에 넣는 서버 전용 이름(카드 둘 + 목록 다섯, 08 4.2절 8~12번).</summary>
        public static readonly string[] ServerOnlyKeys =
        {
            StartNameKeys.MainMenuDuel, StartNameKeys.MainMenuPlaza,
            StartNameKeys.MainMenuMailbox, StartNameKeys.MainMenuFriends, StartNameKeys.MainMenuLeaderboard,
            StartNameKeys.MainMenuSeason, StartNameKeys.MainMenuMarket
        };

        /// <summary>WP4 조각(NamesSolo/Meta.ko.json): 원정 카드의 다음 목표, 서버 전용 한 줄.</summary>
        public const string GoalTitleKey = "str.main_menu.goal.title", GoalRevengeKey = "str.main_menu.goal.revenge",
            GoalRevengeNextKey = "str.main_menu.goal.revenge_next", GoalFreeKey = "str.main_menu.goal.free",
            GoalChallengeKey = "str.main_menu.goal.challenge", GoalAllDoneKey = "str.main_menu.goal.all_done",
            ServerLineKey = "str.main_menu.server_line", ServerLineSepKey = "str.main_menu.server_line_sep";

        public MainMenuEntry Expedition;
        public readonly List<MainMenuEntry> Items = new List<MainMenuEntry>();
        public MainMenuEntry ServerOnly;
        public MainMenuEntry Exit;

        public int BestWave;
        public RevengeTrackerView Tracker;
        public long Gold;
        /// <summary>지금 고른 캐릭터(profile.last_character, 1~5).</summary>
        public int Character = 1;
        /// <summary>단 칭호 id(없으면 null).</summary>
        public string EquippedTitle;
        public bool RunActive;
        /// <summary>복수 중일 때 허상(웨이브 20)까지 남은 웨이브 = 20 − 전 캐릭터 최고 웨이브(최소 1). 복수 중이 아니면 0.</summary>
        public int RevengeWavesLeft;
        /// <summary>지금 캐릭터의 다음 도전 과제(다 이뤘으면 "모두" 과제, 그것도 없으면 null).</summary>
        public Wizard.Solo.Meta.Records.ChallengeDef NextChallenge;
        public Wizard.Solo.Meta.Records.ChallengeProgress NextChallengeProgress;

        /// <summary>커서가 도는 순서(원정 카드, 묶음 상자, 목록, 나가기).</summary>
        public IEnumerable<MainMenuEntry> All
        {
            get
            {
                if (Expedition != null) yield return Expedition;
                if (ServerOnly != null) yield return ServerOnly;
                for (int i = 0; i < Items.Count; i++) yield return Items[i];
                if (Exit != null) yield return Exit;
            }
        }

        static MainMenuEntry E(MainMenuAction a, string id, string key, ScreenId target) =>
            new MainMenuEntry { Action = a, Id = id, NameKey = key, Target = target };

        /// <param name="runActive">판(원정)이 열려 있나(ctx.Session.HasRun). 참이면 강화 항목을 넣지 않는다(TV-133).</param>
        public static MainMenuModel Build(ProfileData profile, IContentDb db, bool runActive)
        {
            var m = new MainMenuModel { RunActive = runActive };
            m.Expedition = E(MainMenuAction.Expedition, "expedition", StartNameKeys.MainMenuExpedition, ScreenId.ExpeditionPrep);
            if (!runActive)
                m.Items.Add(E(MainMenuAction.Enhance, "enhance", StartNameKeys.MainMenuEnhance, ScreenId.WandPicker));
            m.Items.Add(E(MainMenuAction.AssemblyLibrary, "assembly_library", StartNameKeys.MainMenuAssemblyLibrary, ScreenId.Camp));
            m.Items.Add(E(MainMenuAction.Practice, "practice", StartNameKeys.MainMenuPractice, ScreenId.DummyPractice));
            m.Items.Add(E(MainMenuAction.Records, "records", StartNameKeys.MainMenuRecords, ScreenId.Records));
            m.Items.Add(E(MainMenuAction.Wardrobe, "wardrobe", StartNameKeys.MainMenuWardrobe, ScreenId.Wardrobe));
            m.Items.Add(E(MainMenuAction.Shop, "shop", StartNameKeys.MainMenuShop, ScreenId.Shop));
            m.Items.Add(E(MainMenuAction.Settings, "settings", StartNameKeys.MainMenuSettings, ScreenId.Settings));
            m.ServerOnly = E(MainMenuAction.ServerOnly, "server_only", StartNameKeys.MainMenuServerOnly, ScreenId.OfflineGuide);
            m.Exit = E(MainMenuAction.Exit, "exit", StartNameKeys.MainMenuExit, ScreenId.None);

            m.BestWave = BestWaveOf(profile);
            m.Tracker = RevengeTracker.Build(profile, db);
            if (m.Tracker.State == RevengeState.Pursuing)
            {
                int left = RevengeTracker.PhantomWave - m.BestWave;
                m.RevengeWavesLeft = left < 1 ? 1 : left;
            }
            if (profile != null)
            {
                m.Gold = profile.Currencies != null ? profile.Currencies.Gold : 0;
                m.Character = profile.LastCharacter >= 1 ? profile.LastCharacter : 1;
                // 단계 칭호는 붙인 때의 구간이 아니라 지금 든 지팡이 단계로 다시 정한다(가지지 않은 칭호면 없음).
                m.EquippedTitle = Wizard.Solo.Screens.Meta.TitleModel.Resolve(profile, Wizard.Solo.Screens.Meta.TitleModel.Milestones(db));
                var defs = Wizard.Solo.Meta.Records.ChallengeBook.Load(db);
                m.NextChallenge = Wizard.Solo.Meta.Records.ChallengeBook.Next(profile, defs, m.Character)
                    ?? Wizard.Solo.Meta.Records.ChallengeBook.Next(profile, defs, 0);
                if (m.NextChallenge != null) m.NextChallengeProgress = Wizard.Solo.Meta.Records.ChallengeBook.Progress(profile, m.NextChallenge);
            }
            return m;
        }

        /// <summary>전 캐릭터의 최고 웨이브(직접 넘긴 것).</summary>
        public static int BestWaveOf(ProfileData profile)
        {
            int best = 0;
            if (profile == null || profile.Characters == null) return best;
            for (int i = 0; i < profile.Characters.Count; i++)
                if (profile.Characters[i] != null && profile.Characters[i].BestWave > best) best = profile.Characters[i].BestWave;
            return best;
        }

        public string BestWaveLine(INameTable names) =>
            names != null ? names.Format(StartNameKeys.MainMenuBestWave, "n", BestWave) : StartNameKeys.MainMenuBestWave;

        /// <summary>추적기 한 줄(복수 중이 아니면 null — 첫 만남 전에는 어디에도 없다).</summary>
        public string TrackerLine(INameTable names)
        {
            if (!Tracker.Visible) return null;
            return names != null
                ? names.Format(StartNameKeys.RevengeTrackerShort, "n", Tracker.Progress, "max", Tracker.ProgressMax)
                : StartNameKeys.RevengeTrackerShort;
        }

        /// <summary>원정 카드의 복수 목표 줄(복수 중이 아니면 null — 첫 만남 전에는 허상을 알리지 않는다).</summary>
        public string RevengeGoalLine(INameTable names)
        {
            if (RevengeWavesLeft <= 0) return null;
            if (names == null) return GoalRevengeKey;
            return BestWave >= RevengeTracker.PhantomWave - 1
                ? names.Get(GoalRevengeNextKey)
                : names.Format(GoalRevengeKey, "n", RevengeWavesLeft);
        }

        /// <summary>원정 카드의 다음 도전 과제 줄("다음 도전 과제 · 이름 — 설명").</summary>
        public string ChallengeGoalLine(INameTable names)
        {
            if (NextChallenge == null) return null;
            if (names == null) return GoalChallengeKey;
            return names.Format(GoalChallengeKey, "name", names.Get(Wizard.Solo.Meta.Records.ChallengeBook.NameKey(NextChallenge.Id)),
                "desc", Wizard.Solo.Meta.Records.ChallengeText.Describe(names, NextChallenge),
                "who", NextChallenge.Character >= 1 ? names.Get(Ids.Char.Of(NextChallenge.Character)) : names.Get(Wizard.Solo.Screens.Meta.MetaKeys.RecordsChallengeAll));
        }

        /// <summary>그 항목이 여는 화면과 인자(00 문서 8.1절 표의 열쇠·형). 나가기·꺼진 항목은 None.</summary>
        public StartNav Open(MainMenuEntry e)
        {
            if (e == null || !e.Enabled) return StartNav.None;
            var args = ScreenArgs.From(ScreenId.MainMenu);
            switch (e.Action)
            {
                case MainMenuAction.Expedition:
                    return StartNav.Push(ScreenId.ExpeditionPrep, args);
                case MainMenuAction.Enhance:
                    // 강화는 메인 메뉴에서만 연다. 지팡이 고르기의 처음 캐릭터 = last_character(D-56).
                    return StartNav.Push(ScreenId.WandPicker, args.With(ScreenArgKeys.Character, Character));
                case MainMenuAction.AssemblyLibrary:
                    return StartNav.Push(ScreenId.Camp, args.With(ScreenArgKeys.CampMode, CampMode.Library));
                case MainMenuAction.Practice:
                    return StartNav.Push(ScreenId.DummyPractice, args.With(ScreenArgKeys.PracticeEntry, PracticeEntry.MainMenu));
                case MainMenuAction.Records:
                    return StartNav.Push(ScreenId.Records, args);
                case MainMenuAction.Wardrobe:
                    return StartNav.Push(ScreenId.Wardrobe, args);
                case MainMenuAction.Shop:
                    return StartNav.Push(ScreenId.Shop, args);
                case MainMenuAction.Settings:
                    return StartNav.Push(ScreenId.Settings, args);
                case MainMenuAction.ServerOnly:
                    return StartNav.Push(ScreenId.OfflineGuide, args);
                default:
                    return StartNav.None;
            }
        }

        /// <summary>기기 시각(시) → 배경 시간대.</summary>
        public static MenuTimeOfDay TimeOfDay(int hour, StartTuning t)
        {
            if (t == null) t = new StartTuning();
            if (hour >= t.DayStartHour && hour < t.DuskStartHour) return MenuTimeOfDay.Day;
            if (hour >= t.DuskStartHour && hour < t.NightStartHour) return MenuTimeOfDay.Dusk;
            return MenuTimeOfDay.Night;
        }
    }
}
