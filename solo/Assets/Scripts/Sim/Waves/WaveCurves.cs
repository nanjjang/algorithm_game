using System;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Waves
{
    /// <summary>
    /// 웨이브 곡선(순수 함수). 06 요약서 3.1절. 값은 전부 waves.json(WavesDef)에서 읽는다.
    /// 체력 배율은 벡터(TV-022~029)에 맞게 마이크로 정수(1.0 = 1,000,000)를 매 단계 내림으로 센다.
    /// </summary>
    public static class WaveCurves
    {
        /// <summary>단위 환산: 배율 1.0 = 1,000,000 마이크로.</summary>
        public const long MicroOne = 1000000;
        /// <summary>단위 환산: 100% = 100.</summary>
        public const int PercentOne = 100;

        /// <summary>스폰 예산 B(w). 보스 웨이브에서도 곡선 값을 돌려준다(구성 쪽이 보스 웨이브에는 쓰지 않는다).</summary>
        public static int Budget(WavesDef d, int wave)
        {
            if (wave <= d.BudgetLinear.Until) return d.BudgetLinear.Base + d.BudgetLinear.PerWave * wave;
            int late = d.BudgetLate.Base + d.BudgetLate.PerWave * (wave - d.BudgetLinear.Until);
            return Math.Min(d.BudgetLate.Cap, late);
        }

        public static int BundleCount(WavesDef d, int wave)
        {
            var r = d.BundleRule;
            if (wave <= r.GrowAfter || r.GrowEvery <= 0) return r.Base;
            return Math.Min(r.Max, r.Base + (wave - r.GrowAfter) / r.GrowEvery);
        }

        /// <summary>동시 예고 상한(누적 웨이브 기준). 표에 없으면 상한 없음(int.MaxValue).</summary>
        public static int TelegraphCap(WavesDef d, int wave) => RangeValueDef.Lookup(d.TelegraphCap, wave, int.MaxValue);

        /// <summary>엘리트 수. 보스 웨이브와 표 밖(w &lt; 12)은 0. chiefParadeExtra = 우두머리 행차의 예산 밖 추가 수(없으면 0).</summary>
        public static int EliteCount(WavesDef d, int wave, int chiefParadeExtra = 0)
        {
            if (DayCycle.IsBossWave(wave)) return 0;
            int n = RangeValueDef.Lookup(d.EliteCount, wave, 0);
            return n <= 0 ? 0 : n + chiefParadeExtra;
        }

        /// <summary>변덕 수. 보스 웨이브 0.</summary>
        public static int ModifierCount(WavesDef d, int wave)
        {
            if (DayCycle.IsBossWave(wave)) return 0;
            return RangeValueDef.Lookup(d.ModifierCount, wave, 0);
        }

        /// <summary>체력 배율 H(w)의 마이크로 정수. w ≤ hp_linear_until은 선형, 그 뒤는 매 웨이브 ×rate(내림).</summary>
        public static long HpMicro(WavesDef d, int wave)
        {
            if (wave < 1) wave = 1;
            if (wave <= d.HpLinearUntil) return MicroOne + ToMicro(d.HpLinearStep) * (wave - 1);
            long h = ToMicro(d.HpExpBase);
            long ratePercent = (long)Math.Round((double)d.HpExpRate * PercentOne, MidpointRounding.AwayFromZero);
            if (ratePercent < 1) ratePercent = 1;
            long limit = long.MaxValue / ratePercent;
            for (int i = d.HpLinearUntil; i < wave; i++)
            {
                // 아주 깊은 웨이브: 더 곱하면 넘치므로 여기서 멈춘다(적 체력은 어차피 int 포화 — 10 요약서 6절)
                if (h > limit) return limit;
                h = h * ratePercent / PercentOne;
            }
            return h;
        }

        /// <summary>체력 배율 H(w) 실수 값(Enemies.Spawn이 곱한다).</summary>
        public static float HpMul(WavesDef d, int wave) => (float)((double)HpMicro(d, wave) / MicroOne);

        /// <summary>적 최대 체력의 정수식(06 요약서 3.1절). hpMulPercent = 두꺼운 가죽 130, 없으면 100.</summary>
        public static int EnemyHp(WavesDef d, float baseHp, int wave, bool elite, int hpMulPercent = PercentOne)
        {
            decimal v = (decimal)baseHp * HpMicro(d, wave) * (elite ? (decimal)d.EliteHpMul : decimal.One) * hpMulPercent
                        / ((decimal)MicroOne * PercentOne);
            return ClampToInt(decimal.Floor(v));
        }

        /// <summary>보스 체력 = 기본 × H(w) / H(기준 웨이브)(내림).</summary>
        public static int BossHp(WavesDef d, float baseHp, int wave, int anchorWave)
        {
            if (anchorWave <= 0) anchorWave = d.BossHpRefWave;
            decimal v = (decimal)baseHp * HpMicro(d, wave) / HpMicro(d, anchorWave);
            return ClampToInt(decimal.Floor(v));
        }

        /// <summary>배율(1.3) → 백분율 정수(130).</summary>
        public static int ToPercent(float multiplier) => (int)Math.Round((double)multiplier * PercentOne, MidpointRounding.AwayFromZero);

        static long ToMicro(float v) => (long)Math.Round((double)v * MicroOne, MidpointRounding.AwayFromZero);

        static int ClampToInt(decimal v)
        {
            if (v < decimal.One) return 1;
            if (v > int.MaxValue) return int.MaxValue;
            return (int)v;
        }
    }
}
