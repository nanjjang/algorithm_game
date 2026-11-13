using System.Collections.Generic;

namespace Wizard.AssetCheck.Scaffold
{
    // scaffold 의 결과: 넣을 매니페스트(저장소 기준), 항목 초안(JSON 조각), 사람이 볼 메모. 실패면 Error 만 있다
    public sealed class ScaffoldResult
    {
        private ScaffoldResult(bool success, string manifest, string entry, IReadOnlyList<string> notes, string error)
        {
            Success = success;
            Manifest = manifest;
            Entry = entry;
            Notes = notes;
            Error = error;
        }

        public bool Success { get; }

        public string Manifest { get; }

        public string Entry { get; }

        public IReadOnlyList<string> Notes { get; }

        public string Error { get; }

        public static ScaffoldResult Ok(string manifest, string entry, IReadOnlyList<string> notes) => new ScaffoldResult(true, manifest, entry, notes, "");

        public static ScaffoldResult Fail(string error) => new ScaffoldResult(false, "", "", new string[0], error);
    }
}
