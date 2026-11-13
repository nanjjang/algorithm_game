using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Input;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;

namespace Wizard.Sim
{
    // SpellTable 모듈(절 6). CONTRACTS 3.2 "SpellTable 은 세계 상태다", 3.4, DECISIONS-TECH T-57.
    // 세계 상태는 정수뿐이다: 표의 원천(정규 바이트)과 슬롯별 현재 주문의 핸들. CompiledSpell 은 원천에서 다시 만드는 캐시다.
    //
    // 새 핸들이 생기는 문은 둘뿐이다(그때만 컴파일하고 할당한다):
    //   세계 생성 — AddSpellAtCreation / SetActiveSpellAtCreation. 첫 Step 전(틱 0)에만 열린다
    //   Step 0단계 — AddSpell / SetActiveSpell. 적용 중인 RunCommand 를 받는다(0단계 훅만 명령을 손에 든다. 조립이 확정되는 Depart)
    // 실행 경로(5~10단계)는 SpellAt·ActiveSpell 만 부르고 컴파일하지도 할당하지도 않는다.
    // 사본 마법진과 허상의 복사는 이미 있는 핸들(ActiveSpell)을 쓴다.
    //
    // 켜지는 조건: 팩에 blocks 절이 있다(SpellTableOn). 꺼진 세계에서는 절의 내용이 비고(뼈대 시험의 빈 팩) 문은 SimInvariantException 이다.
    // blocks 절은 있는데 주문 규칙(절의 꼬리)이 없는 시험용 팩에서는 표가 비어 있고 주문을 더하면 ContentException 이다.
    //
    // 절의 내용(켜져 있을 때): count, 항목마다 원천의 정규 바이트(핸들 순. CompileSource), 플레이어마다(슬롯 순) i32 현재 핸들(없으면 -1).
    // 스냅숏: 표의 길이와 그 앞머리의 지문, 슬롯별 현재 핸들. 복원은 길이를 줄이고, 지문이 다르면(다른 표의 스냅숏) SimInvariantException.
    //
    // 현재 주문이 바뀌면 열 모듈에 그 주문의 발사 열과 간격을 넣는다(연산 SetHeatShot. Heat 는 앞 작업이다):
    // J 발사 1회의 열은 CompiledSpell.FireHeatHu(거울상 변이면 +50% 뒤), 간격은 코어 블록의 간격이다. 유효하지 않은 주문이면 비운다.
    public sealed partial class SimWorld
    {
        private const int NoSpell = -1;

        // 상태(절 6)
        private readonly SpellTableCore _spellTable = new SpellTableCore();
        private readonly int[] _spellActive = NewSpellActive();   // 색인 = 슬롯. 길이는 MaxPlayers 이고 0..PlayerCount-1 만 쓴다

        // 스냅숏 버퍼(그림자 세계)에만 뜻이 있는 값: 저장한 세계의 표 길이와 그 앞머리의 지문
        private int _spellSnapLength;
        private ulong _spellSnapPrefix;

        private static int[] NewSpellActive()
        {
            var a = new int[MaxPlayers];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = NoSpell;
            }
            return a;
        }

        // ── 다른 모듈과 시험이 읽는 값(같은 어셈블리 안). 할당하지 않는다

        internal bool SpellTableOn => _db.Pack.TryGetSection((ushort)ContentSection.Blocks, out _);

        internal int SpellCount => _spellTable.Count;

        // 실행기가 부른다
        internal CompiledSpell SpellAt(SpellHandle handle) => _spellTable.Get(handle);

        // 슬롯의 현재 주문이 있는가. 있으면 handle 에 준다
        internal bool TryGetActiveSpell(int index, out SpellHandle handle)
        {
            RequireSpellIndex(index);
            int h = _spellActive[index];
            handle = new SpellHandle(h);
            return h != NoSpell;
        }

        // ── 새 핸들의 문(세계 생성, Step 0단계)

        // 세계 생성: 첫 Step 전에만 부른다
        internal SpellHandle AddSpellAtCreation(BlockId core, ReadOnlySpan<BlockId> links, in WandCombatProfile profile)
        {
            RequireCreationDoor();
            return AddSpellCore(core, links, in profile);
        }

        // Step 0단계: 적용 중인 명령(Stage00 훅의 command)을 넘긴다
        internal SpellHandle AddSpell(in RunCommand command, BlockId core, ReadOnlySpan<BlockId> links, in WandCombatProfile profile)
        {
            RequireStageZeroDoor(in command);
            return AddSpellCore(core, links, in profile);
        }

