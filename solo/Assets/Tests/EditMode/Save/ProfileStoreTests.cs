using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Save;

namespace Wizard.Solo.Tests.Save
{
    /// <summary>프로필 저장(07 요약서 9.3절, 12.13절 TV-PF, 00 문서 11절).</summary>
    public class ProfileStoreTests
    {
        string _root;
        FakeContent _content;
        FixedClock _clock;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "wizard-solo-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _content = new FakeContent();
            _clock = new FixedClock(new DateTime(2026, 11, 18, 15, 30, 0, DateTimeKind.Local));
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { }
        }

        string Dir => SavePaths.ProfileDirectory(_root);

        ProfileStore NewStore() => new ProfileStore(Dir, () => NewProfile.Create(_content, _clock), _content.DataVersion);

        ProfileStore Loaded(out bool created, out bool restored)
        {
            var s = NewStore();
            s.LoadOrCreate(out created, out restored);
            return s;
        }

        /// <summary>골드 값을 바꿔 가며 n번 저장한다(첫 LoadOrCreate의 저장 뒤). 마지막 골드 = n.</summary>
        ProfileStore SaveSequence(int n)
        {
            var s = Loaded(out _, out _);
            for (int i = 1; i <= n; i++)
            {
                s.Current.Currencies.Gold = i;
                s.Save();
            }
            return s;
        }

        static long GoldIn(string path)
        {
            Assert.IsTrue(ProfileStore.TryLoadFile(path, out var p), "file should be valid: " + path);
            return p.Currencies.Gold;
        }

