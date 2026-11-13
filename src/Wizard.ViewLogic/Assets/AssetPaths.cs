using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Assets
{
    // art/production 기준 상대 경로 다루기. 매니페스트의 경로는 자기 폴더 기준이고("kit/…", "../backgrounds/…"),
    // 레지스트리는 art/production 기준으로 고쳐 든다. 뿌리 밖으로 나가는 경로와 절대 경로는 받지 않는다
    public static class AssetPaths
    {
        public const char Separator = '/';

        // dir("ui") + rel("../backgrounds/title.png") → "backgrounds/title.png". 뿌리 밖이면 false
        public static bool TryCombine(string dir, string rel, out string path)
        {
            path = "";
            if (rel.Length == 0 || rel[0] == Separator || rel.IndexOf('\\') >= 0 || rel.IndexOf(':') >= 0)
            {
                return false;
            }
            var parts = new List<string>();
            if (dir.Length > 0)
            {
                foreach (string d in dir.Split(Separator))
                {
                    if (d.Length > 0)
                    {
                        parts.Add(d);
                    }
                }
            }
            foreach (string seg in rel.Split(Separator))
            {
                if (seg.Length == 0 || seg == ".")
                {
                    continue;
                }
                if (seg == "..")
                {
                    if (parts.Count == 0)
                    {
                        return false;
                    }
                    parts.RemoveAt(parts.Count - 1);
                    continue;
                }
                parts.Add(seg);
            }
            if (parts.Count == 0)
            {
                return false;
            }
            path = string.Join(Separator.ToString(), parts);
            return true;
        }

        // "ui/kit/k-20.png" → "k-20.png"
        public static string FileName(string path)
        {
            int slash = path.LastIndexOf(Separator);
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        // "k-20_button.png" → "k-20_button"
        public static string WithoutExtension(string fileName)
        {
            int dot = fileName.LastIndexOf('.');
            return dot <= 0 ? fileName : fileName.Substring(0, dot);
        }

        public static bool IsSafeRelative(string path) => TryCombine("", path, out string normalized) && string.Equals(normalized, path, StringComparison.Ordinal);
    }
}
