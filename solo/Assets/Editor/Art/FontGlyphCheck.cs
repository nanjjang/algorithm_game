using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Wizard.Solo.Editor
{
    /// <summary>
    /// TTF cmap 표 읽기(형식 4·12). 글꼴에 어떤 글자가 있는지 Unity 없이 확인한다(00 문서 10절: 특수 글자가 두 TTF에 있는지).
    /// </summary>
    public static class TtfCmap
    {
        public static HashSet<int> ReadCodepoints(byte[] b)
        {
            var cps = new HashSet<int>();
            int numTables = U16(b, 4);
            int cmap = -1;
            for (int i = 0; i < numTables; i++)
            {
                int rec = 12 + 16 * i;
                if (b[rec] == 'c' && b[rec + 1] == 'm' && b[rec + 2] == 'a' && b[rec + 3] == 'p') cmap = (int)U32(b, rec + 8);
            }
            if (cmap < 0) return cps;
            int nt = U16(b, cmap + 2);
            for (int i = 0; i < nt; i++)
            {
                int s = cmap + (int)U32(b, cmap + 4 + 8 * i + 4);
                int fmt = U16(b, s);
                if (fmt == 4) ReadFormat4(b, s, cps);
                else if (fmt == 12) ReadFormat12(b, s, cps);
            }
            return cps;
        }

        static void ReadFormat4(byte[] b, int s, HashSet<int> cps)
        {
            int segX2 = U16(b, s + 6), seg = segX2 / 2;
            int ends = s + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
            for (int k = 0; k < seg; k++)
            {
                int end = U16(b, ends + 2 * k), start = U16(b, starts + 2 * k);
                int delta = (short)U16(b, deltas + 2 * k), ro = U16(b, ranges + 2 * k);
                for (int c = start; c <= end; c++)
                {
                    if (c == 0xFFFF) continue;
                    int g;
                    if (ro == 0) g = (c + delta) & 0xFFFF;
                    else
                    {
                        int at = ranges + 2 * k + ro + 2 * (c - start);
                        if (at + 1 >= b.Length) continue;
                        g = U16(b, at);
                        if (g != 0) g = (g + delta) & 0xFFFF;
                    }
                    if (g != 0) cps.Add(c);
                }
            }
        }

        static void ReadFormat12(byte[] b, int s, HashSet<int> cps)
        {
            long groups = U32(b, s + 12);
            for (long k = 0; k < groups; k++)
            {
                int g0 = s + 16 + (int)(12 * k);
                long sc = U32(b, g0), ec = U32(b, g0 + 4), sg = U32(b, g0 + 8);
                for (long c = sc; c <= ec; c++) if (sg + (c - sc) != 0) cps.Add((int)c);
            }
        }

        static int U16(byte[] b, int o) => (b[o] << 8) | b[o + 1];
        static long U32(byte[] b, int o) => ((long)b[o] << 24) | ((long)b[o + 1] << 16) | ((long)b[o + 2] << 8) | b[o + 3];
    }

    /// <summary>화면이 쓰는 특수 글자가 두 TTF(Galmuri11, neodgm)에 있는지 본다. 메뉴 'Wizard/Check Font Glyphs'.</summary>
    public static class FontGlyphCheck
    {
        /// <summary>▲ ▼ … · ← → ↑ ↓ ◀ ▶ ★ (00 문서 10절 + 작업 지시).</summary>
        public const string SpecialGlyphs = "▲▼…·←→↑↓◀▶★";

        public static readonly string[] FontFiles = { "Assets/Resources/Art/ui/font/Galmuri11.ttf", "Assets/Resources/Art/ui/font/neodgm.ttf" };

        /// <summary>그 TTF에 없는 글자(SpecialGlyphs 순서).</summary>
        public static string Missing(string ttfPath, string glyphs = SpecialGlyphs)
        {
            var cps = TtfCmap.ReadCodepoints(File.ReadAllBytes(ttfPath));
            var sb = new StringBuilder();
            foreach (char c in glyphs) if (!cps.Contains(c)) sb.Append(c);
            return sb.ToString();
        }

        [MenuItem("Wizard/Check Font Glyphs")]
        public static void Run()
        {
            string root = ArtSync.ProjectRoot();
            foreach (var f in FontFiles)
            {
                string p = Path.Combine(root, f);
                if (!File.Exists(p)) { Debug.LogError("[FontGlyphCheck] 글꼴 없음: " + f); continue; }
                string miss = Missing(p);
                if (miss.Length == 0) Debug.Log("[FontGlyphCheck] " + f + ": 특수 글자 전부 있음");
                else Debug.LogWarning("[FontGlyphCheck] " + f + ": 없는 글자 " + Describe(miss) + " (OS 글꼴로 대체된다)");
            }
        }

        public static string Describe(string chars)
        {
            var parts = new List<string>();
            foreach (char c in chars) parts.Add(c + " U+" + ((int)c).ToString("X4"));
            return string.Join(", ", parts);
        }
    }
}
