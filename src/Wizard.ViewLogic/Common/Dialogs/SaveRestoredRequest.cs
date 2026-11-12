using System;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // SaveRestored 를 열 때 주는 값. GenerationsBack = 몇 세대 전 저장으로 되돌렸는가(1 ~ 3, on 8.3: 직전 3세대 보관)
    public sealed class SaveRestoredRequest
    {
        public const int MaxGenerationsBack = 3;

        public SaveRestoredRequest(int generationsBack, Action<DialogButtonRole>? onChosen = null)
        {
            if (generationsBack < 1 || generationsBack > MaxGenerationsBack)
            {
                throw new ArgumentOutOfRangeException(nameof(generationsBack), generationsBack, "1 .. " + MaxGenerationsBack);
            }
            GenerationsBack = generationsBack;
            OnChosen = onChosen;
        }

        public int GenerationsBack { get; }
        public Action<DialogButtonRole>? OnChosen { get; }
    }
}
