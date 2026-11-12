using System;
using System.Collections.Generic;
using Wizard.Content;

namespace Wizard.DataBuild.Validate.Affinity
{
    // IAffinitySource 들이 모은 줄과 묶음마다 다 찼을 때의 종 수(로스터). 로스터는 id 열거형(spec 14 3.6·3.8, CONTRACTS 2.1)에서 센다:
    //   일반 적 = EnemyTypeId 가운데 None 이 아니고 졸개 번호(101~) 앞의 값, 보스 = BossId 가운데 None 이 아닌 값
    public sealed class AffinityTable
    {
        // spec 14 3.6: 일반 적은 1~, 졸개는 101~
        public const int MinionCodeStart = 101;

        public AffinityTable()
            : this(CountEnemySpecies(), CountBosses())
        {
        }

        public AffinityTable(int enemyRoster, int bossRoster)
        {
            EnemyRoster = enemyRoster;
            BossRoster = bossRoster;
        }

        public List<AffinityRow> Rows { get; } = new List<AffinityRow>();

        public int EnemyRoster { get; }

        public int BossRoster { get; }

        public int Roster(AffinityGroup group) => group == AffinityGroup.Enemy ? EnemyRoster : BossRoster;

        // 다섯 속성(None 제외)을 열거형 값 순서로
        public static IReadOnlyList<Element> Elements()
        {
            var list = new List<Element>();
            foreach (Element e in (Element[])Enum.GetValues(typeof(Element)))
            {
                if (e != Element.None)
                {
                    list.Add(e);
                }
            }
            list.Sort((a, b) => ((byte)a).CompareTo((byte)b));
            return list;
        }

        public static int CountEnemySpecies()
        {
            int n = 0;
            foreach (EnemyTypeId id in (EnemyTypeId[])Enum.GetValues(typeof(EnemyTypeId)))
            {
                if (id != EnemyTypeId.None && (ushort)id < MinionCodeStart)
                {
                    n++;
                }
            }
            return n;
        }

        public static int CountBosses()
        {
            int n = 0;
            foreach (BossId id in (BossId[])Enum.GetValues(typeof(BossId)))
            {
                if (id != BossId.None)
                {
                    n++;
                }
            }
            return n;
        }
    }
}