        static void CorruptHash(string path)
        {
            string text = File.ReadAllText(path, new UTF8Encoding(false));
            // 본문의 골드 숫자 한 자리를 바꾸고 해시는 그대로 둔다.
            text = Regex.Replace(text, "\"gold\": (\\d+)", m => "\"gold\": " + (long.Parse(m.Groups[1].Value) + 1000));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        // ---------------------------------------------------------------- 12.13 벡터

        // TV-PF-01: 처음 실행 → 오프라인 프로필 자동 생성, 8.4절 시작 상태(지팡이 5, 마력탄 5 끼움, 골드 0)
        [Test]
        public void S08_TV_PF_01__FirstRunCreatesStartProfile()
        {
            var s = Loaded(out bool created, out bool restored);
            Assert.IsTrue(created);
            Assert.IsFalse(restored);
            Assert.IsTrue(File.Exists(s.FilePath));
            var p = s.Current;
            Assert.AreEqual(5, p.Wands.Count);
            Assert.AreEqual(5, p.Items[Ids.Block.MagicBolt].Qty);
            Assert.AreEqual(5, p.Items[Ids.Block.MagicBolt].QtySocketed);
            Assert.AreEqual(0, p.Currencies.Gold);
            Assert.AreEqual("Offline", p.ProfileKind);

            // 다시 실행하면 같은 프로필을 읽는다(새로 만들지 않는다).
            var again = Loaded(out bool created2, out bool restored2);
            Assert.IsFalse(created2);
            Assert.IsFalse(restored2);
            Assert.AreEqual(5, again.Current.Wands.Count);
        }

        // TV-PF-03: 3세대 있음, 최신 파일 체크섬 깨짐 → 직전 세대로 되돌리고 알림(restored)
        [Test]
        public void S08_TV_PF_03__BrokenChecksumRestoresPreviousGeneration()
        {
            var s = SaveSequence(4);
            Assert.IsTrue(File.Exists(s.GenerationPath(3)));
            CorruptHash(s.FilePath);

            var r = Loaded(out bool created, out bool restored);
            Assert.IsTrue(restored);
            Assert.IsFalse(created);
            Assert.AreEqual(3, r.Current.Currencies.Gold);
            // 현재 자리가 되살린 세대로 고쳐졌다.
            Assert.AreEqual(3, GoldIn(r.FilePath));
        }

        // TV-PF-04: 저장 중 강제 종료 → 이전 완전한 파일로 열린다(임시 파일 + 이름 바꾸기)
        [Test]
        public void S08_TV_PF_04__CrashWhileWritingTempOpensPreviousFile()
        {
            var s = SaveSequence(2);
            // 임시 파일까지 쓰다가 꺼졌다(반쯤 쓴 글자).
            File.WriteAllText(s.TempPath, "{\"schema_version\": 1, \"curr", new UTF8Encoding(false));

            var r = Loaded(out bool created, out bool restored);
            Assert.IsFalse(created);
            Assert.IsFalse(restored);
            Assert.AreEqual(2, r.Current.Currencies.Gold);
            Assert.IsFalse(File.Exists(s.TempPath), "남은 임시 파일은 읽을 때 지운다");
        }

        // TV-PF-04 (다른 순간): 세대를 민 뒤, 이름 바꾸기 전에 꺼져 현재 파일이 없다 → .1(직전 완전한 파일)로 연다
        [Test]
        public void S08_TV_PF_04__CrashBeforeRenameOpensGenerationOne()
        {
            var s = SaveSequence(2);
            File.Copy(s.FilePath, s.GenerationPath(1), true);
            File.Delete(s.FilePath);
            File.WriteAllText(s.TempPath, "garbage", new UTF8Encoding(false));

            var r = Loaded(out bool created, out bool restored);
            Assert.IsFalse(created);
            Assert.IsTrue(restored);
            Assert.AreEqual(2, r.Current.Currencies.Gold);
            Assert.IsTrue(File.Exists(r.FilePath));
            Assert.IsFalse(File.Exists(s.TempPath));
        }

        // TV-PF-06: 오프라인 프로필을 서버로 올리기/바꿔 주기 API는 존재하지 않는다
        [Test]
        public void S08_TV_PF_06__NoUploadOrMigrationApi()
        {
            var bad = new Regex("upload|migrat|transfer|server|sync|export|convert|push", RegexOptions.IgnoreCase);
            foreach (var t in new[] { typeof(IProfileStore), typeof(ProfileStore), typeof(NewProfile), typeof(ProfileFile), typeof(SavePaths) })
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    Assert.IsFalse(bad.IsMatch(m.Name), t.Name + "." + m.Name);
            // 저장 경로는 오프라인 폴더 하나뿐.
            StringAssert.EndsWith(Path.Combine(SavePaths.ProfilesFolder, SavePaths.OfflineFolder), Dir);
        }

        // ---------------------------------------------------------------- 쓰기 규칙

        [Test]
        public void AtomicWrite__NoTempFileRemainsAfterSave()
        {
            var s = SaveSequence(3);
            Assert.IsFalse(File.Exists(s.TempPath));
            foreach (var f in Directory.GetFiles(Dir))
                Assert.IsFalse(f.EndsWith(ProfileStore.TempSuffix), f);
        }

        [Test]
        public void Generations__ThreeKeptAndShifted()
        {
            var s = SaveSequence(5);   // 저장 6번: 첫 생성(골드 0) + 골드 1..5
            Assert.AreEqual(5, GoldIn(s.GenerationPath(0)));
            Assert.AreEqual(4, GoldIn(s.GenerationPath(1)));
            Assert.AreEqual(3, GoldIn(s.GenerationPath(2)));
            Assert.AreEqual(2, GoldIn(s.GenerationPath(3)));
            Assert.IsFalse(File.Exists(s.FilePath + ".4"));
            Assert.AreEqual(4, Directory.GetFiles(Dir).Length);
        }

        [Test]
        public void Generations__FewerSavesHaveFewerGenerations()
        {
            var s = Loaded(out _, out _);   // 저장 1번
            Assert.IsFalse(File.Exists(s.GenerationPath(1)));
            s.Save();
            Assert.IsTrue(File.Exists(s.GenerationPath(1)));
            Assert.IsFalse(File.Exists(s.GenerationPath(2)));
        }

        [Test]
        public void Hash__LastLineIsSha256OfBody()
        {
            var s = Loaded(out _, out _);
            string text = File.ReadAllText(s.FilePath, new UTF8Encoding(false));
            string[] lines = text.TrimEnd('\n').Split('\n');
            string hash = lines[lines.Length - 1];
            Assert.IsTrue(Regex.IsMatch(hash, "^[0-9a-f]{64}$"), hash);
            string body = text.Substring(0, text.TrimEnd('\n').LastIndexOf('\n'));
            Assert.AreEqual(ProfileFile.Sha256Hex(body), hash);
            Assert.IsTrue(ProfileFile.TrySplit(text, out var b2));
            Assert.AreEqual(body, b2);
            // 본문을 한 글자 바꾸면 해시가 맞지 않는다.
            Assert.IsFalse(ProfileFile.TrySplit(text.Replace("\"gold\": 0", "\"gold\": 7"), out _));
            // 해시 줄이 없으면 깨진 것.
            Assert.IsFalse(ProfileFile.TrySplit(body, out _));
            // 알려진 값: SHA-256("abc")
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ProfileFile.Sha256Hex("abc"));
        }

