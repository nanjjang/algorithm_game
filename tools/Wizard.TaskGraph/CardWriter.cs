using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wizard.TaskGraph
{
    // PIPELINE 3.4절의 작업 카드를 tasks.json 항목으로 채운다. WT 는 <메인 저장소>/.claude/worktrees/<id> 의 절대 경로다.
    public static class CardWriter
    {
        public static string Write(TaskDoc doc, TaskDef t, RepoLayout layout, IReadOnlyDictionary<string, string> state)
        {
            string wt = layout.WorktreeOf(t.Id);
            var sb = new StringBuilder();
            sb.Append("# 작업 카드 ").Append(t.Id).Append(" — ").Append(t.Title).Append("\n\n");
            sb.Append("- 마일스톤 / 묶음: ").Append(t.Milestone).Append(" / ").Append(t.ParallelGroup).Append('\n');
            sb.Append("- 담당: ").Append(t.OwnerAgent).Append(" (").Append(t.OwnerModel).Append(")   검토: ").Append(t.ReviewerModel).Append('\n');
            sb.Append("- 작업 트리(절대 경로): WT=").Append(wt).Append("   브랜치: ").Append(RepoLayout.BranchOf(t.Id)).Append('\n');
            sb.Append("- **모든 명령과 파일 경로는 $WT 아래로 준다.** 에이전트의 셸은 호출마다 메인 저장소로 돌아간다. 상대 경로로 치면 main 체크아웃이 빌드·검사되고 \"통과\"가 나온다\n\n");

            sb.Append("## 목표\n");
            sb.Append(t.Title).Append(". 아래 수락 테스트가 전부 통과하면 끝난다.\n\n");

            sb.Append("## 명세 (먼저 읽는다)\n");
            Bullets(sb, t.SpecRefs);
            sb.Append('\n');

            sb.Append("## 계약 (시그니처는 여기 그대로)\n");
            Bullets(sb, t.Contracts);
            sb.Append("명세의 표현이 계약과 다르면 CONTRACTS 0.3절 통합 표와 2.1절 대응표로 바꿔 읽는다.\n\n");

            sb.Append("## 소유 파일 (이 밖은 고치지 않는다)\n");
            Bullets(sb, t.OwnedPaths.Select(p => "`" + p + "`").ToList());
            sb.Append("공유 추가 전용 파일(PIPELINE 4.4절)은 줄을 더할 수만 있다.\n\n");

            sb.Append("## 앞선 작업\n");
            if (t.Deps.Count == 0)
            {
                sb.Append("- 없음\n");
            }
            foreach (string d in t.Deps)
            {
                if (doc.ById.TryGetValue(d, out TaskDef? dt))
                {
                    sb.Append("- ").Append(d).Append(" — ").Append(dt.Title)
                      .Append(" (").Append(Lifecycle.Effective(dt, state)).Append("). 남긴 곳: ")
                      .Append(string.Join(", ", dt.OwnedPaths.Select(p => "`" + p + "`"))).Append('\n');
                }
                else
                {
                    sb.Append("- ").Append(d).Append(" — (tasks.json 에 없다)\n");
                }
            }
            sb.Append('\n');

            sb.Append("## 수락 테스트\n");
            Bullets(sb, t.Acceptance);
            sb.Append("벡터: ");
            var vectors = t.Vectors.Select(v => v.Raw).ToList();
            sb.Append(vectors.Count == 0 ? "없음" : string.Join(", ", vectors)).Append('\n');
            sb.Append("공통: $WT/tools/ci/ci.sh fast 통과, vectors의 벡터가 VectorCoverage에서 빠짐 0.\n\n");

            sb.Append("## 금지 사항\n");
            sb.Append("- 소유 파일 밖 수정. 계약(시그니처, 열거형 번호, 파일 형식) 변경\n");
            sb.Append("- 명세의 테스트 벡터를 구현에 맞춰 고치기\n");
            sb.Append("- 코드에 수치 상수나 화면 문자열 넣기\n");
            sb.Append("- 코어(Core, Content, Sim)에서 CLAUDE.md의 결정론 금지 목록 쓰기\n");
            sb.Append("- 분석기 진단 억제, 테스트 건너뛰기 표시\n");
            sb.Append("- 일정·삭감 제안\n\n");

            sb.Append("## 사람 확인 항목\n");
            sb.Append(string.IsNullOrEmpty(t.HumanCheck) ? "없음" : t.HumanCheck).Append('\n');
            return sb.ToString();
        }

        private static void Bullets(StringBuilder sb, IReadOnlyList<string> items)
        {
            if (items.Count == 0)
            {
                sb.Append("- 없음\n");
                return;
            }
            foreach (string s in items)
            {
                sb.Append("- ").Append(s).Append('\n');
            }
        }
    }
}
