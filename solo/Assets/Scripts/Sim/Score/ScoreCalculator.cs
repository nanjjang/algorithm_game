using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Score
{
    /// <summary>
    /// 점수 규칙(01 요약서 6절, 06 요약서 6절). 순수 함수이고 상태가 없다. 전부 정수로 셈한다.
    /// 수치는 scoring.json, waves.json(예산 곡선), modifiers.json(변덕 배율), DataSolo/G11.json(건너뛴 점수 비율·첫 만남 웨이브).
    /// </summary>
    public sealed class ScoreCalculator : ScoreRules
    {
        /// <summary>보충 수치 조각 이름(DataSolo/G11.json).</summary>
        public const string ExtraName = "G11";
        const string KeySkipPercent = "skip_score_percent";
        const string KeyPhantomFirstWave = "phantom_first_wave";

        // 단위 환산: 틱 → 1/60점(빠른 처리), 웨이브 계수의 10배 정수, 백분율.
        const int TicksPerSecond = SimConst.TicksPerSecond;
        const int CoefScale = 10;
        const int PercentScale = 100;

        /// <summary>웨이브 계수의 10배 정수 S10(w) = 9 + w (scoring.json wave_coef에서).</summary>
        public static int S10(ScoringDef sc, int wave)
        {
            int at = Mathf.RoundToInt(sc.WaveCoef.AtWave1 * CoefScale);
            int per = Mathf.RoundToInt(sc.WaveCoef.PerWave * CoefScale);
            return at + per * (wave - 1);
        }

        /// <summary>웨이브 예산 B(w)(waves.json budget_linear / budget_late). 건너뛴 점수의 처치분에 쓴다.</summary>
        public static int Budget(WavesDef wv, int wave)
        {
            if (wave <= wv.BudgetLinear.Until) return wv.BudgetLinear.Base + wv.BudgetLinear.PerWave * wave;
            int b = wv.BudgetLate.Base + wv.BudgetLate.PerWave * (wave - wv.BudgetLinear.Until);
            return b < wv.BudgetLate.Cap ? b : wv.BudgetLate.Cap;
        }

        static int ExtraInt(IContentDb db, string key)
        {
            var extra = db.SoloExtra(ExtraName);
            if (extra == null) return 0;
            var t = extra[key];
            return t != null ? (int)t : 0;
        }

        /// <summary>
        /// 직접 넘긴 웨이브의 점수(01 요약서 6.2절). breakdown의 줄은 웨이브 계수를 곱한 값이고
        /// Kills + Boss + Clear + Fast + NoHit + Graze + Variance = Total 이 되게 채운다
        /// (줄마다 따로 내리면 합이 어긋나므로 내림에서 남은 점수는 처치 줄에 얹는다).
        /// </summary>
        public override int WaveScore(IContentDb db, WaveStats stats, out ScoreBreakdown breakdown)
        {
            breakdown = default;
            if (db == null || stats == null) return 0;
            ScoringDef sc = db.Scoring;
            if (sc == null) return 0;

            int w = stats.Wave;
            if (stats.PhantomFirst && w == ExtraInt(db, KeyPhantomFirstWave))
            {
                breakdown.PhantomFirst = sc.PhantomFirst;
                breakdown.Total = sc.PhantomFirst;
                return sc.PhantomFirst;
            }

            bool boss = stats.IsBoss || DayCycle.IsBossWave(w);
            long s10 = S10(sc, w);
            long div = (long)TicksPerSecond * CoefScale;

            // 전부 1/60점 단위. KillScoreX2는 처치 원점수의 2배라 × 60 ÷ 2.
            long kill60 = (long)stats.KillScoreX2 * (TicksPerSecond / 2);
            long boss60 = stats.BossKilled ? (long)sc.BossKill * TicksPerSecond : 0;
            long clear60 = (long)sc.WaveClear * TicksPerSecond;
            long nohit60 = stats.NoHit ? (long)sc.NoHit * TicksPerSecond : 0;
            int grazeCap = boss ? sc.Graze.BossCap : sc.Graze.Cap;
            int grazes = stats.Grazes < grazeCap ? stats.Grazes : grazeCap;
            if (grazes < 0) grazes = 0;
            long graze60 = (long)sc.Graze.PerGraze * grazes * TicksPerSecond;
            long targetTicks = (long)(boss ? sc.FastClear.BossTargetSeconds : sc.FastClear.TargetSeconds) * TicksPerSecond;
            long left = targetTicks - stats.ElapsedTicks;
            long fast60 = left > 0 ? left * sc.FastClear.PerSecond : 0;

            long base60 = kill60 + boss60 + clear60 + nohit60 + graze60 + fast60;
            long s = base60 * s10 / div;                       // 한 번만 나눠 내림

            breakdown.Boss = (int)(boss60 * s10 / div);
            breakdown.Clear = (int)(clear60 * s10 / div);
            breakdown.NoHit = (int)(nohit60 * s10 / div);
            breakdown.Graze = (int)(graze60 * s10 / div);
            breakdown.Fast = (int)(fast60 * s10 / div);
            breakdown.Kills = (int)(s - breakdown.Boss - breakdown.Clear - breakdown.NoHit - breakdown.Graze - breakdown.Fast);

            long final = s;
            for (int i = 0; i < stats.Whims.Count; i++)        // 변덕 배율은 차례로 내림
            {
                ModifierDef m = db.Modifier(stats.Whims[i]);
                if (m == null) continue;
                int pct = Mathf.RoundToInt(m.ScoreMultiplier * PercentScale);
                final = final * pct / PercentScale;
            }
            breakdown.Variance = (int)(final - s);
            breakdown.Total = (int)final;
            return (int)final;
        }

        /// <summary>건너뛴 웨이브 하나의 점수(01 요약서 6.3절). 웨이브마다 내린다.</summary>
        public override int SkipScore(IContentDb db, int wave)
        {
            if (db == null || wave <= 0) return 0;
            ScoringDef sc = db.Scoring;
            WavesDef wv = db.Waves;
            if (sc == null || wv == null) return 0;
            long raw = DayCycle.IsBossWave(wave)
                ? sc.BossKill + sc.WaveClear
                : (long)Budget(wv, wave) * sc.KillPerCost + sc.WaveClear;
            long div = (long)CoefScale * PercentScale;
            return (int)(raw * S10(sc, wave) * ExtraInt(db, KeySkipPercent) / div);
        }
    }
}
