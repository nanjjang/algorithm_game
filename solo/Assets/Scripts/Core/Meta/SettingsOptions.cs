using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// 설정 옵션 표(08 요약서 12.2절의 솔로 줄)의 공용 정의: 열쇠, 값 이름, 기본값, 범위.
// 설정 화면(G24)은 이 표로 줄을 만들고 저장하며, 쓰는 쪽(표시 층, 실행기, 소리, 강화 화면)은 여기 도우미로 읽는다.
namespace Wizard.Solo.Meta
{
    /// <summary>옵션 열쇠(SettingsData.Options의 열쇠 = 이름 표 str.settings.opt.&lt;열쇠&gt;).</summary>
    public static class OptionKeys
    {
        // 화면
        public const string WindowMode = "window_mode";
        /// <summary>창 크기(px). 0 = 기기 화면 크기. 창 모드의 선택지는 1920×1080의 정수 배만 준다(00 문서 D-50).</summary>
        public const string ResolutionW = "resolution_w", ResolutionH = "resolution_h";
        /// <summary>연출 감도(첫 실행 물음의 답). full / reduced / minimal.</summary>
        public const string MotionPreset = "motion_preset";
        public const string ShakeScale = "shake_scale", FlashScale = "flash_scale", HitstopEnabled = "hitstop_enabled";
        // 소리
        public const string VolMaster = "vol_master", VolBgm = "vol_bgm", VolSfx = "vol_sfx", VolWand = "vol_wand",
            VolUi = "vol_ui", VolEnemy = "vol_enemy";
        // 조작
        public const string KeyLayout = "key_layout";
        // 전투 보기
        public const string MyBulletOpacity = "my_bullet_opacity", DamageNumberOpacity = "damage_number_opacity",
            DamageNumberMode = "damage_number_mode", WandFxLevel = "wand_fx_level", WandFxOpacity = "wand_fx_opacity",
            BulletTrail = "bullet_trail", GroundMarks = "ground_marks", KillShards = "kill_shards",
            ArtNamePopup = "art_name_popup", ComboPosition = "combo_position", CloneZoneOpacity = "clone_zone_opacity",
            WeatherParticles = "weather_particles", TelegraphFill = "telegraph_fill", ColorblindAssist = "colorblind_assist";
        // 대화와 공개(오프라인에서도 보이는 두 줄)
        public const string EnhanceRitual = "enhance_ritual", EnhanceFastAllowHigh = "enhance_fast_allow_high";
        // 화면이 기억하는 값(설정 화면에 줄을 그리지 않는 숨김 옵션)
        /// <summary>지팡이 목록 정렬(08 요약서 5.3절 "키 wand_list.sort"). 원정 준비와 강화의 지팡이 고르기가 같이 쓴다.</summary>
        public const string WandListSort = "wand_list.sort";
    }

    /// <summary>열거형 옵션의 값 이름(= 이름 표 str.settings.value.&lt;값&gt;).</summary>
    public static class OptionValues
    {
        public const string Full = "full", Reduced = "reduced", Minimal = "minimal", All = "all", CritWeakOnly = "crit_weak_only",
            Off = "off", On = "on", Short = "short", SmokeOnly = "smoke_only", Difficulty5Only = "difficulty5_only",
            NearCharacter = "near_character", Hud = "hud", Fast = "fast", Windowed = "windowed", Fullscreen = "fullscreen",
            Default = "default", Alternate = "alternate", Custom = "custom",
            StageDesc = "stage_desc", StageAsc = "stage_asc", Recent = "recent";
    }

