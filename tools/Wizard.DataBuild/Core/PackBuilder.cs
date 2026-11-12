using System;
using System.Collections.Generic;
using Wizard.Core;

namespace Wizard.DataBuild.Core
{
    // data/sim·data/rules 의 파일마다 그 절의 변환기를 불러 절 바이트를 만들고 팩으로 묶는다(ARCHITECTURE 8.2 2·4단계).
    // 절은 id 오름차순이다. 변환기가 없는 절의 파일은 오류다(파일과 변환기는 같은 작업이 같이 더한다).
    public static class PackBuilder
    {
        public static List<PackSection> BuildSections(DataSet data, ModuleRegistry modules, Diagnostics diagnostics)
        {
            var sections = new List<PackSection>();
            var files = new List<DataFile>();
            foreach (DataFile f in data.Files)
            {
                if (f.Section != Wizard.Content.ContentSection.None)
                {
                    files.Add(f);
                }
            }
            files.Sort((a, b) => ((ushort)a.Section).CompareTo((ushort)b.Section));
            foreach (DataFile f in files)
            {
                ISectionConverter? converter = modules.ConverterFor(f.Section);
                if (converter == null)
                {
                    diagnostics.Error(f.RepoPath, "절 " + Wizard.Content.ContentSections.Name(f.Section) + " 의 변환기(ISectionConverter)가 없다");
                    continue;
                }
                var w = new ByteWriter();
                try
                {
                    converter.Convert(f, w, diagnostics);
                }
                catch (DataBuildException e)
                {
                    diagnostics.Error(f.RepoPath, e.Message);
                    continue;
                }
                sections.Add(new PackSection((ushort)f.Section, w.ToArray()));
            }
            return sections;
        }

        public static byte[] WritePack(IReadOnlyList<PackSection> sections, PackOptions options) =>
            PackWriter.Write(PackWriter.SchemaVersion, options.DataVersion, PackWriter.CoreCompatFromHex(options.CoreVersion), sections);

        // 두 번 만든 절이 같은가. 다르면 다른 절마다 한 줄
        public static void CompareRuns(IReadOnlyList<PackSection> first, IReadOnlyList<PackSection> second, Diagnostics diagnostics)
        {
            if (first.Count != second.Count)
            {
                diagnostics.Error(DataLoader.DataDir + "/", "같은 data/ 에서 두 번 만든 팩의 절 수가 다르다(" + first.Count + " / " + second.Count + ")");
                return;
            }
            for (int i = 0; i < first.Count; i++)
            {
                if (first[i].Id != second[i].Id || !first[i].Body.AsSpan().SequenceEqual(second[i].Body))
                {
                    var section = (Wizard.Content.ContentSection)first[i].Id;
                    diagnostics.Error(DataLoader.DataDir + "/", "같은 data/ 에서 두 번 만든 절 " + Wizard.Content.ContentSections.Name(section)
                        + " 의 바이트가 다르다(변환기가 비결정적이다: 사전·디스크의 나열 순서, 시각, 난수에 기대지 않는다)");
                }
            }
        }
    }
}
