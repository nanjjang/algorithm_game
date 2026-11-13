using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;
using Xunit;
using static Wizard.Sim.Tests.Spell.Compile.CompileKit;

namespace Wizard.Sim.Tests.Spell.Compile
{
    // 수락 조건 "SpellTable 이 세계 상태다"(CONTRACTS 3.2)의 표 자체(SpellTableCore). 세계에 이은 것은 SpellTableWorldTests 가 본다.
    public sealed class SpellTableCoreTests
    {
        private static CompileSource A => Dalgu(Bolt, L(Split3, Trig, Fireball, __));
        private static CompileSource B => Galpi(Orb, L(Pierce, Trig, Chain, Homing, Frost));
        private static CompileSource C => Dalgu(Fireball, L(Bolt, __, __, __), ext: 0, mutations: Mut(MutationId.MirrorImage));

        private static SpellTableCore NewTable() => new SpellTableCore();

        private static ulong Hash(SpellTableCore t)
        {
            StateWriter w = StateWriter.ForHash();
            t.Write(ref w);
            return w.Hash;
        }

        private static byte[] Bytes(SpellTableCore t)
        {
            var bw = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(bw);
            t.Write(ref w);
            return bw.ToArray();
        }

        [Fact]
        public void HandleIsAddOrderIndex_SameSourceSameHandle()
        {
            SpellTableCore t = NewTable();
            Assert.Equal(0, t.Add(A, Db, TestRules).Value);
            Assert.Equal(1, t.Add(B, Db, TestRules).Value);
            Assert.Equal(0, t.Add(A, Db, TestRules).Value);                  // 같은 원천(바이트가 같다)은 있던 핸들
            Assert.Equal(1, t.Add(B, Db, TestRules).Value);
            Assert.Equal(2, t.Count);
            Assert.Equal(Build(A).Hash, t.Get(new SpellHandle(0)).Hash);   // 값은 같은 원천의 컴파일 결과
            Assert.Throws<SimInvariantException>(() => t.Get(new SpellHandle(2)));
            Assert.Throws<SimInvariantException>(() => t.Get(new SpellHandle(-1)));
        }

        [Fact]
        public void LengthAndSourcesAreInHash()
        {
            SpellTableCore empty = NewTable();
            SpellTableCore ab = NewTable();
            ab.Add(A, Db, TestRules);
            ab.Add(B, Db, TestRules);
            SpellTableCore ba = NewTable();
            ba.Add(B, Db, TestRules);
            ba.Add(A, Db, TestRules);
            SpellTableCore ac = NewTable();
            ac.Add(A, Db, TestRules);
            ac.Add(C, Db, TestRules);
            Assert.NotEqual(Hash(empty), Hash(ab));
            Assert.NotEqual(Hash(ab), Hash(ba));               // 핸들 순서도 상태다
            Assert.NotEqual(Hash(ab), Hash(ac));               // 원천이 다르면 다르다
            Assert.Equal(StateHashOfBytes(Bytes(ab)), Hash(ab));   // 해시와 WriteState 가 같은 바이트를 센다
        }

        [Fact]
        public void SnapshotLength_NewHandles_Restore_RecreateInOtherOrder_SameHash()
        {
            SpellTableCore t = NewTable();
            t.Add(A, Db, TestRules);
            int saved = t.Count;                               // 스냅숏에는 길이가 들어간다
            ulong before = Hash(t);
            t.Add(B, Db, TestRules);
            t.Add(C, Db, TestRules);
            Assert.NotEqual(before, Hash(t));
            t.Truncate(saved);                                 // 복원
            Assert.Equal(before, Hash(t));
            Assert.Equal(1, t.Count);
            Assert.Throws<SimInvariantException>(() => t.Get(new SpellHandle(1)));

            // 되감은 뒤 다른 순서로 다시 만든다: 해시는 그 순서의 새 표와 같다(복원이 남긴 것이 없다)
            t.Add(C, Db, TestRules);
            t.Add(B, Db, TestRules);
            SpellTableCore fresh = NewTable();
            fresh.Add(A, Db, TestRules);
            fresh.Add(C, Db, TestRules);
            fresh.Add(B, Db, TestRules);
            Assert.Equal(Hash(fresh), Hash(t));
            Assert.Equal(1, t.Add(C, Db, TestRules).Value);
            Assert.Throws<SimInvariantException>(() => t.Truncate(4));   // 저장 때보다 짧을 수는 없다
        }

        [Fact]
        public void WriteState_ReadIntoNewTable_AllHandlesExecutable()
        {
            SpellTableCore t = NewTable();
            t.Add(A, Db, TestRules);
            t.Add(B, Db, TestRules);
            t.Add(C, Db, TestRules);
            t.Add(Dalgu(__, L(__, __, __, __)), Db, TestRules);                     // 무효 조립도 원천으로 남는다
            byte[] bytes = Bytes(t);

            SpellTableCore other = NewTable();
            other.Add(B, Db, TestRules);                                       // 읽기는 표 전체를 덮어쓴다
            var r = new StateReader(bytes);
            other.Read(ref r, Db, TestRules);
            Assert.Equal(0, r.Remaining);
            Assert.Equal(t.Count, other.Count);
            Assert.Equal(Hash(t), Hash(other));
            for (int i = 0; i < t.Count; i++)
            {
                var h = new SpellHandle(i);
                CompiledSpell x = t.Get(h);
                CompiledSpell y = other.Get(h);                    // 원천에서 다시 컴파일했다
                Assert.Equal(x.Hash, y.Hash);
                Assert.Equal(x.IsValid, y.IsValid);
                Assert.Equal(CompileKit.Print(x), CompileKit.Print(y));
            }
        }

        [Fact]
        public void ReadState_DuplicateSourceIsBroken()
        {
            var bw = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(bw);
            w.Count(2);
            A.Write(ref w);
            A.Write(ref w);
            var r = new StateReader(bw.ToArray());
            SpellTableCore t = NewTable();
            Assert.Throws<SimInvariantException>(() => ReadInto(t, bw.ToArray()));
        }

        [Fact]
        public void GetDoesNotAllocate()
        {
            SpellTableCore t = NewTable();
            SpellHandle h = t.Add(A, Db, TestRules);
            t.Get(h);
            long before = GC.GetAllocatedBytesForCurrentThread();
            int sum = 0;
            for (int i = 0; i < 1000; i++)
            {
                sum += t.Get(h).HeatPerShotHu;
            }
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.True(sum > 0);
        }

        [Fact]
        public void TableGrowsBeyondInitialCapacity()
        {
            SpellTableCore t = NewTable();
            BlockId[] cores = { Bolt, Fireball, Chain, BlockId.Boomerang, Ice, Orb };
            int n = 0;
            foreach (BlockId core in cores)
            {
                foreach (BlockId link in new[] { __, Bolt, Size })
                {
                    Assert.Equal(n, t.Add(Dalgu(core, L(link, __, __, __)), Db, TestRules).Value);
                    n++;
                }
            }
            Assert.Equal(18, t.Count);
            Assert.Equal(17, t.Add(Dalgu(Orb, L(Size, __, __, __)), Db, TestRules).Value);
        }

        private static void ReadInto(SpellTableCore t, byte[] bytes)
        {
            var r = new StateReader(bytes);
            t.Read(ref r, Db, TestRules);
        }

        private static ulong StateHashOfBytes(byte[] bytes) => Wizard.Core.Hash.StateHasher.Compute(bytes);
    }
}
