using System;

namespace Wizard.ReplayCli
{
    // 상태 절 하나와 그 모듈이 구현한 상태 훅(CONTRACTS 3.2: 모듈마다 Snapshot_Save, Snapshot_Load, State_Write, State_Read)
    public sealed class StateSectionInfo
    {
        internal StateSectionInfo(ushort id, string name, Delegate? writer, bool hasWrite, bool hasRead, bool hasSave, bool hasLoad)
        {
            Id = id;
            Name = name;
            Writer = writer;
            HasWrite = hasWrite;
            HasRead = hasRead;
            HasSnapshotSave = hasSave;
            HasSnapshotLoad = hasLoad;
        }

        public ushort Id { get; }

        // 절 열거형의 이름(= 훅 이름의 뒷부분). 예: "Player", "SpellTable"
        public string Name { get; }

        public bool HasWrite { get; }

        public bool HasRead { get; }

        public bool HasSnapshotSave { get; }

        public bool HasSnapshotLoad { get; }

        internal Delegate? Writer { get; }

        public override string ToString() => Name + "(" + Id + ")";
    }
}
