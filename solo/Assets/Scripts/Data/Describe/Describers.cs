using System.Globalization;

namespace Wizard.Solo.Data
{
    /// <summary>
    /// 설명 글 서식의 공통 도우미. 문구는 이름 표(Resources/NamesSolo/Tutorial.ko.json의 desc.*. 블록 설명은 WP3의 BlockDescriber — NamesSolo/Loot.ko.json str.block_desc.*)에만 있고,
    /// 수치는 데이터(arts.json, mutations.json, modifiers.json, elements.json)에서 읽어 {이름} 자리에 넣는다
    /// — 문구에 숫자를 적지 않는다(데이터를 고치면 설명도 따라 바뀐다).
    /// </summary>
    public static class DescribeFormat
    {
        /// <summary>소수 둘째 자리까지, 끝의 0은 지운다: 1.25 → "1.25", 0.80 → "0.8", 3.0 → "3".</summary>
        public static string Num(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        /// <summary>비율(0.2) → 퍼센트 글("20").</summary>
        public static string Pct(float ratio) => Num(ratio * 100f);
        /// <summary>열쇠가 이름 표에 있으면 그 서식, 없으면 null(줄을 숨긴다).</summary>
        public static string Try(INameTable names, string key, params object[] args)
        {
            if (names == null || key == null || !names.Has(key)) return null;
            return names.Format(key, args);
        }
    }

    /// <summary>
    /// 비기 15종의 효과 한 줄(야영 비기 후보 카드·트라이 설명·도감). 열쇠 desc.&lt;artId&gt;. 피해는 "nT"(T = 지팡이 트리 1회 피해, 04 8절 A-12).
    /// 공개 함수: OneLine(names, db, artId), CostLine(names, db, artId) = "결정 N개"/"결정 전부".
    /// </summary>
    public static class ArtDescriber
    {
        public const string Prefix = "desc.";
        public const string CostKey = "desc.art.cost", CostAllKey = "desc.art.cost_all";

        public static string Key(string artId) => artId != null ? Prefix + artId : null;

        public static string OneLine(INameTable names, IContentDb db, string artId)
        {
            var a = db != null && artId != null ? db.Art(artId) : null;
            if (a == null) return null;
            var e = a.Effect ?? new ArtEffectDef();
            float ticks = e.DurationSeconds > 0f && e.IntervalSeconds > 0f ? e.DurationSeconds / e.IntervalSeconds : 0f;
            return DescribeFormat.Try(names, Key(artId),
                "t", DescribeFormat.Num(e.Damage),
                "t_pct", DescribeFormat.Pct(e.Damage),
                "side", DescribeFormat.Pct(e.SideDamage),
                "ways", e.Branches,
                "angle", DescribeFormat.Num(e.BranchAngleDegrees),
                "knock", DescribeFormat.Num(e.Knockback),
                "move", DescribeFormat.Num(e.SelfMove),
                "stagger", DescribeFormat.Num(e.StaggerSeconds),
                "invuln", DescribeFormat.Num(e.InvulnSeconds),
                "range", DescribeFormat.Num(e.Range),
                "radius", DescribeFormat.Num(e.Radius),
                "heat_per_t", DescribeFormat.Num(e.HeatPerT),
                "clear", DescribeFormat.Num(e.ClearRadius),
                "stun", DescribeFormat.Num(e.StunSeconds),
                "dur", DescribeFormat.Num(e.DurationSeconds),
                "every", DescribeFormat.Num(e.IntervalSeconds),
                "times", DescribeFormat.Num(ticks),
                "slow", DescribeFormat.Num(e.SlowPercent),
                "absorb", DescribeFormat.Num(e.HeatAbsorb),
                "copies", e.MaxCopies,
                "pull", DescribeFormat.Num(e.PullDistance),
                "offset", DescribeFormat.Num(e.Offset),
                "width", DescribeFormat.Num(e.Width),
                "repeat", DescribeFormat.Num(e.RepeatIntervalSeconds));
        }

        public static string CostLine(INameTable names, IContentDb db, string artId)
        {
            var a = db != null && artId != null ? db.Art(artId) : null;
            if (a == null) return null;
            if (a.CostIsAll) return DescribeFormat.Try(names, CostAllKey, "min", a.MinCost);
            return DescribeFormat.Try(names, CostKey, "n", a.CostInt);
        }
    }

    /// <summary>
    /// 변이 6종의 이득·대가(강화 변이 카드·도감·문맥 힌트). 열쇠 desc.&lt;mutationId&gt;.gain / .cost. 수치는 solo/mutations.json.
    /// 공개 함수: Gain(names, db, id), Cost(names, db, id).
    /// </summary>
    public static class MutationDescriber
    {
        public static string GainKey(string id) => "desc." + id + ".gain";
        public static string CostKey(string id) => "desc." + id + ".cost";

