using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Meta.Settlement
{
    /// <summary>
    /// 중간 지점 요약(RunSummary) ↔ 프로필의 run_checkpoint.summary. 필드 이름은 형의 이름 그대로 둔다
    /// (저장 구현이 JObject를 바이트 그대로 되살린다). 기기 안에서만 쓰는 값이고 밖으로 나가지 않는다.
    /// </summary>
    public static class CheckpointCodec
    {
        const string KeyWaveResults = "WaveResults";
        const string KeyStats = "Stats";
        const string KeyWhims = "Whims";

        public static JObject ToJson(RunSummary summary)
        {
            return summary != null ? JObject.FromObject(summary, JsonSerializer.CreateDefault()) : null;
        }

        /// <summary>읽지 못하면 null.</summary>
        public static RunSummary FromJson(JObject json)
        {
            if (json == null) return null;
            RunSummary s;
            try { s = json.ToObject<RunSummary>(JsonSerializer.CreateDefault()); }
            catch (JsonException) { return null; }
            catch (InvalidCastException) { return null; }
            catch (FormatException) { return null; }
            catch (ArgumentException) { return null; }
            if (s == null) return null;
            RestoreWhims(s, json);
            return s;
        }

        /// <summary>WaveStats.Whims는 읽기 전용 필드라 직렬화기가 채우지 못했을 때 손으로 채운다(변덕 줄은 점수 내역 표시에 쓴다).</summary>
        static void RestoreWhims(RunSummary s, JObject json)
        {
            if (s.WaveResults == null || !(json[KeyWaveResults] is JArray rows)) return;
            for (int i = 0; i < s.WaveResults.Count && i < rows.Count; i++)
            {
                WaveResult r = s.WaveResults[i];
                if (r == null || r.Stats == null || r.Stats.Whims.Count > 0) continue;
                if (!(rows[i] is JObject row) || !(row[KeyStats] is JObject stats) || !(stats[KeyWhims] is JArray whims)) continue;
                foreach (JToken w in whims)
                    if (w != null && w.Type == JTokenType.String) r.Stats.Whims.Add((string)w);
            }
        }
    }
}
