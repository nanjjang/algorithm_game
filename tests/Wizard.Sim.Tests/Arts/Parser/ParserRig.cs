using System;
using Wizard.Content;
using Wizard.Sim.Arts;
using Xunit;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // 입력열 한 걸음: 방향(F/B/L/R) 또는 K. Gap 은 직전 입력과의 간격(입력 시각 단위)
    internal readonly struct In
    {
        public readonly bool IsK;
        public readonly CmdDir Dir;
        public readonly long Gap;

        private In(bool isK, CmdDir dir, long gap)
        {
            IsK = isK;
            Dir = dir;
            Gap = gap;
        }

        public static In F(long gap) => new In(false, CmdDir.Forward, gap);
        public static In B(long gap) => new In(false, CmdDir.Back, gap);
        public static In L(long gap) => new In(false, CmdDir.Left, gap);
        public static In R(long gap) => new In(false, CmdDir.Right, gap);
        public static In K(long gap) => new In(true, CmdDir.Forward, gap);
    }

    // spec 03 18절의 해금 표기. "전부" = 공용 10 + 그 캐릭터의 고유, "{…} + 고유" = 그 공용 비기 + 고유
    internal readonly struct Unlocks
    {
        private readonly bool _all;
        private readonly ArtId[]? _arts;

        private Unlocks(bool all, ArtId[]? arts)
        {
            _all = all;
            _arts = arts;
        }

        public static Unlocks All => new Unlocks(true, null);

        public static Unlocks Of(params ArtId[] arts) => new Unlocks(false, arts);

        public UnlockSet For(CharacterId c)
        {
            int mask = Bit(SpecArts.UniqueOf(c));
            foreach (ArtId a in _all ? SpecArts.CommonTen : _arts!)
            {
                mask |= Bit(a);
            }
            return new UnlockSet((ArtMask)mask);
        }

        public static UnlockSet Mask(params ArtId[] arts)
        {
            int mask = 0;
            foreach (ArtId a in arts)
            {
                mask |= Bit(a);
            }
            return new UnlockSet((ArtMask)mask);
        }

        private static int Bit(ArtId a) => 1 << (int)a;     // CONTRACTS 2.1: 비트 번호 = (int)ArtId
    }

    // 명세 표의 결과 칸
    internal readonly struct Want
    {
        private enum Kind { Fire, FireVia, NoCrystal, BloomLate }

        private readonly Kind _kind;
        private readonly ArtId _art;
        private readonly int _lateIndex;
        private readonly long _lateBy;
        private readonly int _hundredths;

        private Want(Kind kind, ArtId art, int lateIndex, long lateBy, int hundredths)
        {
            _kind = kind;
            _art = art;
            _lateIndex = lateIndex;
            _lateBy = lateBy;
            _hundredths = hundredths;
        }

        public static Want Fire(ArtId art) => new Want(Kind.Fire, art, 0, 0, 0);

        public static Want FireViaFallback(ArtId art) => new Want(Kind.FireVia, art, 0, 0, 0);

        public static Want NoCrystal() => new Want(Kind.NoCrystal, ArtId.None, 0, 0, 0);

        // 표시 "{index}번째 입력이 0.{hundredths:00}초 늦음"
        public static Want BloomLate(int index, long lateBy, int hundredths) => new Want(Kind.BloomLate, ArtId.Bloom, index, lateBy, hundredths);

        public void Check(in ArtDecision v)
        {
            switch (_kind)
            {
                case Kind.Fire:
                    Assert.Equal(ArtOutcome.Fire, v.Outcome);
                    Assert.Equal(_art, v.Art);
                    Assert.False(v.ViaFallback);
                    Assert.Equal(FizzleReason.None, v.Reason);
                    break;
                case Kind.FireVia:
                    Assert.Equal(ArtOutcome.Fire, v.Outcome);
                    Assert.Equal(_art, v.Art);
                    Assert.True(v.ViaFallback);
                    Assert.NotEqual(_art, v.SelectedArt);
                    break;
                case Kind.NoCrystal:
                    Assert.Equal(ArtOutcome.Fizzle, v.Outcome);
                    Assert.Equal(FizzleReason.NoCrystal, v.Reason);
                    Assert.Equal(0, v.CrystalsPaid);
                    Assert.Equal(0, v.DebtAdded);
                    break;
                case Kind.BloomLate:
                    Assert.Equal(ArtOutcome.Fizzle, v.Outcome);
                    Assert.Equal(FizzleReason.FullBloomLate, v.Reason);
                    Assert.Equal(ArtId.Bloom, v.Art);
                    Assert.Equal(_lateIndex, v.LateIndex);
                    Assert.Equal(_lateBy, v.LateByUnits);
                    Assert.Equal(_hundredths, LateBy.HundredthsCeil(v.LateByUnits));
                    break;
            }
        }
    }

    // spec 03 18절 하네스: 자세 중(입력열 앞에서 이미 자세), 재사용 대기 없음, 커서 루트. 첫 입력 시각은 PY 와 같은 100,000u.
    // 방향과 K 는 판정기에 바로 넣는다(키 → 상대 방향 변환을 거치지 않는다)
    internal static class ParserRig
    {
        public const long T0 = 100_000;

        public static CommandParser InStance(CharacterId c)
        {
            var p = new CommandParser(SpecArts.Trie(c));
            p.OnStanceChanged(true, T0 - SpecArts.GapDefault);
            return p;
        }

        // 입력열을 넣고 마지막 K 의 판정을 돌려준다. K 는 입력열의 끝에 한 번만 있다
        public static ArtDecision Feed(CommandParser p, ICrystalWallet w, in UnlockSet u, In[] steps, long start = T0)
        {
            long t = start;
            ArtDecision? got = null;
            for (int i = 0; i < steps.Length; i++)
            {
                In s = steps[i];
                t += s.Gap;
                if (s.IsK)
                {
                    Assert.True(i == steps.Length - 1, "K must be the last input");
                    Assert.True(p.OnSkillPress(t, w, in u, out ArtDecision v));
                    Assert.Equal(t, v.JudgeTime);
                    got = v;
                }
                else
                {
                    p.OnDirection(s.Dir, t);
                }
            }
            Assert.True(got.HasValue, "no K in the input");
            return got!.Value;
        }

        public static ArtDecision RunA(CharacterId c, Unlocks unlocks, int crystals, int debt, In[] steps, Want want,
                                      int crystalsAfter, int debtAfter)
        {
            CommandParser p = InStance(c);
            TestWallet w = SpecArts.Wallet(c, crystals, debt);
            ArtDecision v = Feed(p, w, unlocks.For(c), steps);
            want.Check(in v);
            Assert.Equal(crystalsAfter, w.Crystals);
            Assert.Equal(debtAfter, w.Debt);
            if (v.IsFire)
            {
                Assert.Equal(crystals - crystalsAfter, v.CrystalsPaid);
                Assert.Equal(debtAfter - debt, v.DebtAdded);
                Assert.Equal(crystalsAfter, v.CrystalsAfter);
            }
            // 판정 뒤에는 커서와 입력 기록이 비워진다(spec 03 6.2)
            Assert.Equal(CommandTrie.Root, p.Cursor);
            Assert.Equal(0, p.DirCount);
            return v;
        }

        public static CmdDir[] Path(string letters)
        {
            var dirs = new CmdDir[letters.Length];
            for (int i = 0; i < letters.Length; i++)
            {
                switch (letters[i])
                {
                    case 'F': dirs[i] = CmdDir.Forward; break;
                    case 'B': dirs[i] = CmdDir.Back; break;
                    case 'L': dirs[i] = CmdDir.Left; break;
                    case 'R': dirs[i] = CmdDir.Right; break;
                    default: throw new ArgumentException("path letter " + letters[i]);
                }
            }
            return dirs;
        }

        public static string Letters(ReadOnlySpan<CmdDir> path)
        {
            var c = new char[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                c[i] = "FBLR"[(int)path[i]];
            }
            return new string(c);
        }
    }
}
