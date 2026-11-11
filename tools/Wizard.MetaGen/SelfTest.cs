using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wizard.MetaGen
{
    // metagen --self-test: 도구 자체 테스트. 임시 폴더에 작은 저장소 꼴을 만들어 돌린다(저장소는 건드리지 않는다).
    // 이 도구의 테스트 프로젝트를 따로 두지 않는 이유: 이 작업의 소유 경로는 tools/Wizard.MetaGen/ 뿐이고,
    // 솔루션과 프로젝트 목록은 M0-BT-01 에서 고정되었다(tools/ 프로젝트 13개, layout-check).
    //
    // 기대값의 출처(구현 출력에서 베끼지 않았다):
    // - GUID: macOS 의 `md5 -s '<경로>'` 출력(CONTRACTS 11.5 의 예시 경로 포함).
    // - .meta 바이트: 이 기기의 Unity 6000.3.20f1 을 배치 모드로 열어, .meta 가 없는 같은 종류의 자산에 Unity 가 쓴 바이트.
    public static class SelfTest
    {
        private delegate void TestBody(string scratch);

        private sealed class TestFailure : Exception
        {
            public TestFailure(string message) : base(message)
            {
            }
        }

        // Unity 6000.3.20f1 이 쓴 꼴(GUID 자리만 이 경로의 MD5). FolderPkg 는 패키지 뿌리라 만들면 안 되는 .meta 의 예로만 쓴다.
        private const string FolderPkg =
            "fileFormatVersion: 2\nguid: fac16958f1b97256fbe3be9f6a08b9d9\nfolderAsset: yes\nDefaultImporter:\n" +
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        private const string FolderPkgSub =
            "fileFormatVersion: 2\nguid: e31d7738fdc1e8763564de34521b1762\nfolderAsset: yes\nDefaultImporter:\n" +
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        private const string FolderGame =
            "fileFormatVersion: 2\nguid: 1dff9838605bae83cf091a48023dbbd0\nfolderAsset: yes\nDefaultImporter:\n" +
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        private const string FolderGameApp =
            "fileFormatVersion: 2\nguid: a4b70e97e202850457d6b211616d181e\nfolderAsset: yes\nDefaultImporter:\n" +
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        private const string PackageManifestPkg =
            "fileFormatVersion: 2\nguid: e4e36921b836a7a299a96a00430a6936\nPackageManifestImporter:\n" +
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        private const string AsmdefPkg =
            "fileFormatVersion: 2\nguid: d825effab643f9f7ffd64a8322f19879\nAssemblyDefinitionImporter:\n" +
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        // MonoImporter·PluginImporter 의 기본 설정은 Unity 6 이 블록 없이 두 줄로, 끝 줄바꿈 없이 쓴다.
        private const string MonoPkgA = "fileFormatVersion: 2\nguid: eedc62f8c8b6d120aeb089858845deac";

        private const string MonoGameBoot = "fileFormatVersion: 2\nguid: f489012cc1653af3a94be5dfae346dbf";

        private const string PluginPkgLib = "fileFormatVersion: 2\nguid: 517ef64fd98f215dac885e2984e18600";

        private const string TextPkgNotes =
            "fileFormatVersion: 2\nguid: 185ca1957b5264a5bb638eb73bb8d8aa\nTextScriptImporter:\n" +
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        private const string DefaultPkgRsp =
            "fileFormatVersion: 2\nguid: e4da54e6a68ba44bc5f3a68e33a72e90\nDefaultImporter:\n" +
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        private const string TextPkgTxt =
            "fileFormatVersion: 2\nguid: 8b14c555e41d6f267f2dd28c09c2339a\nTextScriptImporter:\n" +
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        // 모르는 확장자: Unity 6000.3.20f1 은 DefaultImporter 를 쓴다(.foo 로 확인).
        private const string DefaultPkgUnknown =
            "fileFormatVersion: 2\nguid: 0748a8253ed3d7c9f553e10b7c26bfa3\nDefaultImporter:\n" +
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        // 전용 가져오기가 있는 그림: GUID 만 쓰고 블록은 Unity 가 채운다.
        private const string GuidOnlyPkgPng = "fileFormatVersion: 2\nguid: 8a13a4b7efed08b7a390ed6e061e4d9e";

        // Unity 가 무작위로 만든 GUID 의 예(uproj1 에서 Unity 가 A.cs 에 준 값).
        private const string UnityRandomCsMeta = "fileFormatVersion: 2\nguid: 525719970cf5f4702a02ea86b376e490";

        private const string UnityNativeAssetMeta =
            "fileFormatVersion: 2\nguid: 681886c5eb7344803b6206f758bf0b1c\nNativeFormatImporter:\n" +
            "  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        public static int Run(TextWriter stdout, TextWriter stderr)
        {
            var tests = new List<KeyValuePair<string, TestBody>>
            {
                Case("guid_is_md5_of_repo_relative_path", GuidIsMd5OfRepoRelativePath),
                Case("generate_writes_unity_bytes_per_importer", GenerateWritesUnityBytesPerImporter),
                Case("generate_skips_hidden_and_scope_roots", GenerateSkipsHiddenAndScopeRoots),
                Case("generate_same_bytes_in_another_tree", GenerateSameBytesInAnotherTree),
                Case("generate_is_idempotent", GenerateIsIdempotent),
                Case("generate_keeps_existing_meta", GenerateKeepsExistingMeta),
                Case("package_json_outside_package_root_is_text", PackageJsonOutsidePackageRootIsText),
                Case("check_clean_tree_passes", CheckCleanTreePasses),
                Case("check_finds_missing_meta", CheckFindsMissingMeta),
                Case("check_finds_orphan_meta", CheckFindsOrphanMeta),
                Case("check_finds_duplicate_guid", CheckFindsDuplicateGuid),
                Case("check_finds_guid_mismatch_in_src_and_game", CheckFindsGuidMismatchInSrcAndGame),
                Case("check_allows_unity_guid_outside_strict_scopes", CheckAllowsUnityGuidOutsideStrictScopes),
                Case("check_finds_bad_meta", CheckFindsBadMeta),
                Case("usage_errors_exit_2", UsageErrorsExit2),
                Case("repo_root_found_from_tool_location", RepoRootFoundFromToolLocation),
            };

            int failed = 0;
            foreach (KeyValuePair<string, TestBody> test in tests)
            {
                DirectoryInfo scratch = Directory.CreateTempSubdirectory("wizard-metagen-selftest-");
                try
                {
                    test.Value(scratch.FullName);
                    stdout.WriteLine("PASS  " + test.Key);
                }
                catch (Exception e)
                {
                    failed++;
                    stderr.WriteLine("FAIL  " + test.Key + ": " + e.Message);
                }
                finally
                {
                    scratch.Delete(true);
                }
            }
            stdout.WriteLine("metagen --self-test: " + (tests.Count - failed) + " passed, " + failed + " failed");
            return failed;
        }

        private static KeyValuePair<string, TestBody> Case(string name, TestBody body)
        {
            return new KeyValuePair<string, TestBody>(name, body);
        }

        // ---- 테스트 ----

        private static void GuidIsMd5OfRepoRelativePath(string scratch)
        {
            // CONTRACTS 11.5 의 예시 경로
            Equal("49b6bd5746177e4f98e16edd47f0dca7", MetaGuid.ForPath("src/Wizard.Sim/Heat/HeatState.cs"), "contract example");
            Equal("840c018ee8a182720b078e89f75aeb03", MetaGuid.ForPath("src/Wizard.Core"), "folder path");
            Equal("b843b1c74ebd8943fa596c7ba1b5541d", MetaGuid.ForPath("src/Wizard.Core/package.json"), "package.json");
            // 구분자는 '/', 앞에 '/' 없음
            Equal("49b6bd5746177e4f98e16edd47f0dca7", MetaGuid.ForPath("src\\Wizard.Sim\\Heat\\HeatState.cs"), "backslash separators");
            Equal("49b6bd5746177e4f98e16edd47f0dca7", MetaGuid.ForPath("/src/Wizard.Sim/Heat/HeatState.cs"), "leading slash");
            // 대소문자는 그대로
            NotEqual(MetaGuid.ForPath("src/Wizard.Core"), MetaGuid.ForPath("src/wizard.core"), "case must matter");
            // NFD 로 들어온 이름도 NFC 바이트로 해시한다: "src/가.cs" = 73 72 63 2f ea b0 80 2e 63 73
            string nfd = "src/" + "가" + ".cs";
            Equal("4d2df33eb6dbbbcbd067d8d3283fcb23", MetaGuid.ForPath(nfd), "NFD path hashes as NFC");
            True(MetaGuid.IsWellFormed("4d2df33eb6dbbbcbd067d8d3283fcb23"), "lowercase hex is well formed");
            True(!MetaGuid.IsWellFormed("4D2DF33EB6DBBBCBD067D8D3283FCB23"), "uppercase hex is not well formed");
        }

        private static void GenerateWritesUnityBytesPerImporter(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            ExpectExit(0, root, "generate");
            ExpectFile(root, "src/Pkg/Sub.meta", FolderPkgSub);
            ExpectFile(root, "src/Pkg/package.json.meta", PackageManifestPkg);
            ExpectFile(root, "src/Pkg/Pkg.asmdef.meta", AsmdefPkg);
            ExpectFile(root, "src/Pkg/A.cs.meta", MonoPkgA);
            ExpectFile(root, "src/Pkg/Lib.dll.meta", PluginPkgLib);
            ExpectFile(root, "src/Pkg/notes.md.meta", TextPkgNotes);
            ExpectFile(root, "src/Pkg/csc.rsp.meta", DefaultPkgRsp);
            ExpectFile(root, "src/Pkg/c.txt.meta", TextPkgTxt);
            ExpectFile(root, "src/Pkg/n.foo.meta", DefaultPkgUnknown);
            ExpectFile(root, "src/Pkg/p.png.meta", GuidOnlyPkgPng);
            ExpectFile(root, "unity/Assets/Game.meta", FolderGame);
            ExpectFile(root, "unity/Assets/Game/App.meta", FolderGameApp);
            ExpectFile(root, "unity/Assets/Game/App/Boot.cs.meta", MonoGameBoot);
        }

        private static void GenerateSkipsHiddenAndScopeRoots(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            ExpectExit(0, root, "generate");
            string[] mustNotExist =
            {
                "src.meta", "unity/Assets.meta",
                // 패키지 뿌리 폴더와 패키지 밖의 파일은 Unity 가 보지 않는다(Unity 도 만들지 않는다)
                "src/Pkg.meta", "src/README.md.meta",
                "src/Pkg/.DS_Store.meta", "src/Pkg/back~.meta", "src/Pkg/f.tmp.meta",
                "src/Pkg/cvs.meta", "src/Pkg/cvs/x.cs.meta", "src/Pkg/.git.meta", "src/Pkg/.git/config.meta",
                "unity/Assets/Hid~.meta", "unity/Assets/Hid~/x.cs.meta",
            };
            foreach (string path in mustNotExist)
            {
                True(!File.Exists(Path.Combine(root, path)), path + " must not be created (Unity ignores it)");
            }
            // 숨은 폴더 이름 cvs 는 폴더에만 해당한다. 파일 이름 cvs 는 자산이다.
            True(File.Exists(Path.Combine(root, "src/Pkg/Sub/cvs.meta")), "a file named cvs is an asset");
        }

        private static void GenerateSameBytesInAnotherTree(string scratch)
        {
            string first = MakeFixture(scratch, "worktree-a");
            string second = MakeFixture(scratch, "worktree-b");
            ExpectExit(0, first, "generate");
            ExpectExit(0, second, "generate");
            List<string> a = ListMetas(first);
            List<string> b = ListMetas(second);
            Equal(string.Join("\n", a), string.Join("\n", b), "same .meta file set");
            foreach (string path in a)
            {
                True(BytesEqual(Path.Combine(first, path), Path.Combine(second, path)), path + " differs between trees");
            }
            True(a.Count >= 12, "fixture should produce at least 12 .meta files (got " + a.Count + ")");
        }

        private static void GenerateIsIdempotent(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            ExpectExit(0, root, "generate");
            Dictionary<string, byte[]> before = Snapshot(root);
            var output = new StringWriter();
            int code = Program.Run(new[] { "--root", root }, output, new StringWriter(), scratch);
            Equal("0", code.ToString(), "second run exit code");
            True(output.ToString().Contains("metagen: created 0,", StringComparison.Ordinal), "second run creates nothing: " + output);
            Dictionary<string, byte[]> after = Snapshot(root);
            SameSnapshot(before, after);
        }

        private static void GenerateKeepsExistingMeta(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            // Unity 가 먼저 만든 .meta(무작위 GUID, 다른 꼴)는 src/ 안이어도 덮어쓰지 않는다.
            Write(root, "src/Pkg/A.cs.meta", UnityRandomCsMeta);
            Write(root, "unity/Assets/Settings/X.asset", "%YAML 1.1\n");
            Write(root, "unity/Assets/Settings/X.asset.meta", UnityNativeAssetMeta);
            ExpectExit(0, root, "generate");
            ExpectFile(root, "src/Pkg/A.cs.meta", UnityRandomCsMeta);
            ExpectFile(root, "unity/Assets/Settings/X.asset.meta", UnityNativeAssetMeta);
            True(File.Exists(Path.Combine(root, "unity/Assets/Settings.meta")), "new folder still gets a .meta");
        }

        private static void PackageJsonOutsidePackageRootIsText(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            Write(root, "unity/Assets/Game/package.json", "{}\n");
            Write(root, "src/Pkg/Sub/package.json", "{}\n");
            ExpectExit(0, root, "generate");
            string game = Read(root, "unity/Assets/Game/package.json.meta");
            string nested = Read(root, "src/Pkg/Sub/package.json.meta");
            True(game.Contains("\nTextScriptImporter:\n", StringComparison.Ordinal), "unity/Assets package.json is a text asset");
            True(nested.Contains("\nTextScriptImporter:\n", StringComparison.Ordinal), "nested package.json is a text asset");
        }

        private static void CheckCleanTreePasses(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            ExpectExit(0, root, "generate");
            ExpectExit(0, root, "--check");
        }

        private static void CheckFindsMissingMeta(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            ExpectExit(0, root, "generate");
            File.Delete(Path.Combine(root, "src/Pkg/A.cs.meta"));
            File.Delete(Path.Combine(root, "unity/Assets/Game/App.meta"));
            string err = ExpectExit(1, root, "--check");
            Has(err, "missing-meta: src/Pkg/A.cs ");
            Has(err, "missing-meta: unity/Assets/Game/App ");
        }

        private static void CheckFindsOrphanMeta(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            ExpectExit(0, root, "generate");
            Write(root, "src/Pkg/Gone.cs.meta", "fileFormatVersion: 2\nguid: " + MetaGuid.ForPath("src/Pkg/Gone.cs"));
            Write(root, "src/Pkg/back~.meta", "fileFormatVersion: 2\nguid: " + MetaGuid.ForPath("src/Pkg/back~"));
            Write(root, "unity/Assets/Old.meta", UnityNativeAssetMeta);
            // 패키지 뿌리의 .meta 는 Unity 가 쓰지 않는 파일이다(Unity 가 만들었을 리 없고 누가 손으로 둔 것)
            Write(root, "src/Pkg.meta", FolderPkg);
            string err = ExpectExit(1, root, "--check");
            Has(err, "orphan-meta: src/Pkg.meta ");
            Has(err, "orphan-meta: src/Pkg/Gone.cs.meta ");
            Has(err, "orphan-meta: src/Pkg/back~.meta ");
            Has(err, "orphan-meta: unity/Assets/Old.meta ");
        }

        private static void CheckFindsDuplicateGuid(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            Write(root, "unity/Assets/Settings/One.asset", "%YAML 1.1\n");
            Write(root, "unity/Assets/Settings/Two.asset", "%YAML 1.1\n");
            Write(root, "unity/Assets/Settings/One.asset.meta", UnityNativeAssetMeta);
            Write(root, "unity/Assets/Settings/Two.asset.meta", UnityNativeAssetMeta);
            ExpectExit(0, root, "generate");
            string err = ExpectExit(1, root, "--check");
            Has(err, "duplicate-guid: unity/Assets/Settings/One.asset.meta (guid 681886c5eb7344803b6206f758bf0b1c also in unity/Assets/Settings/Two.asset.meta)");
            Has(err, "duplicate-guid: unity/Assets/Settings/Two.asset.meta (guid 681886c5eb7344803b6206f758bf0b1c also in unity/Assets/Settings/One.asset.meta)");
        }

        private static void CheckFindsGuidMismatchInSrcAndGame(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            Write(root, "src/Pkg/A.cs.meta", UnityRandomCsMeta);
            Write(root, "unity/Assets/Game/App/Boot.cs.meta", "fileFormatVersion: 2\nguid: 734168833c3084fd6807cc370c0e2f9f");
            ExpectExit(0, root, "generate");
            string err = ExpectExit(1, root, "--check");
            Has(err, "guid-mismatch: src/Pkg/A.cs.meta (guid 525719970cf5f4702a02ea86b376e490 != md5(\"src/Pkg/A.cs\") eedc62f8c8b6d120aeb089858845deac");
            Has(err, "guid-mismatch: unity/Assets/Game/App/Boot.cs.meta (guid 734168833c3084fd6807cc370c0e2f9f != md5(\"unity/Assets/Game/App/Boot.cs\") f489012cc1653af3a94be5dfae346dbf");
        }

        private static void CheckAllowsUnityGuidOutsideStrictScopes(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            // unity/Assets/ 의 Game/ 밖(Unity 프로젝트 틀이 만든 설정 자산)은 이미 있는 GUID 를 그대로 둔다(T-34).
            Write(root, "unity/Assets/Settings/X.asset", "%YAML 1.1\n");
            Write(root, "unity/Assets/Settings/X.asset.meta", UnityNativeAssetMeta);
            ExpectExit(0, root, "generate");
            ExpectExit(0, root, "--check");
        }

        private static void CheckFindsBadMeta(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            ExpectExit(0, root, "generate");
            Write(root, "src/Pkg/A.cs.meta", "fileFormatVersion: 2\n");
            Write(root, "src/Pkg/notes.md.meta", "fileFormatVersion: 2\nguid: XYZ\n");
            string err = ExpectExit(1, root, "--check");
            Has(err, "bad-meta: src/Pkg/A.cs.meta ");
            Has(err, "bad-meta: src/Pkg/notes.md.meta ");
        }

        private static void UsageErrorsExit2(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            ExpectExit(2, root, "--bogus");
            Directory.Delete(Path.Combine(root, "unity"), true);
            string err = ExpectExit(2, root, "--check");
            Has(err, "unity/Assets/ does not exist");
            int code = Program.Run(new[] { "--root" }, new StringWriter(), new StringWriter(), scratch);
            Equal("2", code.ToString(), "--root without a value");
            // 뿌리를 못 찾으면(도구가 저장소 밖에 있으면) 사용법 오류로 끝난다.
            code = Program.Run(new[] { "--check" }, new StringWriter(), new StringWriter(), scratch);
            Equal("2", code.ToString(), "no repo root above the tool");
        }

        private static void RepoRootFoundFromToolLocation(string scratch)
        {
            string root = MakeFixture(scratch, "repo");
            Write(root, "Wizard.sln", "\n");
            Write(root, "global.json", "{}\n");
            string toolDir = Path.Combine(root, "artifacts", "bin", "Wizard.MetaGen", "debug");
            Directory.CreateDirectory(toolDir);
            Equal(Path.GetFullPath(root), RepoPath.FindRepoRoot(toolDir) ?? "(null)", "root above the tool");
            var stdout = new StringWriter();
            int code = Program.Run(Array.Empty<string>(), stdout, new StringWriter(), toolDir);
            Equal("0", code.ToString(), "generate via tool location");
            True(File.Exists(Path.Combine(root, "src/Pkg/A.cs.meta")), "generated under the found root");
        }

        // ---- 고정물 ----

        // 작은 저장소 꼴: src/ 의 UPM 패키지 하나, unity/Assets/Game, Unity 가 숨기는 이름들.
        private static string MakeFixture(string scratch, string name)
        {
            string root = Path.Combine(scratch, name);
            Write(root, "src/README.md", "# not inside a package\n");
            Write(root, "src/Pkg/package.json", "{\n  \"name\": \"com.wizard.pkg\",\n  \"version\": \"0.0.0\"\n}\n");
            Write(root, "src/Pkg/Pkg.asmdef", "{\n    \"name\": \"Pkg\",\n    \"noEngineReferences\": true\n}\n");
            Write(root, "src/Pkg/A.cs", "namespace Pkg { public static class A { } }\n");
            Write(root, "src/Pkg/Lib.dll", "MZ");
            Write(root, "src/Pkg/notes.md", "# notes\n");
            Write(root, "src/Pkg/csc.rsp", "-nullable:enable\n");
            Write(root, "src/Pkg/c.txt", "text\n");
            Write(root, "src/Pkg/n.foo", "unknown\n");
            Write(root, "src/Pkg/p.png", "PNG");
            Write(root, "src/Pkg/Sub/cvs", "file named cvs\n");
            Write(root, "src/Pkg/.DS_Store", "x");
            Write(root, "src/Pkg/back~", "x");
            Write(root, "src/Pkg/f.tmp", "x");
            Write(root, "src/Pkg/cvs/x.cs", "x");
            Write(root, "src/Pkg/.git/config", "x");
            Write(root, "unity/Assets/Game/App/Boot.cs", "namespace Game { }\n");
            Write(root, "unity/Assets/Hid~/x.cs", "x");
            return root;
        }

        // ---- 도우미 ----

        private static string ExpectExit(int expected, string root, string mode)
        {
            string[] args = mode == "generate" ? new[] { "--root", root } : new[] { mode, "--root", root };
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int code = Program.Run(args, stdout, stderr, root);
            if (code != expected)
            {
                throw new TestFailure("metagen " + mode + " exited " + code + ", expected " + expected + "; stderr: " + stderr.ToString().Trim());
            }
            return stderr.ToString();
        }

        private static void Write(string root, string relative, string content)
        {
            string full = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, new UTF8Encoding(false).GetBytes(content));
        }

        private static string Read(string root, string relative)
        {
            return Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root, relative)));
        }

        private static void ExpectFile(string root, string relative, string expected)
        {
            string full = Path.Combine(root, relative);
            if (!File.Exists(full))
            {
                throw new TestFailure(relative + " was not created");
            }
            string actual = Read(root, relative);
            if (actual != expected)
            {
                throw new TestFailure(relative + " bytes differ from Unity's format:\n--- expected\n" + Visible(expected) + "\n--- actual\n" + Visible(actual));
            }
        }

        private static string Visible(string s)
        {
            return s.Replace(" \n", "·$\n", StringComparison.Ordinal).Replace("\n", "$\n", StringComparison.Ordinal);
        }

        private static List<string> ListMetas(string root)
        {
            var list = new List<string>();
            foreach (string path in Directory.EnumerateFiles(root, "*.meta", SearchOption.AllDirectories))
            {
                list.Add(RepoPath.Relative(root, path));
            }
            list.Sort(string.CompareOrdinal);
            return list;
        }

        private static Dictionary<string, byte[]> Snapshot(string root)
        {
            var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                map[RepoPath.Relative(root, path)] = File.ReadAllBytes(path);
            }
            return map;
        }

        private static void SameSnapshot(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
        {
            Equal(before.Count.ToString(), after.Count.ToString(), "file count");
            foreach (KeyValuePair<string, byte[]> pair in before)
            {
                True(after.TryGetValue(pair.Key, out byte[]? now) && now.AsSpan().SequenceEqual(pair.Value), pair.Key + " changed");
            }
        }

        private static bool BytesEqual(string a, string b)
        {
            return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
        }

        private static void Has(string text, string needle)
        {
            if (!text.Contains(needle, StringComparison.Ordinal))
            {
                throw new TestFailure("expected output to contain [" + needle + "] but got:\n" + text.Trim());
            }
        }

        private static void Equal(string expected, string actual, string what)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new TestFailure(what + ": expected [" + expected + "] got [" + actual + "]");
            }
        }

        private static void NotEqual(string a, string b, string what)
        {
            if (string.Equals(a, b, StringComparison.Ordinal))
            {
                throw new TestFailure(what + ": both [" + a + "]");
            }
        }

        private static void True(bool condition, string what)
        {
            if (!condition)
            {
                throw new TestFailure(what);
            }
        }
    }
}
