using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Settings;

namespace Wizard.Solo.Tests.ScreensMeta
{
    /// <summary>설정 화면 모델(08 12.1·12.2절, 14.2절, D-50, D-71, 00 11절).</summary>
    public class SettingsModelTests
    {
        string _tmp;

        [SetUp]
        public void SetUp() { _tmp = null; }

        [TearDown]
        public void TearDown()
        {
            if (_tmp != null && Directory.Exists(_tmp)) Directory.Delete(_tmp, true);
        }

        // ---------------------------------------------------------------- 표 → 줄 (계약)

        [Test]
        public void S08_12_2__OptionRowsFollowSettingsOptionsTable()
        {
            var all = SettingsOptions.All;
            int total = 0;
            foreach (var cat in SettingsModel.Categories)
            {
                var expected = new List<OptionSpec>();
                for (int i = 0; i < all.Count; i++) if (all[i].Category == cat && !all[i].Hidden) expected.Add(all[i]);
                var got = new List<OptionSpec>();
                foreach (var r in SettingsModel.Rows(cat))
                {
                    if (r.Kind != SettingsRowKind.Option) continue;
                    got.Add(r.Spec);
                    Assert.AreEqual("str.settings.opt." + r.Spec.Key, r.NameKey);
                    var want = r.Spec.IsChoice ? SettingsWidget.Choice : r.Spec.IsNumber ? SettingsWidget.Slider : SettingsWidget.Toggle;
                    Assert.AreEqual(want, r.Widget, r.Spec.Key);
                }
                CollectionAssert.AreEqual(expected, got, cat);
                total += got.Count;
            }
            int visible = 0;
            for (int i = 0; i < all.Count; i++) if (!all[i].Hidden) visible++;
            Assert.AreEqual(visible, total, "숨기지 않은 옵션은 전부 어느 분류엔가 한 번 나온다");
        }

        [Test]
        public void S08_12_1__SixCategories()
        {
            CollectionAssert.AreEqual(new[] { "display", "sound", "controls", "combat_view", "chat_privacy", "account" }, SettingsModel.Categories);
        }

        [Test]
        public void S08_TV_169__NoUnreducibleOrSoloHiddenRows()
        {
            var names = new List<string>();
            foreach (var cat in SettingsModel.Categories)
                foreach (var r in SettingsModel.Rows(cat)) names.Add(r.NameKey);
            // 08 12.2절 솔로에 보이는 줄(그대로).
            var expected = new List<string>
            {
                "str.settings.opt.window_mode", "str.settings.opt.resolution", "str.settings.opt.motion_preset", "str.settings.opt.shake_scale",
                "str.settings.opt.flash_scale", "str.settings.opt.hitstop_enabled",
                "str.settings.opt.context_hints", "str.settings.opt.reset_hints",
                "str.settings.opt.reduce_flash", // 솔로 가독성 보강
                "str.settings.opt.vol_master", "str.settings.opt.vol_bgm", "str.settings.opt.vol_sfx", "str.settings.opt.vol_wand",
                "str.settings.opt.vol_ui", "str.settings.opt.vol_enemy",
                "str.settings.opt.key_rebind",
                "str.settings.opt.my_bullet_opacity", "str.settings.opt.damage_number_opacity", "str.settings.opt.damage_number_mode",
                "str.settings.opt.wand_fx_level", "str.settings.opt.wand_fx_opacity", "str.settings.opt.bullet_trail",
                "str.settings.opt.ground_marks", "str.settings.opt.kill_shards", "str.settings.opt.art_name_popup",
                "str.settings.opt.combo_position", "str.settings.opt.clone_zone_opacity", "str.settings.opt.weather_particles",
                "str.settings.opt.telegraph_fill", "str.settings.opt.colorblind_assist",
                "str.settings.opt.my_fx_opacity", "str.settings.opt.damage_number_size", // 솔로 가독성 보강
                "str.settings.opt.enhance_ritual", "str.settings.opt.enhance_fast_allow_high", "str.settings.trace_note",
                "str.settings.account.save_path", "str.settings.account.restore_save"
            };
            CollectionAssert.AreEqual(expected, names);
            foreach (var hidden in new[] { "vol_notice_bell", "vol_plaza_others", "others_wand_fx", "chat_mute_all", "loadout_public",
                "server_enhance_notice", "enhance_notice_show_name", "block_list" })
                CollectionAssert.DoesNotContain(names, "str.settings.opt." + hidden);
        }

