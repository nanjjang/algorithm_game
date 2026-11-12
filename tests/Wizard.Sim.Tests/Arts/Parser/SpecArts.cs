using Wizard.Content;
using Wizard.Sim.Arts;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // 명세 값으로 만든 판정기 데이터. 비기·트라이 데이터 절은 아직 팩에 없다(M5-DAT-01). 값의 출처:
    //   상수 — spec 03 1.2(입력 시각 단위), 빚 상한 — 9.2(2개)
    //   경로·슬롯 — 5.2, 비용·외상·종류 — 12.1, 결정 상한 — 2.1
    internal static class SpecArts
    {
        public const long JustWindow = 1075;
        public const long GapFullBloom = 2765;
        public const long GapShort = 3072;
        public const long GapSameSide = 3840;
        public const long GapDefault = 4608;
        public const long ArtCooldown = 6144;
        public const long KBufferWindow = 1536;
        public const int MaxDebt = 2;

        private const CmdDir F = CmdDir.Forward, B = CmdDir.Back, L = CmdDir.Left, R = CmdDir.Right;

        public static ParserTimings Timings =>
            new ParserTimings(JustWindow, GapFullBloom, GapShort, GapSameSide, GapDefault, ArtCooldown, KBufferWindow, MaxDebt);

        public static ParserSource Source()
        {
            return new ParserSource(Timings, new[]
            {
                Common(ArtId.Burst, 1, P()),
                Common(ArtId.Recoil, 1, P(B)),
                Common(ArtId.Fan, 1, P(F)),
                Common(ArtId.Wings, 1, P(L), P(R)),
                Common(ArtId.Afterimage, 2, P(L, L), P(R, R)),
                Common(ArtId.Spear, 2, P(F, F)),
                Common(ArtId.Vent, 2, P(B, F)),
                Common(ArtId.Octastar, 2, P(F, B)),
                new ParserArt(ArtId.ThunderSpear, false, CharacterId.Baseline, true, false, 1, 0, true, new[] { P(F, F) }),
                new ParserArt(ArtId.Bloom, false, CharacterId.Baseline, false, true, 0, 3, false, new[] { P(F, B, B, F) }),
                Unique(ArtId.Kiln, CharacterId.Char1, P(B, B)),
                Unique(ArtId.BoltDash, CharacterId.Char2, P(L, F), P(R, F)),
                Unique(ArtId.CopyCircle, CharacterId.Char3, P(F, L), P(F, R)),
                Unique(ArtId.IceScreen, CharacterId.Char4, P(B, L), P(B, R)),
                Unique(ArtId.ShadowHand, CharacterId.Char5, P(L, R), P(R, L)),
            });
        }

        public static CommandTrie Trie(CharacterId character) => CommandTrie.Build(character, Source());

        // spec 03 2.1 결정 상한
        public static int Cap(CharacterId c)
        {
            switch (c)
            {
                case CharacterId.Char1: return 5;
                case CharacterId.Char2: return 5;
                case CharacterId.Char3: return 4;
                case CharacterId.Char4: return 6;
                case CharacterId.Char5: return 7;
                default: return 0;
            }
        }

        public static ArtId UniqueOf(CharacterId c)
        {
            switch (c)
            {
                case CharacterId.Char1: return ArtId.Kiln;
                case CharacterId.Char2: return ArtId.BoltDash;
                case CharacterId.Char3: return ArtId.CopyCircle;
                case CharacterId.Char4: return ArtId.IceScreen;
                case CharacterId.Char5: return ArtId.ShadowHand;
                default: return ArtId.None;
            }
        }

        public static readonly ArtId[] CommonTen =
        {
            ArtId.Burst, ArtId.Recoil, ArtId.Fan, ArtId.Wings, ArtId.Afterimage,
            ArtId.Spear, ArtId.Vent, ArtId.Octastar, ArtId.ThunderSpear, ArtId.Bloom,
        };

        public static TestWallet Wallet(CharacterId c, int crystals, int debt) =>
            new TestWallet(SpecArts.Cap(c), crystals, debt, c == CharacterId.Char5);

        private static CmdDir[] P(params CmdDir[] dirs) => dirs;

        private static ParserArt Common(ArtId id, int cost, params CmdDir[][] paths) =>
            new ParserArt(id, false, CharacterId.Baseline, false, false, cost, 0, true, paths);

        private static ParserArt Unique(ArtId id, CharacterId owner, params CmdDir[][] paths) =>
            new ParserArt(id, true, owner, false, false, 2, 0, true, paths);
    }
}
