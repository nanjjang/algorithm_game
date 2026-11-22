using NUnit.Framework;
using Wizard.Solo.Screens.Prep;

namespace Wizard.Solo.Tests.ScreensPrep
{
    /// <summary>능력치 판(08 요약서 5.2절, 02 요약서 8.1·8.2절 값)과 칭호·별명 열쇠(07 요약서 칭호 표).</summary>
    public sealed class PrepStatsTests
    {
        static PrepStatRow Row(string charId, string key)
        {
            var rows = PrepStats.Build(TestContent.Db, charId);
            foreach (var r in rows) if (r.LabelKey == "str.stat." + key) return r;
            Assert.Fail("row " + key);
            return default;
        }

        [Test]
        public void S08_5_2__ElevenRowsInOrderWithNames()
        {
            var rows = PrepStats.Build(TestContent.Db, Ids.Char.C1);
            string[] keys = { "health", "move_speed", "stance_speed", "crystal_cap", "combo_per_crystal", "cooling",
                "heat_threshold", "lock_time", "graze", "link_slots", "crystal_loss" };
            Assert.AreEqual(keys.Length, rows.Count);
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.AreEqual("str.stat." + keys[i], rows[i].LabelKey);
                Assert.IsTrue(TestContent.Names.Has(rows[i].LabelKey), rows[i].LabelKey);
            }
        }

        [Test]
        public void S02_8_2__CharacterValues()
        {
            Assert.AreEqual(8f, Row(Ids.Char.C1, "health").Value);
            Assert.AreEqual(4f, Row(Ids.Char.C2, "health").Value);
            Assert.AreEqual(4.7f, Row(Ids.Char.C1, "move_speed").Value, 1e-4f);
            Assert.AreEqual(3.76f, Row(Ids.Char.C1, "stance_speed").Value, 1e-4f);
            Assert.AreEqual(80f, Row(Ids.Char.C1, "heat_threshold").Value);
            Assert.AreEqual(90f, Row(Ids.Char.C4, "heat_threshold").Value);
            Assert.AreEqual(0.75f, Row(Ids.Char.C2, "graze").Value, 1e-4f);
            Assert.AreEqual(5f, Row(Ids.Char.C3, "link_slots").Value);
            Assert.AreEqual(3.5f, Row(Ids.Char.C3, "lock_time").Value, 1e-4f);
            Assert.AreEqual(7f, Row(Ids.Char.C5, "crystal_cap").Value);
            Assert.AreEqual(2f, Row(Ids.Char.C5, "crystal_loss").Value);
            Assert.AreEqual(0f, Row(Ids.Char.C4, "crystal_loss").Value);
            Assert.AreEqual(12f, Row(Ids.Char.C2, "combo_per_crystal").Value);
            Assert.AreEqual(50f, Row(Ids.Char.C2, "cooling").Value);
        }

        [Test]
        public void S08_5_2__BarIsRatioToMaxAcrossFive()
        {
            Assert.AreEqual(1f, Row(Ids.Char.C1, "health").Bar01, 1e-4f);        // 8이 가장 크다
            Assert.AreEqual(0.5f, Row(Ids.Char.C2, "health").Bar01, 1e-4f);      // 4 / 8
            Assert.AreEqual(1f, Row(Ids.Char.C2, "move_speed").Bar01, 1e-4f);    // 5.6이 가장 빠르다
        }

        [Test]
        public void S08_5_2__NumberFormatInvariant()
        {
            Assert.AreEqual("3.76", PrepStats.FormatNumber(3.76f, PrepStatUnit.Decimal));
            Assert.AreEqual("4.5", PrepStats.FormatNumber(4.5f, PrepStatUnit.Decimal));
            Assert.AreEqual("45", PrepStats.FormatNumber(45f, PrepStatUnit.Decimal));
            Assert.AreEqual("8", PrepStats.FormatNumber(8f, PrepStatUnit.Count));
        }

        [Test]
        public void S08_5_2__UnknownCharacterEmpty()
        {
            Assert.AreEqual(0, PrepStats.Build(TestContent.Db, "char.9").Count);
            Assert.AreEqual(0, PrepStats.Build(null, Ids.Char.C1).Count);
        }

        [Test]
        public void S07_TitleBand__StageTitleKeys()
        {
            // b0 +0~3, b1 +4~6, b2 +7~9, b3 +10~12, b4 +13
            Assert.AreEqual("title.stage.1.b0", ExpeditionPrepModel.StageTitleKey(TestContent.Db, 1, 0));
            Assert.AreEqual("title.stage.1.b0", ExpeditionPrepModel.StageTitleKey(TestContent.Db, 1, 3));
            Assert.AreEqual("title.stage.2.b1", ExpeditionPrepModel.StageTitleKey(TestContent.Db, 2, 4));
            Assert.AreEqual("title.stage.3.b2", ExpeditionPrepModel.StageTitleKey(TestContent.Db, 3, 9));
            Assert.AreEqual("title.stage.4.b3", ExpeditionPrepModel.StageTitleKey(TestContent.Db, 4, 12));
            Assert.AreEqual("title.stage.5.b4", ExpeditionPrepModel.StageTitleKey(TestContent.Db, 5, 13));
            Assert.AreEqual("엄청엄청 위대한 제자 5", TestContent.Names.Get("title.stage.5.b4"));
        }

        [Test]
        public void S08_5_3__NicknameKeyAndNamesExist()
        {
            Assert.AreEqual("wand.1.stage.0.nickname", ExpeditionPrepModel.NicknameKey(1, 0));
            Assert.AreEqual("식은 부지깽이", TestContent.Names.Get(ExpeditionPrepModel.NicknameKey(1, 0)));
            Assert.IsTrue(TestContent.Names.Has(PrepKeys.Mutations));
            Assert.IsTrue(TestContent.Names.Has(PrepKeys.Scars));
            Assert.AreEqual("변이 1/3", TestContent.Names.Format(PrepKeys.Mutations, "n", 1, "max", 3));
        }

        [Test]
        public void S08_5__AllPrepKeysInNameTable()
        {
            string[] keys =
            {
                PrepKeys.Title, PrepKeys.BlockedCoreEmpty, PrepKeys.BlockedOnExpedition, PrepKeys.ChangeLoadout, PrepKeys.Depart,
                PrepKeys.HeldTag, PrepKeys.Loadout, PrepKeys.SetHeld, PrepKeys.StartWaveLabel, PrepKeys.StartWaveOption,
                PrepKeys.StartWaveScoreNote, PrepKeys.WandCount, PrepKeys.Wands, PrepKeys.FormatStartWave, PrepKeys.FormatWandStage,
                PrepKeys.FormatSeconds, PrepKeys.EnhanceExtSlots, PrepKeys.RevengeShort, PrepKeys.WarnCoreEmpty, PrepKeys.CommonNone,
                PrepKeys.StatPassive, PrepKeys.StatWandTrait, PrepKeys.StatUniqueArt, PrepKeys.SortStageDesc, PrepKeys.SortStageAsc,
                PrepKeys.SortRecent, PrepKeys.ErrorNotFound,
            };
            foreach (var k in keys) Assert.IsTrue(TestContent.Names.Has(k), k);
            Assert.AreEqual("웨이브 21", TestContent.Names.Format(PrepKeys.StartWaveOption, "wave", 21));
        }
    }
}
