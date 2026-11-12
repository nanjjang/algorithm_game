using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using Wizard.TestKit.Shared;

namespace Wizard.Protocol.Tests.Crypto.Shared
{
    // tests/fixtures/rfc8439/aead.json 읽기. 형식이 틀리면 InvalidDataException(문제를 모아 한 번에 알린다).
    // 저장소 뿌리는 TestKit.Shared 의 RepoRoot 로 찾는다(dotnet 테스트, Unity EditMode, 머리 없는 플레이어가 같은 길).
    public static class AeadVectorFile
    {
        public const string RelativePath = "tests/fixtures/rfc8439/aead.json";
        public const int Format = 1;

        private static readonly string[] TopFields = { "format", "source", "vectors" };
        private static readonly string[] VectorFields = { "id", "section", "key", "nonce", "aad", "plaintext", "ciphertext", "tag" };

        public static IReadOnlyList<AeadVector> LoadFromRepo(string repoRoot)
        {
            return Load(RepoRoot.Resolve(repoRoot, RelativePath));
        }

        public static IReadOnlyList<AeadVector> Load(string path)
        {
            return Parse(File.ReadAllText(path, Encoding.UTF8), path);
        }

        public static IReadOnlyList<AeadVector> Parse(string json, string where)
        {
            var problems = new List<string>();
            var result = new List<AeadVector>();
            JObject root = JsonFiles.ParseObject(json);
            OnlyFields(root, TopFields, where, problems);
            JToken? format = root["format"];
            if (format == null || format.Type != JTokenType.Integer || (long)format != Format)
            {
                problems.Add(where + ": format must be the integer 1");
            }
            if (!(root["vectors"] is JArray vectors) || vectors.Count == 0)
            {
                problems.Add(where + ": vectors must be a non-empty array");
                throw new InvalidDataException(string.Join("\n", problems));
            }
            for (int i = 0; i < vectors.Count; i++)
            {
                string at = where + ": vectors[" + i + "]";
                if (!(vectors[i] is JObject v))
                {
                    problems.Add(at + " must be an object");
                    continue;
                }
                OnlyFields(v, VectorFields, at, problems);
                string id = Text(v, "id", at, problems);
                string section = Text(v, "section", at, problems);
                byte[] key = Bytes(v, "key", at, problems);
                byte[] nonce = Bytes(v, "nonce", at, problems);
                byte[] aad = Bytes(v, "aad", at, problems);
                byte[] plaintext = Bytes(v, "plaintext", at, problems);
                byte[] ciphertext = Bytes(v, "ciphertext", at, problems);
                byte[] tag = Bytes(v, "tag", at, problems);
                if (key.Length != Wizard.Protocol.Crypto.AeadSizes.KeyBytes || nonce.Length != Wizard.Protocol.Crypto.AeadSizes.NonceBytes ||
                    tag.Length != Wizard.Protocol.Crypto.AeadSizes.TagBytes || ciphertext.Length != plaintext.Length)
                {
                    problems.Add(at + ": key 32, nonce 12, tag 16 bytes and ciphertext length == plaintext length are required");
                }
                result.Add(new AeadVector(id, section, key, nonce, aad, plaintext, ciphertext, tag));
            }
            if (problems.Count > 0)
            {
                throw new InvalidDataException(string.Join("\n", problems));
            }
            return result;
        }

        private static void OnlyFields(JObject obj, string[] allowed, string at, List<string> problems)
        {
            foreach (JProperty p in obj.Properties())
            {
                if (System.Array.IndexOf(allowed, p.Name) < 0)
                {
                    problems.Add(at + ": unknown field '" + p.Name + "'");
                }
            }
        }

        private static string Text(JObject obj, string name, string at, List<string> problems)
        {
            JToken? t = obj[name];
            if (t == null || t.Type != JTokenType.String)
            {
                problems.Add(at + ": '" + name + "' must be a string");
                return "";
            }
            return (string?)t ?? "";
        }

        private static byte[] Bytes(JObject obj, string name, string at, List<string> problems)
        {
            string s = Text(obj, name, at, problems);
            if (!AeadHex.TryParse(s, out byte[] b))
            {
                problems.Add(at + ": '" + name + "' must be lowercase hex with an even length");
            }
            return b;
        }
    }
}
