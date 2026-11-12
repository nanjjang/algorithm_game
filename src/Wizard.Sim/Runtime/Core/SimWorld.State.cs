using System;
using Wizard.Core;

namespace Wizard.Sim
{
    // CONTRACTS 3.2 "상태 절과 해시", 1.4 정규 직렬화, T-10·T-14.
    // 상태 절의 순서는 이 파일 한 곳에 있다: 절 id 오름차순(StateSection). 스냅숏·직렬화·해시가 같은 표를 따른다.
    //
    //   절      종류     | 절      종류     | 절        종류
    //   1 Player   스칼라 | 7 Arts       스칼라 | 13 Camp    스칼라
    //   2 Heat     스칼라 | 8 Characters 스칼라 | 14 Outcome 스칼라
    //   3 Graze    스칼라 | 9 Enemies    풀     | 15 Duel    스칼라
    //   4 Combo    스칼라 | 10 Telegraph 풀     | 16 Rng     스칼라(Runtime/Core)
    //   5 Spell    풀     | 11 Bosses    풀     | 17 Debug   스칼라
    //   6 SpellTable 스칼라
    //
    // 바이트: 절마다 (절 id u16 LE, 내용). 길이 칸은 없다. 스칼라 절은 언제나 쓰고(내용이 없어도 id 는 쓴다),
    // 풀 절은 개수가 0이면 id 까지 생략한다. 그래서 풀 모듈을 더해도 그 풀이 빈 세계의 해시는 바뀌지 않는다.
    // 해시는 같은 쓰기 함수를 해시 모드(StateWriter.ForHash)로 부른 것이다. 직렬화와 해시가 갈라질 수 없다.
    public sealed partial class SimWorld
    {
        private const int SectionIdBytes = 2;
        private const int ByteBits = 8;

        // 정규 직렬화의 FNV-1a 64. 할당하지 않는다
        public ulong ComputeStateHash()
        {
            StateWriter w = StateWriter.ForHash();
            WriteSections(ref w);
            return w.Hash;
        }

        // 상태 전체를 target 의 끝에 덧붙인다(재접속, 해시 불일치 복구, 관전 시작). 틱 밖에서 부른다
        public void WriteState(ByteWriter target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            StateWriter w = StateWriter.ForBytes(target);
            WriteSections(ref w);
        }

        // WriteState 가 쓴 바이트 전체를 읽어 상태를 덮어쓴다. 모양(모드, 플레이어 수, 씨앗)이 이 세계와 다르거나
        // 절 순서가 어긋나거나 바이트가 남으면 SimInvariantException(상태가 깨졌다. 세계는 버린다)
        public void ReadState(ReadOnlySpan<byte> state)
        {
            var r = new StateReader(state);
            ReadSections(ref r);
            if (r.Remaining != 0)
            {
                throw new SimInvariantException("state: trailing bytes");
            }
        }

        // 이 세계에 맞는 크기의 빈 스냅숏. 버퍼는 여기서 한 번 만든다(할당). 같은 모양의 다른 세계와도 쓸 수 있다
        public SimSnapshot CreateSnapshotBuffer() => new SimSnapshot(CreateShadow());

        // 값 복사. 할당 없음
        public void SaveSnapshot(SimSnapshot target)
        {
            SimWorld shadow = RequireCompatible(target);
            CopyCoreTo(shadow);
            Snapshot_Save_Player(shadow);
            Snapshot_Save_Heat(shadow);
            Snapshot_Save_Graze(shadow);
            Snapshot_Save_Combo(shadow);
            Snapshot_Save_Spell(shadow);
            Snapshot_Save_SpellTable(shadow);
            Snapshot_Save_Arts(shadow);
            Snapshot_Save_Characters(shadow);
            Snapshot_Save_Enemies(shadow);
            Snapshot_Save_Telegraph(shadow);
            Snapshot_Save_Bosses(shadow);
            Snapshot_Save_Waves(shadow);
            Snapshot_Save_Camp(shadow);
            Snapshot_Save_Outcome(shadow);
            Snapshot_Save_Duel(shadow);
            Snapshot_Save_Debug(shadow);
        }

        // 값 복사. 할당 없음. 뒤에 같은 입력으로 Step 하면 저장하지 않고 진행한 것과 같은 해시가 나온다
        public void LoadSnapshot(SimSnapshot source)
        {
            SimWorld shadow = RequireCompatible(source);
            shadow.CopyCoreTo(this);
            Snapshot_Load_Player(shadow);
            Snapshot_Load_Heat(shadow);
            Snapshot_Load_Graze(shadow);
            Snapshot_Load_Combo(shadow);
            Snapshot_Load_Spell(shadow);
            Snapshot_Load_SpellTable(shadow);
            Snapshot_Load_Arts(shadow);
            Snapshot_Load_Characters(shadow);
            Snapshot_Load_Enemies(shadow);
            Snapshot_Load_Telegraph(shadow);
            Snapshot_Load_Bosses(shadow);
            Snapshot_Load_Waves(shadow);
            Snapshot_Load_Camp(shadow);
            Snapshot_Load_Outcome(shadow);
            Snapshot_Load_Duel(shadow);
            Snapshot_Load_Debug(shadow);
        }

        // ── 풀 절의 틀. 풀 모듈의 State_Write/State_Read 훅이 부른다

