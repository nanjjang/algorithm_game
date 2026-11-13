#nullable enable
using System;
using System.IO;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.Unity.Tests.EditMode.Golden
{
    // 시험용 골든 뿌리: 진짜 골든의 일부를 임시 폴더에 옮겨 고치거나 지워 본다(진짜 tests/golden/ 은 건드리지 않는다).
    internal static class TempGolden
    {
        // 항목 폴더들을 <임시>/<id>/ 로 복사한 뿌리를 돌려준다. arithBundles 의 묶음 파일도 <임시>/arith/ 로 복사한다
        public static string CopyEntries(GoldenEntry entry, params string[] arithBundles)
        {
            string root = Path.Combine(Path.GetTempPath(), "wizard-golden-" + Guid.NewGuid().ToString("N"));
            string to = Path.Combine(root, entry.Id.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(entry.Directory))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            }
            if (arithBundles.Length > 0)
            {
                string from = Path.Combine(GoldenPack.GoldenRoot, GoldenCatalog.ArithFolder);
                string arith = Path.Combine(root, GoldenCatalog.ArithFolder);
                Directory.CreateDirectory(arith);
                foreach (string b in arithBundles)
                {
                    File.Copy(Path.Combine(from, b + ".json"), Path.Combine(arith, b + ".json"));
                }
            }
            return root;
        }

        public static void Delete(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
