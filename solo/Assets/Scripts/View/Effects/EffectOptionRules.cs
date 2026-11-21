using Wizard.Solo.Art;
using Wizard.Solo.Meta;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// G16이 적용하는 설정 옵션 → 그릴 것(순수, 08 요약서 12.2절, ViewOptions 머리 주석).
    /// 줄일 수 없는 것(피격 표시)은 여기 어느 함수도 거치지 않는다(10 요약서 3.10절).
    /// </summary>
    public static class EffectOptionRules
    {
        /// <summary>
        /// 꼬리 길이(px). 지팡이 단계 구간의 꼬리 길이 × 화면 탄 수 단계의 비율 × 옵션(on 1 / short 절반 / off 0), 상한 trail.max_length_px.
        /// 지팡이 효과 최소(wand_fx_level minimal)면 꼬리 없음(10 3.3절).
        /// </summary>
        public static float TrailLengthPx(string bulletTrail, int tierTrailPx, int loadPercent, float shortScale, int maxPx, string wandFxLevel)
        {
            if (bulletTrail == OptionValues.Off) return 0f;
            if (wandFxLevel == OptionValues.Minimal) return 0f;
            float px = tierTrailPx * (loadPercent / 100f);
            if (bulletTrail == OptionValues.Short) px *= shortScale;
            if (maxPx > 0 && px > maxPx) px = maxPx;
            return px < 0f ? 0f : px;
        }

        /// <summary>내 탄 불투명도 = 그림 기본 × my_bullet_opacity(0.3~1). 가림 상한은 이 값에 곱한 뒤 적용한다(10 3.2절).</summary>
        public static float MyBulletAlpha(float baseAlpha, float myBulletOpacity)
        {
            float o = myBulletOpacity < 0f ? 0f : myBulletOpacity > 1f ? 1f : myBulletOpacity; // 30% 하한은 설정 저장소가 지킨다(TV-170)
            return baseAlpha * o;
        }

        /// <summary>사본·지대 불투명도 = 기본 × clone_zone_opacity(0.3~1).</summary>
        public static float CloneZoneAlpha(float baseAlpha, float cloneZoneOpacity)
        {
            float o = cloneZoneOpacity < 0f ? 0f : cloneZoneOpacity > 1f ? 1f : cloneZoneOpacity;
            return baseAlpha * o;
        }

        /// <summary>처치 조각 수(kill_shards: on = 그대로 / smoke_only = 0, 연기만).</summary>
        public static int KillShardCount(string killShards, int wanted) => killShards == OptionValues.SmokeOnly ? 0 : wanted;

        /// <summary>기술 이름 띄우기(art_name_popup: on / difficulty5_only / off).</summary>
        public static bool ShowArtName(string artNamePopup, int difficulty, int largeDifficulty)
        {
            if (artNamePopup == OptionValues.Off) return false;
            if (artNamePopup == OptionValues.Difficulty5Only) return difficulty >= largeDifficulty;
            return true;
        }

        /// <summary>바닥 흔적(ground_marks 끔이면 그리지 않는다).</summary>
        public static bool DrawGroundMark(bool groundMarks) => groundMarks;

        /// <summary>번쩍 세기 × flash_scale(0~1).</summary>
        public static float FlashAlpha(float baseAlpha, float flashScale)
        {
            float s = flashScale < 0f ? 0f : flashScale > 1f ? 1f : flashScale;
            return baseAlpha * s;
        }

        /// <summary>피해 숫자 불투명도 = 기본 × damage_number_opacity(0~1).</summary>
        public static float DamageNumberAlpha(float baseAlpha, float damageNumberOpacity)
        {
            float s = damageNumberOpacity < 0f ? 0f : damageNumberOpacity > 1f ? 1f : damageNumberOpacity;
            return baseAlpha * s;
        }

        /// <summary>
        /// 적중 장식 입자 수(10 3.3절, particle_budget hit): 지팡이 구간 hit + 임계 +4 / 약점 +2 / 저항 −2, 최소 2.
        /// 화면 탄 수 단계 half = 절반(올림), minimal = 최소형(2). 지팡이 효과 줄임 = 절반(올림), 최소 = 최소형.
        /// </summary>
        public static int HitParticles(ParticleBudgetConfig c, int tierHit, bool crit, bool weak, bool resist, string hitDecor, string wandFxLevel)
        {
            int n = tierHit;
            if (crit) n += c.HitExtraCrit;
            if (weak) n += c.HitExtraWeak;
            if (resist) n += c.HitExtraResist;
            if (n < c.HitMinimalParticles) n = c.HitMinimalParticles;
            if (hitDecor == ParticleBudgetConfig.HitDecorMinimal || wandFxLevel == OptionValues.Minimal) return c.HitMinimalParticles;
            if (hitDecor == ParticleBudgetConfig.HitDecorHalf) n = (n + 1) / 2;
            if (wandFxLevel == OptionValues.Reduced) n = (n + 1) / 2;
            if (n < c.HitMinimalParticles) n = c.HitMinimalParticles;
            return n;
        }

        /// <summary>
        /// 가산 혼합은 32px 이하 그림에만(10 3.1·3.2절: 지름 2칸 이상 효과에 가산 금지). 나머지는 알파 혼합.
        /// </summary>
        public static SpriteMaterialKind MaterialFor(int widthPx, int heightPx, bool wantAdditive, int additiveMaxPx)
        {
            if (!wantAdditive) return SpriteMaterialKind.Sprite;
            int m = widthPx > heightPx ? widthPx : heightPx;
            return m <= additiveMaxPx ? SpriteMaterialKind.SpriteAdditive : SpriteMaterialKind.Sprite;
        }
    }
}
