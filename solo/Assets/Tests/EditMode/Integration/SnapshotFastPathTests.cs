using NUnit.Framework;
using Wizard.Solo.Net;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 스냅숏 최적화(빠른 길·다시 쓰는 버퍼)가 바이트를 바꾸지 않는지: 식 트리 빠른 길 = 리플렉션 길,
    /// 다시 쓰는 NetWriter·NetReader·DeflateTo·InflateTo = 새로 만든 것.
    /// </summary>
    public class SnapshotFastPathTests
    {
        IntegrationKit _k;
        bool _fastBefore;

        [SetUp]
        public void SetUp()
        {
            _k = new IntegrationKit();
            _fastBefore = WorldSnapshot.FastPathEnabled;
        }

        [TearDown]
        public void TearDown()
        {
            WorldSnapshot.FastPathEnabled = _fastBefore;
            WorldSnapshot.ResetTypeCache();
            _k.Dispose();
        }

        SimWorld BusyWorld()
        {
            _k.StartRun(1, 7UL);
            _k.AutoPlay(1800, w => w.Run.Phase == RunPhase.WaveCombat && w.Enemies.Count > 0 && w.Projectiles.Count > 0);
            return _k.W;
        }

        byte[] Write(SimWorld w, bool fast)
        {
            WorldSnapshot.FastPathEnabled = fast;
            WorldSnapshot.ResetTypeCache();
            var nw = new NetWriter();
            WorldSnapshot.Write(nw, w);
            return nw.ToArray();
        }

        SimWorld Puppet()
        {
            var p = new SimWorld(_k.Db, SimModuleSet.CreateDefault(), _k.Config.Seed);
            p.StartRun(NetCodec.Clone(_k.Config));
            p.Events.Clear();
            return p;
        }

        [Test]
        public void FastPath_WritesSameBytesAsReflection()
        {
            var w = BusyWorld();
            byte[] slow = Write(w, false);
            WorldSnapshot.FastPathEnabled = true;
            WorldSnapshot.ResetTypeCache();
            WorldSnapshot.Warm();
            WorldSnapshot.Warm(); // 여러 번 불러도 된다
            var warmed = new NetWriter();
            WorldSnapshot.Write(warmed, w);
            CollectionAssert.AreEqual(slow, warmed.ToArray(), "미리 컴파일한 빠른 길");
            byte[] fast = Write(w, true);
            CollectionAssert.AreEqual(slow, fast, "빠른 길도 리플렉션 길과 같은 바이트를 써야 한다");
        }

        [Test]
        public void FastPath_ApplyRoundTripIsLossless()
        {
            var w = BusyWorld();
            byte[] slow = Write(w, false);
            WorldSnapshot.FastPathEnabled = true;
            WorldSnapshot.ResetTypeCache();
            var puppet = Puppet();
            // 다시 쓰는 읽개로 두 번 덮어써도(두 번째는 같은 칸의 객체 위에) 같은 결과
            var reader = new NetReader();
            reader.Load(slow, 0, slow.Length);
            WorldSnapshot.Apply(reader, puppet);
            reader.Load(slow, 0, slow.Length);
            WorldSnapshot.Apply(reader, puppet);
            byte[] again = Write(puppet, true);
            CollectionAssert.AreEqual(slow, again, "빠른 길로 덮어쓴 꼭두각시를 다시 쓰면 같은 바이트");
        }

        [Test]
        public void ReusedWriter_And_DeflateTo_InflateTo_MatchFresh()
        {
            var w = BusyWorld();
            byte[] fresh = Write(w, true);

            var body = new NetWriter();
            body.Str("버려질 앞 내용");
            body.Reset();
            WorldSnapshot.Write(body, w);
            Assert.AreEqual(fresh.Length, body.Length);
            CollectionAssert.AreEqual(fresh, body.ToArray(), "Reset 뒤 다시 쓴 몸");

            var packedMs = new System.IO.MemoryStream();
            int n = NetCodec.DeflateTo(body.GetBuffer(), body.Length, packedMs);
            byte[] packedOld = NetCodec.Deflate(fresh);
            Assert.AreEqual(packedOld.Length, n);
            CollectionAssert.AreEqual(packedOld, packedMs.ToArray(), "DeflateTo = Deflate");

            var msg = new NetWriter(NetMsg.Snapshot);
            msg.Bytes(packedMs.GetBuffer(), 0, n);
            var msgOld = new NetWriter(NetMsg.Snapshot);
            msgOld.Bytes(packedOld);
            CollectionAssert.AreEqual(msgOld.ToArray(), msg.ToArray(), "Bytes(buf, off, len) = Bytes(arr)");

            var r = new NetReader(msg.ToArray());
            Assert.AreEqual((byte)NetMsg.Snapshot, r.Byte());
            var into = new NetReader();
            Assert.IsTrue(NetCodec.InflateTo(r, into));
            Assert.IsTrue(r.AtEnd);
            var puppet = Puppet();
            WorldSnapshot.Apply(into, puppet);
            Assert.IsTrue(into.AtEnd, "풀린 몸을 끝까지 읽는다");
            CollectionAssert.AreEqual(fresh, Write(puppet, true), "InflateTo로 받은 스냅숏");
        }

        [Test]
        public void NetWriterReader_Strings_RoundTrip()
        {
            string longKo = new string('가', 300);
            var w = new NetWriter();
            w.Str(null); w.Str(string.Empty); w.Str("abc"); w.Str("가나다"); w.Str(longKo); w.Str("ab\U0001F600cd");
            w.Bytes(null); w.Bytes(new byte[] { 1, 2, 3 }, 1, 2);
            byte[] data = w.ToArray();
            foreach (var r in new[] { new NetReader(data), Reload(data) })
            {
                Assert.IsNull(r.Str());
                Assert.AreEqual(string.Empty, r.Str());
                Assert.AreEqual("abc", r.Str());
                Assert.AreEqual("가나다", r.Str());
                Assert.AreEqual(longKo, r.Str());
                Assert.AreEqual("ab\U0001F600cd", r.Str());
                Assert.IsNull(r.Bytes());
                Assert.IsTrue(r.BytesView(out var buf, out int off, out int len));
                Assert.AreEqual(2, len);
                Assert.AreEqual(2, buf[off]);
                Assert.AreEqual(3, buf[off + 1]);
                Assert.IsTrue(r.AtEnd);
            }
        }

        static NetReader Reload(byte[] data)
        {
            var r = new NetReader();
            r.Load(new byte[] { 9, 9, 9 }, 0, 3); // 앞 내용이 남지 않아야 한다
            r.Load(data, 0, data.Length);
            return r;
        }
    }
}
