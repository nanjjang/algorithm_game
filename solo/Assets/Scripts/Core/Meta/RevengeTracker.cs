using Wizard.Solo.Data;

namespace Wizard.Solo.Meta
{
    public enum MirrorMark { Intact = 0, Cracked = 1, Broken = 2 }

    /// <summary>복수 추적기가 그릴 값(08 요약서 13.2절, 05 요약서 10.4절). 글자는 화면이 이름 표 열쇠(revenge.tracker.*)로 만든다.</summary>
    public struct RevengeTrackerView
    {
        public RevengeState State;
        /// <summary>추적기를 그리는가: 복수 중이거나, 이번 정산에서 막 끝냈을 때(완료 꼴 한 번). 첫 만남 전에는 어디에도 없다.</summary>
        public bool Visible;
        /// <summary>진행 줄의 n = min(ProgressMax, 전 캐릭터 최고 웨이브).</summary>
        public int Progress;
        public int ProgressMax;
        /// <summary>흠집 줄이 있는가(웨이브 20 허상 싸움을 한 번이라도 시작했다). 없으면 줄을 그리지 않는다.</summary>
        public bool HasDent;
        public int DentPercent;
        public int Tries;
        public MirrorMark Mirror;
        /// <summary>HUD 줄의 n = 허상 웨이브 − 지금 웨이브(허상 전날 다음 ~ 허상 직전 웨이브에서만). 0 = HUD 줄 없음.</summary>
        public int HudWavesLeft;
        public bool JustCompleted;
        /// <summary>완료 줄(기록 화면): 끝낸 날짜(ISO)와 첫 만남에서 몇 판째였나.</summary>
        public string DoneDate;
        public int RunsToComplete;
    }

    /// <summary>
    /// 복수 추적기 계산(순수 함수). 메인 메뉴·원정 준비·HUD·야영·정산·기록·첫 만남 연출이 전부 이 함수 하나를 쓴다
    /// (화면마다 따로 계산하지 않는다).
    /// </summary>
    public static class RevengeTracker
    {
        /// <summary>복수 대상 허상이 나오는 웨이브 = 둘째 날의 보스 웨이브.</summary>
        public const int PhantomWave = 2 * DayCycle.DayLength;

        /// <param name="profile">프로필(null이면 첫 만남 전으로 본다).</param>
        /// <param name="crackedPercent">금 간 거울 문턱(%). const.json revenge_cracked_percent.</param>
        /// <param name="currentWave">판 중이면 지금 웨이브(HUD 줄), 판 밖이면 0.</param>
        /// <param name="metThisRun">이 판에서 첫 만남을 치렀다(RunState.RevengeActive &amp;&amp; 프로필에는 아직 안 적힘) → 복수 중으로 본다.</param>
        /// <param name="completedThisRun">이번 정산에서 복수를 끝냈다(SettlementResult.RevengeReward) → 완료 꼴 한 번.</param>
        public static RevengeTrackerView Build(ProfileData profile, int crackedPercent, int currentWave = 0, bool metThisRun = false,
            bool completedThisRun = false)
        {
            var v = new RevengeTrackerView { ProgressMax = PhantomWave - 1 };
            if (profile == null) { v.State = RevengeState.BeforeMeeting; return v; }

            var state = profile.RevengeState;
            if (state == RevengeState.BeforeMeeting && metThisRun) state = RevengeState.Pursuing;
            v.State = state;

            int best = 0;
            for (int i = 0; i < profile.Characters.Count; i++)
                if (profile.Characters[i].BestWave > best) best = profile.Characters[i].BestWave;
            v.Progress = best < v.ProgressMax ? best : v.ProgressMax;

            v.Tries = profile.Revenge.PhantomTries;
            v.HasDent = v.Tries > 0;
            v.DentPercent = profile.Revenge.PhantomBestDent;
            v.Mirror = state == RevengeState.Done ? MirrorMark.Broken
                : v.HasDent && v.DentPercent >= crackedPercent ? MirrorMark.Cracked : MirrorMark.Intact;

            if (state == RevengeState.Pursuing && currentWave > PhantomWave - DayCycle.DayLength && currentWave < PhantomWave)
                v.HudWavesLeft = PhantomWave - currentWave;

            v.JustCompleted = completedThisRun;
            v.Visible = state == RevengeState.Pursuing || completedThisRun;
            if (state == RevengeState.Done)
            {
                v.DoneDate = profile.Flags.RevengeDoneAt;
                v.RunsToComplete = profile.Revenge.RunsSinceFirstMeet;
            }
            return v;
        }

        /// <summary>문턱을 콘텐츠에서 읽는 편의 꼴.</summary>
        public static RevengeTrackerView Build(ProfileData profile, IContentDb db, int currentWave = 0, bool metThisRun = false,
            bool completedThisRun = false)
        {
            int cracked = db != null && db.Const != null ? db.Const.RevengeCrackedPercent : 0;
            return Build(profile, cracked, currentWave, metThisRun, completedThisRun);
        }
    }
}
