using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Player
{
    /// <summary>
    /// 플레이어 모듈이 읽는 수치를 한곳에 모은 것. 출처: solo/const.json(SoloConstDef), solo/mutations.json,
    /// tries.json(빚 상한), Resources/DataSolo/G01.json(Def에 없는 값). 값을 바꾸지 않고 단위만 틱·HU·u로 옮긴다.
    /// </summary>
    public sealed class PlayerTuning
    {
        /// <summary>G01.json의 조각 이름(IContentDb.SoloExtra).</summary>
        public const string ExtraName = "G01";
        /// <summary>열 눈금의 끝(열은 0~100으로 정의된 단위다. 02 요약서 0절·6.1절). 100 초과가 잠금.</summary>
        public const int HeatScaleMax = 100;
        public const int HeatMaxHu = HeatScaleMax * SimConst.HuPerHeat;

        public int MaxInputEventsPerTick;
        public long DiagonalHoldU;
        public int ComboIdleTicks;
        public int ComboPerShotCap;
        public int ComboCritGain;
        public int GrazeComboGain;
        public float MirrorHeatMul = 1f;
        public int UnstableCoreLockTicks;
        public int MaxDebt;
        /// <summary>Imminent가 시작되는 열 = 임계 시작 − 이 값(HU).</summary>
        public int RingImminentBelowCritHu;
        /// <summary>Brink가 시작되는 열(HU). 값이 없으면 열 끝(= Brink는 정확히 100에서만).</summary>
        public int RingBrinkFromHu = HeatMaxHu;
        public int DebtExtraDamageCells;
        public int GrazeCastForgetTicks;
        /// <summary>
        /// 솔로 조정(G01 graze_heat_relief, 기본 0 = 명세 그대로): 임계 구간 안(열 &gt; 임계 시작)에서 스치면 열을 이만큼(HU) 덜어 낸다.
        /// 임계 시작 아래로는 내리지 않는다 → 스침이 임계 몰아 쏘기를 늘려 준다. 낮을 때 올리기·잠금 깨기(02 요약서 7.2절)는 그대로.
        /// </summary>
        public int GrazeHeatReliefHu;

        public static PlayerTuning From(IContentDb db)
        {
            var t = new PlayerTuning();
            if (db == null) return t;
            var c = db.Const;
            if (c != null)
            {
                t.MaxInputEventsPerTick = c.MaxInputEventsPerTick;
                t.DiagonalHoldU = SimConst.Units(c.DiagonalHoldSeconds);
                t.ComboIdleTicks = SimConst.Ticks(c.ComboIdleSeconds);
                t.ComboPerShotCap = c.ComboPerShotCap;
                t.ComboCritGain = c.ComboCritGain;
                t.GrazeComboGain = c.GrazeComboGain;
            }
            var m = db.Mutations;
            if (m != null)
            {
                if (m.MirrorImageHeatMul > 0f) t.MirrorHeatMul = m.MirrorImageHeatMul;
                t.UnstableCoreLockTicks = SimConst.Ticks(m.UnstableCoreLockSeconds);
            }
            var tries = db.Tries;
            if (tries != null && tries.Parser != null) t.MaxDebt = tries.Parser.MaxDebt;

            JObject extra = db.SoloExtra(ExtraName);
            if (extra != null)
            {
                t.RingImminentBelowCritHu = SimConst.HeatToHu(Num(extra, "ring_imminent_below_crit", 0f));
                t.RingBrinkFromHu = SimConst.HeatToHu(Num(extra, "ring_brink_from", HeatScaleMax));
                t.DebtExtraDamageCells = (int)Num(extra, "debt_extra_damage_cells", 0f);
                t.GrazeCastForgetTicks = SimConst.Ticks(Num(extra, "graze_cast_forget_seconds", 0f));
                t.GrazeHeatReliefHu = SimConst.HeatToHu(Num(extra, "graze_heat_relief", 0f));
            }
            return t;
        }

        static float Num(JObject o, string key, float fallback)
        {
            JToken v = o[key];
            if (v == null) return fallback;
            return v.Type == JTokenType.Integer || v.Type == JTokenType.Float ? v.Value<float>() : fallback;
        }
    }
}
