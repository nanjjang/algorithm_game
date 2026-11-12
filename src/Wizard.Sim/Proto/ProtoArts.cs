using Wizard.Content;
using Wizard.Sim.Arts;

namespace Wizard.Sim.Proto
{
    // 시제품의 판정기 데이터(T-52: 팩에 비기 절이 없으므로 CommandTrie.Build(character, ParserSource)를 쓴다).
    // 임시 효과의 기술 셋 ①②⑥(GDD 16 1단계, spec 03 21절 끝): 경로는 spec 03 5.2, 비용은 12.1, 상수는 1.2.
    // 세계마다 한 번 만든다(세계 생성 때의 할당). 정적 표로 두지 않는다(가변 참조 형식이다).
    internal static class ProtoArts
    {
        public static ParserSource Source()
        {
            var timings = new ParserTimings(ProtoTuning.JustWindowUnits, ProtoTuning.GapFullBloomUnits, ProtoTuning.GapShortUnits,
                                            ProtoTuning.GapSameSideUnits, ProtoTuning.GapDefaultUnits, ProtoTuning.ArtCooldownUnits,
                                            ProtoTuning.KBufferWindowUnits, ProtoTuning.MaxDebt);
            return new ParserSource(timings, new[]
            {
                Common(ArtId.Burst, ProtoTuning.BurstCost, new CmdDir[0]),
                Common(ArtId.Recoil, ProtoTuning.RecoilCost, new[] { CmdDir.Back }),
                Common(ArtId.Spear, ProtoTuning.SpearCost, new[] { CmdDir.Forward, CmdDir.Forward }),
            });
        }

        // 셋 다 처음부터 쓸 수 있다(시제품. 해금은 M5-ART-04)
        public static UnlockSet Unlocks() =>
            new UnlockSet((ArtMask)((1 << (int)ArtId.Burst) | (1 << (int)ArtId.Recoil) | (1 << (int)ArtId.Spear)));

        // 연습의 캐릭터는 기준값이다(SimWorld 의 StartCharacter: PracticeSetup 에 캐릭터 칸이 아직 없다). 플레이어 칸마다 판정기 하나
        public static CommandParser[] NewParsers(int count)
        {
            CommandTrie trie = CommandTrie.Build(CharacterId.Baseline, Source());
            var parsers = new CommandParser[count];
            for (int i = 0; i < count; i++)
            {
                parsers[i] = new CommandParser(trie);
            }
            return parsers;
        }

        private static ParserArt Common(ArtId id, int cost, CmdDir[] path) =>
            new ParserArt(id, false, CharacterId.Baseline, false, false, cost, 0, true, new[] { path });
    }
}
