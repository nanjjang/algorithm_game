using System.Collections.Generic;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Meta
{
    /// <summary>"전체" 탭 맨 아래 복수 기록 자리의 꼴(08 11.1절, 13.2절).</summary>
    public enum RevengeRecordKind
    {
        /// <summary>첫 만남 전(또는 캐릭터 탭) — 자리 없음(TV-167).</summary>
        None = 0,
        /// <summary>복수 중 — 추적기 큰 꼴 432×96.</summary>
        Tracker = 1,
        /// <summary>복수 완료 — revenge.tracker.done_line + 거울 표식.</summary>
        Done = 2
    }

    /// <summary>요약 판의 값(탭 하나).</summary>
    public sealed class RecordsSummary
    {
        public long BestScore;
        /// <summary>직접 넘긴 최고 웨이브("N일째 웨이브 M"으로 그린다).</summary>
        public int BestWave;
        /// <summary>누적 넘긴 웨이브("전체" 탭은 다섯의 합).</summary>
        public int TotalWaves;
        /// <summary>야생 물 단계 1~4(캐릭터 탭만. "전체" 탭은 0 = 그리지 않는다).</summary>
        public int WildStainStage;
        /// <summary>얻은 칭호 수(기록·손맛·복수 칭호 + 다섯 캐릭터의 강화 단계 칭호).</summary>
        public int TitleCount;
    }

    /// <summary>
    /// 기록 화면 규칙(08 11.1절, 엔진 없음). 탭 0 = 전체, 1~5 = 캐릭터. 결투 전적·시즌 결과 줄은 없다(D-51, N-11),
    /// 판 목록의 [다시 보기]는 숨긴다(N-10).
    /// </summary>
    public static class RecordsModel
    {
        /// <summary>탭 수 = 전체 + 캐릭터 다섯.</summary>
        public const int TabCount = 1 + Ids.Char.Count;
        public const int AllTab = 0;

        /// <summary>요약 판에 그리는 줄의 이름 열쇠(순서 그대로). 결투 전적(.duel_record)·시즌 결과(.season_result)는 넣지 않는다.</summary>
        public static readonly string[] SummaryRowKeys =
        {
            MetaKeys.RecordsBestScore, MetaKeys.RecordsBestWave, MetaKeys.RecordsTotalWaves, MetaKeys.RecordsTitles
        };

        /// <summary>판 목록의 열(순서 그대로). [다시 보기](.replay)는 넣지 않는다(N-10 [임시] 단추 숨김).</summary>
        public static readonly string[] ColumnKeys =
        {
            MetaKeys.RecordsDate, MetaKeys.RecordsCharacter, MetaKeys.RecordsReachedWave, MetaKeys.RecordsStartWave,
            MetaKeys.RecordsScore, MetaKeys.RecordsWandStage, MetaKeys.RecordsLoadout
        };

        /// <summary>탭 → 캐릭터 번호(0 = 전체).</summary>
        public static int CharacterOfTab(int tab) => tab >= 1 && tab <= Ids.Char.Count ? tab : AllTab;

        /// <summary>탭 이름 열쇠.</summary>
        public static string TabKey(int tab) => tab == AllTab ? MetaKeys.RecordsTabAll : MetaKeys.Character(tab);

        public static RecordsSummary Summary(ProfileData p, int tab, IReadOnlyList<int> stainThresholds, IReadOnlyList<int> milestones)
        {
            var s = new RecordsSummary();
            if (p == null) return s;
            int ch = CharacterOfTab(tab);
            for (int i = 0; i < p.Characters.Count; i++)
            {
                var c = p.Characters[i];
                if (c == null || (ch != AllTab && c.Id != ch)) continue;
                if (c.BestScore > s.BestScore) s.BestScore = c.BestScore;
                if (c.BestWave > s.BestWave) s.BestWave = c.BestWave;
                s.TotalWaves += c.TotalWaves;
                if (ch != AllTab) s.WildStainStage = WardrobeModel.StainMax(c, stainThresholds);
            }
            s.TitleCount = TitleModel.Available(p, milestones).Count;
            return s;
        }

        /// <summary>"N일째 웨이브 M"의 틀 값(str.format.day_wave: day, wave).</summary>
        public static int DayOf(int wave) => wave > 0 ? DayCycle.Day(wave) : 0;

        /// <summary>판 목록: 최신부터(run_history는 정산 때 맨 뒤에 붙는다 — 뒤가 최신), 캐릭터 탭이면 그 캐릭터만.</summary>
        public static List<RunRecordData> Runs(ProfileData p, int tab)
        {
            var list = new List<RunRecordData>();
            if (p == null || p.RunHistory == null) return list;
            int ch = CharacterOfTab(tab);
            for (int i = p.RunHistory.Count - 1; i >= 0; i--)
            {
                var r = p.RunHistory[i];
                if (r == null) continue;
                if (ch != AllTab && r.Character != ch) continue;
                list.Add(r);
            }
            return list;
        }

        /// <summary>복수 기록 자리: "전체" 탭에서만. 첫 만남 전이면 자리 없음(TV-167).</summary>
        public static RevengeRecordKind RevengeKind(int tab, RevengeTrackerView v)
        {
            if (CharacterOfTab(tab) != AllTab) return RevengeRecordKind.None;
            switch (v.State)
            {
                case RevengeState.Pursuing: return RevengeRecordKind.Tracker;
                case RevengeState.Done: return RevengeRecordKind.Done;
                default: return RevengeRecordKind.None;
            }
        }

        /// <summary>추적기 큰 꼴의 줄 수: 목표 + 진행, 흠집 줄은 웨이브 20 허상 싸움을 시작한 적이 있을 때만(TV-234/235).</summary>
        public static int TrackerLines(RevengeTrackerView v) => v.HasDent ? 3 : 2;

        /// <summary>진행 막대 값 0~1(n / 19).</summary>
        public static float TrackerProgress01(RevengeTrackerView v) =>
            v.ProgressMax > 0 ? (float)v.Progress / v.ProgressMax : 0f;

        /// <summary>ISO 8601 날짜·시각에서 날짜 부분(yyyy-MM-dd). 꼴이 다르면 그대로.</summary>
        public static string DatePart(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return string.Empty;
            int t = iso.IndexOf('T');
            if (t > 0) return iso.Substring(0, t);
            return iso.Length > 10 && iso[10] == ' ' ? iso.Substring(0, 10) : iso;
        }

        /// <summary>판 목록의 날짜 칸(yyyy-MM-dd HH:mm). 꼴이 다르면 그대로.</summary>
        public static string DateTimeText(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return string.Empty;
            string s = iso.Replace('T', ' ');
            return s.Length > 16 ? s.Substring(0, 16) : s;
        }
    }
}
