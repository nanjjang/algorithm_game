using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Wizard.Solo.Data;

namespace Wizard.Solo.Tests.Data
{
    /// <summary>NameTable: 원본 + 조각 합치기, 중복 검출, Get/Has/Format/Number, 조사 틀.</summary>
    public class NameTableTests
    {
        // 테스트용 글자는 코드에 한국어를 두지 않으려고 \u 이스케이프로 적는다.
        const string Ga = "\uAC00";        // 가 (받침 없음)
        const string Gak = "\uAC01";       // 각 (받침 ㄱ)
        const string Gal = "\uAC08";       // 갈 (받침 ㄹ)
        const string JosaIGa = "{\uC774/\uAC00}";   // {이/가}
        const string I = "\uC774", GaJosa = "\uAC00";
        const string JosaEuro = "{(\uC73C)\uB85C}";   // {(으)로}
        const string Euro = "\uC73C\uB85C", Ro = "\uB85C";

        static NameTable Make(string main, params string[] fragmentPairs)
        {
            var frags = new List<KeyValuePair<string, string>>();
            for (int i = 0; i + 1 < fragmentPairs.Length; i += 2)
                frags.Add(new KeyValuePair<string, string>(fragmentPairs[i], fragmentPairs[i + 1]));
            return NameTable.FromJson(main, frags, msg => { });
        }

        static string Obj(params string[] kv)
        {
            var jo = new JObject();
            for (int i = 0; i + 1 < kv.Length; i += 2) jo[kv[i]] = kv[i + 1];
            return jo.ToString();
        }

        // ---------------------------------------------------------------- 실제 표

        [Test]
        public void S00_RealTable__LoadsMainAndFragments()
        {
            var t = NameTable.LoadFromDirectory(TestContent.NamesMainFile, TestContent.NamesFragmentsDir, msg => { });
            Assert.Greater(t.Count, 1000);
            Assert.IsFalse(t.Has(NameTable.EndMarkerKey));
            Assert.AreEqual("names.ko", t.OriginOf(Ids.Char.C1));
            Assert.AreEqual("F1.ko", t.OriginOf(Ids.Enemy.Dummy));
            Assert.IsTrue(t.Has("str.battle_hud.now_k_fizzle"));
            Assert.IsTrue(t.Has("str.battle_hud.now_k_just"));
            Assert.IsTrue(t.Has("str.battle_hud.death_line"));
            Assert.IsTrue(t.Has("str.settlement.best_line"));
            Assert.IsTrue(t.Has("str.enhance.fail_line"));
        }

        // 조각끼리, 조각과 원본 사이에 같은 열쇠가 없어야 한다(00 문서 9절 이름 조각 규칙).
        [Test]
        public void S00_NamesSolo__NoDuplicateKeysAcrossFragments()
        {
            var t = NameTable.LoadFromDirectory(TestContent.NamesMainFile, TestContent.NamesFragmentsDir, msg => { });
            Assert.IsEmpty(t.Errors, string.Join("\n", t.Errors));
        }

        // 조각 파일: 열쇠 오름차순(서수), 값은 글자.
        [Test]
        public void S00_NamesSolo__FragmentsAreSortedAscending()
        {
            foreach (var file in Directory.GetFiles(TestContent.NamesFragmentsDir, "*.json"))
            {
                var jo = JObject.Parse(File.ReadAllText(file, Encoding.UTF8));
                string prev = null;
                foreach (var p in jo.Properties())
                {
                    if (prev != null) Assert.Less(string.CompareOrdinal(prev, p.Name), 0, Path.GetFileName(file) + ": " + p.Name);
                    Assert.AreEqual(JTokenType.String, p.Value.Type, p.Name);
                    prev = p.Name;
                }
            }
        }

        // Ids.cs의 모든 콘텐츠 id에 이름 열쇠가 있다.
        [Test]
        public void S00_Ids__EveryIdHasNameKey()
        {
            var t = TestContent.Names;
            Type[] groups =
            {
                typeof(Ids.Char), typeof(Ids.Element), typeof(Ids.Block), typeof(Ids.Art), typeof(Ids.Passive), typeof(Ids.WandTrait),
                typeof(Ids.Mutation), typeof(Ids.Tag), typeof(Ids.Enemy), typeof(Ids.Boss), typeof(Ids.Tod), typeof(Ids.Whim),
                typeof(Ids.Frame), typeof(Ids.Event), typeof(Ids.Item), typeof(Ids.Place), typeof(Ids.Title), typeof(Ids.Cosmetic)
            };
            var missing = new List<string>();
            foreach (var g in groups)
                foreach (var id in ContentDbTests.ConstStrings(g))
                    if (!t.Has(id)) missing.Add(id);
            for (int n = 1; n <= Ids.Char.Count; n++)
            {
                if (!t.Has(Ids.Char.Of(n))) missing.Add(Ids.Char.Of(n));
                if (!t.Has(Ids.Char.WandKindOf(n))) missing.Add(Ids.Char.WandKindOf(n));
            }
            foreach (var e in ((ContentDb)TestContent.Db).Elites) if (!t.Has(e.Id)) missing.Add(e.Id);
            Assert.IsEmpty(missing, string.Join(", ", missing));
        }

