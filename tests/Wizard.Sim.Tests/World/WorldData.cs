using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Sim.Tests.World
{
    // World 시험의 도우미. 저장소의 data/ 로 만든 팩(전장, 기준값 캐릭터)과 명세 표의 값으로 만든 캐릭터.
    internal static class WorldData
    {
        public const long OneRaw = 4_294_967_296L;                 // 2^32

        // spec 01 2.2 상수표의 0.30(몸 반지름) Raw
        public const long BodyRadiusRaw = 1_288_490_189L;

        private static readonly Lazy<ContentDb> RepoDbLazy = new Lazy<ContentDb>(BuildRepoDb);

        public static ContentDb RepoDb => RepoDbLazy.Value;

        private static ContentDb BuildRepoDb()
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(TestRepo.Root, ModuleRegistry.Discover(),
                                             PackOptions.Default(PackOptions.DefaultDataVersion), d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            return ContentDb.Build(ContentPack.Load(o.Pack!));
        }

        // 기준값 캐릭터(= 갈피의 이동·자세·스침 수치, spec 01 15절 C16)
        public static CharacterDef Baseline => RepoDb.Character(CharacterId.Baseline);

        // 명세 spec 01 5.1 표의 이동 값으로 만든 캐릭터. 다섯 캐릭터의 데이터 줄은 아직 팩에 없다(M5-DAT-01)
        public static CharacterDef Spec(CharacterId id, int walkCenti, int stanceCenti)
        {
            return new CharacterDef
            {
                Id = id,
                WalkSpeedCenti = walkCenti,
                StanceSpeedCenti = stanceCenti,
                BodyRadius = Fix64.FromRaw(BodyRadiusRaw),
                GrazeBuffPermille = 1200,                           // spec 01 5.6: 스침 가속 +20%
            };
        }

        // spec 01 5.1: 달구 D1, 초롱 D2, 갈피 D3, 성에 D4, 그믐 D5
        public static CharacterDef D1 => Spec(CharacterId.Char1, 470, 376);
        public static CharacterDef D2 => Spec(CharacterId.Char2, 560, 504);
        public static CharacterDef D3 => Spec(CharacterId.Char3, 500, 425);
        public static CharacterDef D4 => Spec(CharacterId.Char4, 450, 450);
        public static CharacterDef D5 => Spec(CharacterId.Char5, 520, 442);

        public static ArenaGeometryFor Field => new ArenaGeometryFor(RepoDb.Arena(SimMode.Endless));

        public static ArenaGeometryFor DuelArena => new ArenaGeometryFor(RepoDb.Arena(SimMode.Duel));

        public static Fix64 Cells(long whole) => Fix64.FromRaw(whole * OneRaw);

        public static Fix64 Ratio(long num, long den) => Fix64.FromRatio(num, den);

        public static FixVec2 At(Fix64 x, Fix64 y) => new FixVec2(x, y);
    }

    // 전장 정의와 그 기하를 같이 들고 다닌다
    internal readonly struct ArenaGeometryFor
    {
        public readonly ArenaDef Def;
        public readonly Wizard.Sim.World.ArenaGeometry Geometry;

        public ArenaGeometryFor(ArenaDef def)
        {
            Def = def;
            Geometry = new Wizard.Sim.World.ArenaGeometry(def);
        }
    }
}
