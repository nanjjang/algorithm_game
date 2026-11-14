using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Wizard.Solo.View.World
{
    /// <summary>맵 배치 하나에 놓인 그림(칸 좌표, 그림 id, 좌우 반전, 차지하는 칸).</summary>
    public struct MapPiece
    {
        public Vector2Int Tile;
        public string Sprite;
        public bool MirrorX;
        public Vector2Int Extent;
        /// <summary>등장구의 변(n/s/w/e). 그 밖은 null.</summary>
        public string Side;
    }

    /// <summary>
    /// world/maps/field.0N.json(09 7.4절): 바닥 타일 [행][열], 가장자리 벽, 등장구 12곳, 판정 없는 소품.
    /// 모닥불 자리(campfireCenter)는 읽기만 하고 쓰지 않는다 — 모닥불 자리는 RunState 값이 정본이다(00 5.5절).
    /// </summary>
    public sealed class ArenaMap
    {
        public string Id;
        public int Width, Height;
        /// <summary>[y][x] 타일 id(시뮬레이션 좌표와 같은 방향: y = 0이 위쪽 줄).</summary>
        public string[][] Ground;
        public readonly List<MapPiece> Walls = new List<MapPiece>();
        public readonly List<MapPiece> Spawns = new List<MapPiece>();
        public readonly List<MapPiece> Props = new List<MapPiece>();

        public static ArenaMap Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            JObject j;
            try { j = JObject.Parse(json); }
            catch (Newtonsoft.Json.JsonException) { return null; }
            var m = new ArenaMap { Id = (string)j["id"] };
            if (j["sizeTiles"] is JArray size && size.Count >= 2)
            {
                m.Width = (int)size[0];
                m.Height = (int)size[1];
            }
            if (j["ground"] is JArray rows)
            {
                if (m.Height <= 0) m.Height = rows.Count;
                m.Ground = new string[rows.Count][];
                for (int y = 0; y < rows.Count; y++)
                {
                    var row = rows[y] as JArray;
                    m.Ground[y] = new string[row != null ? row.Count : 0];
                    if (row == null) continue;
                    if (m.Width <= 0) m.Width = row.Count;
                    for (int x = 0; x < row.Count; x++) m.Ground[y][x] = row[x].Type == JTokenType.String ? (string)row[x] : null;
                }
            }
            Pieces(j["walls"], m.Walls);
            Pieces(j["spawns"], m.Spawns);
            Pieces(j["props"], m.Props);
            return m;
        }

        static void Pieces(JToken t, List<MapPiece> into)
        {
            if (!(t is JArray a)) return;
            for (int i = 0; i < a.Count; i++)
            {
                if (!(a[i] is JObject o)) continue;
                var p = new MapPiece { Extent = new Vector2Int(1, 1) };
                if (o["tile"] is JArray tile && tile.Count >= 2) p.Tile = new Vector2Int((int)tile[0], (int)tile[1]);
                p.Sprite = o["sprite"] != null ? (string)o["sprite"] : null;
                p.MirrorX = o["mirrorX"] != null && o["mirrorX"].Type == JTokenType.Boolean && (bool)o["mirrorX"];
                if (o["tileExtent"] is JArray ext && ext.Count >= 2) p.Extent = new Vector2Int((int)ext[0], (int)ext[1]);
                p.Side = o["side"] != null ? (string)o["side"] : null;
                into.Add(p);
            }
        }

        /// <summary>등장구의 가운데(시뮬레이션 좌표) — 등장 예고 자리에서 가장 가까운 덤불을 고를 때 쓴다.</summary>
        public static Vector2 SpawnCenter(in MapPiece p) => new Vector2(p.Tile.x + p.Extent.x * 0.5f, p.Tile.y + p.Extent.y * 0.5f);

        /// <summary>가장 가까운 등장구 번호(없으면 −1).</summary>
        public int NearestSpawn(Vector2 simPos)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < Spawns.Count; i++)
            {
                float d = (SpawnCenter(Spawns[i]) - simPos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }
}