        // 요약서가 쓰는 화면 열쇠 몇 개(04 6절, 07 8절)가 실제로 있다.
        [Test]
        public void S00_RealTable__FormatNowK()
        {
            var t = TestContent.Names;
            string artName = t.Get(Ids.Art.Burst);
            Assert.AreNotEqual(Ids.Art.Burst, artName);
            string line = t.Format("str.battle_hud.now_k", "art", artName, "n", 1);
            StringAssert.Contains(artName, line);
            StringAssert.Contains("1", line);
            StringAssert.DoesNotContain("{art}", line);
            StringAssert.DoesNotContain("{n}", line);
        }

        // ---------------------------------------------------------------- Get / Has

        [Test]
        public void S00_Get__MissingKeyReturnsKey()
        {
            var t = Make(Obj("a.b", "x", "~end", ""));
            Assert.AreEqual("x", t.Get("a.b"));
            Assert.AreEqual("no.such", t.Get("no.such"));
            Assert.AreEqual(string.Empty, t.Get(null));
            Assert.IsTrue(t.Has("a.b"));
            Assert.IsFalse(t.Has("no.such"));
            Assert.IsFalse(t.Has(null));
            Assert.IsFalse(t.Has("~end"));
            Assert.AreEqual("no.such", t.Format("no.such", "n", 1));
        }

        [Test]
        public void S00_Get__NestedNameKeyOneLevel()
        {
            var t = Make(Obj("char.1", "C1", "who", "{char.1}!", "deep", "[{who}]", "slot", "n={n}"));
            Assert.AreEqual("C1!", t.Get("who"));
            Assert.AreEqual("[C1!]", t.Get("deep"));          // 한 단계: who 안의 {char.1}까지
            Assert.AreEqual("n={n}", t.Get("slot"));           // 값 자리는 그대로
        }

        [Test]
        public void S00_Get__TwoLevelNestingStopsAtKey()
        {
            var t = Make(Obj("a.x", "A", "b.x", "{a.x}", "c.x", "{b.x}", "d.x", "<{c.x}>"));
            Assert.AreEqual("A", t.Get("b.x"));
            Assert.AreEqual("A", t.Get("c.x"));
            Assert.AreEqual("<b.x>", t.Get("d.x"));            // 2단계 이상은 열쇠 그대로
        }

        // ---------------------------------------------------------------- Format

        [Test]
        public void S00_Format__FillsNamedSlots()
        {
            var t = Make(Obj("s.week", "week {n} / {max}", "s.braces", "{{n}} = {n}", "s.miss", "{a}+{b}", "s.name", "by {char.1}", "char.1", "C1"));
            Assert.AreEqual("week 2 / 3", t.Format("s.week", "n", 2, "max", 3));
            Assert.AreEqual("week 2 / {max}", t.Format("s.week", "n", 2));
            Assert.AreEqual("{n} = 7", t.Format("s.braces", "n", 7));
            Assert.AreEqual("1+{b}", t.Format("s.miss", "a", 1, "b"));   // 짝 없는 마지막은 버림
            Assert.AreEqual("by C1", t.Format("s.name"));
            Assert.AreEqual("by X", t.Format("s.name", "char.1", "X"));   // 호출자 값이 먼저
            Assert.AreEqual("week 1.5 / 3", t.Format("s.week", "n", 1.5f, "max", 3L));   // 문화권 무관
        }

        [Test]
        public void S00_Format__UnclosedAndStrayBraces()
        {
            var t = Make(Obj("s.a", "x{n", "s.b", "y}z", "s.c", "p}}q"));
            Assert.AreEqual("x{n", t.Format("s.a", "n", 1));
            Assert.AreEqual("y}z", t.Format("s.b"));
            Assert.AreEqual("p}q", t.Format("s.c"));
        }

