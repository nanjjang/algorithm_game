using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wizard.MetaGen
{
    // 빠진 .meta 를 만든다(T-34). 이미 있는 .meta 는 열지도 덮어쓰지도 않는다(Unity 가 만든 것도 그대로 둔다).
    public static class MetaGenerator
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        // 만든 .meta 의 저장소 기준 상대 경로를 오름차순으로 돌려준다.
        public static List<string> Generate(string root, AssetTree tree)
        {
            var existing = new HashSet<string>(tree.Metas, StringComparer.Ordinal);
            var created = new List<string>();
            foreach (AssetEntry entry in tree.Assets)
            {
                string metaRelative = entry.Path + AssetTree.MetaSuffix;
                if (existing.Contains(metaRelative))
                {
                    continue;
                }
                string metaFull = Path.Combine(root, metaRelative);
                byte[] bytes = Utf8NoBom.GetBytes(MetaTemplates.RenderFor(entry));
                // CreateNew: 그 사이에 누가 만들었거나 대소문자만 다른 이름이 있으면 덮어쓰지 않고 실패한다.
                using (var stream = new FileStream(metaFull, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
                created.Add(metaRelative);
            }
            return created;
        }
    }
}
