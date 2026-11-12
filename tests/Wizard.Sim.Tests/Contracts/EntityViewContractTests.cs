using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Spell;
using Xunit;

namespace Wizard.Sim.Tests.Contracts
{
    // CONTRACTS 3.3(T-54): EnemyView·ProjectileView 의 칸. 칸은 더하기만 한다(순서와 형식이 계약의 코드 블록과 같다).
    // 읽기 전용 값이고 참조가 없다는 것은 ViewContractTests.OtherViews_AreReadonlyValuesWithoutReferences 가 본다.
    public sealed class EntityViewContractTests
    {
        [Fact]
        public void EnemyView_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(EnemyView),
                ("Id", typeof(EntityId)), ("Type", typeof(EnemyTypeId)), ("Pos", typeof(FixVec2)), ("BodyRadius", typeof(Fix64)),
                ("Hp", typeof(Fix64)), ("MaxHp", typeof(Fix64)), ("WindupStartTick", typeof(uint)), ("WindupTicks", typeof(int)));
            var pos = new FixVec2(Fix64.FromInt(12), Fix64.FromInt(2));
            var v = new EnemyView(new EntityId(9), EnemyTypeId.ShamanCap, pos, Fix64.FromRatio(3, 10), Fix64.FromInt(15), Fix64.FromInt(40), 120u, 24);
            Assert.Equal((new EntityId(9), EnemyTypeId.ShamanCap, pos, Fix64.FromRatio(3, 10)), (v.Id, v.Type, v.Pos, v.BodyRadius));
            Assert.Equal((Fix64.FromInt(15), Fix64.FromInt(40), 120u, 24), (v.Hp, v.MaxHp, v.WindupStartTick, v.WindupTicks));
            Assert.Equal(0, default(EnemyView).WindupTicks);          // 기본값은 "준비 중이 아니다"
        }

        [Fact]
        public void ProjectileView_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(ProjectileView),
                ("Id", typeof(EntityId)), ("Faction", typeof(Faction)), ("Owner", typeof(PlayerSlot)), ("Core", typeof(BlockId)),
                ("Element", typeof(Element)), ("Pos", typeof(FixVec2)), ("Dir", typeof(FixVec2)), ("Radius", typeof(Fix64)), ("Group", typeof(int)));
            var pos = new FixVec2(Fix64.FromInt(3), Fix64.FromInt(4));
            var dir = new FixVec2(Fix64.Zero, Fix64.One);
            var v = new ProjectileView(new EntityId(5), Faction.Enemy, PlayerSlot.None, BlockId.None, Element.Fire, pos, dir, Fix64.FromRatio(12, 100), 7);
            Assert.Equal((new EntityId(5), Faction.Enemy, PlayerSlot.None, BlockId.None, Element.Fire), (v.Id, v.Faction, v.Owner, v.Core, v.Element));
            Assert.Equal((pos, dir, Fix64.FromRatio(12, 100), 7), (v.Pos, v.Dir, v.Radius, v.Group));
        }

        [Fact]
        public void Faction_NumbersAreFixed()
        {
            ContractShape.Enum<Faction>(("Player", 0), ("Enemy", 1));
        }
    }
}
