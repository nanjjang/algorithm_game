using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Meta.Settlement
{
    /// <summary>기록 칭호 한 줄: 그 웨이브를 넘기면 받는다(01 요약서 10.5절).</summary>
    public sealed class RecordTitleRule
    {
        public string Title;
        public int Wave;
    }

    /// <summary>
    /// 정산의 보충 수치(Resources/DataSolo/G11.json). Def에 없는 명세 값만 든다. 조각이 없으면 전부 빈 값이고
    /// 그 규칙은 꺼진다(칭호 없음, 판 목록 상한 없음, 건너뛰기 0만).
    /// </summary>
    public sealed class SettlementTuning
    {
        public int StartSkipStep;
        public int StartSkipLimitNum;
        public int StartSkipLimitDen;
        public int PhantomFirstWave;
        /// <summary>판 목록 보관 수(0 이하 = 상한 없음).</summary>
        public int RunHistoryCap;
        public readonly List<RecordTitleRule> RecordTitles = new List<RecordTitleRule>();
        public int BloomTitleCount;
        /// <summary>무피격으로 넘겨야 하는 같은 날의 하루 속 순번들(오름차순).</summary>
        public readonly List<int> FlawlessNightSlots = new List<int>();
        /// <summary>야생 물 단계가 오르는 누적 웨이브(첫 값 = 1단계).</summary>
        public readonly List<int> WildStainTotalWaves = new List<int>();

        public static SettlementTuning From(JObject j)
        {
            var t = new SettlementTuning();
            if (j == null) return t;
            t.StartSkipStep = Int(j, "start_skip_step");
            t.StartSkipLimitNum = Int(j, "start_skip_limit_num");
            t.StartSkipLimitDen = Int(j, "start_skip_limit_den");
            t.PhantomFirstWave = Int(j, "phantom_first_wave");
            t.RunHistoryCap = Int(j, "run_history_cap");
            t.BloomTitleCount = Int(j, "bloom_title_count");
            if (j["record_titles"] is JArray titles)
                foreach (JToken row in titles)
                    t.RecordTitles.Add(new RecordTitleRule { Title = (string)row["title"], Wave = (int)row["wave"] });
            if (j["flawless_night_slots"] is JArray slots)
                foreach (JToken s in slots) t.FlawlessNightSlots.Add((int)s);
            if (j["wild_stain_total_waves"] is JArray stains)
                foreach (JToken s in stains) t.WildStainTotalWaves.Add((int)s);
            return t;
        }

        static int Int(JObject j, string key)
        {
            JToken v = j[key];
            return v != null && v.Type == JTokenType.Integer ? (int)v : 0;
        }
    }
}
