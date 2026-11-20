using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>bosses.json의 phantom_copy(05 요약서 9.2절 변환 규칙 표).</summary>
    public sealed class PhantomCopyParams
    {
        public float IntervalRatio;
        public int MaxProjectiles;
        public float Speed;
        public int Damage;
        public int BounceCap;
        public float ExplosionRadiusRatio;
        public float ExplosionRadiusMax;
        public float ExplosionTelegraphSeconds;
        public int ExplosionDamage;
        public float ChainLineTelegraphSeconds;
        public float ChainLineWidth;
        public float ChainLineLength;
        public float SplitMinAngleDegrees;
        public int CopyMutationsFromCycle;

        public static PhantomCopyParams From(JObject j)
        {
            var p = new PhantomCopyParams();
            if (j == null) return p;
            p.IntervalRatio = BossTuning.Float(j, "interval_ratio", 0f);
            p.MaxProjectiles = BossTuning.Int(j, "max_projectiles", 0);
            p.Speed = BossTuning.Float(j, "speed", 0f);
            p.Damage = BossTuning.Int(j, "damage", 0);
            p.BounceCap = BossTuning.Int(j, "bounce_cap", 0);
            p.ExplosionRadiusRatio = BossTuning.Float(j, "explosion_radius_ratio", 0f);
            p.ExplosionRadiusMax = BossTuning.Float(j, "explosion_radius_max", 0f);
            p.ExplosionTelegraphSeconds = BossTuning.Float(j, "explosion_telegraph_seconds", 0f);
            p.ExplosionDamage = BossTuning.Int(j, "explosion_damage", 0);
            p.ChainLineTelegraphSeconds = BossTuning.Float(j, "chain_line_telegraph_seconds", 0f);
            p.ChainLineWidth = BossTuning.Float(j, "chain_line_width", 0f);
            p.ChainLineLength = BossTuning.Float(j, "chain_line_length", 0f);
            p.SplitMinAngleDegrees = BossTuning.Float(j, "split_min_angle_degrees", 0f);
            p.CopyMutationsFromCycle = BossTuning.Int(j, "copy_mutations_from_cycle", 0);
            return p;
        }
    }

    /// <summary>복사해 쏠 탄 하나(변환을 거친 값).</summary>
    public struct PhantomShot
    {
        public BlockDef Block;
        /// <summary>조준 방향에서의 각(도). 거울상의 뒤 묶음은 180이 더해져 있다.</summary>
        public float OffsetDegrees;
        /// <summary>스코프 깊이(0 = 루트). 표시의 흰 점 개수 = 깊이 + 1.</summary>
        public int ScopeDepth;
        public float Radius;
        /// <summary>사거리(칸). 블록의 값 그대로.</summary>
        public float Range;
        /// <summary>벽 튕김(bounce_cap까지).</summary>
        public int Bounces;
        /// <summary>연쇄 번개 → 조준 예고선 뒤 직선 번개.</summary>
        public bool ChainLine;
        /// <summary>폭발 반경(0 = 폭발 없음). 플레이어의 60%, 최대 1.5.</summary>
        public float ExplosionRadius;
        /// <summary>부메랑(돌아오는 길에도 판정).</summary>
        public bool Returns;
    }

    /// <summary>
    /// 허상의 복사 변환(05 요약서 9.2절, 03 요약서 12절 끝)의 순수 함수: 유도·서리·잠식 제거, 튕김 1회, 간격 ×2.5(올림),
    /// 속도 6·피해 1(값은 PhantomCopyParams), 발사 1회 12개 상한, 연쇄 번개 → 예고선, 좁은 갈래 벌림(3갈래 ±19.19도).
    /// </summary>
    public static class PhantomCopyRules
    {
        /// <summary>복사 사격 간격 = 플레이어 간격 × 2.5(올림) → 격노 −10%/스택(내림) → 하한 min_interval_ticks.</summary>
        public static int CopyIntervalTicks(int playerIntervalTicks, float intervalRatio, BossCommonDef common, int rageStacks, int minIntervalTicks)
        {
            int baseTicks = (int)Math.Ceiling((double)playerIntervalTicks * intervalRatio);
            return BossRules.RapidIntervalTicks(common, baseTicks, rageStacks, minIntervalTicks);
        }

        /// <summary>
        /// 갈래 사이 최소 각(도). 발사점에서 checkDistance(3칸) 지점의 탄 사이가 minGap(1.0칸)이 되는 각 2·asin(gap / (2·d)) = 19.19도와
        /// 데이터의 split_min_angle_degrees(호 길이 기준 19.1도) 중 큰 쪽 — 둘 다 만족한다.
        /// </summary>
        public static float MinSplitGapDegrees(PhantomCopyParams prm, BossTuning tuning)
        {
            float chord = 0f;
            if (tuning.R3CheckDistance > 0f)
            {
                float s = Mathf.Clamp01(tuning.R3MinGap / (tuning.R3CheckDistance + tuning.R3CheckDistance));
                chord = (Mathf.Asin(s) + Mathf.Asin(s)) * Mathf.Rad2Deg;
            }
            return Mathf.Max(prm.SplitMinAngleDegrees, chord);
        }

        /// <summary>부채 각(오름차순). 1 → 0 / n → 가운데 대칭, 이웃 사이 = max(플레이어의 간격, minGap).</summary>
        public static void FanOffsets(int splitCount, float fanHalfAngleDegrees, float minGapDegrees, List<float> into)
        {
            into.Clear();
            if (splitCount <= 1)
            {
                into.Add(0f);
                return;
            }
            float gap = (fanHalfAngleDegrees + fanHalfAngleDegrees) / (splitCount - 1);
            if (gap < minGapDegrees) gap = minGapDegrees;
            float start = -gap * (splitCount - 1) / 2f;
            for (int i = 0; i < splitCount; i++) into.Add(start + gap * i);
        }

        /// <summary>
        /// 스코프 하나의 탄 목록(투사체는 슬롯 순, 각은 오름차순). maxCount를 넘는 것은 만들지 않는다(뒤에서부터 잘린다).
        /// mirror면 같은 묶음을 뒤(+180도)로 한 벌 더(앞 묶음 다음 뒤 묶음). 유도·서리·잠식·관통·감속은 옮기지 않는다.
        /// </summary>
        public static void ScopeShots(IContentDb db, CompiledSpell spell, int depth, PhantomCopyParams prm, float minGapDegrees,
            bool mirror, int maxCount, List<PhantomShot> into, List<float> offsetBuf)
        {
            if (spell == null || !spell.IsValid || depth < 0 || depth >= spell.Scopes.Count) return;
            var scope = spell.Scopes[depth];
            FanOffsets(scope.SplitCount, scope.FanHalfAngleDegrees, minGapDegrees, offsetBuf);
            int passes = mirror ? 2 : 1;
            for (int pass = 0; pass < passes; pass++)
            {
                float turn = pass == 0 ? 0f : BossContext.HalfTurnDegrees;
                for (int i = 0; i < scope.Projectiles.Count; i++)
                {
                    var block = scope.Projectiles[i].Block;
                    if (block == null) continue;
                    for (int k = 0; k < offsetBuf.Count; k++)
                    {
                        if (into.Count >= maxCount) return;
                        into.Add(Transform(db, block, scope, prm, offsetBuf[k] + turn));
                    }
                }
            }
        }

        static PhantomShot Transform(IContentDb db, BlockDef block, SpellScope scope, PhantomCopyParams prm, float offsetDegrees)
        {
            var shot = new PhantomShot
            {
                Block = block, OffsetDegrees = offsetDegrees, ScopeDepth = scope.Depth, Range = block.Range,
                Radius = block.HitRadius * SizeMul(db, scope.Mods.SizeDoublings),
                Bounces = Math.Min(scope.Mods.BounceCount, prm.BounceCap),
                ChainLine = block.HasFlag(Ids.BlockFlag.Chains),
                Returns = block.HasFlag(Ids.BlockFlag.Returns)
            };
            if (block.HasFlag(Ids.BlockFlag.Explodes) && block.Params != null)
                shot.ExplosionRadius = Mathf.Min(block.Params.ExplosionRadius * prm.ExplosionRadiusRatio, prm.ExplosionRadiusMax);
            return shot;
        }

        /// <summary>크기 변형의 반지름 배율(두 배씩, 상한까지). 그림자 구체 0.25는 변형이 없으면 그대로다.</summary>
        static float SizeMul(IContentDb db, int doublings)
        {
            if (doublings <= 0 || db == null) return 1f;
            var size = db.Block(Ids.Block.DoubleSize);
            if (size == null || size.Params == null || size.Params.RadiusMultiplier <= 0f) return 1f;
            float m = 1f;
            for (int i = 0; i < doublings; i++) m *= size.Params.RadiusMultiplier;
            if (size.Params.RadiusMultiplierCap > 0f && m > size.Params.RadiusMultiplierCap) m = size.Params.RadiusMultiplierCap;
            return m;
        }

        /// <summary>
        /// 따라 쓰는 비기의 갈래 각(도): 1갈래 0 / 2갈래 ±각(양익) / 한 바퀴를 채우면 각 간격으로 돌린다(팔방) /
        /// 그 밖은 가운데 대칭이고 이웃 사이가 minGap보다 좁으면 벌린다(부채 ±15 → ±19.19).
        /// </summary>
        public static void ArtBranchOffsets(int branches, float branchAngleDegrees, float minGapDegrees, List<float> into)
        {
            into.Clear();
            if (branches <= 1)
            {
                into.Add(0f);
                return;
            }
            if (branches * branchAngleDegrees >= BossContext.FullTurnDegrees)
            {
                for (int i = 0; i < branches; i++) into.Add(branchAngleDegrees * i);
                return;
            }
            if (branches == 2)
            {
                into.Add(-branchAngleDegrees);
                into.Add(branchAngleDegrees);
                return;
            }
            float gap = Mathf.Max(branchAngleDegrees, minGapDegrees);
            float start = -gap * (branches - 1) / 2f;
            for (int i = 0; i < branches; i++) into.Add(start + gap * i);
        }

        /// <summary>
        /// mimic_art가 쓸 비기를 고른다: 목록(③④⑧) 중 플레이어가 해금한 것(첫 만남은 해금과 무관하게 전부), 같은 기술 연속 금지
        /// (하나뿐이면 반복), 없으면 fallback(①). roll = 후보 수를 받아 0 이상 그 미만의 수를 돌려주는 난수.
        /// </summary>
        public static string PickMimicArt(IReadOnlyList<string> arts, string fallback, ICollection<string> unlocked, bool firstEncounter,
            string lastArt, Func<int, int> roll, List<string> buf)
        {
            buf.Clear();
            if (arts != null)
                for (int i = 0; i < arts.Count; i++)
                    if (firstEncounter || (unlocked != null && unlocked.Contains(arts[i]))) buf.Add(arts[i]);
            if (buf.Count == 0) return fallback;
            if (buf.Count > 1 && lastArt != null) buf.Remove(lastArt);
            if (buf.Count == 1) return buf[0];
            int pick = roll != null ? roll(buf.Count) : 0;
            if (pick < 0 || pick >= buf.Count) pick = 0;
            return buf[pick];
        }
    }
}