    /// <summary>옵션 한 줄의 꼴. Choices가 있으면 고르기, Min &lt; Max면 숫자(슬라이더), 둘 다 아니면 켬/끔.</summary>
    public sealed class OptionSpec
    {
        public string Key;
        /// <summary>분류: display / sound / controls / combat_view / chat_privacy (str.settings.category.*).</summary>
        public string Category;
        public JToken Default;
        public string[] Choices;
        public int Min, Max;
        /// <summary>숫자 옵션이 가질 수 있는 값이 정해져 있을 때(telegraph_fill 70/80/90).</summary>
        public int[] Steps;
        /// <summary>설정 화면 목록에 그리지 않는다(다른 화면이 고친다: motion_preset은 그리되 key_layout은 키 재설정 쪽).</summary>
        public bool Hidden;
        public bool IsChoice => Choices != null;
        public bool IsNumber => Choices == null && Max > Min;
        public bool IsToggle => Choices == null && Max <= Min;
    }

    /// <summary>
    /// 옵션 표와 읽기·쓰기 도우미(순수 함수). 값의 꼴: 숫자 = int, 켬/끔 = bool, 고르기 = OptionValues 문자열.
    /// 기본값은 08 요약서 12.2절·TV-168. 범위 밖 값은 고정한다(TV-170: my_bullet_opacity 29 → 30).
    /// </summary>
    public static class SettingsOptions
    {
        public const string CatDisplay = "display", CatSound = "sound", CatControls = "controls", CatCombatView = "combat_view",
            CatChatPrivacy = "chat_privacy";

        static readonly OptionSpec[] Table = BuildTable();
        static readonly Dictionary<string, OptionSpec> ByKey = Index(Table);

        static OptionSpec Choice(string key, string cat, string def, params string[] choices) =>
            new OptionSpec { Key = key, Category = cat, Default = def, Choices = choices };
        static OptionSpec Number(string key, string cat, int def, int min, int max, int[] steps = null) =>
            new OptionSpec { Key = key, Category = cat, Default = def, Min = min, Max = max, Steps = steps };
        static OptionSpec Toggle(string key, string cat, bool def) =>
            new OptionSpec { Key = key, Category = cat, Default = def };

        static OptionSpec[] BuildTable()
        {
            const int Full = 100;
            var resW = Number(OptionKeys.ResolutionW, CatDisplay, 0, 0, 16384); resW.Hidden = true;
            var resH = Number(OptionKeys.ResolutionH, CatDisplay, 0, 0, 16384); resH.Hidden = true;
            var layout = Choice(OptionKeys.KeyLayout, CatControls, OptionValues.Default,
                OptionValues.Default, OptionValues.Alternate, OptionValues.Custom);
            layout.Hidden = true;
            var wandSort = Choice(OptionKeys.WandListSort, CatDisplay, OptionValues.StageDesc,
                OptionValues.Recent, OptionValues.StageDesc, OptionValues.StageAsc);
            wandSort.Hidden = true;
            return new[]
            {
                wandSort,
                Choice(OptionKeys.WindowMode, CatDisplay, OptionValues.Fullscreen, OptionValues.Windowed, OptionValues.Fullscreen),
                resW, resH,
                Choice(OptionKeys.MotionPreset, CatDisplay, OptionValues.Full, OptionValues.Full, OptionValues.Reduced, OptionValues.Minimal),
                Number(OptionKeys.ShakeScale, CatDisplay, Full, 0, Full),
                Number(OptionKeys.FlashScale, CatDisplay, Full, 0, Full),
                Toggle(OptionKeys.HitstopEnabled, CatDisplay, true),
                Number(OptionKeys.VolMaster, CatSound, Full, 0, Full),
                Number(OptionKeys.VolBgm, CatSound, Full, 0, Full),
                Number(OptionKeys.VolSfx, CatSound, Full, 0, Full),
                Number(OptionKeys.VolWand, CatSound, Full, 0, Full),
                Number(OptionKeys.VolUi, CatSound, Full, 0, Full),
                Number(OptionKeys.VolEnemy, CatSound, Full, 0, Full),
                layout,
                Number(OptionKeys.MyBulletOpacity, CatCombatView, Full, 30, Full),
                Number(OptionKeys.DamageNumberOpacity, CatCombatView, Full, 0, Full),
                Choice(OptionKeys.DamageNumberMode, CatCombatView, OptionValues.All, OptionValues.All, OptionValues.CritWeakOnly, OptionValues.Off),
                Choice(OptionKeys.WandFxLevel, CatCombatView, OptionValues.All, OptionValues.All, OptionValues.Reduced, OptionValues.Minimal),
                Number(OptionKeys.WandFxOpacity, CatCombatView, Full, 20, Full),
                Choice(OptionKeys.BulletTrail, CatCombatView, OptionValues.On, OptionValues.On, OptionValues.Short, OptionValues.Off),
                Toggle(OptionKeys.GroundMarks, CatCombatView, true),
                Choice(OptionKeys.KillShards, CatCombatView, OptionValues.On, OptionValues.On, OptionValues.SmokeOnly),
                Choice(OptionKeys.ArtNamePopup, CatCombatView, OptionValues.On, OptionValues.On, OptionValues.Difficulty5Only, OptionValues.Off),
                Choice(OptionKeys.ComboPosition, CatCombatView, OptionValues.NearCharacter, OptionValues.NearCharacter, OptionValues.Hud),
                Number(OptionKeys.CloneZoneOpacity, CatCombatView, Full, 30, Full),
                Toggle(OptionKeys.WeatherParticles, CatCombatView, true),
                Number(OptionKeys.TelegraphFill, CatCombatView, 70, 70, 90, new[] { 70, 80, 90 }),
                Toggle(OptionKeys.ColorblindAssist, CatCombatView, false),
                Choice(OptionKeys.EnhanceRitual, CatChatPrivacy, OptionValues.Full, OptionValues.Full, OptionValues.Fast),
                Toggle(OptionKeys.EnhanceFastAllowHigh, CatChatPrivacy, false),
            };
        }

