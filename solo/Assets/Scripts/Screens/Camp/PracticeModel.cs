using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>입력 간격 막대 하나(08 요약서 8.5절: 허용 간격 = 막대 길이, 내 간격 = 표식, 넘으면 표식이 막대 밖·호박색).</summary>
    public struct GapBar
    {
        public CmdDir Dir;
        public long GapU;
        public long AllowedGapU;
        /// <summary>표식 자리 = GapU / AllowedGapU(막대 길이 1 기준). 1을 넘으면 막대 밖.</summary>
        public float MarkRatio;
        public bool Over;
    }

    /// <summary>실패 원인 한 줄.</summary>
    public struct PracticeFailLine
    {
        public string Key;
        /// <summary>"{n}번째"의 n(late_input만).</summary>
        public int Index;
        /// <summary>소수 둘째 자리 글자("0.02").</summary>
        public string Seconds;
        public bool IsEmpty => Key == null;
    }

    /// <summary>
    /// 허수아비 연습 화면의 규칙(08 요약서 8.5절, 04 요약서 6.1절). 엔진 없음.
    /// 막대·게이지는 Arts.GetRecentInputs(TimeU, GapU, AllowedGapU)와 Arts.Preview(LastDirTimeU, JustWindowU)만 본다.
    /// </summary>
    public static class PracticeModel
    {
        /// <summary>옵션 명령(A = PracticeOption, B = 1 켬 / 0 끔).</summary>
        public static RunCommand OptionCommand(PracticeOption opt, bool on) =>
            new RunCommand(RunCommandKind.PracticeSetOption, (int)opt, on ? 1 : 0);

        public static RunCommand ResetCommand() => new RunCommand(RunCommandKind.PracticeReset);

        public static bool OptionValue(RunState run, PracticeOption opt)
        {
            if (run == null) return false;
            switch (opt)
            {
                case PracticeOption.InfiniteCrystal: return run.PracticeInfiniteCrystal;
                case PracticeOption.EnemyFire: return run.PracticeEnemyFire;
                default: return run.PracticeHeatFreeze;
            }
        }

        public static string OptionKey(PracticeOption opt)
        {
            switch (opt)
            {
                case PracticeOption.InfiniteCrystal: return "str.dummy_practice.option.infinite_crystal";
                case PracticeOption.EnemyFire: return "str.dummy_practice.option.enemy_fire";
                default: return "str.dummy_practice.option.heat_lock";
            }
        }

        /// <summary>연습 옵션 셋(D-57). 결투 입력 지연 체험은 넣지 않는다(D-51).</summary>
        public static readonly PracticeOption[] Options = { PracticeOption.InfiniteCrystal, PracticeOption.EnemyFire, PracticeOption.HeatFreeze };

        /// <summary>입력 사이마다 막대. 첫 입력(AllowedGapU 0)은 막대가 없다.</summary>
        public static void GapBars(IList<RecentInput> recent, List<GapBar> into)
        {
            into.Clear();
            if (recent == null) return;
            for (int i = 0; i < recent.Count; i++)
            {
                var r = recent[i];
                if (r.AllowedGapU <= 0) continue;
                into.Add(new GapBar
                {
                    Dir = r.Dir, GapU = r.GapU, AllowedGapU = r.AllowedGapU,
                    MarkRatio = (float)r.GapU / r.AllowedGapU,
                    Over = r.GapU > r.AllowedGapU
                });
            }
        }

        /// <summary>저스트 게이지의 차오름(0..1): 마지막 방향 입력부터 JustWindowU 동안 찬다. 입력이 없으면 0.</summary>
        public static float JustGauge(long nowU, long lastDirTimeU, long justWindowU)
        {
            if (lastDirTimeU < 0 || justWindowU <= 0) return 0f;
            long d = nowU - lastDirTimeU;
            if (d <= 0) return 0f;
            if (d >= justWindowU) return 1f;
            return (float)d / justWindowU;
        }

        /// <summary>K가 저스트 창 안인가: 방향 입력 뒤 0 ≤ Δ ≤ 창(1,075u). 경계 1,075u는 안, 1,076u는 밖(04 6절: 일반 기술은 lastDir + 1075 + 1부터).</summary>
        public static bool InJustWindow(long kTimeU, long lastDirTimeU, long justWindowU) =>
            lastDirTimeU >= 0 && kTimeU >= lastDirTimeU && kTimeU - lastDirTimeU <= justWindowU;

        /// <summary>섬을 놓친 K의 초과량(04 6.1절: (t_K − lastDirTime) − 1075). 창 안이면 0.</summary>
        public static long LateKUnits(long kTimeU, long lastDirTimeU, long justWindowU)
        {
            if (lastDirTimeU < 0) return 0;
            long over = kTimeU - lastDirTimeU - justWindowU;
            return over > 0 ? over : 0;
        }

        /// <summary>"K가 {sec}초 늦음"(TV-081: 0.03초).</summary>
        public static PracticeFailLine LateK(long lateU) =>
            lateU <= 0 ? default : new PracticeFailLine { Key = "str.dummy_practice.fail.late_k", Seconds = CampFormat.LateText(CampFormat.LateSecondsFromUnits(lateU)) };

        /// <summary>ArtFizzled 이벤트 → 실패 한 줄(A = 이유, B = 늦은 입력 번호, F = 늦은 초). TV-081: "3번째 입력이 0.02초 늦음".</summary>
        public static PracticeFailLine FromFizzle(in SimEvent e)
        {
            switch ((ArtFizzleReason)e.A)
            {
                case ArtFizzleReason.NoCrystal:
                    return new PracticeFailLine { Key = "str.dummy_practice.fail.no_crystal" };
                case ArtFizzleReason.FullBloomLate:
                    return new PracticeFailLine
                    {
                        Key = "str.dummy_practice.fail.late_input", Index = e.B,
                        Seconds = CampFormat.LateText(CampFormat.LateSecondsCeil(e.F))
                    };
                default: return default;
            }
        }

        /// <summary>목표 커맨드에서 넣은 만큼(밝아지는 칸 수): 최근의 이어진 입력열 끝이 목표 경로의 앞부분과 같은 가장 긴 길이.</summary>
        public static int TargetProgress(IList<CmdDir> target, IList<RecentInput> recent)
        {
            if (target == null || target.Count == 0 || recent == null || recent.Count == 0) return 0;
            int start = recent.Count - 1;
            while (start > 0 && recent[start].Chained) start--;
            int avail = recent.Count - start;
            for (int k = target.Count < avail ? target.Count : avail; k > 0; k--)
            {
                int off = recent.Count - k;
                bool ok = true;
                for (int i = 0; i < k && ok; i++) ok = recent[off + i].Dir == target[i];
                if (ok) return k;
            }
            return 0;
        }

        /// <summary>비기의 목표 커맨드(arts.json paths의 첫 경로, "F/B/L/R" → CmdDir).</summary>
        public static List<CmdDir> TargetPath(IContentDb db, string artId)
        {
            var list = new List<CmdDir>();
            var art = db != null && artId != null ? db.Art(artId) : null;
            if (art == null || art.Paths == null || art.Paths.Count == 0 || art.Paths[0] == null) return list;
            for (int i = 0; i < art.Paths[0].Count; i++)
            {
                switch (art.Paths[0][i])
                {
                    case "F": list.Add(CmdDir.Forward); break;
                    case "B": list.Add(CmdDir.Back); break;
                    case "L": list.Add(CmdDir.Left); break;
                    case "R": list.Add(CmdDir.Right); break;
                }
            }
            return list;
        }

        /// <summary>
        /// 비기 목록: 그 캐릭터 트라이의 비기(tries.json 순서) 중 이 판에서 쓸 수 있는 것(프로필 해금 그대로, U-26) + 섬 기술.
        /// </summary>
        public static List<string> ArtList(IContentDb db, string charId, ICollection<string> unlocked)
        {
            var list = new List<string>();
            var trie = db != null ? db.TrieArts(charId) : null;
            if (trie == null) return list;
            for (int i = 0; i < trie.Count; i++)
                if (unlocked == null || unlocked.Contains(trie[i])) list.Add(trie[i]);
            return list;
        }
    }

    /// <summary>허수아비 수치판(PracticeReport 이벤트의 마지막 값).</summary>
    public sealed class PracticeStats
    {
        public float Dps, RecentDamage, HeatRatio, DamagePerCrystal;
        public bool Any;

        public void Apply(in SimEvent e)
        {
            if (e.Type != SimEventType.PracticeReport || e.Id == null) return;
            Any = true;
            switch (e.Id)
            {
                case PracticeReportKeys.Dps: Dps = e.F; break;
                case PracticeReportKeys.RecentDamage: RecentDamage = e.F; break;
                case PracticeReportKeys.HeatRatio: HeatRatio = e.F; break;
                case PracticeReportKeys.DamagePerCrystal: DamagePerCrystal = e.F; break;
            }
        }

        public void Clear() { Dps = RecentDamage = HeatRatio = DamagePerCrystal = 0f; Any = false; }
    }
}
