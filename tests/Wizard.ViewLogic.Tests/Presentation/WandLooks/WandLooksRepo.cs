using System;
using System.Collections.Generic;
using System.IO;
using Wizard.TestKit;
using Wizard.ViewLogic.Presentation.Limits;
using Wizard.ViewLogic.Presentation.WandLooks;
using Wizard.ViewLogic.Tests.Presentation.Limits;

namespace Wizard.ViewLogic.Tests.Presentation.WandLooks
{
    // 저장소의 data/presentation/wand_looks/ 를 입자 예산 표와 함께 한 번 읽어 둔다(작업 트리에서 돌면 그 작업 트리의 파일)
    internal static class WandLooksRepo
    {
        private static readonly Lazy<WandLookLoadResult> Loaded = new Lazy<WandLookLoadResult>(() =>
            WandLookLoader.LoadDirectory(TestRepo.Path(WandLookLoader.RepoDir), ParticleBudgetJson.Table));

        public static WandLookLoadResult Result => Loaded.Value;

        public static WandLookTable Table => Result.Table;

        public static ParticleRequestPolicy Policy => new ParticleRequestPolicy(ParticleBudgetJson.Table);

        public static string FileText(int wandKind) => File.ReadAllText(TestRepo.Path(WandLookLoader.RepoDir + "/wand_" + wandKind + ".json"));

        // 저장소 파일의 "skins": [] 를 바꿔 끼운 한 파일만 읽는다(덧씌움 등록 검사 시험용)
        public static WandLookLoadResult LoadWithSkins(int wandKind, string skinsJson)
        {
            string text = FileText(wandKind);
            const string Empty = "\"skins\": []";
            if (!text.Contains(Empty, StringComparison.Ordinal))
            {
                throw new InvalidDataException("wand_" + wandKind + ".json has no empty skins list");
            }
            var files = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("wand_" + wandKind + ".json", text.Replace(Empty, "\"skins\": " + skinsJson, StringComparison.Ordinal)),
            };
            return WandLookLoader.Load(files, ParticleBudgetJson.Table);
        }
    }
}