        static Dictionary<string, OptionSpec> Index(OptionSpec[] table)
        {
            var d = new Dictionary<string, OptionSpec>();
            for (int i = 0; i < table.Length; i++) d[table[i].Key] = table[i];
            return d;
        }

        /// <summary>옵션 표(설정 화면이 그릴 순서 그대로).</summary>
        public static IReadOnlyList<OptionSpec> All => Table;
        public static OptionSpec Spec(string key) => ByKey.TryGetValue(key, out var s) ? s : null;

        static JToken Raw(SettingsData s, string key)
        {
            if (s != null && s.Options != null && s.Options.TryGetValue(key, out var v) && v != null && v.Type != JTokenType.Null) return v;
            var spec = Spec(key);
            return spec != null ? spec.Default : null;
        }

        /// <summary>숫자 옵션(범위 안으로 고정한 값). 모르는 열쇠는 0.</summary>
        public static int Int(SettingsData s, string key)
        {
            var v = Raw(s, key);
            int n = 0;
            if (v != null && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float)) n = (int)Math.Round((double)v);
            else { var spec0 = Spec(key); if (spec0 != null && spec0.IsNumber) n = (int)spec0.Default; }
            return Clamp(Spec(key), n);
        }

        public static bool Bool(SettingsData s, string key)
        {
            var v = Raw(s, key);
            return v != null && v.Type == JTokenType.Boolean && (bool)v;
        }

        /// <summary>고르기 옵션의 값 이름. 표에 없는 값이 저장돼 있으면 기본값.</summary>
        public static string Str(SettingsData s, string key)
        {
            var spec = Spec(key);
            var v = Raw(s, key);
            string str = v != null && v.Type == JTokenType.String ? (string)v : null;
            if (spec != null && spec.Choices != null && Array.IndexOf(spec.Choices, str) < 0) str = (string)spec.Default;
            return str;
        }

