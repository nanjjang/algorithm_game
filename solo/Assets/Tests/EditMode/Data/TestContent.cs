using System;
using System.IO;
using Wizard.Solo.Data;

namespace Wizard.Solo.Tests
{
    /// <summary>
    /// 실제 JSON(Assets/Resources/Data)을 읽은 콘텐츠와 이름 표. 다른 모듈의 EditMode 테스트가 쓴다(00 문서 12절).
    /// 처음 쓸 때 한 번 읽는다(지연 로드). 에디터 테스트라 Application.dataPath 아래를 File IO로 읽는다.
    /// 이름 조각끼리 열쇠가 겹쳐도 다른 모듈 테스트가 깨지지 않게 이름 표의 오류는 모아 두기만 한다
    /// (겹침 자체는 Data 테스트 S00_NamesSolo__NoDuplicateKeysAcrossFragments가 잡는다). 콘텐츠의 읽기 오류도 같다
    /// (S00_Load__AllFilesWithoutErrors가 잡는다).
    /// </summary>
    public static class TestContent
    {
        static IContentDb _db;
        static INameTable _names;

        /// <summary>…/Assets 폴더. Unity 밖(도구로 돌릴 때)은 환경 변수 SOLO_ASSETS_DIR.</summary>
        public static string AssetsDir
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("SOLO_ASSETS_DIR");
                if (!string.IsNullOrEmpty(env)) return env;
                try { return UnityEngine.Application.dataPath; }
                catch (Exception) { return Path.Combine(Directory.GetCurrentDirectory(), "Assets"); }
            }
        }

        public static string DataDir => Path.Combine(AssetsDir, "Resources", ContentDb.ResourcesDataRoot);
        public static string SoloExtraDir => Path.Combine(AssetsDir, "Resources", ContentDb.ResourcesSoloExtraRoot);
        public static string NamesMainFile => Path.Combine(DataDir, "names", "names.ko.json");
        public static string NamesFragmentsDir => Path.Combine(AssetsDir, "Resources", NameTable.ResourcesFragmentsFolder);

        public static IContentDb Db
        {
            get
            {
                if (_db == null) _db = ContentDb.LoadFromDirectory(DataDir, SoloExtraDir, msg => { });
                return _db;
            }
            set { _db = value; }
        }

        public static INameTable Names
        {
            get
            {
                if (_names == null) _names = NameTable.LoadFromDirectory(NamesMainFile, NamesFragmentsDir, msg => { });
                return _names;
            }
            set { _names = value; }
        }
    }
}
