using System;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Spell.Compile
{
    // 컴파일의 원천(CONTRACTS 3.2 "SpellTable 은 세계 상태다": 항목 = (core, links[], WandCombatProfile 의 정규 바이트)).
    // 불변이다. 같은 원천이면 언제나 같은 컴파일 결과가 나온다(spec 02 15절). CompiledSpell 은 이것에서 다시 만들 수 있는 캐시다.
    //
    // 정규 바이트(StateWriter 로 쓴다. 해시와 WriteState 가 같은 바이트를 센다):
    //   u8 core  u8 link_count  u8 × link_count(슬롯 1..L 순, 빈 칸 = 0)
    //   u8 character  u8 base_links  u8 extra_slots  u8 depth_decay(PowBase)  u8 underline(0 | 1)  u8 mutation_bits
    internal sealed class CompileSource
    {
        public const int MaxLinks = byte.MaxValue;   // 정규 바이트의 u8 칸(자료 꼴의 한계. 명세의 L 은 4..8)

        public readonly BlockId Core;
        private readonly BlockId[] _links;
        public readonly CharacterId Character;
        public readonly int BaseLinks;
        public readonly int ExtraSlots;
        public readonly PowBase DepthDecay;
        public readonly bool UnderlinePassive;
        public readonly byte MutationBits;

        public CompileSource(BlockId core, ReadOnlySpan<BlockId> links, CharacterId character, int baseLinks, int extraSlots,
                             PowBase depthDecay, bool underlinePassive, byte mutationBits)
        {
            if (links.Length > MaxLinks || baseLinks < 0 || baseLinks > MaxLinks || extraSlots < 0 || extraSlots > MaxLinks)
            {
                throw new SimInvariantException("CompileSource: link counts out of the canonical range");
            }
            if (depthDecay != PowBase.Decay085 && depthDecay != PowBase.Decay092)
            {
                throw new SimInvariantException("CompileSource: depth decay must be Decay085 or Decay092");
            }
            Core = core;
            _links = links.ToArray();
            Character = character;
            BaseLinks = baseLinks;
            ExtraSlots = extraSlots;
            DepthDecay = depthDecay;
            UnderlinePassive = underlinePassive;
            MutationBits = mutationBits;
        }

        // 계약의 입력(CONTRACTS 3.4 ISpellCompiler.Compile)에서 만든다
        public static CompileSource From(BlockId core, ReadOnlySpan<BlockId> links, in WandCombatProfile profile) =>
            new CompileSource(core, links, profile.Character, profile.BaseLinks, profile.ExtraSlots, profile.DepthDecay,
                              profile.UnderlinePassive, profile.Mutations.Bits);

        public WandCombatProfile Profile =>
            new WandCombatProfile(Character, BaseLinks, ExtraSlots, DepthDecay, UnderlinePassive, new MutationSet(MutationBits));

        public ReadOnlySpan<BlockId> Links => _links;

        public int LinkCount => _links.Length;

        // 연결 슬롯 번호 1..L 의 블록
        public BlockId LinkAt(int slot) => _links[slot - 1];

        public bool HasMutation(MutationId id) => (MutationBits & (1 << (int)id)) != 0;

        public void Write(ref StateWriter w)
        {
            w.U8((byte)Core);
            w.U8((byte)_links.Length);
            for (int i = 0; i < _links.Length; i++)
            {
                w.U8((byte)_links[i]);
            }
            w.U8((byte)Character);
            w.U8((byte)BaseLinks);
            w.U8((byte)ExtraSlots);
            w.U8((byte)DepthDecay);
            w.Bool(UnderlinePassive);
            w.U8(MutationBits);
        }

        // 상태 바이트에서 읽는다. 꼴이 깨졌으면 SimInvariantException(블록 id 가 팩에 있는지는 컴파일러가 진단으로 본다)
        public static CompileSource Read(ref StateReader r)
        {
            var core = (BlockId)r.U8();
            int n = r.U8();
            var links = new BlockId[n];
            for (int i = 0; i < n; i++)
            {
                links[i] = (BlockId)r.U8();
            }
            var character = (CharacterId)r.U8();
            int baseLinks = r.U8();
            int extraSlots = r.U8();
            var decay = (PowBase)r.U8();
            bool underline = r.Bool();
            byte mutations = r.U8();
            if (decay != PowBase.Decay085 && decay != PowBase.Decay092)
            {
                throw new SimInvariantException("spell table: unknown depth decay");
            }
            return new CompileSource(core, links, character, baseLinks, extraSlots, decay, underline, mutations);
        }

        public bool SameAs(CompileSource other)
        {
            if (Core != other.Core || Character != other.Character || BaseLinks != other.BaseLinks || ExtraSlots != other.ExtraSlots
                || DepthDecay != other.DepthDecay || UnderlinePassive != other.UnderlinePassive || MutationBits != other.MutationBits
                || _links.Length != other._links.Length)
            {
                return false;
            }
            for (int i = 0; i < _links.Length; i++)
            {
                if (_links[i] != other._links[i])
                {
                    return false;
                }
            }
            return true;
        }

        // spec 02 6.3 CompiledSpell.Hash: 슬롯 배열 + 문맥(정규 바이트)의 FNV-1a 64
        public ulong ComputeHash()
        {
            StateWriter w = StateWriter.ForHash();
            Write(ref w);
            return w.Hash;
        }
    }
}
