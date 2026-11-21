using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Settlement
{
    /// <summary>점수 내역 한 줄(열쇠 = str.settlement.score.*). Total이면 합계 줄.</summary>
    public struct ScoreRow
    {
        public string Key;
        public long Value;
        public bool Total;

        public ScoreRow(string key, long value, bool total = false) { Key = key; Value = value; Total = total; }
        public override string ToString() => Key + " " + Value;
    }

    /// <summary>
    /// 정산 화면의 순수 규칙(엔진 없음). 계산은 정산 서비스(ISettlementService)가 하고, 여기는 "무엇을 어떤 줄로 보이나"만 정한다.
    /// </summary>
    public static class SettlementRules
    {
        /// <summary>가방 칸 한 줄의 칸 수(08 요약서 9.2절 "한 줄 8칸", TV-156).</summary>
        public const int BagSlotsPerRow = 8;

        /// <summary>
        /// 점수 내역 줄(08 요약서 9.1절, 01 요약서 10.6절). 처치·보스·클리어·빠른 처리·무피격·스침·변덕 배율은 0이어도 늘 그린다(값 0).
        /// "허상과의 첫 만남" 줄은 첫 만남이 든 판(점수가 있거나 이 판에서 첫 만남을 치름)에만, "건너뛴 웨이브" 줄은
        /// 시작 웨이브가 1이 아닌 판(또는 건너뛴 점수가 있을 때)에만 그린다. 합계는 맨 끝.
        /// </summary>
        public static List<ScoreRow> ScoreRows(ScoreBreakdown s, bool phantomFirstRun, int startWave)
        {
            var rows = new List<ScoreRow>
            {
                new ScoreRow(SettlementNameKeys.ScoreKills, s.Kills),
                new ScoreRow(SettlementNameKeys.ScoreBoss, s.Boss),
                new ScoreRow(SettlementNameKeys.ScoreClear, s.Clear),
                new ScoreRow(SettlementNameKeys.ScoreFast, s.Fast),
                new ScoreRow(SettlementNameKeys.ScoreNoHit, s.NoHit),
                new ScoreRow(SettlementNameKeys.ScoreGraze, s.Graze),
                new ScoreRow(SettlementNameKeys.ScoreVariance, s.Variance),
            };
            if (s.PhantomFirst > 0 || phantomFirstRun) rows.Add(new ScoreRow(SettlementNameKeys.ScorePhantomFirst, s.PhantomFirst));
            if (s.Skipped > 0 || startWave > 1) rows.Add(new ScoreRow(SettlementNameKeys.ScoreSkipped, s.Skipped));
            rows.Add(new ScoreRow(SettlementNameKeys.ScoreTotal, s.Total, true));
            return rows;
        }

        /// <summary>
        /// 정산 서비스가 없을 때(ctx.Settlement == null) 요약만으로 낸 점수 내역: 웨이브별 내역의 합 + 건너뛴 점수.
        /// 합계는 요약의 Score와 합 중 큰 쪽(서비스와 같은 규칙).
        /// </summary>
        public static ScoreBreakdown SumScore(RunSummary s)
        {
            var total = default(ScoreBreakdown);
            if (s == null) return total;
            long sum = 0;
            if (s.WaveResults != null)
                for (int i = 0; i < s.WaveResults.Count; i++)
                {
                    WaveResult r = s.WaveResults[i];
                    if (r == null) continue;
                    ScoreBreakdown b = r.Score;
                    total.Kills += b.Kills; total.Boss += b.Boss; total.Clear += b.Clear; total.Fast += b.Fast;
                    total.NoHit += b.NoHit; total.Graze += b.Graze; total.Variance += b.Variance; total.PhantomFirst += b.PhantomFirst;
                    sum += b.Total;
                }
            sum += s.SkippedScore;
            if (s.Score > sum) sum = s.Score;
            total.Skipped = s.SkippedScore;
            total.Total = (int)sum;
            return total;
        }

        /// <summary>기록 줄에 쓰는 웨이브: 도달 웨이브(마지막으로 시작한 웨이브, D-22). 출발 야영에서 끝났으면 시작 웨이브.</summary>
        public static int DisplayWave(RunSummary s)
        {
            if (s == null) return 1;
            if (s.ReachedWave > 0) return s.ReachedWave;
            return s.StartWave > 1 ? s.StartWave : 1;
        }

        /// <summary>"웨이브 {start}에서 출발" 줄을 그리나(출발 웨이브가 1이 아닐 때, TV-161).</summary>
        public static bool ShowStartWave(RunSummary s) => s != null && s.StartWave > 1;

        /// <summary>가방 칸을 한 줄 8칸으로 나눈 줄마다의 칸 수(12 → 8, 4 / 3 → 3 / 0 → 없음).</summary>
        public static List<int> BagRows(int slots)
        {
            var rows = new List<int>();
            for (int left = slots; left > 0; left -= BagSlotsPerRow)
                rows.Add(left < BagSlotsPerRow ? left : BagSlotsPerRow);
            return rows;
        }

        /// <summary>블록의 등급: 요약에 적힌 값, 없으면 블록 표.</summary>
        public static string RarityOf(IContentDb db, WildBlock b)
        {
            if (b == null) return Ids.Rarity.Common;
            if (!string.IsNullOrEmpty(b.Rarity)) return b.Rarity;
            BlockDef def = db != null ? db.Block(b.BlockId) : null;
            return def != null && !string.IsNullOrEmpty(def.Rarity) ? def.Rarity : Ids.Rarity.Common;
        }

        /// <summary>
        /// 담지 못한 블록의 골드 미리보기(08 요약서 9.2절, TV-160): 판 끝에 든 야생 블록(서고 블록 제외) 중 실제로 서고에 들어갈 것
        /// (고른 순서대로 서고 남은 자리까지 — TV-164)을 뺀 나머지를 drops.json 값(일반 5, 희귀 15, 영웅 50, 빌린 블록 50)으로 더한다.
        /// </summary>
        public static long LeftoverGold(IContentDb db, IReadOnlyList<WildBlock> wild, IReadOnlyList<int> pickedUids, int libraryRoom)
        {
            DropsConfig drops = db != null ? db.Drops : null;
            if (drops == null || wild == null) return 0;
            int kept = pickedUids != null ? pickedUids.Count : 0;
            if (kept > libraryRoom) kept = libraryRoom < 0 ? 0 : libraryRoom;
            long gold = 0;
            for (int i = 0; i < wild.Count; i++)
            {
                WildBlock b = wild[i];
                if (b == null || b.FromLibrary) continue;
                if (IndexIn(pickedUids, b.Uid, kept) >= 0) continue;
                gold += drops.DiscardGold(RarityOf(db, b), b.Borrowed);
            }
            return gold;
        }

        static int IndexIn(IReadOnlyList<int> list, int uid, int count)
        {
            if (list == null) return -1;
            for (int i = 0; i < count && i < list.Count; i++) if (list[i] == uid) return i;
            return -1;
        }

        /// <summary>
        /// 받은 재료를 3쪽의 줄로 나눈다(08 요약서 9.3절): 계정에 묶인 것(복수 보상) / 까마귀 보따리 / 확정 재료 / 꾸러미(추첨분).
        /// </summary>
        public static void SplitMaterials(IReadOnlyList<MaterialGain> all, List<MaterialGain> bound, List<MaterialGain> crow,
            List<MaterialGain> confirmed, List<MaterialGain> bundle)
        {
            if (all == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                MaterialGain g = all[i];
                if (g == null || g.Qty <= 0) continue;
                if (g.Bound) bound?.Add(g);
                else if (g.Source == DropChanceSource.CrowBundle) crow?.Add(g);
                else if (g.Guaranteed) confirmed?.Add(g);
                else bundle?.Add(g);
            }
        }

        /// <summary>새 칭호 줄: 복수 보상 줄에 이미 나온 칭호(거울을 깬)는 빼고 나머지.</summary>
        public static List<string> OtherTitles(SettlementResult r)
        {
            var list = new List<string>();
            if (r == null || r.NewTitles == null) return list;
            for (int i = 0; i < r.NewTitles.Count; i++)
            {
                string t = r.NewTitles[i];
                if (string.IsNullOrEmpty(t)) continue;
                if (r.RevengeReward && t == Ids.Title.MirrorBreaker) continue;
                if (!list.Contains(t)) list.Add(t);
            }
            return list;
        }
    }
}
