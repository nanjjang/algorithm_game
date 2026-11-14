using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Spells
{
    /// <summary>
    /// 주문 모듈이 쓰는 수치를 Def·JSON에서 한 번 모아 둔 것(콘텐츠 하나에 하나, 불변).
    /// 출처: solo/const.json(감쇠·밑줄·상한), elements.json(상성), blocks.json의 변형 블록 params(서리·잠식·유도·크기·과열 3배),
    /// arts.json art.recoil(반동 넉백), solo/mutations.json(거울상 열), DataSolo/G02.json(임계 배율 2, 열 최대 100).
    /// 조각이나 Def가 없으면(가짜 콘텐츠) 결과를 바꾸지 않는 중립 값(배율 1, 상한 없음)으로 둔다.
    /// </summary>
    public sealed class SpellTuning
    {
        public const string ExtraName = "G02";
        /// <summary>전리품 개편 조각(DataSolo/Loot.json)의 star 절: 판 한정 블록 성장이 블록 수치에 닿는 몫.</summary>
        public const string LootExtraName = "Loot";
        const float PercentToRatio = 0.01f;

        public float DepthDecayDefault = 1f;
        public int UnderlineMaxCutHu = int.MaxValue;
        public int TriggerMaxDepth = int.MaxValue;
        public int TreeShotCap = int.MaxValue;
        public int PlayerShotCap = int.MaxValue;

        public float WeakMul = 1f, ResistMul = 1f;
        /// <summary>임계 배율(2)과 과열 3배 스코프의 임계 배율(3).</summary>
        public float CritMultiplier = 2f, TripleHeatCritMultiplier = 2f;
        /// <summary>열 최대(HU). 0 = 모름(조각 없음).</summary>
        public int HeatMaxHu;

        public float CorrosionMul = 1f;
        public int CorrosionTicks;
        public float FrostStrength;
        public int FrostTicks;
        public float HomingTurnDegreesPerTick, HomingConeHalfDegrees, HomingRange;
        public float SizeRadiusMul = 1f, SizeRadiusCap = float.MaxValue;
        public float RecoilKnockback;
        public int RecoilStaggerTicks;
        public float MirrorHeatMul = 1f;

        // 판 한정 블록 성장(★). 조각이 없으면 0 = 별이 아무것도 바꾸지 않는다.
        public int StarMax;
        public float StarProjectileDamage, StarHeatCut, StarModifierDamageBonus, StarSplitHeatMulCut, StarTriggerDamageBonus;

        public static SpellTuning From(IContentDb db)
        {
            var t = new SpellTuning();
            if (db == null) return t;

            var c = db.Const;
            if (c != null)
            {
                if (c.DepthDecayDefault > 0f) t.DepthDecayDefault = c.DepthDecayDefault;
                if (c.UnderlineMaxCut > 0f) t.UnderlineMaxCutHu = SimConst.HeatToHu(c.UnderlineMaxCut);
                if (c.TriggerMaxDepth > 0) t.TriggerMaxDepth = c.TriggerMaxDepth;
                if (c.TreeShotCap > 0) t.TreeShotCap = c.TreeShotCap;
                if (c.PlayerShotCap > 0) t.PlayerShotCap = c.PlayerShotCap;
            }

            var el = db.Elements;
            if (el != null)
            {
                if (el.WeakMultiplier > 0f) t.WeakMul = el.WeakMultiplier;
                if (el.ResistMultiplier > 0f) t.ResistMul = el.ResistMultiplier;
            }

            JObject extra = db.SoloExtra(ExtraName);
            t.CritMultiplier = Number(extra, "crit_multiplier", t.CritMultiplier);
            t.HeatMaxHu = SimConst.HeatToHu(Number(extra, "heat_max", 0f));
            t.TripleHeatCritMultiplier = t.CritMultiplier;

            var triple = db.Block(Ids.Block.TripleHeat);
            if (triple != null && triple.Params != null && triple.Params.CritMultiplier > 0f)
                t.TripleHeatCritMultiplier = triple.Params.CritMultiplier;

            var corrosion = db.Block(Ids.Block.Corrosion);
            if (corrosion != null && corrosion.Params != null)
            {
                t.CorrosionMul = 1f + corrosion.Params.DamageTakenBonusPercent * PercentToRatio;
                t.CorrosionTicks = SimConst.Ticks(corrosion.Params.DamageTakenBonusSeconds);
            }

            var frost = db.Block(Ids.Block.Frost);
            if (frost != null && frost.Params != null)
            {
                t.FrostStrength = frost.Params.SlowPercent * PercentToRatio;
                t.FrostTicks = SimConst.Ticks(frost.Params.SlowSeconds);
            }

            var homing = db.Block(Ids.Block.Homing);
            if (homing != null && homing.Params != null)
            {
                t.HomingTurnDegreesPerTick = homing.Params.HomingTurnDegreesPerSecond * SimConst.TickSeconds;
                t.HomingConeHalfDegrees = homing.Params.HomingConeHalfDegrees;
                t.HomingRange = homing.Params.HomingRange;
            }

            var size = db.Block(Ids.Block.DoubleSize);
            if (size != null && size.Params != null)
            {
                if (size.Params.RadiusMultiplier > 0f) t.SizeRadiusMul = size.Params.RadiusMultiplier;
                if (size.Params.RadiusMultiplierCap > 0f) t.SizeRadiusCap = size.Params.RadiusMultiplierCap;
            }

            var recoil = db.Art(Ids.Art.Recoil);
            if (recoil != null && recoil.Effect != null)
            {
                t.RecoilKnockback = recoil.Effect.Knockback;
                t.RecoilStaggerTicks = SimConst.Ticks(recoil.Effect.StaggerSeconds);
            }

            JObject loot = db.SoloExtra(LootExtraName);
            JObject star = loot != null ? loot["star"] as JObject : null;
            if (star != null)
            {
                t.StarMax = (int)Number(star, "max", 0f);
                t.StarProjectileDamage = Number(star, "projectile_damage_per_star", 0f);
                t.StarHeatCut = Number(star, "heat_cut_per_star", 0f);
                t.StarModifierDamageBonus = Number(star, "modifier_damage_bonus_per_star", 0f);
                t.StarSplitHeatMulCut = Number(star, "split_heat_mul_cut_per_star", 0f);
                t.StarTriggerDamageBonus = Number(star, "trigger_damage_bonus_per_star", 0f);
            }

            var mut = db.Mutations;
            if (mut != null && mut.MirrorImageHeatMul > 0f) t.MirrorHeatMul = mut.MirrorImageHeatMul;
            return t;
        }

        /// <summary>칸의 별 수를 0..StarMax로 자른다.</summary>
        public int ClampStars(int stars) => stars <= 0 ? 0 : stars > StarMax ? StarMax : stars;

        /// <summary>감속 %(0~100) → 세기(0~1).</summary>
        public static float PercentRatio(float percent) => percent * PercentToRatio;

        static float Number(JObject o, string key, float fallback)
        {
            if (o == null) return fallback;
            JToken v = o[key];
            if (v == null) return fallback;
            return v.Type == JTokenType.Integer || v.Type == JTokenType.Float ? (float)v : fallback;
        }
    }
}
