using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;

namespace Wizard.Unity.Editor
{
    // FIX-3: 플레이어 빌드 앞뒤의 기본 팩 놓기와 지우기. 진짜 Assets 가 아니라 임시 폴더의 Assets/StreamingAssets 로 본다.
    // 팩의 꼴은 CONTRACTS 11.2(magic "WZPK", schema u16, data_version u32, core_compat[16], section_count u16, 목차, sha256[32])다.
    public sealed class ContentPackStageTests
    {
        private string _root = "";
        private string _assets = "";
        private string _streaming = "";
        private string _source = "";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "wizard-fix3-" + Guid.NewGuid().ToString("N"));
            _assets = Path.Combine(_root, "unity", "Assets");
            _streaming = Path.Combine(_assets, "StreamingAssets");
            Directory.CreateDirectory(_assets);
            File.WriteAllText(Path.Combine(_assets, "Keep.txt"), "not ours");
            _source = ContentPackStage.DefaultSourcePath(_root);
            Directory.CreateDirectory(Path.GetDirectoryName(_source)!);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        // ---- 팩의 꼴 ----

        private static byte[] WellFormedPack(uint dataVersion)
        {
            var body = new MemoryStream();
            body.Write(new[] { (byte)'W', (byte)'Z', (byte)'P', (byte)'K' }, 0, 4);
            WriteU16(body, 1);                    // schema
            WriteU32(body, dataVersion);
            body.Write(new byte[16], 0, 16);      // core_compat
            WriteU16(body, 0);                    // section_count: 목차와 절 없음
            byte[] head = body.ToArray();
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(head);
            }
            var pack = new byte[head.Length + digest.Length];
            Array.Copy(head, 0, pack, 0, head.Length);
            Array.Copy(digest, 0, pack, head.Length, digest.Length);
            return pack;
        }

        private static void WriteU16(Stream s, ushort v)
        {
            s.WriteByte((byte)v);
            s.WriteByte((byte)(v >> 8));
        }

        private static void WriteU32(Stream s, uint v)
        {
            WriteU16(s, (ushort)v);
            WriteU16(s, (ushort)(v >> 16));
        }

        private static string? AcceptAll(byte[] bytes) => null;

        private string[] AssetsEntries()
        {
            string[] entries = Directory.GetFileSystemEntries(_assets, "*", SearchOption.AllDirectories);
            Array.Sort(entries, StringComparer.Ordinal);
            return entries;
        }

        // Unity 가 새로 읽으면서 만드는 .meta 를 흉내 낸다
        private void ImportLikeUnity()
        {
            File.WriteAllText(_streaming + ".meta", "guid: a");
            File.WriteAllText(Path.Combine(_streaming, "content") + ".meta", "guid: b");
            File.WriteAllText(ContentPackStage.TargetPathIn(_streaming) + ".meta", "guid: c");
        }

        // ---- ContentPack.Load 검사(빌드가 쓰는 진짜 검사) ----

        [Test]
        public void LoadValidator_AcceptsWellFormedPack()
        {
            Assert.That(ContentPackStage.LoadValidator()(WellFormedPack(7)), Is.Null);
        }

        [Test]
        public void LoadValidator_RejectsTamperedShaGarbageAndEmpty()
        {
            Func<byte[], string?> validate = ContentPackStage.LoadValidator();
            byte[] tampered = WellFormedPack(7);
            tampered[6] ^= 0x01;   // data_version 의 한 비트: 해시가 맞지 않는다
            Assert.That(validate(tampered), Does.Contain("sha256"));
            Assert.That(validate(new byte[64]), Does.Contain("magic"));   // 길이는 머리말+해시보다 길고 magic 이 0
            Assert.That(validate(Array.Empty<byte>()), Is.Not.Null);
        }

        // ---- 놓기와 지우기 ----

        [Test]
        public void Place_CopiesPackBytesIntoStreamingAssetsContent()
        {
            byte[] pack = WellFormedPack(3);
            File.WriteAllBytes(_source, pack);

            bool ok = ContentPackStage.TryPlace(_source, _streaming, ContentPackStage.LoadValidator(), out ContentPackStage? stage, out string message);

            Assert.That(ok, Is.True, message);
            Assert.That(stage, Is.Not.Null);
            string target = Path.Combine(_streaming, "content", "base.wzpack");
            Assert.That(stage!.TargetPath, Is.EqualTo(target));
            Assert.That(File.ReadAllBytes(target), Is.EqualTo(pack));
            Assert.That(stage.Sha256, Is.EqualTo(ContentPackStage.Sha256Hex(pack)));
            Assert.That(stage.Sha256, Has.Length.EqualTo(64));
            Assert.That(stage.Sha256, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(stage.Removed, Is.False);
            Assert.That(stage.TryRemove(out _), Is.True);
        }

        [Test]
        public void Remove_DeletesPackMetaAndCreatedFolders_LeavingAssetsAsBefore()
        {
            string[] before = AssetsEntries();
            File.WriteAllBytes(_source, WellFormedPack(3));
            Assert.That(ContentPackStage.TryPlace(_source, _streaming, AcceptAll, out ContentPackStage? stage, out string placed), Is.True, placed);
            ImportLikeUnity();

            bool removed = stage!.TryRemove(out string message);

            Assert.That(removed, Is.True, message);
            Assert.That(stage.Removed, Is.True);
            Assert.That(AssetsEntries(), Is.EqualTo(before), "no file or folder left under Assets");
            Assert.That(File.Exists(_source), Is.True, "the databuild output stays");
        }

        [Test]
        public void Remove_Twice_IsHarmless()
        {
            string[] before = AssetsEntries();
            File.WriteAllBytes(_source, WellFormedPack(3));
            Assert.That(ContentPackStage.TryPlace(_source, _streaming, AcceptAll, out ContentPackStage? stage, out string placed), Is.True, placed);

            Assert.That(stage!.TryRemove(out string first), Is.True, first);
            Assert.That(stage.TryRemove(out string second), Is.True, second);
            Assert.That(AssetsEntries(), Is.EqualTo(before));
        }

        [Test]
        public void Remove_KeepsExistingStreamingAssetsFolderAndItsOtherFiles()
        {
            Directory.CreateDirectory(_streaming);
            File.WriteAllText(_streaming + ".meta", "guid: tracked");
            File.WriteAllText(Path.Combine(_streaming, "other.bytes"), "someone else's");
            string[] before = AssetsEntries();
            File.WriteAllBytes(_source, WellFormedPack(3));
            Assert.That(ContentPackStage.TryPlace(_source, _streaming, AcceptAll, out ContentPackStage? stage, out string placed), Is.True, placed);
            ImportLikeUnity();
            File.WriteAllText(_streaming + ".meta", "guid: tracked");   // ImportLikeUnity 가 덮어쓴 것을 원래대로(이미 있던 폴더의 .meta 는 Unity 가 바꾸지 않는다)

            Assert.That(stage!.TryRemove(out string message), Is.True, message);

            Assert.That(AssetsEntries(), Is.EqualTo(before));
            Assert.That(File.ReadAllText(_streaming + ".meta"), Is.EqualTo("guid: tracked"));
        }

        [Test]
        public void Remove_KeepsCreatedFolderThatGotSomeoneElsesFile()
        {
            File.WriteAllBytes(_source, WellFormedPack(3));
            Assert.That(ContentPackStage.TryPlace(_source, _streaming, AcceptAll, out ContentPackStage? stage, out string placed), Is.True, placed);
            string foreign = Path.Combine(_streaming, "content", "names.ko.wznm");
            File.WriteAllText(foreign, "not placed by the stage");

            Assert.That(stage!.TryRemove(out string message), Is.True, message);

            Assert.That(File.Exists(ContentPackStage.TargetPathIn(_streaming)), Is.False, "our pack is gone");
            Assert.That(File.Exists(foreign), Is.True, "a file the stage did not place is kept");
            Assert.That(message, Does.Contain("not empty"));
        }

        [Test]
        public void Remove_RestoresAHandPlacedPackAndMeta()
        {
            string target = ContentPackStage.TargetPathIn(_streaming);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            byte[] handPlaced = WellFormedPack(99);
            File.WriteAllBytes(target, handPlaced);
            File.WriteAllText(target + ".meta", "guid: hand");
            string[] before = AssetsEntries();
            byte[] built = WellFormedPack(3);
            File.WriteAllBytes(_source, built);

            Assert.That(ContentPackStage.TryPlace(_source, _streaming, AcceptAll, out ContentPackStage? stage, out string placed), Is.True, placed);
            Assert.That(File.ReadAllBytes(target), Is.EqualTo(built), "the build uses the databuild output");
            File.WriteAllText(target + ".meta", "guid: reimported");
            Assert.That(stage!.TryRemove(out string message), Is.True, message);

            Assert.That(AssetsEntries(), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(target), Is.EqualTo(handPlaced));
            Assert.That(File.ReadAllText(target + ".meta"), Is.EqualTo("guid: hand"));
        }

        // ---- 실패 경로: 건너뛰지 않고 무엇이 없는지 적고 실패한다 ----

        [Test]
        public void Place_MissingPack_FailsNamingThePathAndDatabuild_PlacingNothing()
        {
            string[] before = AssetsEntries();

            bool ok = ContentPackStage.TryPlace(_source, _streaming, ContentPackStage.LoadValidator(), out ContentPackStage? stage, out string message);

            Assert.That(ok, Is.False);
            Assert.That(stage, Is.Null);
            Assert.That(message, Does.Contain(_source));
            Assert.That(message, Does.Contain("not found"));
            Assert.That(message, Does.Contain("Wizard.DataBuild"));
            Assert.That(AssetsEntries(), Is.EqualTo(before));
        }

        [Test]
        public void Place_UnloadablePack_FailsWithTheLoadReason_PlacingNothing()
        {
            string[] before = AssetsEntries();
            byte[] broken = WellFormedPack(3);
            broken[broken.Length - 1] ^= 0xFF;   // 끝의 sha256 이 틀린다
            File.WriteAllBytes(_source, broken);

            bool ok = ContentPackStage.TryPlace(_source, _streaming, ContentPackStage.LoadValidator(), out ContentPackStage? stage, out string message);

            Assert.That(ok, Is.False);
            Assert.That(stage, Is.Null);
            Assert.That(message, Does.Contain(_source));
            Assert.That(message, Does.Contain("ContentPack.Load"));
            Assert.That(message, Does.Contain("sha256 mismatch"));
            Assert.That(AssetsEntries(), Is.EqualTo(before));
        }

        [Test]
        public void Place_WhenTheContentFolderCannotBeCreated_FailsAndUndoes()
        {
            Directory.CreateDirectory(_streaming);
            File.WriteAllText(Path.Combine(_streaming, "content"), "a file where the folder should be");
            string[] before = AssetsEntries();
            File.WriteAllBytes(_source, WellFormedPack(3));

            bool ok = ContentPackStage.TryPlace(_source, _streaming, AcceptAll, out ContentPackStage? stage, out string message);

            Assert.That(ok, Is.False);
            Assert.That(stage, Is.Null);
            Assert.That(message, Does.Contain("cannot place"));
            Assert.That(AssetsEntries(), Is.EqualTo(before));
        }

        [Test]
        public void Place_WhenStreamingAssetsCannotBeCreated_FailsAndTouchesNothing()
        {
            // StreamingAssets 의 부모가 폴더가 아니라 파일이다: 만들지 못하고 그 파일도 그대로 둔다
            string[] before = AssetsEntries();
            string blocked = Path.Combine(_root, "blocked-root");
            File.WriteAllText(blocked, "a file, not a folder");
            File.WriteAllBytes(_source, WellFormedPack(3));

            bool ok = ContentPackStage.TryPlace(_source, Path.Combine(blocked, "StreamingAssets"), AcceptAll, out ContentPackStage? stage, out string message);

            Assert.That(ok, Is.False);
            Assert.That(stage, Is.Null);
            Assert.That(message, Does.Contain("cannot place"));
            Assert.That(AssetsEntries(), Is.EqualTo(before));
            Assert.That(File.ReadAllText(blocked), Is.EqualTo("a file, not a folder"));
        }

        // ---- 플레이어 안의 자리 ----

        [Test]
        public void PlayerPackPath_IsTheMacPlayerStreamingAssetsContentPack()
        {
            Assert.That(ContentPackStage.PlayerPackPath(Path.Combine("out", "Wizard.app")),
                Is.EqualTo(Path.Combine("out", "Wizard.app", "Contents", "Resources", "Data", "StreamingAssets", "content", "base.wzpack")));
        }
    }
}
