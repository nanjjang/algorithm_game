using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;

namespace Wizard.Solo.Tests.Data
{
    /// <summary>drops.json 재료 표(06 요약서 5.2절 6.3-A~D)가 형으로 읽히고 요약서의 bp 식과 벡터를 따른다.</summary>
    public class DropsMaterialsTests
    {
        static MaterialsDef M => TestContent.Db.Drops.MaterialTable;

        static List<MaterialRowDef> Rows(List<MaterialTierDef> tiers, int day)
        {
            var t = MaterialTierDef.ForDay(tiers, day);
            return t != null ? t.Rows : new List<MaterialRowDef>();
        }

        static int Fixed(List<MaterialRowDef> rows, string item)
        {
            int n = 0;
            foreach (var r in rows) if (r.Item == item && r.IsFixed) n += r.Fixed;
            return n;
        }

        static int Bp(List<MaterialRowDef> rows, string item, int day, bool extra = false)
        {
            foreach (var r in rows) if (r.Item == item && !r.IsFixed && r.IsExtra == extra) return r.ChanceBp(day);
            return 0;
        }

        [Test]
        public void S07_Materials__TableIsTyped()
        {
            Assert.IsNotNull(TestContent.Db.Drops.Materials);
            Assert.IsNotNull(M);
            Assert.AreEqual(5, M.FifthWaveSlot);
            Assert.AreEqual(12, M.EliteMinWave);
            Assert.IsNotEmpty(M.FifthWave);
            Assert.IsNotEmpty(M.BossKill);
            Assert.IsNotEmpty(M.EliteKill);
        }

        // A. 까마귀 보따리(TV-250·251): 처음 웨이브 3을 넘긴 판 → 하급 3 확정
        [Test]
        public void S07_TV_250__CrowBundleThreeLowStones()
        {
            Assert.AreEqual(3, M.CrowBundle.Wave);
            Assert.AreEqual(3, Fixed(M.CrowBundle.Rows, Ids.Item.StoneLow));
            Assert.AreEqual(1, M.CrowBundle.Rows.Count);
        }

        // B. 다섯 번째 웨이브
        [Test]
        public void S07_FifthWave__ByDay()
        {
            var d1 = Rows(M.FifthWave, 1);
            Assert.AreEqual(1, Fixed(d1, Ids.Item.StoneLow));
            Assert.AreEqual(0, Fixed(d1, Ids.Item.StoneMid));

            var d2 = Rows(M.FifthWave, 2);
            Assert.AreEqual(1, Fixed(d2, Ids.Item.StoneLow));
            Assert.AreEqual(1, Fixed(d2, Ids.Item.StoneMid));

            // TV-252 꼴: w25(d3) → 중급 1 확정 + 상급 500bp
            var d3 = Rows(M.FifthWave, 3);
            Assert.AreEqual(0, Fixed(d3, Ids.Item.StoneLow));
            Assert.AreEqual(1, Fixed(d3, Ids.Item.StoneMid));
            Assert.AreEqual(500, Bp(d3, Ids.Item.StoneHigh, 3));
            Assert.AreEqual(1000, Bp(Rows(M.FifthWave, 4), Ids.Item.StoneHigh, 4));
            Assert.AreEqual(2500, Bp(Rows(M.FifthWave, 7), Ids.Item.StoneHigh, 7));
            Assert.AreEqual(2500, Bp(Rows(M.FifthWave, 12), Ids.Item.StoneHigh, 12));
        }

        // C. 보스 처치
        [Test]
        public void S07_BossKill__ByDay()
        {
            var d1 = Rows(M.BossKill, 1);
            Assert.AreEqual(1, Fixed(d1, Ids.Item.StoneLow));
            Assert.AreEqual(5000, Bp(d1, Ids.Item.StoneMid, 1));
            Assert.AreEqual(200, Bp(d1, Ids.Item.Charm, 1));

            var d2 = Rows(M.BossKill, 2);
            Assert.AreEqual(0, Fixed(d2, Ids.Item.StoneLow));
            Assert.AreEqual(1, Fixed(d2, Ids.Item.StoneMid));
            Assert.AreEqual(2000, Bp(d2, Ids.Item.StoneHigh, 2));
            Assert.AreEqual(400, Bp(d2, Ids.Item.Charm, 2));

            var d3 = Rows(M.BossKill, 3);
            Assert.AreEqual(1, Fixed(d3, Ids.Item.StoneMid));
            Assert.AreEqual(1, Fixed(d3, Ids.Item.StoneHigh));
            Assert.AreEqual(0, Bp(d3, Ids.Item.StoneHigh, 3, extra: true));   // d=3이면 추첨 생략
            Assert.AreEqual(600, Bp(d3, Ids.Item.Charm, 3));

            // TV 예: w40(d4) → 상급 추가 1000bp, 부적 800bp
            var d4 = Rows(M.BossKill, 4);
            Assert.AreEqual(1000, Bp(d4, Ids.Item.StoneHigh, 4, extra: true));
            Assert.AreEqual(800, Bp(d4, Ids.Item.Charm, 4));
            Assert.AreEqual(5000, Bp(Rows(M.BossKill, 9), Ids.Item.StoneHigh, 9, extra: true));
            Assert.AreEqual(1000, Bp(Rows(M.BossKill, 6), Ids.Item.Charm, 6));
        }

