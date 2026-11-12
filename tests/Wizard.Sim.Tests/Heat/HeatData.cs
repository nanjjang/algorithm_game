using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json.Nodes;
using Wizard.Content;
using Wizard.DataBuild.Core;
using Wizard.Sim.Tests.World;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Sim.Tests.Heat
{
    // 과열 시험의 팩. 저장소 data/ 의 팩(기준값 캐릭터, 블록 2종)과, 기준값 캐릭터의 과열 수치만 spec 01 7.1·7.8 표의
    // 다섯 캐릭터 값으로 바꾼 팩. 다섯 캐릭터의 데이터 줄은 아직 팩에 없다(M5-DAT-01). 그래서 data/ 를 임시 폴더에 복사하고
    // characters.json 의 baseline 세 칸(임계 시작, 냉각, 잠금)을 명세 값으로 바꾼 뒤 같은 DataBuild 파이프라인으로 만든다.
    // 세계는 이 팩을 평소처럼 읽고 돈다(시험이 세계의 수치를 덮어쓰지 않는다).
    internal static class HeatData
    {
        public static ContentDb Repo => WorldData.RepoDb;

        // spec 01 7.8 표: (임계 시작, 냉각/초, 잠금 초)
        public static ContentDb Baseline => Repo;
        public static ContentDb Dalgu => Variant("80", "45", "3.0");      // 달구: 임계 80(지팡이 특성, 15절 C17)
        public static ContentDb Chorong => Variant("85", "50", "2.5");    // 초롱: 냉각 50, 잠금 2.5초
        public static ContentDb Galpi => Variant("85", "45", "3.5");      // 갈피: 잠금 3.5초
        public static ContentDb Seonge => Variant("90", "45", "3.0");     // 성에: 임계 90

        private static readonly ConcurrentDictionary<string, Lazy<ContentDb>> Variants =
            new ConcurrentDictionary<string, Lazy<ContentDb>>();

        public static ContentDb Variant(string critStart, string coolPerSecond, string lockSeconds)
        {
            string key = critStart + "/" + coolPerSecond + "/" + lockSeconds;
            return Variants.GetOrAdd(key, _ => new Lazy<ContentDb>(() => Build(critStart, coolPerSecond, lockSeconds))).Value;
        }

        private static ContentDb Build(string critStart, string coolPerSecond, string lockSeconds)
        {
            string root = Path.Combine(Path.GetTempPath(), "wizard-heat-" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyDir(Path.Combine(TestRepo.Root, "data"), Path.Combine(root, "data"));
                string file = Path.Combine(root, "data", "sim", "characters.json");
                JsonNode doc = JsonNode.Parse(File.ReadAllText(file))!;
                JsonObject b = doc["baseline"]!.AsObject();
                b["crit_start"] = JsonNode.Parse(critStart);
                b["cool_per_second"] = JsonNode.Parse(coolPerSecond);
                b["lock_seconds"] = JsonNode.Parse(lockSeconds);
                File.WriteAllText(file, doc.ToJsonString());

                var d = new Diagnostics();
                Pipeline.Output o = Pipeline.Run(root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
                Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
                return ContentDb.Build(ContentPack.Load(o.Pack!));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
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
