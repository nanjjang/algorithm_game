using Wizard.Solo.Meta;

namespace Wizard.Solo.View
{
    /// <summary>
    /// 표시 층이 읽는 설정 값(08 요약서 12.2절). WorldViewHost(F5)가 판을 세울 때와 ISettingsStore.Changed 때 ReadFrom으로 갱신한다.
    /// 적용하는 곳(각 작업 지시에 같은 내용이 있다):
    ///   호스트(F5)  — EffectsLevel(vc.EffectsLevel), ShakeScale, HitStopEnabled
    ///   G14 전장·캐릭터 — WeatherParticles, WandFxLevel, WandFxOpacity
    ///   G15 적·예고   — TelegraphFillPct, ColorblindAssist
    ///   G16 탄·이펙트 — MyBulletOpacity, BulletTrail, DamageNumberMode, DamageNumberOpacity, KillShards, ArtNamePopup,
    ///                    CloneZoneOpacity, GroundMarks, FlashScale, MyFxOpacity, DamageNumberSize, ReduceFlash(솔로 가독성 보강)
    ///   G17 HUD      — ComboPosition
    /// 줄일 수 없는 것(적 예고·적 탄·열 링·피격 표시·시야 화살표)은 어떤 옵션에도 줄이지 않는다.
    /// </summary>
    public sealed class ViewOptions
    {
        /// <summary>연출 감도 0 전체 / 1 줄임 / 2 최소.</summary>
        public int EffectsLevel;
        /// <summary>0~1.</summary>
        public float ShakeScale = 1f, FlashScale = 1f;
        public bool HitStopEnabled = true;
        /// <summary>0.3~1.</summary>
        public float MyBulletOpacity = 1f;
        public float DamageNumberOpacity = 1f;
        /// <summary>OptionValues.All / CritWeakOnly / Off.</summary>
        public string DamageNumberMode = OptionValues.All;
        /// <summary>OptionValues.All / Reduced / Minimal.</summary>
        public string WandFxLevel = OptionValues.All;
        /// <summary>0.2~1.</summary>
        public float WandFxOpacity = 1f;
        /// <summary>OptionValues.On / Short / Off.</summary>
        public string BulletTrail = OptionValues.On;
        public bool GroundMarks = true;
        /// <summary>OptionValues.On / SmokeOnly.</summary>
        public string KillShards = OptionValues.On;
        /// <summary>OptionValues.On / Difficulty5Only / Off.</summary>
        public string ArtNamePopup = OptionValues.On;
        /// <summary>OptionValues.NearCharacter / Hud.</summary>
        public string ComboPosition = OptionValues.NearCharacter;
        /// <summary>0.3~1.</summary>
        public float CloneZoneOpacity = 1f;
        public bool WeatherParticles = true;
        /// <summary>예고 채움 진하기(%): 70 / 80 / 90.</summary>
        public int TelegraphFillPct = 70;
        /// <summary>색각 보조: 차오르는 채움에 줄무늬, 적 탄에 주황 줄무늬.</summary>
        public bool ColorblindAssist;
        /// <summary>내 이펙트 불투명도 0~1(기본 0.85). 내 탄·폭발·충격파·빛에 곱한다. 적 예고·적 탄은 줄이지 않는다.</summary>
        public float MyFxOpacity = 0.85f;
        /// <summary>피해 숫자 크기: OptionValues.Large / Small / Off.</summary>
        public string DamageNumberSize = OptionValues.Small;
        /// <summary>섬광 줄이기: 전체 화면 번쩍을 끄고 번쩍 배율을 ReduceFlashCap 이하로 누른다.</summary>
        public bool ReduceFlash;
        public const float ReduceFlashCap = 0.3f;

        public void ReadFrom(SettingsData s)
        {
            EffectsLevel = SettingsOptions.EffectsLevel(s);
            ShakeScale = SettingsOptions.Percent01(s, OptionKeys.ShakeScale);
            FlashScale = SettingsOptions.Percent01(s, OptionKeys.FlashScale);
            HitStopEnabled = SettingsOptions.Bool(s, OptionKeys.HitstopEnabled);
            MyBulletOpacity = SettingsOptions.Percent01(s, OptionKeys.MyBulletOpacity);
            DamageNumberOpacity = SettingsOptions.Percent01(s, OptionKeys.DamageNumberOpacity);
            DamageNumberMode = SettingsOptions.Str(s, OptionKeys.DamageNumberMode);
            WandFxLevel = SettingsOptions.Str(s, OptionKeys.WandFxLevel);
            WandFxOpacity = SettingsOptions.Percent01(s, OptionKeys.WandFxOpacity);
            BulletTrail = SettingsOptions.Str(s, OptionKeys.BulletTrail);
            GroundMarks = SettingsOptions.Bool(s, OptionKeys.GroundMarks);
            KillShards = SettingsOptions.Str(s, OptionKeys.KillShards);
            ArtNamePopup = SettingsOptions.Str(s, OptionKeys.ArtNamePopup);
            ComboPosition = SettingsOptions.Str(s, OptionKeys.ComboPosition);
            CloneZoneOpacity = SettingsOptions.Percent01(s, OptionKeys.CloneZoneOpacity);
            WeatherParticles = SettingsOptions.Bool(s, OptionKeys.WeatherParticles);
            TelegraphFillPct = SettingsOptions.Int(s, OptionKeys.TelegraphFill);
            ColorblindAssist = SettingsOptions.Bool(s, OptionKeys.ColorblindAssist);
            MyFxOpacity = SettingsOptions.Percent01(s, OptionKeys.MyFxOpacity);
            DamageNumberSize = SettingsOptions.Str(s, OptionKeys.DamageNumberSize);
            ReduceFlash = SettingsOptions.Bool(s, OptionKeys.ReduceFlash);
            if (ReduceFlash && FlashScale > ReduceFlashCap) FlashScale = ReduceFlashCap;
        }
    }
}
