using System.Collections.Generic;

namespace Wizard.BalanceLab.Feel
{
    // 임계 구간 봇의 기대값을 세계 없이 계산하는 독립 모형(자체 점검의 기준). spec 01 7.3 발사 절차와 7.4 냉각의 의사코드를 그대로 옮겼다.
    // 세계의 Heat 모듈과 봇 코드(FireRatioMeter)를 건드리지 않는 별도 구현이라, 둘 가운데 어느 쪽이 명세에서 벗어나면 값이 갈라진다.
    // 피해는 "기본 피해 단위"로 센다(일반 1, 임계 critMul ÷ 1000).
    internal static class CritZoneModel
    {
        internal sealed class Result
        {
            public int Cycles;
            public long WindowTicks;
            public long WindowShots;
            public long WindowCritShots;
            public long WindowDamageMilli;    // 기본 피해의 1/1000 단위(일반 1,000, 임계 critMulPermille)
        }

        public static Result Run(uint ticks, uint startTick, int shotHeatHu, int intervalTicks, int coolDelayTicks, int coolHuPerTick,
                                 int critStartHu, int maxHeatHu, int critMulPermille)
        {
            int heat = 0;
            long lastShot = -1;
            long fireReady = 0;
            bool firing = true;
            var boundaries = new List<long>();
            var shotTicks = new List<long>();
            var shotCrit = new List<bool>();

            for (long t = startTick; t < ticks; t++)
            {
                // 판단(틱 시작의 열로)
                if (firing && heat + shotHeatHu > maxHeatHu)
                {
                    firing = false;
                }
                else if (!firing && heat - coolHuPerTick < critStartHu - shotHeatHu)
                {
                    firing = true;
                    boundaries.Add(t);
                }
                // 7.3: J 를 누르고 있고 쏠 수 있으면 쏜다
                bool shotThisTick = false;
                if (firing && t >= fireReady)
                {
                    heat += shotHeatHu;
                    bool crit = heat >= critStartHu;
                    shotTicks.Add(t);
                    shotCrit.Add(crit);
                    lastShot = t;
                    fireReady = t + intervalTicks;
                    shotThisTick = true;
                }
                // 7.4: 이번 틱에 쏘지 않았고 마지막 발사의 간격 + 대기가 지났으면 틱 끝에 식는다
                if (!shotThisTick && lastShot >= 0 && t >= lastShot + intervalTicks + coolDelayTicks)
                {
                    heat = heat > coolHuPerTick ? heat - coolHuPerTick : 0;
                }
            }

            var r = new Result();
            if (boundaries.Count < 2)
            {
                return r;
            }
            long start = boundaries[0];
            long end = boundaries[boundaries.Count - 1];
            r.Cycles = boundaries.Count - 1;
            r.WindowTicks = end - start;
            for (int i = 0; i < shotTicks.Count; i++)
            {
                if (shotTicks[i] >= start && shotTicks[i] < end)
                {
                    r.WindowShots++;
                    if (shotCrit[i])
                    {
                        r.WindowCritShots++;
                        r.WindowDamageMilli += critMulPermille;
                    }
                    else
                    {
                        r.WindowDamageMilli += 1000;
                    }
                }
            }
            return r;
        }
    }
}
