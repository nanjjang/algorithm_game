using System;

namespace Wizard.Sim.Spell
{
    // CONTRACTS 3.4(T-57), spec 02 14.4 조립 화면용 미리보기 자료. 글자는 담지 않는다: 문구와 수의 꼴은 ViewLogic 이 이름 표의 틀로 만든다.
    // 불변: 컴파일러가 채운 뒤 바꾸지 않는다. 유효하지 않은 조립(IsValid == false)은 마디와 스코프가 비고 진단만 있다
    public sealed class SpellPreview
    {
        public bool IsValid;
        public PreviewScope[] Scopes = Array.Empty<PreviewScope>();
        public PreviewNode[] Nodes = Array.Empty<PreviewNode>();
        public CompileDiagnostic[] Diagnostics = Array.Empty<CompileDiagnostic>();   // 컴파일 결과의 정렬 순서 그대로
        public int ProjectileCount;          // 맨 아래 줄의 N: 가지치기 뒤 트리 실행 1회의 투사체 수(거울상 없음)
        public int HeatPerShotHu;            // 맨 아래 줄의 H(HU. 열 1 = 120 HU)

        // 경고 줄: 진단 정렬 순서의 앞 maxLines 개(14.4 "경고 줄은 최대 3줄". 줄 수는 화면의 값이라 부르는 쪽이 준다)
        public ReadOnlySpan<CompileDiagnostic> WarningLines(int maxLines)
        {
            if (maxLines < 0)
            {
                maxLines = 0;
            }
            int n = Diagnostics.Length < maxLines ? Diagnostics.Length : maxLines;
            return new ReadOnlySpan<CompileDiagnostic>(Diagnostics, 0, n);
        }

        // 회로 칸에 걸린 진단이 있는가(경고 딱지). 트리에 마디가 없는 칸(무시된 분열·트리거)도 답한다
        public bool SlotHasWarning(int slot)
        {
            foreach (CompileDiagnostic d in Diagnostics)
            {
                if (d.Slot == slot)
                {
                    return true;
                }
            }
            return false;
        }

        // 회로 칸의 투사체 마디 번호. 그 칸이 투사체 마디가 아니면 -1(커서 연동)
        public int NodeOfSlot(int slot)
        {
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (Nodes[i].Slot == slot)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
