using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Arts;
using Xunit;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // CONTRACTS 3.5의 겉면(T-52): ICommandParser 로만 부른 판정, ArtDecision 의 바이트 왕복, TrieNode, CommandPreview.
    // 미리보기 벡터(B08·B13·B14·B15·B15b·B37)는 PlayerView 까지 잇는 비기 공통 작업(M5-ART-01)에 배정되어 있어
    // 벡터 이름을 쓰지 않는다. 여기서는 같은 규칙(spec 03 11절)을 판정기의 CommandPreview 에서 본다
    public sealed class ParserContractTests
    {
        private const long T0 = ParserRig.T0;
        private const long S015 = 2304;                                        // 0.15초

        private static ICommandParser InStance(CharacterId c, out CommandTrie trie)
        {
            CommandParser p = ParserRig.InStance(c);
            trie = p.Trie;
            return p;
        }

        // 계약의 인터페이스만으로 판정 결과 전부를 받는다(spec 03 7.4의 필드)
        [Fact]
        public void Interface_OnSkillPress_CarriesTheWholeDecision()
        {
            ICommandParser ip = InStance(CharacterId.Char1, out CommandTrie trie);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            UnlockSet u = Unlocks.All.For(CharacterId.Char1);
            ip.OnDirection(CmdDir.Forward, T0);
            ip.OnDirection(CmdDir.Forward, T0 + S015);
            Assert.True(ip.OnSkillPress(T0 + S015 + 500, w, in u, out ArtDecision d));
            Assert.Equal(ArtOutcome.Fire, d.Outcome);
            Assert.True(d.IsFire);
            Assert.Equal(ArtId.ThunderSpear, d.Art);
            Assert.Equal(ArtId.ThunderSpear, d.SelectedArt);
            Assert.True(d.IsJust);
            Assert.False(d.ViaFallback);
            Assert.Equal(1, d.CrystalsPaid);
            Assert.Equal(0, d.DebtAdded);
            Assert.Equal(4, d.CrystalsAfter);
            Assert.Equal(0, d.Repeats);
            Assert.Equal(FizzleReason.None, d.Reason);
            Assert.Equal(T0 + S015 + 500, d.JudgeTime);
            Assert.Equal(trie.Find(ParserRig.Path("FF")), d.NodeId);
            Assert.Equal(0, d.JustLateByUnits);
            Assert.Equal(4, w.Crystals);
        }

        [Fact]
        public void Interface_FallbackCreditAndBloomFields()
        {
            // char.5 결정 0·빚 1, 좌·우·K: 그림자 손(2) 결제 실패 → 부모 양익 사격(1)이 외상으로(spec 03 9.2-6)
            ICommandParser ip = InStance(CharacterId.Char5, out CommandTrie trie);
            TestWallet w = SpecArts.Wallet(CharacterId.Char5, 0, 1);
            UnlockSet u = Unlocks.All.For(CharacterId.Char5);
            ip.OnDirection(CmdDir.Left, T0);
            ip.OnDirection(CmdDir.Right, T0 + S015);
            Assert.True(ip.OnSkillPress(T0 + 2 * S015, w, in u, out ArtDecision d));
            Assert.Equal(ArtId.Wings, d.Art);
            Assert.Equal(ArtId.ShadowHand, d.SelectedArt);
            Assert.True(d.ViaFallback);
            Assert.Equal(0, d.CrystalsPaid);
            Assert.Equal(1, d.DebtAdded);
            Assert.Equal(0, d.CrystalsAfter);
            Assert.Equal(trie.Find(ParserRig.Path("LR")), d.NodeId);
            Assert.Equal(2, w.Debt);

            // 만개(결정 4 전부, 반복 4)와 만개 간격 초과(3번째 입력이 1단위 늦음)
            ICommandParser ib = InStance(CharacterId.Char3, out _);
            TestWallet wb = SpecArts.Wallet(CharacterId.Char3, 4, 0);
            UnlockSet ub = Unlocks.All.For(CharacterId.Char3);
            long g = SpecArts.GapFullBloom;
            ib.OnDirection(CmdDir.Forward, T0);
            ib.OnDirection(CmdDir.Back, T0 + g);
            ib.OnDirection(CmdDir.Back, T0 + 2 * g);
            ib.OnDirection(CmdDir.Forward, T0 + 3 * g);
            Assert.True(ib.OnSkillPress(T0 + 4 * g, wb, in ub, out ArtDecision bloom));
            Assert.Equal(ArtId.Bloom, bloom.Art);
            Assert.Equal(4, bloom.Repeats);
            Assert.Equal(4, bloom.CrystalsPaid);
            Assert.Equal(0, bloom.CrystalsAfter);

            ICommandParser il = InStance(CharacterId.Char3, out _);
            TestWallet wl = SpecArts.Wallet(CharacterId.Char3, 4, 0);
            il.OnDirection(CmdDir.Forward, T0);
            il.OnDirection(CmdDir.Back, T0 + g);
            il.OnDirection(CmdDir.Back, T0 + 2 * g + 1);
            il.OnDirection(CmdDir.Forward, T0 + 3 * g);
            Assert.True(il.OnSkillPress(T0 + 4 * g, wl, in ub, out ArtDecision late));
            Assert.Equal(ArtOutcome.Fizzle, late.Outcome);
            Assert.Equal(FizzleReason.FullBloomLate, late.Reason);
            Assert.Equal(ArtId.Bloom, late.Art);
            Assert.Equal(3, late.LateIndex);
            Assert.Equal(1, late.LateByUnits);
            Assert.Equal(4, wl.Crystals);
        }

        // 보관된 K 도 인터페이스로 같은 판정을 낸다. tickStartTime = 틱 × 256
        [Fact]
        public void Interface_TryTakeBuffered_ReturnsTheHeldDecision()
        {
            var p = new CommandParser(SpecArts.Trie(CharacterId.Char1));
            ICommandParser ip = p;
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            UnlockSet u = Unlocks.All.For(CharacterId.Char1);
            Assert.True(ip.OnSkillPress(T0, w, in u, out _));
            Assert.False(ip.OnSkillPress(T0 + 4700, w, in u, out ArtDecision none));
            Assert.Equal(default(ArtDecision).JudgeTime, none.JudgeTime);
            long fireTick = (T0 + SpecArts.ArtCooldown + 255) / 256;
            Assert.False(ip.TryTakeBuffered((fireTick - 1) * 256, out _));
            Assert.True(ip.TryTakeBuffered(fireTick * 256, out ArtDecision held));
            Assert.Equal(ArtId.Burst, held.Art);
            Assert.Equal(T0 + 4700, held.JudgeTime);
            Assert.Equal(1, held.CrystalsPaid);
            Assert.Equal(3, held.CrystalsAfter);
        }

        // 인터페이스의 WriteState → ReadState 가 상태 전체(보관된 판정 포함)를 옮긴다
        [Fact]
        public void Interface_ReadState_RestoresWhatWriteStateWrote()
        {
            CommandTrie trie = SpecArts.Trie(CharacterId.Char1);
            ICommandParser a = new CommandParser(trie);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            UnlockSet u = Unlocks.All.For(CharacterId.Char1);
            a.OnStanceChanged(true, T0);
            Assert.True(a.OnSkillPress(T0 + 10, w, in u, out _));
            a.OnDirection(CmdDir.Forward, T0 + 4000);
            a.OnDirection(CmdDir.Forward, T0 + 4000 + S015);
            Assert.False(a.OnSkillPress(T0 + 10 + SpecArts.ArtCooldown - 5, w, in u, out _));     // 꿰뚫는 창을 보관
            a.OnDirection(CmdDir.Back, T0 + 10 + SpecArts.ArtCooldown - 2);

            byte[] bytes = StateBytes(a);

            ICommandParser b = new CommandParser(SpecArts.Trie(CharacterId.Char1));
            var sr = new StateReader(bytes);
            b.ReadState(ref sr);
            Assert.Equal(0, sr.Remaining);
            Assert.Equal(bytes, StateBytes(b));

            long tick = (T0 + 10 + SpecArts.ArtCooldown + 255) / 256 * 256;
            Assert.True(a.TryTakeBuffered(tick, out ArtDecision da));
            Assert.True(b.TryTakeBuffered(tick, out ArtDecision db));
            Assert.Equal(ArtId.Spear, da.Art);
            Assert.Equal(Encode(da), Encode(db));
        }

        // ArtDecision.Write/Read: 모든 칸이 왕복하고, 모르는 열거형 값은 거절한다
        [Fact]
        public void ArtDecision_WriteRead_RoundTripsEveryField()
        {
            var d = new ArtDecision(ArtOutcome.Fizzle, ArtId.Bloom, ArtId.ShadowHand, true, true, 3, 2, 4, 7,
                                    FizzleReason.FullBloomLate, 5, 1234567890123L, 9876543210L, 13, 307);
            var bw = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(bw);
            d.Write(ref w);
            byte[] bytes = bw.ToArray();
            var r = new StateReader(bytes);
            ArtDecision e = ArtDecision.Read(ref r);
            Assert.Equal(0, r.Remaining);
            Assert.Equal(Encode(d), Encode(e));
            Assert.Equal(ArtOutcome.Fizzle, e.Outcome);
            Assert.Equal(ArtId.Bloom, e.Art);
            Assert.Equal(ArtId.ShadowHand, e.SelectedArt);
            Assert.True(e.ViaFallback);
            Assert.True(e.IsJust);
            Assert.Equal((3, 2, 4, 7, 5), (e.CrystalsPaid, e.DebtAdded, e.CrystalsAfter, e.Repeats, e.LateIndex));
            Assert.Equal(FizzleReason.FullBloomLate, e.Reason);
            Assert.Equal(1234567890123L, e.LateByUnits);
            Assert.Equal(9876543210L, e.JudgeTime);
            Assert.Equal(13, e.NodeId);
            Assert.Equal(307, e.JustLateByUnits);

            foreach (int at in new[] { 0, 1, 2, 9 })                           // outcome, art, selected, reason
            {
                byte[] bad = (byte[])bytes.Clone();
                bad[at] = 0xEE;
                Assert.Throws<SimInvariantException>(() =>
                {
                    var br = new StateReader(bad);
                    ArtDecision.Read(ref br);
                });
            }
        }

        // 번호는 기록·해시에 들어간다. 더하기만 하고 바꾸지 않는다(CONTRACTS 3.5)
        [Fact]
        public void EnumNumbers_AreFixed()
        {
            Assert.Equal(0, (int)ArtOutcome.Fire);
            Assert.Equal(1, (int)ArtOutcome.Fizzle);
            Assert.Equal(0, (int)FizzleReason.None);
            Assert.Equal(1, (int)FizzleReason.NoCrystal);
            Assert.Equal(2, (int)FizzleReason.FullBloomLate);
        }

        // Node(id)의 값이 노드별 접근자와 같다(다섯 캐릭터의 모든 노드)
        [Fact]
        public void TrieNode_MatchesAccessors_ForEveryNode()
        {
            foreach (CharacterId c in new[] { CharacterId.Char1, CharacterId.Char2, CharacterId.Char3, CharacterId.Char4, CharacterId.Char5 })
            {
                CommandTrie t = SpecArts.Trie(c);
                for (int id = 0; id < t.NodeCount; id++)
                {
                    ref readonly TrieNode n = ref t.Node(id);
                    Assert.Equal(id, n.Id);
                    Assert.Equal(t.Depth(id), n.Depth);
                    Assert.Equal(t.Parent(id), n.Parent);
                    Assert.Equal(t.Fail(id), n.Fail);
                    Assert.Equal(t.KArt(id), n.KArt);
                    Assert.Equal(t.JustArt(id), n.JustArt);
                    Assert.Equal(t.UniqueFallback(id), n.UniqueFallback);
                    Assert.Equal(t.IsFullBloom(id), n.IsFullBloom);
                    bool has = t.TryGetInEdgeGap(id, out long gap);
                    Assert.Equal(has, n.HasInEdgeGap);
                    Assert.Equal(has ? gap : TrieNode.NoGap, n.InEdgeGapUnits);
                    Assert.Equal(n.Depth >= 2, n.HasInEdgeGap);
                }
                ref readonly TrieNode root = ref t.Node(CommandTrie.Root);
                Assert.Equal(0, root.Depth);
                Assert.Equal(CommandTrie.NoNode, root.Parent);
                Assert.Equal(CommandTrie.Root, root.Fail);
                Assert.Equal(ArtId.Burst, root.KArt);

                ref readonly TrieNode ff = ref t.Node(t.Find(ParserRig.Path("FF")));
                Assert.Equal(ArtId.Spear, ff.KArt);
                Assert.Equal(ArtId.ThunderSpear, ff.JustArt);
                Assert.Equal(SpecArts.GapDefault, ff.InEdgeGapUnits);
                ref readonly TrieNode bloom = ref t.Node(t.Find(ParserRig.Path("FBBF")));
                Assert.True(bloom.IsFullBloom);
                Assert.Equal(SpecArts.GapFullBloom, bloom.InEdgeGapUnits);
                Assert.Equal(4, bloom.Depth);
            }
            Assert.Throws<SimInvariantException>(() => SpecArts.Trie(CharacterId.Char1).Node(13));
            Assert.Throws<SimInvariantException>(() => SpecArts.Trie(CharacterId.Char1).Node(-1));
        }

        // 팩에 arts·tries 절이 아직 없다(M5-DAT-01). ContentDb 로 만드는 길은 그때까지 ContentException 이다
        [Fact]
        public void Build_FromContentDb_WithoutArtsSection_IsContentError()
        {
            ContentDb db = Wizard.Sim.Tests.Runtime.WorldKit.EmptyDb();
            Assert.Throws<ContentException>(() => ParserSourceLoader.FromContent(db));
            Assert.Throws<ContentException>(() => CommandTrie.Build(CharacterId.Char1, db));
            Assert.Throws<ArgumentNullException>(() => CommandTrie.Build(CharacterId.Char1, (ContentDb)null!));
            Assert.Throws<ArgumentNullException>(() => ParserSourceLoader.FromContent(null!));
        }

        // ── 미리보기(spec 03 11절)

        private static CommandParser At(CharacterId c, string path, long lastDirTime, long gap = S015)
        {
            CommandParser p = ParserRig.InStance(c);
            CmdDir[] dirs = ParserRig.Path(path);
            long t = lastDirTime - gap * (dirs.Length - 1);
            foreach (CmdDir d in dirs)
            {
                p.OnDirection(d, t);
                t += gap;
            }
            return p;
        }

        // 커서 F: 0.3초(4,608) 안은 부채 전개, 넘으면 마력 방출. 한 줄. 지갑과 상태는 그대로(R-10)
        [Fact]
        public void Preview_SingleLine_FollowsTimeout_AndTouchesNothing()
        {
            CommandParser p = At(CharacterId.Char1, "F", T0);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            UnlockSet u = Unlocks.All.For(CharacterId.Char1);
            byte[] before = StateBytes(p);

            CommandPreview at = p.Preview(T0 + SpecArts.GapDefault, w, in u);
            CommandPreview after = p.Preview(T0 + SpecArts.GapDefault + 1, w, in u);
            Assert.False(at.HasJust);
            Assert.Equal(ArtId.Fan, at.Normal.Art);
            Assert.Equal(1, at.Normal.CrystalsPaid);
            Assert.Equal(T0 + SpecArts.GapDefault, at.Normal.JudgeTime);
            Assert.False(after.HasJust);
            Assert.Equal(ArtId.Burst, after.Normal.Art);
            Assert.Equal(1, after.Normal.CrystalsPaid);

            Assert.Equal(5, w.Crystals);
            Assert.Equal(0, w.Debt);
            Assert.Equal(before, StateBytes(p));
            Assert.Equal(1, p.DirCount);
        }

        // 자세 밖은 언제나 마력 방출(R-06)
        [Fact]
        public void Preview_OutOfStance_IsRootArt()
        {
            var p = new CommandParser(SpecArts.Trie(CharacterId.Char1));
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            CommandPreview v = p.Preview(T0, w, Unlocks.All.For(CharacterId.Char1));
            Assert.False(v.HasJust);
            Assert.Equal(ArtId.Burst, v.Normal.Art);
            Assert.True(v.Normal.IsFire);
        }

        // 커서 FF, 뇌광창 해금: 섬 창 안이든 밖이든 두 줄. 윗줄 = 마지막 방향 시각의 판정(뇌광창 1),
        // 아랫줄 = 섬 창을 넘긴 시각의 판정(꿰뚫는 창 2). 0.3초를 넘기면 루트라 한 줄
        [Fact]
        public void Preview_JustSpot_AlwaysTwoLines()
        {
            CommandParser p = At(CharacterId.Char1, "FF", T0);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            UnlockSet u = Unlocks.All.For(CharacterId.Char1);
            foreach (long dt in new long[] { 0, 1, SpecArts.JustWindow, SpecArts.JustWindow + 1, 2000, SpecArts.GapDefault })
            {
                CommandPreview v = p.Preview(T0 + dt, w, in u);
                Assert.True(v.HasJust, "dt " + dt);
                Assert.Equal(ArtId.ThunderSpear, v.Just.Art);
                Assert.True(v.Just.IsJust);
                Assert.Equal(1, v.Just.CrystalsPaid);
                Assert.Equal(T0, v.Just.JudgeTime);
                Assert.Equal(ArtId.Spear, v.Normal.Art);
                Assert.False(v.Normal.IsJust);
                Assert.Equal(2, v.Normal.CrystalsPaid);
                long normalTime = dt > SpecArts.JustWindow ? T0 + dt : T0 + SpecArts.JustWindow + 1;
                Assert.Equal(normalTime, v.Normal.JudgeTime);
                Assert.Equal(normalTime - T0 - SpecArts.JustWindow, v.Normal.JustLateByUnits);
            }
            CommandPreview gone = p.Preview(T0 + SpecArts.GapDefault + 1, w, in u);
            Assert.False(gone.HasJust);
            Assert.Equal(ArtId.Burst, gone.Normal.Art);
            Assert.Equal(5, w.Crystals);
        }

        // 뇌광창 미해금이면 같은 자리도 한 줄(꿰뚫는 창 2)
        [Fact]
        public void Preview_JustSpot_JustLocked_SingleLine()
        {
            CommandParser p = At(CharacterId.Char1, "FF", T0);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            UnlockSet u = Unlocks.Of(ArtId.Burst, ArtId.Recoil, ArtId.Fan, ArtId.Spear).For(CharacterId.Char1);
            CommandPreview v = p.Preview(T0 + 100, w, in u);
            Assert.False(v.HasJust);
            Assert.Equal(ArtId.Spear, v.Normal.Art);
            Assert.Equal(2, v.Normal.CrystalsPaid);
        }

        // 결정 1개로 FF: 윗줄 뇌광창(1)은 나가고 아랫줄 꿰뚫는 창(2)은 불발(공용은 부모 대체 없음)
        [Fact]
        public void Preview_JustSpot_LinesPayIndependently()
        {
            CommandParser p = At(CharacterId.Char1, "FF", T0);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 1, 0);
            CommandPreview v = p.Preview(T0 + 100, w, Unlocks.All.For(CharacterId.Char1));
            Assert.True(v.HasJust);
            Assert.True(v.Just.IsFire);
            Assert.Equal(ArtId.ThunderSpear, v.Just.Art);
            Assert.Equal(ArtOutcome.Fizzle, v.Normal.Outcome);
            Assert.Equal(FizzleReason.NoCrystal, v.Normal.Reason);
            Assert.Equal(ArtId.Spear, v.Normal.Art);
            Assert.Equal(1, w.Crystals);
        }

        // char.5 결정 1·빚 0, 커서 LR: 그림자 손(비용 2 = 결정 1 + 외상 1). 지갑은 그대로
        [Fact]
        public void Preview_Credit_ShowsDebtToBeAdded()
        {
            CommandParser p = At(CharacterId.Char5, "LR", T0);
            TestWallet w = SpecArts.Wallet(CharacterId.Char5, 1, 0);
            CommandPreview v = p.Preview(T0 + 100, w, Unlocks.All.For(CharacterId.Char5));
            Assert.False(v.HasJust);
            Assert.True(v.Normal.IsFire);
            Assert.Equal(ArtId.ShadowHand, v.Normal.Art);
            Assert.Equal(1, v.Normal.CrystalsPaid);
            Assert.Equal(1, v.Normal.DebtAdded);
            Assert.Equal(2, v.Normal.CrystalsPaid + v.Normal.DebtAdded);
            Assert.Equal((1, 0), (w.Crystals, w.Debt));
        }

        // char.1 결정 1, 커서 BB: 불가마(2)를 못 내 부모 반동 사격(1)으로 내려간 이름
        [Fact]
        public void Preview_UniqueFallback_ShowsParentArt()
        {
            CommandParser p = At(CharacterId.Char1, "BB", T0);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 1, 0);
            CommandPreview v = p.Preview(T0 + 100, w, Unlocks.All.For(CharacterId.Char1));
            Assert.False(v.HasJust);
            Assert.True(v.Normal.IsFire);
            Assert.Equal(ArtId.Recoil, v.Normal.Art);
            Assert.Equal(ArtId.Kiln, v.Normal.SelectedArt);
            Assert.True(v.Normal.ViaFallback);
            Assert.Equal(1, v.Normal.CrystalsPaid);
            Assert.Equal(1, w.Crystals);
        }

        // 만개 자리(커서 FBBF)에서 앞→뒤 간격이 0.19초(2,918): 지금 K 를 누르면 불발, "2번째 입력이 153단위(0.01초) 늦음".
        // 앞 간격이 다 맞으면 만개이고, 마지막 방향 뒤 0.18초를 넘기면 5번째 입력(K)이 늦다(U-30)
        [Fact]
        public void Preview_FullBloomLate_NamesTheLateInput()
        {
            const long S019 = 2918;                                            // round(0.19 × 15,360)
            long g = SpecArts.GapFullBloom;
            CommandParser p = ParserRig.InStance(CharacterId.Char1);
            p.OnDirection(CmdDir.Forward, T0);
            p.OnDirection(CmdDir.Back, T0 + S019);
            p.OnDirection(CmdDir.Back, T0 + S019 + g);
            p.OnDirection(CmdDir.Forward, T0 + S019 + 2 * g);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            UnlockSet u = Unlocks.All.For(CharacterId.Char1);
            CommandPreview v = p.Preview(T0 + S019 + 2 * g + 10, w, in u);
            Assert.False(v.HasJust);
            Assert.Equal(ArtOutcome.Fizzle, v.Normal.Outcome);
            Assert.Equal(FizzleReason.FullBloomLate, v.Normal.Reason);
            Assert.Equal(ArtId.Bloom, v.Normal.Art);
            Assert.Equal(2, v.Normal.LateIndex);
            Assert.Equal(S019 - g, v.Normal.LateByUnits);
            Assert.Equal(1, LateBy.HundredthsCeil(v.Normal.LateByUnits));
            Assert.Equal(5, w.Crystals);

            CommandParser ok = At(CharacterId.Char1, "FBBF", T0, g);
            CommandPreview fine = ok.Preview(T0 + g, w, in u);
            Assert.True(fine.Normal.IsFire);
            Assert.Equal(ArtId.Bloom, fine.Normal.Art);
            Assert.Equal(5, fine.Normal.Repeats);
            CommandPreview lateK = ok.Preview(T0 + g + 1, w, in u);
            Assert.Equal(FizzleReason.FullBloomLate, lateK.Normal.Reason);
            Assert.Equal(5, lateK.Normal.LateIndex);
            Assert.Equal(1, lateK.Normal.LateByUnits);
        }

        // 미리보기의 아랫줄은 그 시각에 실제로 K 를 누른 판정과 같다(같은 판정 함수)
        [Fact]
        public void Preview_Normal_EqualsTheCommittedDecisionAtThatTime()
        {
            foreach (string path in new[] { "", "F", "B", "FB", "BF", "BB", "LL", "FF", "FBBF" })
            {
                foreach (int crystals in new[] { 0, 1, 2, 5 })
                {
                    long last = T0;
                    CommandParser p = path.Length == 0 ? ParserRig.InStance(CharacterId.Char1) : At(CharacterId.Char1, path, last, 2000);
                    TestWallet w = SpecArts.Wallet(CharacterId.Char1, crystals, 0);
                    UnlockSet u = Unlocks.All.For(CharacterId.Char1);
                    long now = last + 2000;                                    // 섬 창 밖, 0.18초 안
                    CommandPreview v = p.Preview(now, w, in u);
                    Assert.True(p.OnSkillPress(now, w, in u, out ArtDecision d));
                    Assert.Equal(Encode(d), Encode(v.Normal));
                }
            }
        }

        private static byte[] StateBytes(ICommandParser p)
        {
            var bw = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(bw);
            p.WriteState(ref w);
            return bw.ToArray();
        }

        private static string Encode(in ArtDecision v)
        {
            var bw = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(bw);
            v.Write(ref w);
            return Convert.ToHexString(bw.ToArray());
        }
    }
}