        [Test]
        public void Load__UnparsableCurrentRestoresGenerationOne()
        {
            var s = SaveSequence(2);
            // 해시는 맞지만 JSON이 아닌 본문.
            File.WriteAllText(s.FilePath, ProfileFile.Compose("not json {"), new UTF8Encoding(false));
            var r = Loaded(out bool created, out bool restored);
            Assert.IsTrue(restored);
            Assert.IsFalse(created);
            Assert.AreEqual(1, r.Current.Currencies.Gold);
        }

        [Test]
        public void Load__SkipsBrokenGenerationsInOrder()
        {
            var s = SaveSequence(4);   // 현재 4, .1 3, .2 2, .3 1
            CorruptHash(s.FilePath);
            CorruptHash(s.GenerationPath(1));
            var r = Loaded(out _, out bool restored);
            Assert.IsTrue(restored);
            Assert.AreEqual(2, r.Current.Currencies.Gold);
        }

        [Test]
        public void Load__AllGenerationsBrokenStartsNewProfile()
        {
            var s = SaveSequence(4);
            for (int k = 0; k <= ProfileStore.Generations; k++) CorruptHash(s.GenerationPath(k));
            var r = Loaded(out bool created, out bool restored);
            Assert.IsTrue(created);
            Assert.IsFalse(restored);
            Assert.AreEqual(0, r.Current.Currencies.Gold);
            Assert.AreEqual(5, r.Current.Wands.Count);
            Assert.AreEqual(0, GoldIn(r.FilePath));
        }

        [Test]
        public void Saved__EventFiresOnSave()
        {
            var s = Loaded(out _, out _);
            int n = 0;
            s.Saved += () => n++;
            s.Save();
            Assert.AreEqual(1, n);
        }

        // ---------------------------------------------------------------- RestorePrevious

        [Test]
        public void RestorePrevious__RestoresGenerationOneAndPullsOthers()
        {
            var s = SaveSequence(4);   // 현재 4, .1 3, .2 2, .3 1
            int saved = 0;
            s.Saved += () => saved++;
            Assert.IsTrue(s.RestorePrevious());
            Assert.AreEqual(3, s.Current.Currencies.Gold);
            Assert.AreEqual(3, GoldIn(s.GenerationPath(0)));
            Assert.AreEqual(2, GoldIn(s.GenerationPath(1)));
            Assert.AreEqual(1, GoldIn(s.GenerationPath(2)));
            Assert.IsFalse(File.Exists(s.GenerationPath(3)));
            Assert.IsFalse(File.Exists(s.TempPath));
            Assert.AreEqual(1, saved);

            // 새 저장소로 다시 읽어도 되돌린 값이다.
            var r = Loaded(out _, out bool restored);
            Assert.IsFalse(restored);
            Assert.AreEqual(3, r.Current.Currencies.Gold);
        }

