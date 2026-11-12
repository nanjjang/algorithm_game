using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Arts;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Contracts
{
    // CONTRACTS 3.3: ISimView 와 읽기 전용 뷰 구조체
    public sealed class ViewContractTests
    {
        [Fact]
        public void ISimView_MembersMatchContract()
        {
            Type t = typeof(ISimView);
            Assert.True(t.IsInterface);
            Assert.Equal(typeof(uint), t.GetProperty("Tick")!.PropertyType);
            Assert.Equal(typeof(int), t.GetProperty("PlayerCount")!.PropertyType);
            ContractShape.HasPublicInstanceMethod(t, "Player", typeof(PlayerView), typeof(PlayerSlot));
            Assert.Equal(typeof(ReadOnlySpan<EnemyView>), t.GetProperty("Enemies")!.PropertyType);
            Assert.Equal(typeof(ReadOnlySpan<ProjectileView>), t.GetProperty("Projectiles")!.PropertyType);
            Assert.Equal(typeof(ReadOnlySpan<TelegraphView>), t.GetProperty("Telegraphs")!.PropertyType);
            Assert.Equal(typeof(ReadOnlySpan<HazardView>), t.GetProperty("Hazards")!.PropertyType);
            Assert.Equal(typeof(WaveView), t.GetProperty("Wave")!.PropertyType);
            Assert.Equal(typeof(CampView), t.GetProperty("Camp")!.PropertyType);
            Assert.Equal(typeof(DuelView), t.GetProperty("Duel")!.PropertyType);
            // 읽기 전용: 설정자가 없다
            Assert.All(t.GetProperties(), p => Assert.False(p.CanWrite, p.Name + " 에 설정자가 있다"));
            Assert.Equal(new[] { "Camp", "Duel", "Enemies", "Hazards", "PlayerCount", "Projectiles", "Telegraphs", "Tick", "Wave" },
                         t.GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        }

        [Fact]
        public void ISimEventSink_EmitsByReadonlyRef()
        {
            MethodInfo m = typeof(ISimEventSink).GetMethod("Emit")!;
            ParameterInfo p = m.GetParameters().Single();
            Assert.Equal(typeof(SimEvent).MakeByRefType(), p.ParameterType);
            Assert.True(p.IsIn);
        }

        [Fact]
        public void PlayerView_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(PlayerView),
                ("Id", typeof(EntityId)), ("Character", typeof(CharacterId)),
                ("Pos", typeof(FixVec2)), ("Facing", typeof(Dir8)), ("Stance", typeof(StanceMode)),
                ("HpPoints", typeof(int)), ("MaxHpPoints", typeof(int)), ("Invulnerable", typeof(bool)),
                ("HeatHu", typeof(int)), ("CritStartHu", typeof(int)), ("HeatRing", typeof(HeatRingState)), ("ExemptArmed", typeof(bool)),
                ("LockRemainTicks", typeof(int)), ("LockTotalTicks", typeof(int)),
                ("Combo", typeof(int)), ("ComboIdleTicks", typeof(int)), ("ComboTimerPaused", typeof(bool)),
                ("Crystals", typeof(int)), ("CrystalCap", typeof(int)), ("Debt", typeof(int)), ("Crack", typeof(int)),
                ("GrazeBuffActive", typeof(bool)),
                ("Preview", typeof(ArtPreview)), ("Recent", typeof(RecentInputs)), ("Keys", typeof(KeyMask)));
            AssertReadonlyValue(typeof(PlayerView));
        }

        [Fact]
        public void PlayerView_ConstructorKeepsEveryField()
        {
            var preview = new ArtPreview(ArtId.None, 1, ArtId.None, 2, 3, 4, 5);
            RecentInputs recent = RecentInputs.Empty.Push(CmdDir.Left, false);
            var v = new PlayerView(new EntityId(7), CharacterId.Char3, new FixVec2(Fix64.FromInt(2), Fix64.FromInt(-3)), Dir8.NW, StanceMode.Reverse,
                                   400, 600, true, 480, 9600, HeatRingState.Critical, true, 11, 180, 12, 13, true,
                                   2, 5, 1, 1, true, preview, recent, KeyState.Bit(LogicalKey.Up));
            Assert.Equal(7, v.Id.Value);
            Assert.Equal(CharacterId.Char3, v.Character);
            Assert.Equal(Fix64.FromInt(-3), v.Pos.Y);
            Assert.Equal(Dir8.NW, v.Facing);
            Assert.Equal(StanceMode.Reverse, v.Stance);
            Assert.Equal((400, 600, true), (v.HpPoints, v.MaxHpPoints, v.Invulnerable));
            Assert.Equal((480, 9600, HeatRingState.Critical, true), (v.HeatHu, v.CritStartHu, v.HeatRing, v.ExemptArmed));
            Assert.Equal((11, 180), (v.LockRemainTicks, v.LockTotalTicks));
            Assert.Equal((12, 13, true), (v.Combo, v.ComboIdleTicks, v.ComboTimerPaused));
            Assert.Equal((2, 5, 1, 1, true), (v.Crystals, v.CrystalCap, v.Debt, v.Crack, v.GrazeBuffActive));
            Assert.Equal(3, v.Preview.CreditCount);
            Assert.Equal(CmdDir.Left, v.Recent.Dir(0));
            Assert.Equal(KeyState.Bit(LogicalKey.Up), v.Keys);
        }

        [Fact]
        public void ArtPreview_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(ArtPreview),
                ("JustArt", typeof(ArtId)), ("JustCost", typeof(int)), ("NormalArt", typeof(ArtId)), ("NormalCost", typeof(int)),
                ("CreditCount", typeof(int)), ("FizzleInputIndex", typeof(int)), ("FizzleLateByUnits", typeof(int)));
            AssertReadonlyValue(typeof(ArtPreview));
            var p = new ArtPreview(ArtId.None, 1, ArtId.None, 2, 3, 4, 5);
            Assert.Equal((1, 2, 3, 4, 5), (p.JustCost, p.NormalCost, p.CreditCount, p.FizzleInputIndex, p.FizzleLateByUnits));
        }

        [Fact]
        public void RecentInputs_KeepsFiveNewestFirst()
        {
            Assert.Equal(5, RecentInputs.Capacity);
            ContractShape.HasPublicInstanceMethod(typeof(RecentInputs), "Dir", typeof(CmdDir), typeof(int));
            ContractShape.HasPublicInstanceMethod(typeof(RecentInputs), "Chained", typeof(bool), typeof(int));
            AssertReadonlyValue(typeof(RecentInputs));

            RecentInputs r = RecentInputs.Empty;
            Assert.Equal(0, r.Count);
            r = r.Push(CmdDir.Forward, false)
                 .Push(CmdDir.Back, true)
                 .Push(CmdDir.Left, true)
                 .Push(CmdDir.Right, false)
                 .Push(CmdDir.Forward, true);
            Assert.Equal(5, r.Count);
            Assert.Equal(new[] { CmdDir.Forward, CmdDir.Right, CmdDir.Left, CmdDir.Back, CmdDir.Forward }, Enumerable.Range(0, 5).Select(r.Dir));
            Assert.Equal(new[] { true, false, true, true, false }, Enumerable.Range(0, 5).Select(r.Chained));

            r = r.Push(CmdDir.Back, false);                   // 여섯 번째: 가장 오래된 것이 빠진다
            Assert.Equal(5, r.Count);
            Assert.Equal(new[] { CmdDir.Back, CmdDir.Forward, CmdDir.Right, CmdDir.Left, CmdDir.Back }, Enumerable.Range(0, 5).Select(r.Dir));
            Assert.Equal(new[] { false, true, false, true, true }, Enumerable.Range(0, 5).Select(r.Chained));
        }

        [Fact]
        public void RecentInputs_IndexOutsideCountIsInvariantViolation()
        {
            RecentInputs r = RecentInputs.Empty.Push(CmdDir.Left, false);
            Assert.Throws<SimInvariantException>(() => r.Dir(1));
            Assert.Throws<SimInvariantException>(() => r.Chained(-1));
            Assert.Throws<SimInvariantException>(() => RecentInputs.Empty.Push((CmdDir)4, false));
        }

        [Fact]
        public void RecentInputs_PushAllocatesNothing()
        {
            RecentInputs r = RecentInputs.Empty;
            r = r.Push(CmdDir.Left, true);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                r = r.Push((CmdDir)(i & 3), (i & 1) == 0);
            }
            Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
            Assert.Equal(5, r.Count);
        }

        [Fact]
        public void CampView_HasSubAndScalarCampFields()
        {
            ContractShape.Fields(typeof(CampView),
                ("Sub", typeof(CampSub)), ("ClearedWave", typeof(int)), ("HealsThisCamp", typeof(int)), ("UnlockTurnPending", typeof(bool)));
            AssertReadonlyValue(typeof(CampView));
            var v = new CampView(CampSub.CampScreen, 5, 1, true);
            Assert.Equal((CampSub.CampScreen, 5, 1, true), (v.Sub, v.ClearedWave, v.HealsThisCamp, v.UnlockTurnPending));
            Assert.Equal(CampSub.None, default(CampView).Sub);
        }

        [Fact]
        public void CampSub_ValuesMatchContractTable()
        {
            ContractShape.Enum<CampSub>(("None", 0), ("LootPick", 1), ("FireIdle", 2), ("CampScreen", 3), ("Practice", 4), ("DepartHold", 5));
        }

        [Fact]
        public void TelegraphView_GivesStartTickAndLength()
        {
            ContractShape.Fields(typeof(TelegraphView), ("StartTick", typeof(uint)), ("LengthTicks", typeof(int)));
            AssertReadonlyValue(typeof(TelegraphView));
            var t = new TelegraphView(100u, 36);
            Assert.Equal((100u, 36), (t.StartTick, t.LengthTicks));
        }

        [Fact]
        public void OtherViews_AreReadonlyValuesWithoutReferences()
        {
            foreach (Type t in new[] { typeof(EnemyView), typeof(ProjectileView), typeof(HazardView), typeof(WaveView), typeof(DuelView) })
            {
                AssertReadonlyValue(t);
            }
        }

        private static void AssertReadonlyValue(Type t)
        {
            Assert.True(t.IsValueType, t.Name + " 는 값 형식이어야 한다");
            Assert.True(t.GetCustomAttributes().Any(a => a.GetType().FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute"),
                        t.Name + " 는 readonly struct 여야 한다");
            MethodInfo check = typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences))!.MakeGenericMethod(t);
            Assert.False((bool)check.Invoke(null, null)!, t.Name + " 에 참조 칸이 있다");
        }
    }
}
