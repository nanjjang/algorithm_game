using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Wizard.TestKit;
using Xunit;

namespace Wizard.DataBuild.Tests.Evasion
{
    // 저장소의 data/ 를 통째로 임시 저장소(Wizard.sln 표지)로 복사한다. 시험은 복사본만 고친다.
    internal sealed class RepoDataCopy : IDisposable
    {
        public RepoDataCopy()
        {
            Root = Path.Combine(Path.GetTempPath(), "wizard-databuild-evasion-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "Wizard.sln"), "");
            CopyDir(TestRepo.Path("data"), Path.Combine(Root, "data"));
        }

        public string Root { get; }

        public string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

        public void Write(string relative, string text) =>
            File.WriteAllBytes(Path.Combine(Root, relative), new UTF8Encoding(false).GetBytes(text));

        // from 이 꼭 한 번 나와야 한다(시험이 엉뚱한 곳을 고치지 않게)
        public void ReplaceOnce(string relative, string from, string to)
        {
            string text = Read(relative);
            int at = text.IndexOf(from, StringComparison.Ordinal);
            Assert.True(at >= 0, relative + " 에 '" + from + "' 가 없다");
            Assert.True(text.IndexOf(from, at + from.Length, StringComparison.Ordinal) < 0, relative + " 에 '" + from + "' 가 둘 이상이다");
            Write(relative, text.Substring(0, at) + to + text.Substring(at + from.Length));
        }

        // 캐릭터 줄(기준값 줄과 characters 배열) 모두의 자세 속도를 같은 값으로 둔다 → 회피 검산의 V 가 그 값이 된다.
        // 캐릭터 다섯 줄이 데이터에 들어오기 전(기준값 줄만 있을 때)과 뒤 모두에서 같은 V 로 시험하려고 쓴다
        public void SetAllStanceSpeeds(string value)
        {
            const string rel = "data/sim/characters.json";
            string text = Read(rel);
            string changed = Regex.Replace(text, "\"stance_speed\": [0-9.]+", "\"stance_speed\": " + value, RegexOptions.CultureInvariant);
            Assert.NotEqual(text, changed);
            Write(rel, changed);
        }

        public (int Code, string Out, string Err) Run(params string[] args)
        {
            var o = new StringWriter();
            var e = new StringWriter();
            int code = Program.Run(args, o, e, Root);
            return (code, o.ToString(), e.ToString());
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (IOException)
            {
            }
        }

        private static void CopyDir(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string f in Directory.GetFiles(from))
            {
                File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
            }
            foreach (string d in Directory.GetDirectories(from))
            {
                CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
            }
        }
    }
}
