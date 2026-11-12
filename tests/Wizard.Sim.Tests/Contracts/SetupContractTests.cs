using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Duel;
using Wizard.Sim.Input;
using Wizard.Sim.Waves;
using Xunit;

namespace Wizard.Sim.Tests.Contracts
{
    // CONTRACTS 3.2·3.7·3.8: SimConfig, 설정 타입, ReplayStatus, RunCheckpoint
    public sealed class SetupContractTests
    {
        // 절이 하나도 없는 팩(형식은 CONTRACTS 11.2). 공장 함수는 db 를 받기만 하고 아직 읽지 않는다
        private static ContentDb EmptyDb()
        {
            var w = new ByteWriter();
            w.U8((byte)'W'); w.U8((byte)'Z'); w.U8((byte)'P'); w.U8((byte)'K');
            w.U16(1);
            w.U32(1);
            for (int i = 0; i < 16; i++)
            {
                w.U8(0);
            }
            w.U16(0);
            byte[] head = w.ToArray();
            byte[] all = head.Concat(SHA256.HashData(head)).ToArray();
            return ContentDb.Build(ContentPack.Load(all));
        }

        [Fact]
        public void SimConfig_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(SimConfig),
                ("Mode", typeof(SimMode)),
                ("HitRadiusOverrideOn", typeof(bool)), ("HitRadiusOverride", typeof(Fix64)),
                ("InvulnTicksOverride", typeof(int)),
                ("BurstCapPermille", typeof(int)), ("BurstWindowTicks", typeof(int)),
                ("GrazeCooldownTicks", typeof(int)), ("GrazeFullReward", typeof(bool)),
                ("PenaltyWindowTicks", typeof(int)), ("SkillPrepTicks", typeof(int)), ("StatusApplyDelayTicks", typeof(int)),
                ("AimAssistCenti", typeof(int)),
                ("PlayerProjectileCap", typeof(int)), ("EnemyProjectileCap", typeof(int)), ("ProjectileCapPerPlayer", typeof(bool)),
                ("FireEnabled", typeof(bool)), ("SkillEnabled", typeof(bool)), ("StarfallEnabled", typeof(bool)),
                ("UnstableCoreSelfBlowup", typeof(bool)), ("AllArtsUnlocked", typeof(bool)),
                ("AllowDebugOps", typeof(bool)));
            Assert.True(typeof(SimConfig).IsSealed && typeof(SimConfig).IsClass);
            Assert.Equal(-1, new SimConfig().InvulnTicksOverride);
            ContractShape.HasPublicStaticMethod(typeof(SimConfig), "ForEndless", typeof(SimConfig), typeof(ContentDb));
            ContractShape.HasPublicStaticMethod(typeof(SimConfig), "ForDuel", typeof(SimConfig), typeof(ContentDb), typeof(DuelRoomOptions));
            ContractShape.HasPublicStaticMethod(typeof(SimConfig), "ForPlaza", typeof(SimConfig), typeof(ContentDb));
            ContractShape.HasPublicStaticMethod(typeof(SimConfig), "ForPractice", typeof(SimConfig), typeof(ContentDb), typeof(SimConfig));
        }

        [Fact]
        public void SimConfig_DebugOpsOffForEndlessDuelPlazaOnForPractice()
        {
            ContentDb db = EmptyDb();
            SimConfig endless = SimConfig.ForEndless(db);
            SimConfig duel = SimConfig.ForDuel(db, new DuelRoomOptions());
            SimConfig plaza = SimConfig.ForPlaza(db);
            Assert.False(endless.AllowDebugOps);
            Assert.False(duel.AllowDebugOps);
            Assert.False(plaza.AllowDebugOps);
            Assert.Equal(SimMode.Endless, endless.Mode);
            Assert.Equal(SimMode.Duel, duel.Mode);
            Assert.Equal(SimMode.Plaza, plaza.Mode);

            SimConfig practice = SimConfig.ForPractice(db, endless);
            Assert.Equal(SimMode.Practice, practice.Mode);
            Assert.True(practice.AllowDebugOps);
            Assert.NotSame(endless, practice);
            Assert.Equal(SimMode.Endless, endless.Mode);           // 바탕 설정은 바뀌지 않는다
            Assert.False(endless.AllowDebugOps);
        }

        // spec 01 12.1, 4.1의 켬·끔 스위치(수치가 아닌 칸)
        [Fact]
        public void SimConfig_ModeSwitchesFollowSpec01Table12_1()
        {
            ContentDb db = EmptyDb();
            SimConfig e = SimConfig.ForEndless(db);
            Assert.False(e.HitRadiusOverrideOn);
            Assert.Equal(-1, e.InvulnTicksOverride);
            Assert.True(e.GrazeFullReward);
            Assert.False(e.ProjectileCapPerPlayer);
            Assert.True(e.FireEnabled && e.SkillEnabled && e.StarfallEnabled && e.UnstableCoreSelfBlowup);

            SimConfig d = SimConfig.ForDuel(db, new DuelRoomOptions());
            Assert.True(d.HitRadiusOverrideOn);
            Assert.False(d.GrazeFullReward);
            Assert.True(d.ProjectileCapPerPlayer);
            Assert.True(d.FireEnabled && d.SkillEnabled);
            Assert.False(d.StarfallEnabled);
            Assert.False(d.UnstableCoreSelfBlowup);

            SimConfig p = SimConfig.ForPlaza(db);
            Assert.False(p.FireEnabled);
            Assert.False(p.SkillEnabled);
            Assert.False(p.StarfallEnabled);
        }

        [Fact]
        public void SimConfig_FactoriesRejectNull()
        {
            Assert.Throws<ArgumentNullException>(() => SimConfig.ForEndless(null!));
            Assert.Throws<ArgumentNullException>(() => SimConfig.ForDuel(EmptyDb(), null!));
            Assert.Throws<ArgumentNullException>(() => SimConfig.ForPractice(EmptyDb(), null!));
        }

        [Fact]
        public void WandSnapshot_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(WandSnapshot),
                ("Character", typeof(CharacterId)), ("Stage", typeof(int)), ("SlotsExtra", typeof(int)),
                ("Mutations", typeof(MutationId[])), ("Core", typeof(BlockId)), ("Links", typeof(BlockId[])));
        }

        [Fact]
        public void EndlessSetup_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(EndlessSetup),
                ("RunId", typeof(Guid)), ("Seed", typeof(ulong)), ("Kind", typeof(RunKind)), ("StartWave", typeof(int)),
                ("PhantomFirst", typeof(bool)), ("Wand", typeof(WandSnapshot)), ("UnlockedArts", typeof(ArtMask)),
                ("SeasonTagOn", typeof(bool)),
                ("CoreVersion", typeof(string)), ("DataVersion", typeof(int)));          // 판의 봉투 값(T-46)
            ContractShape.Enum<RunKind>(("Normal", 0), ("Weekly", 1));
            Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(RunKind)));
        }

        // CONTRACTS 3.8: 머리말은 Setup 의 정규 바이트다. core_version·data_version 을 따로 들지 않는다(T-46)
        [Fact]
        public void InputRecordHeader_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(InputRecordHeader), ("FormatVersion", typeof(ushort)), ("Setup", typeof(EndlessSetup)));
            Assert.Equal(EndlessSetup.CanonicalFormatVersion, new InputRecordHeader().FormatVersion);
        }

        [Fact]
        public void DuelSetup_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(DuelSetup),
                ("MatchId", typeof(Guid)), ("Seed", typeof(ulong)), ("Options", typeof(DuelRoomOptions)), ("Players", typeof(DuelPlayerSetup[])),
                ("CoreVersion", typeof(string)), ("DataVersion", typeof(int)));          // 경기의 봉투 값(T-48)
            Assert.Equal((ushort)1, DuelSetup.CanonicalFormatVersion);
            ContractShape.HasPublicInstanceMethod(typeof(DuelSetup), "WriteCanonical", typeof(void), typeof(ByteWriter));
            Assert.Equal(string.Empty, new DuelSetup().CoreVersion);                     // 채우기 전에는 쓰지 못한다
        }

        // CONTRACTS 3.9, spec 10 9.2: 방 옵션의 칸(명세 표의 순서). 비밀번호는 없다(T-48)
        [Fact]
        public void DuelRoomOptions_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(DuelRoomOptions),
                ("Rounds", typeof(int)), ("RoundSeconds", typeof(int)), ("HpMultPermille", typeof(int)), ("EnhanceCap", typeof(int)),
                ("IlCap", typeof(int)), ("AllowMutations", typeof(bool)), ("HeroBlockCap", typeof(int)), ("Equalize", typeof(bool)),
                ("WildIntrusion", typeof(bool)), ("ThornsOff", typeof(bool)), ("FloorColor", typeof(TimeOfDay)), ("Spectate", typeof(bool)));
            Assert.Equal(-1, DuelRoomOptions.NoCap);
            var fresh = new DuelRoomOptions();
            Assert.Equal(DuelRoomOptions.NoCap, fresh.IlCap);
            Assert.Equal(DuelRoomOptions.NoCap, fresh.HeroBlockCap);
            MemberInfo[] all = typeof(DuelRoomOptions).GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.DoesNotContain(all, m => m.Name.IndexOf("Password", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // CONTRACTS 3.8: 검사를 통과한 기록에서 서버의 1차 검사가 읽는 칸(T-49)
        [Fact]
        public void InputRecordBody_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(InputRecordBody),
                ("HeaderLength", typeof(int)), ("EndTick", typeof(uint)), ("FinalStateHash", typeof(ulong)),
                ("DebugUsed", typeof(bool)), ("HasDebugOps", typeof(bool)),
                ("CommandCount", typeof(int)), ("InputCount", typeof(int)), ("PauseCount", typeof(int)), ("PauseTotalMs", typeof(long)),
                ("Checkpoints", typeof(RunCheckpoint[])));
            Assert.Empty(new InputRecordBody().Checkpoints);
        }

        [Fact]
        public void DuelRecordBody_And_DuelReplayResult_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(DuelRecordBody),
                ("HeaderLength", typeof(int)), ("InputDelayTicks0", typeof(int)), ("InputDelayTicks1", typeof(int)),
                ("EndTick", typeof(uint)), ("FinalStateHash", typeof(ulong)), ("InputCount", typeof(int)));
            ContractShape.Fields(typeof(DuelReplayResult),
                ("Status", typeof(ReplayStatus)), ("Outcome", typeof(DuelOutcome)), ("TicksSimulated", typeof(uint)), ("FaultDetail", typeof(string)));
        }

        // CONTRACTS 3.8: 번호는 더하기만 한다
        [Fact]
        public void RecordError_ValuesMatchContract()
        {
            Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(RecordError)));
            ContractShape.Enum<RecordError>(
                ("None", 0), ("BadMagic", 1), ("BadVersion", 2), ("Truncated", 3), ("TrailingBytes", 4), ("ShaMismatch", 5),
                ("TickOverflow", 6), ("TickBackwards", 7), ("TooManyKeys", 8), ("KeyOrder", 9), ("CommandAfterKey", 10),
                ("BadKey", 11), ("BadOp", 12), ("UnknownTag", 13), ("EndTickOverLimit", 14),
                ("BadSlot", 15), ("BadHeader", 16), ("BadFlags", 17), ("BadSeq", 18));
        }

        // CONTRACTS 3.8: 신뢰할 수 없는 바이트를 읽는 문 둘과 WZDR 을 쓰는 쪽의 시그니처
        [Fact]
        public void RecordCodecs_SignaturesMatchContract()
        {
            Type span = typeof(ReadOnlySpan<byte>);
            ContractShape.HasPublicStaticMethod(typeof(InputRecordCodec), "TryRead", typeof(bool), span, typeof(ReplayLimits),
                typeof(InputRecordHeader).MakeByRefType(), typeof(InputRecordBody).MakeByRefType(), typeof(RecordError).MakeByRefType());
            ContractShape.HasPublicStaticMethod(typeof(DuelRecordCodec), "TryRead", typeof(bool), span, typeof(ReplayLimits),
                typeof(DuelSetup).MakeByRefType(), typeof(DuelRecordBody).MakeByRefType(), typeof(RecordError).MakeByRefType());
            ContractShape.HasPublicStaticMethod(typeof(DuelSetup), "TryReadCanonical", typeof(bool), span, typeof(DuelSetup).MakeByRefType());
            ContractShape.HasPublicStaticMethod(typeof(DuelSetup), "TryReadCanonicalPrefix", typeof(bool), span,
                typeof(DuelSetup).MakeByRefType(), typeof(int).MakeByRefType());
            ContractShape.HasPublicStaticMethod(typeof(ReplayRunner), "RunDuel", typeof(DuelReplayResult), typeof(ContentDb), span, typeof(ReplayLimits));
            Assert.NotNull(typeof(DuelRecordWriter).GetConstructor(new[] { typeof(DuelSetup) }));
            ContractShape.HasPublicInstanceMethod(typeof(DuelRecordWriter), "SetInputDelay", typeof(void), typeof(PlayerSlot), typeof(int));
            ContractShape.HasPublicInstanceMethod(typeof(DuelRecordWriter), "Append", typeof(void), typeof(PlayerInput).MakeByRefType());
            ContractShape.HasPublicInstanceMethod(typeof(DuelRecordWriter), "Finish", typeof(ReadOnlyMemory<byte>), typeof(uint), typeof(ulong));
        }

        // 입력 지연 D는 시뮬레이션이 모른다(CONTRACTS 0.3, 10.5절)
        [Fact]
        public void DuelPlayerSetup_FieldsMatchContractAndHasNoInputDelay()
        {
            ContractShape.Fields(typeof(DuelPlayerSetup),
                ("Slot", typeof(PlayerSlot)), ("AppliedWand", typeof(WandSnapshot)), ("ActualStage", typeof(int)),
                ("MaxHpPoints", typeof(int)), ("UnlockedArts", typeof(ArtMask)));
            MemberInfo[] all = typeof(DuelPlayerSetup).GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.DoesNotContain(all, m => m.Name.IndexOf("Delay", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(typeof(DuelSetup).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                                  m => m.Name.IndexOf("Delay", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void ReplayStatus_SevenValues()
        {
            Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(ReplayStatus)));
            ContractShape.Enum<ReplayStatus>(
                ("Completed", 0), ("RecordCorrupt", 1), ("EndedEarly", 2), ("EndedLate", 3), ("SimFault", 4), ("Timeout", 5), ("PackMissing", 6));
        }

        [Fact]
        public void RunCheckpoint_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(RunCheckpoint), ("Wave", typeof(int)), ("Tick", typeof(uint)), ("StateHash", typeof(ulong)));
            var c = new RunCheckpoint(5, 1234u, 0xDEADBEEFUL);
            Assert.Equal(5, c.Wave);
            Assert.Equal(1234u, c.Tick);
            Assert.Equal(0xDEADBEEFUL, c.StateHash);
        }

        [Fact]
        public void SimVersion_Constants()
        {
            Assert.Equal(60, SimVersion.TickRate);
            Assert.Equal((byte)1, SimVersion.FactsFormat);
            Assert.Matches("^[0-9a-f]{32}$", SimVersion.CoreVersion);
        }
    }
}
