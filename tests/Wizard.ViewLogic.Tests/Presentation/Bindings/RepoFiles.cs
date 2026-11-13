using System;
using System.IO;
using System.Text;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Bindings
{
    // 저장소의 데이터·문서 파일을 읽는다(테스트 실행 폴더에서 Wizard.sln 이 든 폴더를 찾아 올라간다)
    internal static class RepoFiles
    {
        public static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Wizard.sln")))
            {
                dir = dir.Parent;
            }
            Assert.True(dir != null, "Wizard.sln 이 든 폴더를 찾지 못했다");
            return dir!.FullName;
        }

        public static string Read(string repoRelative) =>
            File.ReadAllText(Path.Combine(Root(), repoRelative.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);

        public static string[] Lines(string repoRelative) =>
            File.ReadAllLines(Path.Combine(Root(), repoRelative.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);

        // 저장소의 실제 바인딩 표. 읽기 오류가 있으면 실패한다
        public static EventBindingTable LoadRealTable()
        {
            var errors = new System.Collections.Generic.List<string>();
            EventBindingTable t = EventBindingTable.Load(Read(EventBindingTable.BindingsFile), Read(EventBindingTable.NoneFile), errors);
            Assert.True(errors.Count == 0, string.Join("\n", errors));
            return t;
        }
    }
}