        internal void SetActiveSpellAtCreation(PlayerSlot slot, SpellHandle handle)
        {
            RequireCreationDoor();
            SetActiveSpellCore(slot, handle);
        }

        internal void SetActiveSpell(in RunCommand command, PlayerSlot slot, SpellHandle handle)
        {
            RequireStageZeroDoor(in command);
            SetActiveSpellCore(slot, handle);
        }

        private SpellHandle AddSpellCore(BlockId core, ReadOnlySpan<BlockId> links, in WandCombatProfile profile)
        {
            if (!SpellTableOn)
            {
                throw new SimInvariantException("spell: the pack has no blocks (spell table is off)");
            }
            return _spellTable.Add(CompileSource.From(core, links, in profile), _db, CompileRules.FromContent(_db));
        }

        private void SetActiveSpellCore(PlayerSlot slot, SpellHandle handle)
        {
            if (!IsValidSlot(slot))
            {
                throw new SimInvariantException("spell: slot is not in this world");
            }
            CompiledSpell spell = _spellTable.Get(handle);      // 범위 밖이면 SimInvariantException
            _spellActive[slot.Value] = handle.Value;
            if (!HeatModuleOn)
            {
                return;
            }
            if (spell.IsValid)
            {
                SetHeatShot(slot, _spellTable.SourceAt(handle).Core, spell.FireHeatHu, spell.FireIntervalTicks);
            }
            else
            {
                ClearHeatShot(slot);
            }
        }

        private void RequireCreationDoor()
        {
            if (_tick != 0U)
            {
                throw new SimInvariantException("spell: the creation door is open only before the first Step");
            }
        }

        private void RequireStageZeroDoor(in RunCommand command)
        {
            if (command.Tick != _tick || !IsValidSlot(command.Slot))
            {
                throw new SimInvariantException("spell: the stage 0 door needs the command being applied this tick");
            }
        }

        private void RequireSpellIndex(int index)
        {
            if (index < 0 || index >= PlayerCount)
            {
                throw new SimInvariantException("spell: player index out of range");
            }
        }

        // ── 상태 훅(절 6, 스칼라)

        partial void Snapshot_Save_SpellTable(SimWorld target)
        {
            target._spellSnapLength = _spellTable.Count;
            target._spellSnapPrefix = _spellTable.PrefixHash(_spellTable.Count);
            new ReadOnlySpan<int>(_spellActive).CopyTo(target._spellActive);
        }

        // 표는 추가만 하므로 길이를 줄이면 저장 때의 표다(되감은 뒤 다시 돌리면 같은 순서로 다시 채워진다).
        // 앞머리의 지문이 다르면 다른 표에서 뜬 스냅숏이다(핸들이 다른 주문을 가리키게 된다)
        partial void Snapshot_Load_SpellTable(SimWorld source)
        {
            int length = source._spellSnapLength;
            if (length > _spellTable.Count || _spellTable.PrefixHash(length) != source._spellSnapPrefix)
            {
                throw new SimInvariantException("spell: the snapshot was taken from a different spell table");
            }
            _spellTable.Truncate(length);
            new ReadOnlySpan<int>(source._spellActive).CopyTo(_spellActive);
        }

        partial void State_Write_SpellTable(ref StateWriter w)
        {
            if (!SpellTableOn)
            {
                return;
            }
            _spellTable.Write(ref w);
            for (int i = 0; i < PlayerCount; i++)
            {
                w.I32(_spellActive[i]);
            }
        }

        // 원천에서 다시 컴파일한다(받은 쪽의 모든 핸들이 실행 가능하다). 틱 밖에서만 불린다(ReadState)
        partial void State_Read_SpellTable(ref StateReader r)
        {
            if (!SpellTableOn)
            {
                return;
            }
            StateReader peek = r;
            if (peek.Count() == 0)
            {
                // 빈 표: 규칙이 없는 시험용 팩에서도 읽힌다(규칙은 컴파일할 때만 읽는다)
                r = peek;
                _spellTable.Truncate(0);
            }
            else
            {
                _spellTable.Read(ref r, _db, CompileRules.FromContent(_db));
            }
            for (int i = 0; i < MaxPlayers; i++)
            {
                _spellActive[i] = NoSpell;
            }
            for (int i = 0; i < PlayerCount; i++)
            {
                int h = r.I32();
                if (h < NoSpell || h >= _spellTable.Count)
                {
                    throw new SimInvariantException("spell: active handle out of range");
                }
                _spellActive[i] = h;
            }
        }
    }
}
