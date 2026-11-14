using System.Collections.Generic;

namespace Wizard.Solo.View
{
    // 00 문서 2.1·7절: 첫 만남·복수 연출, 대사 판, 처음 쪽지의 등록 자리.
    public static partial class ViewRegistry
    {
        static partial void AddStory(List<IWorldViewLayer> layers)
        {
            layers.Add(new global::Wizard.Solo.Screens.Story.StoryLayer());
        }
    }
}