        public static string Gain(INameTable names, IContentDb db, string id) => Line(names, db, id, GainKey(id));
        public static string Cost(INameTable names, IContentDb db, string id) => Line(names, db, id, CostKey(id));

        static string Line(INameTable names, IContentDb db, string id, string key)
        {
            var m = db != null ? db.Mutations : null;
            if (m == null || id == null) return null;
            return DescribeFormat.Try(names, key,
                "dmg_mul", DescribeFormat.Num(m.TwistedBarrelDamageMul),
                "spread", m.TwistedBarrelSpreadDegrees,
                "radius_pct", DescribeFormat.Pct(m.UnstableCoreRadiusMul - 1f),
                "chance", m.UnstableCoreChancePercent,
                "lock_sec", DescribeFormat.Num(m.UnstableCoreLockSeconds),
                "crit", DescribeFormat.Num(m.HypersensitiveCritStart),
                "lock_plus", DescribeFormat.Num(m.HypersensitiveLockPlusSeconds),
                "heat_kill", DescribeFormat.Num(m.GluttonyHeatPerKill),
                "heat_cap", DescribeFormat.Num(m.GluttonyHeatPerSecondCap),
                "cool_pct", DescribeFormat.Pct(m.GluttonyCoolMul),
                "heat_pct", DescribeFormat.Pct(m.MirrorImageHeatMul - 1f),
                "crit_bonus", m.GlassWandCritBonus,
                "min_stage", m.GlassWandMinStage);
        }
    }

    /// <summary>
    /// 변덕 8종의 효과 한 줄(야영 다음 웨이브 칸·HUD 툴팁·도감). 열쇠 desc.&lt;whimId&gt;. 수치는 modifiers.json.
    /// 공개 함수: OneLine(names, db, whimId), ScoreLine(names, db, whimId) = "점수 ×N".
    /// </summary>
    public static class WhimDescriber
    {
        public const string ScoreKey = "desc.whim.score";

        public static string Key(string whimId) => whimId != null ? "desc." + whimId : null;

        public static string OneLine(INameTable names, IContentDb db, string whimId)
        {
            var m = db != null && whimId != null ? db.Modifier(whimId) : null;
            if (m == null) return null;
            return DescribeFormat.Try(names, Key(whimId),
                "interval", DescribeFormat.Num(m.BundleIntervalSeconds ?? 0f),
                "hp_pct", DescribeFormat.Pct((m.EnemyHpMultiplier ?? 1f) - 1f),
                "extra_pct", m.ExtraBudget != null ? m.ExtraBudget.Percent : 0,
                "elites", m.ExtraElites,
                "circle_r", DescribeFormat.Num(m.DeathCircle != null ? m.DeathCircle.Radius : 0f),
                "circle_sec", DescribeFormat.Num(m.DeathCircle != null ? m.DeathCircle.TelegraphSeconds : 0f),
                "shots_mul", DescribeFormat.Num(m.EnemyShots != null ? m.EnemyShots.CountMultiplier : 1f),
                "speed_pct", DescribeFormat.Pct(m.EnemyShots != null ? 1f - m.EnemyShots.SpeedMultiplier : 0f),
                "thorn_w", DescribeFormat.Num(m.ThornBorder != null ? m.ThornBorder.Width : 0f),
                "score", DescribeFormat.Num(m.ScoreMultiplier));
        }

        public static string ScoreLine(INameTable names, IContentDb db, string whimId)
        {
            var m = db != null && whimId != null ? db.Modifier(whimId) : null;
            return m == null ? null : DescribeFormat.Try(names, ScoreKey, "score", DescribeFormat.Num(m.ScoreMultiplier));
        }
    }

    /// <summary>
    /// 약점 ▲ / 저항 ▼의 뜻(피해 숫자·보스 줄·야영 다음 웨이브의 문양). 배율은 elements.json.
    /// 공개 함수: Weak(names, db), Resist(names, db), Mark(isWeak) = 열쇠.
    /// </summary>
    public static class AffinityDescriber
    {
        public const string WeakKey = "desc.affinity.weak", ResistKey = "desc.affinity.resist";

        public static string Weak(INameTable names, IContentDb db) =>
            DescribeFormat.Try(names, WeakKey, "mul", DescribeFormat.Num(db != null && db.Elements != null ? db.Elements.WeakMultiplier : 0f));

        public static string Resist(INameTable names, IContentDb db) =>
            DescribeFormat.Try(names, ResistKey, "mul", DescribeFormat.Num(db != null && db.Elements != null ? db.Elements.ResistMultiplier : 0f));
    }
}
