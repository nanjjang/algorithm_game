using System.Collections.Generic;
using Wizard.DataBuild.Names;

namespace Wizard.DataBuild.Core
{
    // databuild check / build 의 몸통(ARCHITECTURE 8.2):
    //   1. data/ 읽기(꼴, 같은 열쇠), 이름 표 형식(names --check 와 같은 검사)
    //   2. JSON Schema
    //   3. 변환(절마다) + 교차 검사(IDataValidator: 이름 열쇠 …)        ← 1·2 에 오류가 있으면 하지 않는다
    //   4. 결정성: data/ 를 처음부터 다시 읽고 다시 변환해 절 바이트가 같은지 본다(G5 "같은 입력에서 팩 바이트가 같다")
    //   5. 팩 바이트와 이름 표 이진 절(names.ko.wznm)
    // 오류가 하나라도 있으면 Pack·Names 는 null 이다.
    public static class Pipeline
    {
        public sealed class Output
        {
            public Output(byte[]? pack, byte[]? names, int sectionCount, int fileCount)
            {
                Pack = pack;
                Names = names;
                SectionCount = sectionCount;
                FileCount = fileCount;
            }

            public byte[]? Pack { get; }

            public byte[]? Names { get; }

            public int SectionCount { get; }

            public int FileCount { get; }
        }

        public static Output Run(string root, ModuleRegistry modules, PackOptions options, Diagnostics diagnostics)
        {
            List<PackSection> first;
            byte[]? names = null;
            int fileCount;
            using (DataSet data = DataLoader.Load(root, diagnostics))
            {
                fileCount = data.Files.Count;
                SchemaCheck.Run(data, diagnostics);
                if (diagnostics.HasErrors)
                {
                    return new Output(null, null, 0, fileCount);
                }
                first = PackBuilder.BuildSections(data, modules, diagnostics);
                foreach (IDataValidator v in modules.Validators)
                {
                    v.Validate(data, diagnostics);
                }
                if (data.Names != null)
                {
                    names = NameTableWriter.Write(data.Names);
                }
            }
            if (diagnostics.HasErrors)
            {
                return new Output(null, null, first.Count, fileCount);
            }
            var again = new Diagnostics();
            List<PackSection> second;
            using (DataSet data = DataLoader.Load(root, again))
            {
                second = PackBuilder.BuildSections(data, modules, again);
            }
            if (again.HasErrors)
            {
                diagnostics.Error(DataLoader.DataDir + "/", "data/ 를 다시 읽었더니 결과가 달라졌다: " + again.Sorted()[0]);
                return new Output(null, null, first.Count, fileCount);
            }
            PackBuilder.CompareRuns(first, second, diagnostics);
            if (diagnostics.HasErrors)
            {
                return new Output(null, null, first.Count, fileCount);
            }
            return new Output(PackBuilder.WritePack(first, options), names, first.Count, fileCount);
        }
    }
}
