using System;
using System.Collections.Generic;
using Wizard.DataBuild.Names;

namespace Wizard.DataBuild.Core
{
    // 한 번의 build/check 가 보는 data/ 전부. 파일은 data/ 기준 경로의 서수 오름차순이다(디스크의 나열 순서에 기대지 않는다).
    public sealed class DataSet : IDisposable
    {
        public DataSet(string root, IReadOnlyList<DataFile> files, NamesFile? names)
        {
            Root = root;
            Files = files;
            Names = names;
        }

        // 저장소 뿌리(절대 경로)
        public string Root { get; }

        public IReadOnlyList<DataFile> Files { get; }

        // data/names/names.ko.json. 없거나 읽지 못했으면 null(그 사실은 이미 진단에 있다)
        public NamesFile? Names { get; }

        public void Dispose()
        {
            foreach (DataFile f in Files)
            {
                f.Document.Dispose();
            }
        }
    }
}
