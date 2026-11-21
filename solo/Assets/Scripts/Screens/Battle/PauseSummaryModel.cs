using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Battle
{
    /// <summary>일시정지 화면 왼쪽의 "지금 판 요약"(08 요약서 7절): 캐릭터, 지팡이, 조립, 지금 점수, 누적 일시정지 시간.</summary>
    public sealed class PauseSummary
    {
        public string CharId;
        public int CharNumber;
        public int WandStage;
        /// <summary>코어 블록 id(빈 칸 null).</summary>
        public string CoreBlockId;
        /// <summary>연결 칸 블록 id(빈 칸 null). 길이 = 연결 칸 수.</summary>
        public readonly List<string> LinkBlockIds = new List<string>();
        public int Score;
        /// <summary>누적 일시정지(내림한 초).</summary>
        public int PausedWholeSeconds;
    }

    /// <summary>판 요약 계산(순수). 순위 판용 10분 안내(pause_limit_note)는 그리지 않는다(D-11).</summary>
    public static class PauseSummaryModel
    {
        public const string PauseTotalKey = "str.pause.pause_total";
        public const string SecondsKey = "str.format.seconds";
        public const string MinSecKey = "str.pause.time.min_sec";
        const int SecondsPerMinute = 60;

        /// <summary>본판 세계에서 요약을 만든다. 플레이어는 슬롯 순 첫 활동 중인 사람.</summary>
        public static PauseSummary Build(SimWorld w, float pausedSeconds)
        {
            var s = new PauseSummary { PausedWholeSeconds = WholeSeconds(pausedSeconds) };
            if (w == null) return s;
            s.Score = w.Run.Score;
            PlayerState p = null;
            for (int i = 0; i < w.Players.Length; i++)
                if (w.Players[i] != null && w.Players[i].Active) { p = w.Players[i]; break; }
            var cfg = w.Run.Config;
            if (p != null)
            {
                s.CharId = p.CharId;
                s.WandStage = p.Wand != null ? p.Wand.EffectiveStage : 0;
                if (p.Loadout != null)
                {
                    s.CoreBlockId = Empty(p.Loadout.Core.BlockId);
                    var links = p.Loadout.Links;
                    if (links != null) for (int i = 0; i < links.Length; i++) s.LinkBlockIds.Add(Empty(links[i].BlockId));
                }
            }
            if (s.CharId == null && cfg != null) s.CharId = cfg.CharId;
            if (p == null && cfg != null)
            {
                s.WandStage = cfg.WandStage;
                if (cfg.Loadout != null)
                {
                    s.CoreBlockId = Empty(cfg.Loadout.Core);
                    for (int i = 0; i < cfg.Loadout.Links.Count; i++) s.LinkBlockIds.Add(Empty(cfg.Loadout.Links[i]));
                }
            }
            s.CharNumber = Ids.Char.NumberOf(s.CharId);
            return s;
        }

        static string Empty(string id) => string.IsNullOrEmpty(id) ? null : id;

        public static int WholeSeconds(float seconds)
        {
            if (float.IsNaN(seconds) || seconds <= 0f) return 0;
            if (seconds >= int.MaxValue) return int.MaxValue;
            return (int)Math.Floor(seconds);
        }

        /// <summary>시간 글: 1분 미만 "{n}초"(str.format.seconds), 그 밖 "{m}분 {s}초"(str.pause.time.min_sec).</summary>
        public static string TimeText(INameTable names, float seconds)
        {
            int total = WholeSeconds(seconds);
            if (total < SecondsPerMinute)
                return names != null ? names.Format(SecondsKey, "n", total) : total.ToString();
            int m = total / SecondsPerMinute, sec = total % SecondsPerMinute;
            return names != null ? names.Format(MinSecKey, "m", m, "s", sec) : m + ":" + sec.ToString("00");
        }

        /// <summary>"누적 일시정지 {time}"(str.pause.pause_total).</summary>
        public static string PauseTotalText(INameTable names, float seconds)
        {
            string t = TimeText(names, seconds);
            return names != null ? names.Format(PauseTotalKey, "time", t) : t;
        }
    }
}
