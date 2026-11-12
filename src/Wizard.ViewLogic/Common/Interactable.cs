using System;

namespace Wizard.ViewLogic.Common
{
    // spec 13 1.3절, spec 14 4.3절. 누를 수 있는 것 하나(버튼, 탭, 칸, 목록 줄, 카드)의 상태.
    // 상태 = (Enabled, On, Hovered, Cursor, Picked, Pressed). 바꾸는 것은 InteractableGroup 의 입력 처리뿐이다(입력 모드와 같이 봐야 하므로).
    // Id 는 화면 안의 식별자(이벤트의 "대상 id")다. 화면에 찍는 글자가 아니다.
    public sealed class Interactable
    {
        public Interactable(string id, int widthPx, int heightPx, bool isToggle = false, bool hasOnSkin = true)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            WidthPx = widthPx;
            HeightPx = heightPx;
            IsToggle = isToggle;
            HasOnSkin = hasOnSkin;
        }

        public string Id { get; }

        // 그림 크기. 괄호 크기를 고른다(spec 14 4.3 BracketSize). 배치가 바뀌면 고친다
        public int WidthPx { get; set; }
        public int HeightPx { get; set; }

        public bool IsToggle { get; }

        // 그림에 on 상태가 있는가(spec 14 4.3 "On and 그림에 on 상태가 있음")
        public bool HasOnSkin { get; }

        public bool Enabled { get; internal set; } = true;

        // 꺼짐일 때 아래 한 줄·툴팁의 이유(이름 표 열쇠). "골드가 모자란다" 따위
        public string? DisabledReasonKey { get; internal set; }

        public bool ToggledOn { get; internal set; }

        // 포인터가 위에 있는가(입력 모드와 무관한 원래 상태. 표시는 InteractableGroup.SkinOf 가 모드를 보고 정한다)
        public bool Hovered { get; internal set; }

        // 키보드 커서가 여기에 있는가(마우스로 눌러 고른 것에도 온다)
        public bool Cursor { get; internal set; }

        // 이미 고른 것(확정)
        public bool Picked { get; internal set; }

        public bool Pressed { get; internal set; }

        public bool On => IsToggle && ToggledOn;

        public BracketSize BracketSize => Brackets.SizeFor(WidthPx, HeightPx);
    }
}