        [Test]
        public void S08_TV_168__DefaultValues()
        {
            var s = new SettingsData();
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.MyBulletOpacity));
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.DamageNumberOpacity));
            Assert.AreEqual(OptionValues.All, SettingsOptions.Str(s, OptionKeys.DamageNumberMode));
            Assert.AreEqual(OptionValues.Full, SettingsOptions.Str(s, OptionKeys.MotionPreset));
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.ShakeScale));
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.FlashScale));
            Assert.IsTrue(SettingsOptions.Bool(s, OptionKeys.HitstopEnabled));
            Assert.AreEqual(OptionValues.All, SettingsOptions.Str(s, OptionKeys.WandFxLevel));
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.WandFxOpacity));
            Assert.AreEqual(OptionValues.On, SettingsOptions.Str(s, OptionKeys.BulletTrail));
            Assert.IsTrue(SettingsOptions.Bool(s, OptionKeys.GroundMarks));
            Assert.AreEqual(OptionValues.On, SettingsOptions.Str(s, OptionKeys.KillShards));
            Assert.AreEqual(OptionValues.On, SettingsOptions.Str(s, OptionKeys.ArtNamePopup));
            Assert.AreEqual(OptionValues.NearCharacter, SettingsOptions.Str(s, OptionKeys.ComboPosition));
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.CloneZoneOpacity));
            Assert.IsTrue(SettingsOptions.Bool(s, OptionKeys.WeatherParticles));
            Assert.AreEqual(70, SettingsOptions.Int(s, OptionKeys.TelegraphFill));
            Assert.IsFalse(SettingsOptions.Bool(s, OptionKeys.ColorblindAssist));
            Assert.AreEqual(OptionValues.Full, SettingsOptions.Str(s, OptionKeys.EnhanceRitual));
            // "남의 지팡이 효과 전부", "서버 강화 알림 전체"는 솔로에서 숨기는 줄이라 표에 없다(08 12.2절 [임시]).
            Assert.IsNull(SettingsOptions.Spec("others_wand_fx"));
        }

        [Test]
        public void S08_TV_170__OpacityClampsOnSave()
        {
            var store = new CountingSettingsStore();
            SettingsModel.SetInt(store, OptionKeys.MyBulletOpacity, 29);
            SettingsModel.SetInt(store, OptionKeys.WandFxOpacity, 19);
            SettingsModel.SetInt(store, OptionKeys.CloneZoneOpacity, 29);
            Assert.AreEqual(30, SettingsOptions.Int(store.Current, OptionKeys.MyBulletOpacity));
            Assert.AreEqual(20, SettingsOptions.Int(store.Current, OptionKeys.WandFxOpacity));
            Assert.AreEqual(30, SettingsOptions.Int(store.Current, OptionKeys.CloneZoneOpacity));
        }

        [Test]
        public void S00_11__ChangingAnOptionSavesOnceAndRaisesChangedOnce()
        {
            var store = new CountingSettingsStore();
            Assert.IsTrue(SettingsModel.SetBool(store, OptionKeys.ColorblindAssist, true));
            Assert.AreEqual(1, store.Saves);
            Assert.AreEqual(1, store.ChangedCount);
            Assert.IsFalse(SettingsModel.SetBool(store, OptionKeys.ColorblindAssist, true), "같은 값이면 저장하지 않는다");
            Assert.AreEqual(1, store.Saves);
            var spec = SettingsOptions.Spec(OptionKeys.MotionPreset);
            Assert.IsTrue(SettingsModel.SetChoice(store, spec, 2));
            Assert.AreEqual(OptionValues.Minimal, SettingsOptions.Str(store.Current, OptionKeys.MotionPreset));
            Assert.AreEqual(2, store.Saves);
            Assert.AreEqual(2, store.ChangedCount);
        }

        [Test]
        public void S00_11__SettingsSaveRoundTrip()
        {
            _tmp = Path.Combine(Path.GetTempPath(), "g24-settings-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmp);
            string file = Path.Combine(_tmp, "settings.json");
            var a = new Wizard.Solo.Save.SettingsStore(file);
            a.Load();
            SettingsModel.SetInt(a, OptionKeys.TelegraphFill, 90);
            SettingsModel.SetChoice(a, SettingsOptions.Spec(OptionKeys.WindowMode), 0);
            SettingsModel.SetBool(a, OptionKeys.HitstopEnabled, false);
            SettingsModel.SetResolution(a, new Vector2Int(3840, 2160));
            var b = new Wizard.Solo.Save.SettingsStore(file);
            b.Load();
            Assert.IsTrue(b.ExistedAtLoad);
            Assert.AreEqual(90, SettingsOptions.Int(b.Current, OptionKeys.TelegraphFill));
            Assert.AreEqual(OptionValues.Windowed, SettingsOptions.Str(b.Current, OptionKeys.WindowMode));
            Assert.IsFalse(SettingsOptions.Bool(b.Current, OptionKeys.HitstopEnabled));
            Assert.AreEqual(3840, SettingsOptions.Int(b.Current, OptionKeys.ResolutionW));
            Assert.AreEqual(2160, SettingsOptions.Int(b.Current, OptionKeys.ResolutionH));
        }

        [Test]
        public void S08_12_1__DefaultsResetOnlyThatCategory()
        {
            var store = new CountingSettingsStore();
            SettingsModel.SetInt(store, OptionKeys.ShakeScale, 10);
            SettingsModel.SetInt(store, OptionKeys.VolBgm, 20);
            store.Saves = 0;
            SettingsModel.ResetCategory(store, SettingsOptions.CatDisplay);
            Assert.AreEqual(100, SettingsOptions.Int(store.Current, OptionKeys.ShakeScale));
            Assert.AreEqual(20, SettingsOptions.Int(store.Current, OptionKeys.VolBgm));
            Assert.AreEqual(1, store.Saves);
            Assert.IsFalse(SettingsModel.HasDefaults(SettingsModel.CatAccount));
        }

        [Test]
        public void S08_12_1__ControlsDefaultsRestoreDefaultKeyTable()
        {
            var store = new CountingSettingsStore();
            KeyLayouts.Apply(store.Current, OptionValues.Alternate);
            SettingsModel.ResetCategory(store, SettingsOptions.CatControls);
            Assert.AreEqual(OptionValues.Default, SettingsOptions.Str(store.Current, OptionKeys.KeyLayout));
            CollectionAssert.AreEqual(KeyLayouts.DefaultPrimary, store.Current.KeyPrimary);
        }

        // ---------------------------------------------------------------- 해상도 (D-50)

        [Test]
        public void S00_D_50__WindowedResolutionsAreIntegerMultiplesThatFit()
        {
            CollectionAssert.AreEqual(new[] { new Vector2Int(1920, 1080) }, SettingsModel.WindowedResolutions(2560, 1440));
            CollectionAssert.AreEqual(new[] { new Vector2Int(1920, 1080), new Vector2Int(3840, 2160) },
                SettingsModel.WindowedResolutions(3840, 2160));
            CollectionAssert.AreEqual(new[] { new Vector2Int(1920, 1080) }, SettingsModel.WindowedResolutions(1920, 1080));
        }

        [Test]
        public void S00_D_50__FullscreenUsesDeviceResolution()
        {
            var s = new SettingsData();
            Assert.AreEqual(new Vector2Int(2560, 1440), SettingsModel.TargetResolution(s, 2560, 1440), "기본은 전체 화면 = 기기 해상도");
            SettingsOptions.Set(s, OptionKeys.WindowMode, OptionValues.Windowed);
            Assert.AreEqual(new Vector2Int(1920, 1080), SettingsModel.TargetResolution(s, 2560, 1440));
            SettingsOptions.Set(s, OptionKeys.ResolutionW, 3840);
            SettingsOptions.Set(s, OptionKeys.ResolutionH, 2160);
            Assert.AreEqual(new Vector2Int(3840, 2160), SettingsModel.TargetResolution(s, 3840, 2160));
            Assert.AreEqual(new Vector2Int(1920, 1080), SettingsModel.TargetResolution(s, 2560, 1440), "들어가지 않는 크기는 첫 선택지로");
        }

        // ---------------------------------------------------------------- 계정(오프라인)

        [Test]
        public void S08_12_2__AccountSavePathIsProfilesDirectory()
        {
            var p = new FakeProfileStore { Dir = "D:/data/profiles/offline" };
            Assert.AreEqual("D:/data/profiles/offline", SettingsModel.SavePath(p));
            Assert.AreEqual(string.Empty, SettingsModel.SavePath(null));
        }

        [Test]
        public void S08_12_2__RestoreSaveCallsRestorePreviousOnceAndReportsResult()
        {
            var p = new FakeProfileStore { RestoreResult = true };
            Assert.AreEqual("str.settings.account.restore_done", SettingsModel.RestoreSave(p));
            Assert.AreEqual(1, p.RestoreCalls);
            p.RestoreResult = false;
            Assert.AreEqual("str.settings.account.restore_failed", SettingsModel.RestoreSave(p));
            Assert.AreEqual(2, p.RestoreCalls);
            Assert.AreEqual("str.settings.account.restore_failed", SettingsModel.RestoreSave(null));
        }

        [Test]
        public void S08_12_2__RestoreSaveExceptionIsFailure()
        {
            var p = new FakeProfileStore { RestoreThrows = true };
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("broken"));
            Assert.AreEqual("str.settings.account.restore_failed", SettingsModel.RestoreSave(p));
            Assert.AreEqual(1, p.RestoreCalls);
        }

        // ---------------------------------------------------------------- 미리보기 (D-71)

        [Test]
        public void S08_D_71__PreviewTelegraphFillAlpha()
        {
            var s = new SettingsData();
            Assert.AreEqual(0.70f, SettingsPreviewModel.Build(s).TelegraphFillAlpha, 1e-6f);
            SettingsOptions.Set(s, OptionKeys.TelegraphFill, 80);
            Assert.AreEqual(0.80f, SettingsPreviewModel.Build(s).TelegraphFillAlpha, 1e-6f);
            SettingsOptions.Set(s, OptionKeys.TelegraphFill, 90);
            Assert.AreEqual(0.90f, SettingsPreviewModel.Build(s).TelegraphFillAlpha, 1e-6f);
        }

        [Test]
        public void S08_D_71__PreviewColorblindStripesAndBulletAlpha()
        {
            var s = new SettingsData();
            Assert.IsFalse(SettingsPreviewModel.Build(s).Stripes);
            Assert.AreEqual(1f, SettingsPreviewModel.Build(s).MyBulletAlpha, 1e-6f);
            SettingsOptions.Set(s, OptionKeys.ColorblindAssist, true);
            SettingsOptions.Set(s, OptionKeys.MyBulletOpacity, 29);
            var v = SettingsPreviewModel.Build(s);
            Assert.IsTrue(v.Stripes);
            Assert.AreEqual(0.30f, v.MyBulletAlpha, 1e-6f);
        }

        [Test]
        public void S08_14_2__PreviewTelegraphFlashByMotionLevel()
        {
            var s = new SettingsData();
            Assert.IsTrue(SettingsPreviewModel.Build(s).FlashWholeZone, "Full = 구역 전체");
            SettingsOptions.Set(s, OptionKeys.MotionPreset, OptionValues.Reduced);
            Assert.IsTrue(SettingsPreviewModel.Build(s).FlashThickBorder, "Reduced = 테두리만 굵게");
            SettingsOptions.Set(s, OptionKeys.MotionPreset, OptionValues.Minimal);
            Assert.IsTrue(SettingsPreviewModel.Build(s).FlashThickBorder, "Minimal = 테두리만 굵게");
            CollectionAssert.AreEquivalent(new[] { "motion_preset", "colorblind_assist", "telegraph_fill", "my_bullet_opacity" },
                SettingsPreviewModel.PreviewKeys);
            Assert.IsFalse(SettingsPreviewModel.HasPreview(OptionKeys.VolBgm));
        }

        [Test]
        public void S08_12_1__ChoiceCycleWraps()
        {
            Assert.AreEqual(1, SettingsModel.Cycle(3, 0, 1));
            Assert.AreEqual(0, SettingsModel.Cycle(3, 2, 1));
            Assert.AreEqual(2, SettingsModel.Cycle(3, 0, -1));
        }
    }
}
