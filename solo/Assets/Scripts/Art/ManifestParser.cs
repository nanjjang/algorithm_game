using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Art
{
    /// <summary>
    /// 원본 manifest JSON 글 → 모델(엔진 텍스처 없이 시험할 수 있다). 형식은 09 요약서 2~8절.
    /// 빠진 필드는 0·빈 목록으로 두고 예외를 던지지 않는다(JSON 자체가 깨졌을 때만 Newtonsoft 예외).
    /// </summary>
    public static class ManifestParser
    {
        // ---------- 공용 읽기 ----------

        /// <summary>"pixelScale"(그림 해상도 배수). 없거나 1보다 작으면 1.</summary>
        static float PixelScaleOf(JToken t)
        {
            float v = Float(t);
            return v >= 1f ? v : 1f;
        }

        public static int Int(JToken t, int fallback = 0)
        {
            if (t == null) return fallback;
            if (t.Type == JTokenType.Integer) return t.Value<int>();
            if (t.Type == JTokenType.Float) return (int)t.Value<double>();
            return fallback;
        }

        public static float Float(JToken t, float fallback = 0f)
        {
            if (t == null) return fallback;
            if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return t.Value<float>();
            return fallback;
        }

        public static bool Bool(JToken t, bool fallback = false)
        {
            return t != null && t.Type == JTokenType.Boolean ? t.Value<bool>() : fallback;
        }

        public static string Str(JToken t)
        {
            return t != null && t.Type == JTokenType.String ? t.Value<string>() : null;
        }

        public static PixelPoint Point(JToken t)
        {
            var a = t as JArray;
            if (a == null || a.Count < 2) return default;
            return new PixelPoint(Int(a[0]), Int(a[1]));
        }

        public static bool HasPoint(JToken t)
        {
            var a = t as JArray;
            return a != null && a.Count >= 2;
        }

        public static PixelRect Rect(JToken t)
        {
            var a = t as JArray;
            if (a == null || a.Count < 4) return default;
            return new PixelRect(Int(a[0]), Int(a[1]), Int(a[2]), Int(a[3]));
        }

        static int[] IntArray(JToken t)
        {
            var a = t as JArray;
            if (a == null) return null;
            var r = new int[a.Count];
            for (int i = 0; i < a.Count; i++) r[i] = Int(a[i]);
            return r;
        }

        // ---------- players ----------

        public static PlayerManifest ParsePlayers(string json)
        {
            var root = JObject.Parse(json);
            var m = new PlayerManifest
            {
                Canvas = Point(root["canvas"]),
                PivotPx = Point(root["pivotPx"]),
                MirrorRightForLeft = Bool(root["mirrorRightForLeft"], true)
            };
            if (root["characters"] is JObject chars)
            {
                foreach (var p in chars.Properties())
                {
                    var c = p.Value as JObject;
                    if (c == null) continue;
                    var pc = new PlayerCharacter
                    {
                        Folder = p.Name,
                        Body = Str(c["body"]),
                        Portrait = Str(c["portrait"]),
                        Selection = Str(c["selection"]),
                        Silhouette = Str(c["silhouette"]),
                        FrameCount = Int(c["frames"]),
                        PixelScale = System.Math.Max(1, Int(c["pixelScale"], 1))
                    };
                    if (c["clips"] is JObject clips)
                    {
                        foreach (var cp in clips.Properties())
                        {
                            var co = cp.Value as JObject;
                            if (co == null) continue;
                            var clip = new PlayerClip { Name = cp.Name, Fps = Float(co["fps"]), Loop = Bool(co["loop"]) };
                            if (co["frames"] is JArray frames)
                            {
                                foreach (var ft in frames)
                                {
                                    clip.Frames.Add(new PlayerFrame
                                    {
                                        Rect = Rect(ft["rect"]),
                                        Feet = HasPoint(ft["feet"]) ? Point(ft["feet"]) : m.PivotPx,
                                        Hand = Point(ft["hand"]),
                                        Head = Point(ft["head"]),
                                        Shoulder = Point(ft["shoulder"])
                                    });
                                }
                            }
                            pc.Clips.Add(clip);
                            pc.ClipByName[clip.Name] = clip;
                        }
                    }
                    m.Characters[p.Name] = pc;
                }
            }
            return m;
        }

        // ---------- wands ----------

        public static WandManifest ParseWands(string json)
        {
            var root = JObject.Parse(json);
            var m = new WandManifest { HandCanvas = Point(root["handCanvas"]), Grip = Point(root["grip"]) };
            if (root["sprites"] is JObject sprites)
            {
                foreach (var p in sprites.Properties())
                {
                    var s = p.Value as JObject;
                    if (s == null) continue;
                    var w = new WandSprites { Folder = p.Name };
                    var hand = s["hand"];
                    if (hand != null)
                    {
                        w.HandFile = Str(hand["file"]);
                        w.HandCell = Point(hand["cell"]);
                        w.HandColumns = Int(hand["columns"]);
                        if (hand["rows"] is JArray rows) foreach (var r in rows) w.HandRows.Add(Str(r));
                    }
                    var insp = s["inspection"];
                    if (insp != null)
                    {
                        w.InspectionFile = Str(insp["file"]);
                        w.InspectionCell = Point(insp["cell"]);
                        w.InspectionColumns = Int(insp["columns"]);
                        w.InspectionRows = Int(insp["rows"]);
                    }
                    var icon = s["icon"];
                    if (icon != null)
                    {
                        w.IconFile = Str(icon["file"]);
                        w.IconCell = Point(icon["cell"]);
                        w.IconColumns = Int(icon["columns"]);
                    }
                    if (s["stages"] is JArray stages)
                    {
                        foreach (var st in stages)
                        {
                            var ws = new WandStage { Stage = Int(st["stage"]), Head = Point(st["head"]), Expansion = Point(st["expansion"]) };
                            if (st["mutations"] is JArray mu) foreach (var mp in mu) ws.Mutations.Add(Point(mp));
                            w.Stages.Add(ws);
                        }
                    }
                    m.Sprites[p.Name] = w;
                }
            }
            if (root["detachedEffects"] is JObject det)
            {
                foreach (var p in det.Properties())
                {
                    m.Detached[p.Name] = new WandDetached
                    {
                        Key = p.Name,
                        File = Str(p.Value["file"]),
                        Cell = Point(p.Value["cell"]),
                        Frames = Int(p.Value["frames"]),
                        Anchor = Point(p.Value["anchor"])
                    };
                }
            }
            return m;
        }

        // ---------- monsters ----------

        public static MonsterManifest ParseMonsters(string json)
        {
            var root = JObject.Parse(json);
            var m = new MonsterManifest();
            if (root["entries"] is JObject entries)
            {
                foreach (var p in entries.Properties())
                {
                    var e = p.Value as JObject;
                    if (e == null) continue;
                    var me = new MonsterEntry
                    {
                        Id = p.Name,
                        Path = Str(e["path"]),
                        Size = Point(e["size"]),
                        Grid = Point(e["grid"]),
                        Origin = Point(e["origin"])
                    };
                    if (e["clips"] is JObject clips)
                    {
                        foreach (var cp in clips.Properties())
                        {
                            var clip = new MonsterClipDef { Name = cp.Name };
                            if (cp.Value is JArray frames)
                            {
                                foreach (var ft in frames)
                                {
                                    clip.Frames.Add(new MonsterFrame
                                    {
                                        Rect = Rect(ft["rect"]),
                                        Origin = HasPoint(ft["origin"]) ? Point(ft["origin"]) : me.Origin
                                    });
                                }
                            }
                            me.Clips.Add(clip);
                        }
                    }
                    m.Entries.Add(me);
                    m.ById[me.Id] = me;
                }
            }
            if (root["bossUi"] is JObject bu)
            {
                foreach (var p in bu.Properties())
                {
                    m.BossUi[p.Name] = new BossUiEntry
                    {
                        Portrait = Str(p.Value["portrait"]),
                        PortraitSize = Point(p.Value["portraitSize"]),
                        Hud = Str(p.Value["hud"]),
                        HudSize = Point(p.Value["hudSize"])
                    };
                }
            }
            return m;
        }

        // ---------- effects ----------

        /// <summary>effects/manifest.json. 묶음 7개(notes 제외). pathPrefix는 Path 앞에 붙인다(Resources/Art 기준 폴더, 예 "effects/").</summary>
        public static EffectManifest ParseEffects(string json, string pathPrefix = "") => ParseEffects(json, pathPrefix, false);

        /// <summary>
        /// readPivot = 항목의 "pivot"(프레임 왼쪽 위 기준 px)을 기준점으로 쓴다(기본 공격 유형 그림 effects/styles/*, Tools/godot-fx).
        /// 기존 효과 묶음은 pivot 칸이 있어도 가운데 기준으로 그려 왔으므로 false로 둔다(그림이 옮겨지지 않게).
        /// "clips"(이름 → [처음, 끝] 프레임)는 늘 읽는다.
        /// </summary>
        public static EffectManifest ParseEffects(string json, string pathPrefix, bool readPivot)
        {
            var root = JObject.Parse(json);
            var m = new EffectManifest();
            foreach (var gp in root.Properties())
            {
                var go = gp.Value as JObject;
                if (go == null || gp.Name == "notes") continue;
                var g = new EffectGroup { Name = gp.Name };
                foreach (var p in go.Properties())
                {
                    var e = p.Value as JObject;
                    if (e == null || e["path"] == null) continue;
                    var ee = new EffectEntry
                    {
                        Group = gp.Name,
                        Key = p.Name,
                        Path = pathPrefix + Str(e["path"]),
                        Size = Point(e["size"]),
                        Fps = Float(e["fps"]),
                        Loop = Bool(e["loop"]),
                        PixelScale = PixelScaleOf(e["pixelScale"]),
                        Raw = e
                    };
                    if (readPivot && HasPoint(e["pivot"]))
                    {
                        ee.HasPivot = true;
                        ee.Pivot = Point(e["pivot"]);
                    }
                    if (e["clips"] is JObject clips)
                    {
                        ee.Clips = new Dictionary<string, int[]>();
                        foreach (var cp in clips.Properties())
                        {
                            var range = IntArray(cp.Value);
                            if (range != null && range.Length >= 2) ee.Clips[cp.Name] = new[] { range[0], range[1] };
                        }
                    }
                    if (e["frames"] is JArray frames) foreach (var f in frames) ee.Frames.Add(Rect(f));
                    g.Add(ee);
                }
                m.Groups.Add(g);
                m.ByName[g.Name] = g;
            }
            return m;
        }

        /// <summary>sprite-pack/native-64/enhancement/manifest.json의 new 묶음 → 효과 묶음 하나(열쇠 = new의 열쇠).</summary>
        public static EffectGroup ParseEnhancement(string json, string groupName, string pathPrefix, out string backgroundPath)
        {
            var root = JObject.Parse(json);
            var g = new EffectGroup { Name = groupName };
            var bg = Str(root["background"]);
            backgroundPath = bg != null ? pathPrefix + bg : null;
            if (root["new"] is JObject nw)
            {
                foreach (var p in nw.Properties())
                {
                    var e = p.Value as JObject;
                    if (e == null) continue;
                    var ee = new EffectEntry
                    {
                        Group = groupName,
                        Key = p.Name,
                        Path = pathPrefix + Str(e["sheet"]),
                        Size = Point(e["size"]),
                        Fps = Float(e["fps"]),
                        Loop = Bool(e["loop"]),
                        HasPivot = HasPoint(e["pivot"]),
                        Pivot = Point(e["pivot"]),
                        Raw = e
                    };
                    if (e["frames"] is JArray frames) foreach (var f in frames) ee.Frames.Add(Rect(f["rect"]));
                    g.Add(ee);
                }
            }
            return g;
        }

        /// <summary>sprite-pack/characters/players/layers/manifest.json의 groups → 효과 묶음 하나(열쇠 = 그룹 경로, 예 reactive/dalgu/steam).</summary>
        public static EffectGroup ParseLayers(string json, string groupName, string pathPrefix)
        {
            var root = JObject.Parse(json);
            var g = new EffectGroup { Name = groupName };
            if (root["groups"] is JObject groups)
            {
                foreach (var p in groups.Properties())
                {
                    var e = p.Value as JObject;
                    if (e == null) continue;
                    var ee = new EffectEntry
                    {
                        Group = groupName,
                        Key = p.Name,
                        Path = pathPrefix + Str(e["sheet"]),
                        Size = Point(e["frameSize"]),
                        Fps = Float(e["fps"]),
                        Loop = true,
                        HasPivot = HasPoint(e["pivot"]),
                        Pivot = Point(e["pivot"]),
                        FrameKeys = new List<string>(),
                        PixelScale = PixelScaleOf(e["pixelScale"]),
                        Raw = e
                    };
                    if (e["frames"] is JArray frames)
                    {
                        foreach (var f in frames)
                        {
                            ee.Frames.Add(Rect(f["rect"]));
                            ee.FrameKeys.Add(Str(f["key"]) ?? "");
                        }
                    }
                    g.Add(ee);
                }
            }
            return g;
        }

        // ---------- ui ----------

        public static UiManifest ParseUi(string json)
        {
            var root = JObject.Parse(json);
            var m = new UiManifest();
            if (root["assets"] is JArray assets)
            {
                foreach (var a in assets)
                {
                    var e = new UiEntry
                    {
                        Id = Str(a["id"]),
                        File = Str(a["file"]),
                        Width = Int(a["width"]),
                        Height = Int(a["height"]),
                        Frames = Int(a["frames"], 1),
                        FrameMs = Int(a["frame_ms"]),
                        NineSliceLtrb = IntArray(a["nine_slice_ltrb"]),
                        HasHotspot = HasPoint(a["hotspot"]),
                        Hotspot = Point(a["hotspot"]),
                        HasOrigin = HasPoint(a["sprite_origin"]),
                        SpriteOrigin = Point(a["sprite_origin"])
                    };
                    if (e.Id == null || e.File == null) continue;
                    if (e.Frames < 1) e.Frames = 1;
                    string fileName = e.File;
                    int slash = fileName.LastIndexOf('/');
                    if (slash >= 0) fileName = fileName.Substring(slash + 1);
                    if (!UiAssetName.TrySplit(e.Id, fileName, e.Width, e.Height, out e.Variant, out e.State))
                    {
                        e.Variant = "";
                        e.State = UiAssetName.DefaultState;
                    }
                    m.Assets.Add(e);
                    if (!m.ById.TryGetValue(e.Id, out var list)) m.ById[e.Id] = list = new List<UiEntry>();
                    list.Add(e);
                }
            }
            if (root["external"] is JObject ext)
            {
                foreach (var p in ext.Properties())
                {
                    var f = Str(p.Value["file"]);
                    if (f != null) m.External[p.Name] = f;
                }
            }
            return m;
        }

        // ---------- world ----------

        public static WorldManifest ParseWorld(string json)
        {
            var root = JObject.Parse(json);
            var m = new WorldManifest { TilePx = Int(root["tilePx"], 64) };
            if (root["shared"] is JObject shared)
                foreach (var p in shared.Properties()) m.Shared[p.Name] = Str(p.Value);
            if (root["assets"] is JObject assets)
            {
                foreach (var p in assets.Properties())
                {
                    var a = p.Value as JObject;
                    if (a == null) continue;
                    var e = new WorldEntry { Id = p.Name };
                    if (a["variants"] is JObject vars)
                    {
                        e.IsTile = true;
                        e.FrameSize = Point(a["size"]);
                        e.FrameCount = 1;
                        e.Columns = 1;
                        foreach (var vp in vars.Properties())
                            e.Variants[vp.Name] = new WorldTileVariant { File = Str(vp.Value["file"]), Rect = Rect(vp.Value["rect"]) };
                    }
                    else
                    {
                        e.File = Str(a["file"]);
                        e.FrameSize = Point(a["frameSize"]);
                        e.FrameCount = Int(a["frameCount"], 1);
                        e.Columns = Int(a["columns"], 1);
                        e.Origin = Point(a["origin"]);
                        e.Fps = Float(a["fps"]);
                        e.MirrorFor = Str(a["mirrorFor"]);
                        if (a["poseFrames"] is JObject poses)
                        {
                            foreach (var pp in poses.Properties())
                            {
                                if (pp.Value is JArray arr) e.PoseFrames[pp.Name] = IntArray(arr);
                                else e.PoseFrames[pp.Name] = new[] { Int(pp.Value) };
                            }
                        }
                    }
                    m.Assets[p.Name] = e;
                }
            }
            return m;
        }
    }
}
