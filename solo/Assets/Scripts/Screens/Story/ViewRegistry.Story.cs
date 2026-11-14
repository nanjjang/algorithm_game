using System.Collections.Generic;

namespace Wizard.Solo.View
{
    // 00 문서 2.1·7절: 첫 만남·복수 연출, 대사 판, 처음 쪽지의 등록 자리.
    public static partial class ViewRegistry
    {
        static partial void AddStory(List<IWorldViewLayer> layers)
        {
            layers.Add(new global::Wizard.Solo.Screens.Story.StoryLayer());
            // 튜토리얼 판의 전장 위 표시(이동 목표 고리, 표적 고리, 칭찬 반짝). 다른 판에서는 그리지 않는다.
            layers.Add(new global::Wizard.Solo.Screens.Story.TutorialLayer());
        }
    }
}
