using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Data
{
    /// <summary>
    /// 블록 한 줄 설명을 블록 데이터(blocks.json의 flags·effect·params)에서 만든다. 글의 꼴은 이름 표(NamesSolo/Loot.ko.json의
    /// str.block_desc.*), 숫자는 데이터에서 — 코드에 문구도 기획 수치도 두지 않는다. 블록 id마다가 아니라 동작마다 꼴을 고르므로
    /// 새 블록도 같은 동작이면 따로 적지 않아도 설명이 나온다. 전리품 카드·야영 설명 판·툴팁이 같이 쓴다.
    /// 별(★) 줄은 DataSolo/Loot.json의 star 절(판 한정 블록 성장)에서 만든다.
    /// </summary>
    public static class BlockDescriber
    {
        /// <summary>설명 한 줄: 이름 표 열쇠 + 채울 값((이름, 값) 쌍). 화면은 names.Format(Key, Args).</summary>
        public struct Line
        {
            public string Key;
            public object[] Args;
            public string Format(INameTable names) => names == null ? Key : names.Format(Key, Args ?? new object[0]);
        }

        const string Prefix = "str.block_desc.";
        const float Hundred = 100f;

        /// <summary>그 블록이 하는 일(한 줄). 모르는 블록이면 Key = null.</summary>
        public static Line Describe(BlockDef b)
        {
            if (b == null) return default;
            var p = b.Params ?? new BlockParams();
            switch (b.KindEnum)
            {
                case BlockKind.Projectile:
                    if (b.HasFlag(Ids.BlockFlag.Chains))
                        return L("chain", "hops", p.ChainHops, "radius", Num(p.ChainRadius), "mul", Num(p.ChainHopMultiplier));
                    if (b.HasFlag(Ids.BlockFlag.Explodes))
                        return L("explode", "radius", Num(p.ExplosionRadius));
                    if (b.HasFlag(Ids.BlockFlag.Returns))
                        return L("return", "range", Num(b.Range));
                    if (b.HasFlag(Ids.BlockFlag.PassThrough))
                        return L("pass_through");
                    if (p.SlowPercent > 0f)
                        return L("slow_shot", "sec", Num(p.SlowSeconds), "pct", Num(p.SlowPercent));
                    return L("straight", "range", Num(b.Range));
                case BlockKind.Control:
                    if (b.IsTrigger) return L("trigger");
                    if (b.SplitCount > 1)
                        return L("split", "n", b.SplitCount, "deg", Num(p.FanHalfAngleDegrees), "mul", Num(b.HeatMultiplier));
                    return default;
                default:
                    int bonus = Pct(b.DamageBonus);
                    switch (b.Effect)
                    {
                        case Ids.BlockEffect.Bounce: return L("bounce", "n", p.BounceCount);
                        case Ids.BlockEffect.Size: return L("size", "mul", Num(p.RadiusMultiplier), "cap", Num(p.RadiusMultiplierCap), "pct", bonus);
                        case Ids.BlockEffect.Pierce: return L("pierce", "n", p.PierceCount, "pct", bonus);
                        case Ids.BlockEffect.Frost: return L("frost", "sec", Num(p.SlowSeconds), "pct", Num(p.SlowPercent));
                        case Ids.BlockEffect.Homing: return L("homing", "deg", Num(p.HomingConeHalfDegrees), "range", Num(p.HomingRange));
                        case Ids.BlockEffect.TripleHeat: return L("triple_heat", "mul", Num(p.CritMultiplier));
                        case Ids.BlockEffect.Corrosion: return L("corrosion", "sec", Num(p.DamageTakenBonusSeconds), "pct", Num(p.DamageTakenBonusPercent));
                        default: return bonus > 0 ? L("damage_bonus", "pct", bonus) : default;
                    }
            }
        }

        /// <summary>변형·제어가 무엇에 붙는지(규칙 한 줄): 변형 = 같은 묶음의 모든 투사체, 분열 = 같은 묶음, 트리거 = 뒤 칸 전부.</summary>
        public static Line ScopeRule(BlockDef b)
        {
            if (b == null) return default;
            if (b.KindEnum == BlockKind.Modifier) return L("scope.modifier");
            if (b.IsTrigger) return L("scope.trigger");
            if (b.SplitCount > 1) return L("scope.split");
            return L("scope.projectile");
        }

        /// <summary>판 한정 블록 성장의 값(DataSolo/Loot.json star 절). 조각이 없으면 전부 0.</summary>
        public struct StarValues
        {
            public int Max;
            public float ProjectileDamage, HeatCut, ModifierDamageBonus, SplitHeatMulCut, TriggerDamageBonus;
        }

        public static StarValues Stars(IContentDb db)
        {
            var v = default(StarValues);
            JObject jo = db != null ? db.SoloExtra("Loot") : null;
            if (!(jo != null && jo["star"] is JObject s)) return v;
            v.Max = (int)F(s, "max");
            v.ProjectileDamage = F(s, "projectile_damage_per_star");
            v.HeatCut = F(s, "heat_cut_per_star");
            v.ModifierDamageBonus = F(s, "modifier_damage_bonus_per_star");
            v.SplitHeatMulCut = F(s, "split_heat_mul_cut_per_star");
            v.TriggerDamageBonus = F(s, "trigger_damage_bonus_per_star");
            return v;
        }

        /// <summary>★stars일 때 그 블록이 받는 몫(한 줄). stars ≤ 0이거나 몫이 없으면 Key = null.</summary>
        public static Line StarLine(BlockDef b, int stars, in StarValues v)
        {
            if (b == null || stars <= 0) return default;
            switch (b.KindEnum)
            {
                case BlockKind.Projectile:
                    return L("star.projectile", "n", stars, "dmg", Pct(v.ProjectileDamage * stars), "heat", Pct(v.HeatCut * stars));
                case BlockKind.Modifier:
                    return L("star.modifier", "n", stars, "dmg", Pct(v.ModifierDamageBonus * stars), "heat", Pct(v.HeatCut * stars));
                default:
                    if (b.IsTrigger) return L("star.trigger", "n", stars, "dmg", Pct(v.TriggerDamageBonus * stars));
                    if (b.SplitCount > 1)
                    {
                        float cut = Math.Max(0f, 1f - v.SplitHeatMulCut * stars);
                        return L("star.split", "n", stars, "mul", Num(1f + (b.HeatMultiplier - 1f) * cut));
                    }
                    return default;
            }
        }

        /// <summary>설명 글 하나로(줄 사이는 이름 표의 구분 글자). 없는 줄은 건너뛴다.</summary>
        public static string Text(INameTable names, BlockDef b, bool withScope)
        {
            var lines = new List<string>(2);
            var d = Describe(b);
            if (d.Key != null) lines.Add(d.Format(names));
            if (withScope)
            {
                var s = ScopeRule(b);
                if (s.Key != null) lines.Add(s.Format(names));
            }
            return string.Join(names != null ? names.Get("str.block_desc.sep") : " ", lines);
        }

        static Line L(string key, params object[] args) => new Line { Key = Prefix + key, Args = args };

        /// <summary>비율 → 정수 %(0.2 → 20). 반올림.</summary>
        static int Pct(float ratio) => (int)Math.Round(ratio * Hundred, MidpointRounding.AwayFromZero);

        /// <summary>정수면 정수, 아니면 소수 둘째 자리까지(끝의 0은 지움). 문화권과 무관.</summary>
        static string Num(float v)
        {
            double r = Math.Round(v, 2, MidpointRounding.AwayFromZero);
            return r.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static float F(JObject o, string key)
        {
            JToken t = o[key];
            return t == null || (t.Type != JTokenType.Integer && t.Type != JTokenType.Float) ? 0f : (float)t;
        }
    }
}