        [Test]
        public void RestorePrevious__NoGenerationReturnsFalseAndChangesNothing()
        {
            var s = Loaded(out _, out _);
            s.Current.Currencies.Gold = 77;
            byte[] before = File.ReadAllBytes(s.FilePath);
            var current = s.Current;
            Assert.IsFalse(s.RestorePrevious());
            Assert.AreSame(current, s.Current);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(s.FilePath));
        }

        [Test]
        public void RestorePrevious__BrokenGenerationReturnsFalseAndChangesNothing()
        {
            var s = SaveSequence(3);
            CorruptHash(s.GenerationPath(1));
            byte[] cur = File.ReadAllBytes(s.FilePath);
            byte[] g1 = File.ReadAllBytes(s.GenerationPath(1));
            byte[] g2 = File.ReadAllBytes(s.GenerationPath(2));
            Assert.IsFalse(s.RestorePrevious());
            Assert.AreEqual(3, s.Current.Currencies.Gold);
            CollectionAssert.AreEqual(cur, File.ReadAllBytes(s.FilePath));
            CollectionAssert.AreEqual(g1, File.ReadAllBytes(s.GenerationPath(1)));
            CollectionAssert.AreEqual(g2, File.ReadAllBytes(s.GenerationPath(2)));
        }

        // ---------------------------------------------------------------- 직렬화

        [Test]
        public void Serialize__SnakeCaseKeysAndCanonicalDictionaryKeys()
        {
            var s = Loaded(out _, out _);
            s.Current.Cosmetics.Equipped["HeadWear.Slot"] = "cosmetic.headwear.cracked_mirror_shard";
            s.Save();
            string text = File.ReadAllText(s.FilePath, new UTF8Encoding(false));
            foreach (var key in new[] { "schema_version", "data_version", "profile_kind", "created_at", "equipped_wand_id", "unlocked_arts",
                         "floor_peak", "record_peak", "slots_extra", "pending_offer", "qty_socketed", "qty_bound", "library_expansions",
                         "shop_week", "week_start", "phantom_met", "next_wand_id", "run_history", "last_settled_run_id",
                         "known_affinities", "last_character", "enhance_log_tail", "show_scars" })
                StringAssert.Contains("\"" + key + "\"", text, key);
            StringAssert.Contains("\"mat.stone_low\"", text);
            StringAssert.Contains("\"charm.safeguard\"", text);
            StringAssert.Contains("\"block.magic_bolt\"", text);
            StringAssert.Contains("\"HeadWear.Slot\"", text);
            StringAssert.DoesNotContain("\"SchemaVersion\"", text);
            StringAssert.Contains("\"data_version\": \"test-data-1\"", text);
        }

        [Test]
        public void Roundtrip__NewSoloFieldsSurvive()
        {
            var s = Loaded(out _, out _);
            var p = s.Current;
            p.LastSettledRunId = "run-42";
            p.LastCharacter = 3;
            p.KnownAffinities.Add("enemy.slime_moss|element.fire|weak");
            p.KnownAffinities.Add("boss.kiln_boar|element.ice|resist");
            p.RunHistory.Add(new RunRecordData
            {
                RunId = "run-42", Date = "2026-11-18T15:30:00+09:00", Character = 3, ReachedWave = 17, StartWave = 11,
                Score = 123456, WandStage = 4, Loadout = { Ids.Block.MagicBolt, null, Ids.Block.Fireball }, EndReason = "HpZero"
            });
            p.RunCheckpoint = new RunCheckpointData { RunId = "run-43", Wave = 5, Summary = JObject.Parse("{\"WaveCleared\": 5, \"at\": \"2026-11-18T10:00:00Z\"}") };
            p.Wands[0].Mutations.Add(new MutationEntry { Id = "mutation.x", Seq = 9 });
            s.Save();

            var r = Loaded(out _, out _).Current;
            Assert.AreEqual("run-42", r.LastSettledRunId);
            Assert.AreEqual(3, r.LastCharacter);
            CollectionAssert.AreEqual(p.KnownAffinities, r.KnownAffinities);
            Assert.AreEqual(1, r.RunHistory.Count);
            var h = r.RunHistory[0];
            Assert.AreEqual(17, h.ReachedWave);
            Assert.AreEqual(11, h.StartWave);
            Assert.AreEqual(123456, h.Score);
            Assert.AreEqual("2026-11-18T15:30:00+09:00", h.Date);
            CollectionAssert.AreEqual(new[] { Ids.Block.MagicBolt, null, Ids.Block.Fireball }, h.Loadout);
            Assert.AreEqual("HpZero", h.EndReason);
            Assert.AreEqual(5, (int)r.RunCheckpoint.Summary["WaveCleared"]);
            // JObject 안의 날짜 꼴 글자는 글자 그대로 남는다.
            Assert.AreEqual(JTokenType.String, r.RunCheckpoint.Summary["at"].Type);
            Assert.AreEqual("2026-11-18T10:00:00Z", (string)r.RunCheckpoint.Summary["at"]);
            Assert.AreEqual("mutation.x", r.Wands[0].Mutations[0].Id);
            // 빈 연결 칸(null)도 칸 수 그대로
            Assert.AreEqual(4, r.Wands[0].Loadout.Links.Count);
            Assert.IsNull(r.Wands[0].Loadout.Links[0]);
            Assert.AreEqual(5, r.Items[Ids.Block.MagicBolt].QtySocketed);
        }

        [Test]
        public void Roundtrip__UnknownFieldsArePreserved()
        {
            var s = Loaded(out _, out _);
            string body = ProfileFile.TrySplit(File.ReadAllText(s.FilePath, new UTF8Encoding(false)), out var b) ? b : null;
            var o = JObject.Parse(body);
            o["future_field"] = JObject.Parse("{\"a\": [1, 2, 3], \"when\": \"2030-01-01T00:00:00Z\"}");
            ((JObject)o["flags"])["future_flag"] = true;
            ((JObject)o["feats"])["future_feat"] = 12;
            File.WriteAllText(s.FilePath, ProfileFile.Compose(o.ToString()), new UTF8Encoding(false));

            var r = Loaded(out _, out bool restored);
            Assert.IsFalse(restored);
            Assert.IsNotNull(r.Current.Extra);
            Assert.IsTrue(r.Current.Extra.ContainsKey("future_field"));
            r.Current.Currencies.Gold = 9;
            r.Save();

            var again = JObject.Parse(ProfileFile.TrySplit(File.ReadAllText(s.FilePath, new UTF8Encoding(false)), out var b2) ? b2 : null);
            Assert.AreEqual(9, (long)again["currencies"]["gold"]);
            Assert.IsTrue(JToken.DeepEquals(JObject.Parse("{\"a\": [1, 2, 3], \"when\": \"2030-01-01T00:00:00Z\"}"), again["future_field"]));
            Assert.AreEqual(true, (bool)again["flags"]["future_flag"]);
            Assert.AreEqual(12, (int)again["feats"]["future_feat"]);
        }

        [Test]
        public void DataVersion__WrittenFromContentOnSave()
        {
            var s = Loaded(out _, out _);
            s.Current.DataVersion = "old";
            s.Save();
            Assert.AreEqual("test-data-1", Loaded(out _, out _).Current.DataVersion);
            Assert.AreEqual(1, s.Current.SchemaVersion);
        }

        [Test]
        public void DataRoot__DifferentRootsUseDifferentFiles()
        {
            string rootB = Path.Combine(_root, "other");
            var a = new ProfileStore(SavePaths.ProfileDirectory(_root), () => NewProfile.Create(_content, _clock), null);
            var b = new ProfileStore(SavePaths.ProfileDirectory(rootB), () => NewProfile.Create(_content, _clock), null);
            a.LoadOrCreate(out _, out _);
            b.LoadOrCreate(out bool createdB, out _);
            Assert.IsTrue(createdB);
            a.Current.Currencies.Gold = 11;
            a.Save();
            Assert.AreNotEqual(a.FilePath, b.FilePath);
            Assert.AreEqual(11, GoldIn(a.FilePath));
            Assert.AreEqual(0, GoldIn(b.FilePath));
            Assert.AreEqual(Path.Combine(Path.Combine(Path.Combine(_root, "profiles"), "offline"), "profile.json"), a.FilePath);
            Assert.AreEqual(Path.Combine(rootB, "settings.json"), SavePaths.SettingsFile(rootB));
        }
    }
}
