using System;
using System.Security.Cryptography;
using System.Text;

namespace Wizard.MetaGen
{
    // GUID = MD5(저장소 기준 상대 경로). CONTRACTS 11.5, T-34.
    // 입력은 .meta 를 뗀 대상 경로의 UTF-8 바이트다. 구분자 '/', 앞에 '/' 없음, 대소문자 그대로.
    // 출력은 소문자 hex 32자다.
    public static class MetaGuid
    {
        public static string ForPath(string repoRelativePath)
        {
            string path = RepoPath.Normalize(repoRelativePath);
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(path));
            return Convert.ToHexStringLower(hash);
        }

        // Unity 의 GUID 는 소문자 hex 32자다. 대문자나 다른 길이는 받지 않는다.
        public static bool IsWellFormed(string guid)
        {
            if (guid.Length != 32)
            {
                return false;
            }
            foreach (char c in guid)
            {
                bool digit = c >= '0' && c <= '9';
                bool lowerHex = c >= 'a' && c <= 'f';
                if (!digit && !lowerHex)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