        // itemCount 가 0이면 아무것도 쓰지 않고 false(절 생략). 아니면 절 id 를 쓰고 true. 내용은 부른 쪽이 쓴다
        internal static bool TryBeginPoolSection(ref StateWriter w, StateSection section, int itemCount)
        {
            if (itemCount < 0)
            {
                throw new SimInvariantException("state: negative pool count");
            }
            if (itemCount == 0)
            {
                return false;
            }
            WriteSectionId(ref w, section);
            return true;
        }

        // 다음 절이 이 풀 절이면 id 를 읽고 true(내용은 부른 쪽이 읽는다). 아니면 아무것도 읽지 않고 false(부른 쪽이 풀을 비운다)
        internal static bool TryBeginPoolSection(ref StateReader r, StateSection section)
        {
            if (r.Remaining < SectionIdBytes)
            {
                return false;
            }
            StateReader peek = r;
            if (ReadSectionId(ref peek) != (ushort)section)
            {
                return false;
            }
            r = peek;
            return true;
        }

        // ── 순서표(여기만 고친다)

        private void WriteSections(ref StateWriter w)
        {
            BeginScalarSection(ref w, StateSection.Player);
            State_Write_Player(ref w);
            BeginScalarSection(ref w, StateSection.Heat);
            State_Write_Heat(ref w);
            BeginScalarSection(ref w, StateSection.Graze);
            State_Write_Graze(ref w);
            BeginScalarSection(ref w, StateSection.Combo);
            State_Write_Combo(ref w);
            State_Write_Spell(ref w);                         // 풀
            BeginScalarSection(ref w, StateSection.SpellTable);
            State_Write_SpellTable(ref w);
            BeginScalarSection(ref w, StateSection.Arts);
            State_Write_Arts(ref w);
            BeginScalarSection(ref w, StateSection.Characters);
            State_Write_Characters(ref w);
            State_Write_Enemies(ref w);                       // 풀
            State_Write_Telegraph(ref w);                     // 풀
            State_Write_Bosses(ref w);                        // 풀
            BeginScalarSection(ref w, StateSection.Waves);
            State_Write_Waves(ref w);
            BeginScalarSection(ref w, StateSection.Camp);
            State_Write_Camp(ref w);
            BeginScalarSection(ref w, StateSection.Outcome);
            State_Write_Outcome(ref w);
            BeginScalarSection(ref w, StateSection.Duel);
            State_Write_Duel(ref w);
            BeginScalarSection(ref w, StateSection.Rng);
            State_Write_Rng(ref w);
            BeginScalarSection(ref w, StateSection.Debug);
            State_Write_Debug(ref w);
        }

        private void ReadSections(ref StateReader r)
        {
            ExpectScalarSection(ref r, StateSection.Player);
            State_Read_Player(ref r);
            ExpectScalarSection(ref r, StateSection.Heat);
            State_Read_Heat(ref r);
            ExpectScalarSection(ref r, StateSection.Graze);
            State_Read_Graze(ref r);
            ExpectScalarSection(ref r, StateSection.Combo);
            State_Read_Combo(ref r);
            State_Read_Spell(ref r);                          // 풀
            ExpectScalarSection(ref r, StateSection.SpellTable);
            State_Read_SpellTable(ref r);
            ExpectScalarSection(ref r, StateSection.Arts);
            State_Read_Arts(ref r);
            ExpectScalarSection(ref r, StateSection.Characters);
            State_Read_Characters(ref r);
            State_Read_Enemies(ref r);                        // 풀
            State_Read_Telegraph(ref r);                      // 풀
            State_Read_Bosses(ref r);                         // 풀
            ExpectScalarSection(ref r, StateSection.Waves);
            State_Read_Waves(ref r);
            ExpectScalarSection(ref r, StateSection.Camp);
            State_Read_Camp(ref r);
            ExpectScalarSection(ref r, StateSection.Outcome);
            State_Read_Outcome(ref r);
            ExpectScalarSection(ref r, StateSection.Duel);
            State_Read_Duel(ref r);
            ExpectScalarSection(ref r, StateSection.Rng);
            State_Read_Rng(ref r);
            ExpectScalarSection(ref r, StateSection.Debug);
            State_Read_Debug(ref r);
        }

        private static void BeginScalarSection(ref StateWriter w, StateSection section) => WriteSectionId(ref w, section);

        private static void ExpectScalarSection(ref StateReader r, StateSection section)
        {
            if (ReadSectionId(ref r) != (ushort)section)
            {
                throw new SimInvariantException("state: unexpected section id");
            }
        }

        // StateWriter 에는 u16 이 없다. 리틀 엔디언 두 바이트로 쓴다(CONTRACTS 1.4)
        private static void WriteSectionId(ref StateWriter w, StateSection section)
        {
            ushort id = (ushort)section;
            w.U8((byte)id);
            w.U8((byte)(id >> ByteBits));
        }

        private static ushort ReadSectionId(ref StateReader r)
        {
            int lo = r.U8();
            int hi = r.U8();
            return (ushort)(lo | (hi << ByteBits));
        }

        // 스냅숏은 같은 모양의 세계 사이에서만 오간다(DuelClientCore 는 확정 세계의 스냅숏을 예측 세계에 싣는다, T-14)
        private SimWorld RequireCompatible(SimSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            SimWorld shadow = snapshot.World;
            if (!ReferenceEquals(shadow._db, _db) || shadow._mode != _mode || shadow._playerSlots.Length != _playerSlots.Length
                || shadow._seed != _seed)
            {
                throw new ArgumentException("snapshot buffer was made for a world of a different shape", nameof(snapshot));
            }
            return shadow;
        }
    }
}