        // TV-257: 첫 만남(프로필 첫 w10) → 하급 1 확정만, 추첨 없음
        [Test]
        public void S07_TV_257__PhantomFirstOnlyOneLowStone()
        {
            Assert.AreEqual(1, M.PhantomFirst.Count);
            Assert.AreEqual(1, Fixed(M.PhantomFirst, Ids.Item.StoneLow));
            foreach (var r in M.PhantomFirst) Assert.IsTrue(r.IsFixed);
        }

        // D. 엘리트 1마리
        [Test]
        public void S07_EliteKill__ByDay()
        {
            Assert.IsNull(MaterialTierDef.ForDay(M.EliteKill, 1));
            var d2 = Rows(M.EliteKill, 2);
            Assert.AreEqual(1000, Bp(d2, Ids.Item.StoneLow, 2));
            Assert.AreEqual(800, Bp(d2, Ids.Item.StoneMid, 2));
            Assert.AreEqual(0, Bp(d2, Ids.Item.Charm, 2));

            var d3 = Rows(M.EliteKill, 3);
            Assert.AreEqual(500, Bp(d3, Ids.Item.StoneMid, 3));
            Assert.AreEqual(200, Bp(d3, Ids.Item.StoneHigh, 3));
            Assert.AreEqual(50, Bp(d3, Ids.Item.Charm, 3));
            // TV 예: w45(d5) → 상급 300bp
            Assert.AreEqual(300, Bp(Rows(M.EliteKill, 5), Ids.Item.StoneHigh, 5));
            Assert.AreEqual(500, Bp(Rows(M.EliteKill, 9), Ids.Item.StoneHigh, 9));
        }

        // TV-261: 하급 강화석은 웨이브 21(d3)부터 나오지 않는다
        [Test]
        public void S07_TV_261__NoLowStoneFromDayThree()
        {
            for (int day = 3; day <= 10; day++)
            {
                foreach (var tiers in new[] { M.FifthWave, M.BossKill, M.EliteKill })
                    foreach (var r in Rows(tiers, day))
                        Assert.AreNotEqual(Ids.Item.StoneLow, r.Item, "day " + day);
            }
        }

        // 줄 순서 고정: 하급, 중급, 상급, 상급 추가, 부적
        [Test]
        public void S07_Materials__RowOrderFixed()
        {
            string[] order = { Ids.Item.StoneLow, Ids.Item.StoneMid, Ids.Item.StoneHigh, Ids.Item.Charm };
            foreach (var tiers in new[] { M.FifthWave, M.BossKill, M.EliteKill })
                foreach (var t in tiers)
                {
                    int last = -1;
                    bool seenExtra = false;
                    foreach (var r in t.Rows)
                    {
                        int rank = System.Array.IndexOf(order, r.Item);
                        Assert.GreaterOrEqual(rank, 0, r.Item);
                        int key = rank * 2 + (r.IsExtra ? 1 : 0);
                        if (r.Item == Ids.Item.Charm) key = order.Length * 2;
                        Assert.GreaterOrEqual(key, last, "tier day " + t.DayFrom);
                        last = key;
                        if (r.IsExtra) seenExtra = true;
                    }
                    if (seenExtra) Assert.AreEqual(3, t.DayFrom);
                }
        }

        [Test]
        public void S07_MaterialRow__ChanceBpFormula()
        {
            var row = new MaterialRowDef { Item = Ids.Item.StoneHigh, BpBase = 0, BpPerDay = 1000, BpDayOffset = 3, BpCap = 5000 };
            Assert.AreEqual(0, row.ChanceBp(2));
            Assert.AreEqual(0, row.ChanceBp(3));
            Assert.AreEqual(1000, row.ChanceBp(4));
            Assert.AreEqual(5000, row.ChanceBp(20));
            var fixedRow = new MaterialRowDef { Item = Ids.Item.StoneLow, Fixed = 1, BpBase = 9999, BpCap = 9999 };
            Assert.IsTrue(fixedRow.IsFixed);
            Assert.AreEqual(0, fixedRow.ChanceBp(5));
        }
    }
}