        [Test]
        public void S00_Format__JosaFollowsFinalSound()
        {
            var t = Make(Obj("s.j", "{w}" + JosaIGa, "s.r", "{w}" + JosaEuro, "s.lead", JosaIGa + "{w}"));
            Assert.AreEqual(Gak + I, t.Format("s.j", "w", Gak));
            Assert.AreEqual(Ga + GaJosa, t.Format("s.j", "w", Ga));
            Assert.AreEqual("3" + I, t.Format("s.j", "w", 3));     // 삼
            Assert.AreEqual("2" + GaJosa, t.Format("s.j", "w", 2)); // 이
            Assert.AreEqual(Gak + Euro, t.Format("s.r", "w", Gak));
            Assert.AreEqual(Gal + Ro, t.Format("s.r", "w", Gal));   // ㄹ 받침
            Assert.AreEqual(Ga + Ro, t.Format("s.r", "w", Ga));
            Assert.AreEqual("1" + Ro, t.Format("s.r", "w", 1));     // 일(ㄹ)
            Assert.AreEqual(I + "x", t.Format("s.lead", "w", "x")); // 앞에 값이 없으면 받침형
        }

        // ---------------------------------------------------------------- Number (08 요약서 1.4절, TV-239)

        [TestCase(48200L, "48,200")]
        [TestCase(0L, "0")]
        [TestCase(999L, "999")]
        [TestCase(1000L, "1,000")]
        [TestCase(1234567L, "1,234,567")]
        [TestCase(-2500L, "-2,500")]
        public void S13_TV_239__NumberThousandsSeparator(long value, string expected)
        {
            Assert.AreEqual(expected, Make(Obj()).Number(value));
        }

        // ---------------------------------------------------------------- 조각 합치기

        [Test]
        public void S00_Fragments__MergedAfterMain()
        {
            var t = Make(Obj("a.x", "A", "~end", ""), "G01.ko", Obj("g.one", "1"), "G02.ko", Obj("g.two", "2", "~end", ""));
            Assert.IsEmpty(t.Errors);
            Assert.AreEqual(3, t.Count);
            Assert.AreEqual("1", t.Get("g.one"));
            Assert.AreEqual("G02.ko", t.OriginOf("g.two"));
        }

        [Test]
        public void S00_Fragments__DuplicateBetweenFragmentsIsError()
        {
            var logged = new List<string>();
            var frags = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("G02.ko", Obj("dup.key", "second")),
                new KeyValuePair<string, string>("G01.ko", Obj("dup.key", "first")),
            };
            var t = NameTable.FromJson(Obj("a.x", "A"), frags, logged.Add);
            Assert.AreEqual(1, t.Errors.Count);
            Assert.AreEqual(1, logged.Count);
            StringAssert.Contains("dup.key", t.Errors[0]);
            Assert.AreEqual("first", t.Get("dup.key"));   // 이름 오름차순으로 합치므로 G01이 먼저
        }

        [Test]
        public void S00_Fragments__RepeatingOriginalKeyIsError()
        {
            var logged = new List<string>();
            var t = NameTable.FromJson(Obj("a.x", "A"),
                new[] { new KeyValuePair<string, string>("G05.ko", Obj("a.x", "B")) }, logged.Add);
            Assert.AreEqual(1, t.Errors.Count);
            Assert.AreEqual(1, logged.Count);
            Assert.AreEqual("A", t.Get("a.x"));
            Assert.AreEqual("names.ko", t.OriginOf("a.x"));
        }

        [Test]
        public void S00_Fragments__BrokenJsonAndNonStringValuesAreErrors()
        {
            var logged = new List<string>();
            var t = NameTable.FromJson(Obj("a.x", "A"), new[]
            {
                new KeyValuePair<string, string>("G06.ko", "{ not json"),
                new KeyValuePair<string, string>("G07.ko", "{ \"n.x\": 3, \"s.x\": \"ok\" }"),
            }, logged.Add);
            Assert.AreEqual(2, t.Errors.Count);
            Assert.AreEqual(2, logged.Count);
            Assert.IsFalse(t.Has("n.x"));
            Assert.AreEqual("ok", t.Get("s.x"));
        }

        [Test]
        public void S00_LoadFromDirectory__MissingMainIsError()
        {
            var logged = new List<string>();
            var t = NameTable.LoadFromDirectory(Path.Combine(TestContent.DataDir, "__nope__.json"), null, logged.Add);
            Assert.AreEqual(0, t.Count);
            Assert.AreEqual(1, t.Errors.Count);
            Assert.AreEqual(1, logged.Count);
        }
    }
}
