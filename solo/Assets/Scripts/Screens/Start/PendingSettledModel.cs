using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Start
{
    public enum PendingLineKind { Message = 0, Gold = 1, Material = 2, Block = 3, CrowBundle = 4, Title = 5, RevengeReward = 6, Art = 7, Nothing = 8 }

    /// <summary>알림 창의 한 줄(글을 만들기 전 꼴 — 테스트가 이름 표 없이도 단언한다).</summary>
    public sealed class PendingLine
    {
        public PendingLineKind Kind;
        public string Key;
        /// <summary>그 줄이 가리키는 콘텐츠 id(재료·블록·칭호·비기). 아이콘과 {name}에 쓴다.</summary>
        public string ContentId;
        public long N;
    }

    /// <summary>
    /// 자동 정산 알림(00 문서 D-68, D-23; 08 요약서 13.1절) — SettlementResult → 줄 목록.
    /// 첫 줄은 언제나 "지난 실행이 갑자기 끝나 중간 지점까지 정산했다"(받은 것이 없어도 알림 한 줄은 나온다).
    /// 그 뒤 받은 것: 골드, 재료(같은 id는 합침), 가방에 담은 블록, 까마귀 보따리, 새 칭호, 복수 보상, 새 비기. 하나도 없으면 "받은 것은 없다".
    /// </summary>
    public static class PendingSettledModel
    {
        public static List<PendingLine> Lines(SettlementResult r)
        {
            var lines = new List<PendingLine>();
            int wave = r != null && r.Record != null ? r.Record.ReachedWave : 0;
            lines.Add(wave > 0
                ? new PendingLine { Kind = PendingLineKind.Message, Key = StartNameKeys.PendingMessage, N = wave }
                : new PendingLine { Kind = PendingLineKind.Message, Key = StartNameKeys.PendingMessageNoWave });
            if (r == null)
            {
                lines.Add(new PendingLine { Kind = PendingLineKind.Nothing, Key = StartNameKeys.PendingNothing });
                return lines;
            }
            int gains = 0;
            if (r.GoldGained > 0)
            {
                lines.Add(new PendingLine { Kind = PendingLineKind.Gold, Key = StartNameKeys.PendingGold, N = r.GoldGained, ContentId = "gold" });
                gains++;
            }
            if (r.Materials != null)
            {
                var order = new List<string>();
                var sum = new Dictionary<string, long>();
                for (int i = 0; i < r.Materials.Count; i++)
                {
                    var g = r.Materials[i];
                    if (g == null || string.IsNullOrEmpty(g.ItemId) || g.Qty <= 0) continue;
                    if (!sum.ContainsKey(g.ItemId)) { sum[g.ItemId] = 0; order.Add(g.ItemId); }
                    sum[g.ItemId] += g.Qty;
                }
                for (int i = 0; i < order.Count; i++)
                {
                    lines.Add(new PendingLine { Kind = PendingLineKind.Material, Key = StartNameKeys.PendingMaterial, ContentId = order[i], N = sum[order[i]] });
                    gains++;
                }
            }
            if (r.Bagged != null)
                for (int i = 0; i < r.Bagged.Count; i++)
                {
                    var b = r.Bagged[i];
                    if (b == null || string.IsNullOrEmpty(b.BlockId)) continue;
                    lines.Add(new PendingLine { Kind = PendingLineKind.Block, Key = StartNameKeys.PendingBlock, ContentId = b.BlockId, N = 1 });
                    gains++;
                }
            if (r.CrowBundle)
            {
                lines.Add(new PendingLine { Kind = PendingLineKind.CrowBundle, Key = StartNameKeys.PendingCrowBundle, ContentId = "item.crow_bundle" });
                gains++;
            }
            if (r.NewTitles != null)
                for (int i = 0; i < r.NewTitles.Count; i++)
                {
                    if (string.IsNullOrEmpty(r.NewTitles[i])) continue;
                    lines.Add(new PendingLine { Kind = PendingLineKind.Title, Key = StartNameKeys.PendingTitleLine, ContentId = r.NewTitles[i] });
                    gains++;
                }
            if (r.RevengeReward)
            {
                lines.Add(new PendingLine { Kind = PendingLineKind.RevengeReward, Key = StartNameKeys.PendingRevengeReward });
                gains++;
            }
            if (r.NewArts != null)
                for (int i = 0; i < r.NewArts.Count; i++)
                {
                    if (string.IsNullOrEmpty(r.NewArts[i])) continue;
                    lines.Add(new PendingLine { Kind = PendingLineKind.Art, Key = StartNameKeys.PendingArt, ContentId = r.NewArts[i] });
                    gains++;
                }
            if (gains == 0) lines.Add(new PendingLine { Kind = PendingLineKind.Nothing, Key = StartNameKeys.PendingNothing });
            return lines;
        }

        /// <summary>한 줄의 글(이름 표를 거친 것).</summary>
        public static string Text(PendingLine l, INameTable names)
        {
            if (l == null) return string.Empty;
            if (names == null) return l.Key;
            switch (l.Kind)
            {
                case PendingLineKind.Message:
                    return l.N > 0 ? names.Format(l.Key, "wave", l.N) : names.Get(l.Key);
                case PendingLineKind.Gold:
                    return names.Format(l.Key, "n", names.Number(l.N));
                case PendingLineKind.Material:
                    return names.Format(l.Key, "name", names.Get(l.ContentId), "n", names.Number(l.N));
                case PendingLineKind.Block:
                case PendingLineKind.Title:
                case PendingLineKind.Art:
                    return names.Format(l.Key, "name", names.Get(l.ContentId));
                default:
                    return names.Get(l.Key);
            }
        }

        public static List<string> Render(SettlementResult r, INameTable names)
        {
            var lines = Lines(r);
            var outp = new List<string>(lines.Count);
            for (int i = 0; i < lines.Count; i++) outp.Add(Text(lines[i], names));
            return outp;
        }
    }
}
