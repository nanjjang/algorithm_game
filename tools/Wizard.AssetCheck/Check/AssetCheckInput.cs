using System.Collections.Generic;
using Wizard.AssetCheck.Slots;
using Wizard.ViewLogic.Assets;

namespace Wizard.AssetCheck.Check
{
    // 반입 검사의 입력. 저장소에서 읽는 것은 AssetCheckRepo.Load 가 만들고, 시험은 레지스트리를 직접 만들어 넣는다
    public sealed class AssetCheckInput
    {
        public AssetCheckInput(AssetRegistry registry, AssetAliases aliases, IReadOnlyList<AssetLoadProblem> loadProblems,
            AssetSlotTable slots, AssetRules rules, ImageCache images, bool referenceExists)
        {
            Registry = registry;
            Aliases = aliases;
            LoadProblems = loadProblems;
            Slots = slots;
            Rules = rules;
            Images = images;
            ReferenceExists = referenceExists;
        }

        public AssetRegistry Registry { get; }

        public AssetAliases Aliases { get; }

        public IReadOnlyList<AssetLoadProblem> LoadProblems { get; }

        public AssetSlotTable Slots { get; }

        public AssetRules Rules { get; }

        public ImageCache Images { get; }

        // asset_rules.json char5_palette 의 reference(원본 그림)가 저장소에 있는가
        public bool ReferenceExists { get; }
    }
}