        /// <summary>값을 넣는다(숫자는 범위·단계로 고정, 고르기는 표에 있는 값만). 저장은 부른 쪽이 ISettingsStore.Save로 한다.</summary>
        public static void Set(SettingsData s, string key, JToken value)
        {
            var spec = Spec(key);
            if (s == null || spec == null || value == null) return;
            if (spec.IsNumber)
            {
                if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float) return;
                s.Options[key] = Clamp(spec, (int)Math.Round((double)value));
            }
            else if (spec.IsChoice)
            {
                if (value.Type != JTokenType.String || Array.IndexOf(spec.Choices, (string)value) < 0) return;
                s.Options[key] = (string)value;
            }
            else
            {
                if (value.Type != JTokenType.Boolean) return;
                s.Options[key] = (bool)value;
            }
        }

        /// <summary>기억해 둔 지팡이 목록 정렬.</summary>
        public static WandSort WandListSort(SettingsData s)
        {
            string v = Str(s, OptionKeys.WandListSort);
            return v == OptionValues.StageAsc ? WandSort.StageAsc : v == OptionValues.Recent ? WandSort.Recent : WandSort.StageDesc;
        }

        public static void SetWandListSort(SettingsData s, WandSort sort)
        {
            Set(s, OptionKeys.WandListSort, sort == WandSort.StageAsc ? OptionValues.StageAsc : sort == WandSort.Recent ? OptionValues.Recent : OptionValues.StageDesc);
        }

        /// <summary>그 분류의 옵션을 기본값으로 되돌린다([기본값으로] 버튼). 화면이 기억하는 정렬(wand_list.sort)은 그대로 둔다.</summary>
        public static void ResetCategory(SettingsData s, string category)
        {
            if (s == null) return;
            for (int i = 0; i < Table.Length; i++)
                if (Table[i].Category == category && Table[i].Key != OptionKeys.WandListSort) s.Options.Remove(Table[i].Key);
        }

        static int Clamp(OptionSpec spec, int n)
        {
            if (spec == null || !spec.IsNumber) return n;
            if (n < spec.Min) n = spec.Min;
            if (n > spec.Max) n = spec.Max;
            if (spec.Steps != null)
            {
                int best = spec.Steps[0];
                for (int i = 1; i < spec.Steps.Length; i++)
                    if (Math.Abs(spec.Steps[i] - n) < Math.Abs(best - n)) best = spec.Steps[i];
                n = best;
            }
            return n;
        }

        /// <summary>연출 감도 0 전체 / 1 줄임 / 2 최소.</summary>
        public static int EffectsLevel(SettingsData s)
        {
            string v = Str(s, OptionKeys.MotionPreset);
            return v == OptionValues.Minimal ? 2 : v == OptionValues.Reduced ? 1 : 0;
        }

        public static void SetEffectsLevel(SettingsData s, int level)
        {
            Set(s, OptionKeys.MotionPreset, level >= 2 ? OptionValues.Minimal : level == 1 ? OptionValues.Reduced : OptionValues.Full);
        }

        public static string VolumeKey(SoundBus bus)
        {
            switch (bus)
            {
                case SoundBus.Bgm: return OptionKeys.VolBgm;
                case SoundBus.Sfx: return OptionKeys.VolSfx;
                case SoundBus.Wand: return OptionKeys.VolWand;
                case SoundBus.Ui: return OptionKeys.VolUi;
                case SoundBus.Enemy: return OptionKeys.VolEnemy;
                default: return OptionKeys.VolMaster;
            }
        }

        /// <summary>묶음 음량 0~1.</summary>
        public static float Volume(SettingsData s, SoundBus bus)
        {
            var spec = Spec(VolumeKey(bus));
            return Int(s, VolumeKey(bus)) / (float)spec.Max;
        }

        /// <summary>0~100(%) 옵션을 0~1로.</summary>
        public static float Percent01(SettingsData s, string key)
        {
            var spec = Spec(key);
            return spec == null || spec.Max <= 0 ? 0f : Int(s, key) / (float)spec.Max;
        }
    }
}
