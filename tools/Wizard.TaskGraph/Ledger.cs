using System;

namespace Wizard.TaskGraph
{
    // 개정 장부(DECISIONS-TECH T-65 1번). tech-lead 가 어느 작업의 브랜치에서든 고치는 세 파일이다.
    // tools/check-tasks.mjs 의 LEDGER 와 같은 목록이다(두 도구에 각각 상수 한 곳).
    public static class Ledger
    {
        public const string DecisionsPath = "docs/dev/DECISIONS-TECH.md";
        public const string QuestionsPath = "docs/dev/OPEN-QUESTIONS.md";
        public const string TasksPath = "docs/dev/tasks.json";

        public static readonly string[] Paths = { DecisionsPath, QuestionsPath, TasksPath };

        // owned_paths 의 항목이 장부와 글자가 같은가. docs/dev/ 같은 폴더 무늬는 장부가 아니다(T-65 2번)
        public static bool IsLedger(string path) => Array.IndexOf(Paths, path) >= 0;
    }
}
