using System.Collections.Generic;

namespace Wizard.Solo.Art
{
    // 원본 manifest JSON을 읽은 모양(09 요약서 2~8절). 좌표는 전부 원본 그대로(왼쪽 위 원점, px).
    // Unity 좌표로 바꾸는 것은 RectMath 한 곳이다. 순서가 뜻을 갖는 목록(클립, 효과 열쇠)은 List로 둬 JSON 순서를 지킨다.

    /// <summary>원본 rect [x, y, w, h] (왼쪽 위 원점).</summary>
    public struct PixelRect
    {
        public int X, Y, W, H;
        public PixelRect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        public override string ToString() => "[" + X + "," + Y + "," + W + "," + H + "]";
    }

    /// <summary>원본 점 (x, y) (그림 안 px, 왼쪽 위 원점).</summary>
    public struct PixelPoint
    {
        public int X, Y;
        public PixelPoint(int x, int y) { X = x; Y = y; }
        public override string ToString() => "(" + X + "," + Y + ")";
    }

    // ---- players/manifest.json ----

    public sealed class PlayerFrame
    {
        public PixelRect Rect;
        public PixelPoint Feet, Hand, Head, Shoulder;
    }

    public sealed class PlayerClip
    {
        public string Name;
        public float Fps;
        public bool Loop;
        public List<PlayerFrame> Frames = new List<PlayerFrame>();
    }

    public sealed class PlayerCharacter
    {
        public string Folder;
        public string Body, Portrait, Selection, Silhouette;
        public int FrameCount;
        /// <summary>그림 해상도 배수(기본 1). 2 = 칸 rect·점은 64px 논리 좌표로 적고 시트는 2배 해상도(rect×2로 자르고 PPU 128).
        /// 화면 크기와 점(발·손·머리)은 다른 제자와 같고 세부만 두 배다(제자 1 교체본, Tools/art_disciple1.py).</summary>
        public int PixelScale = 1;
        public List<PlayerClip> Clips = new List<PlayerClip>();
        public readonly Dictionary<string, PlayerClip> ClipByName = new Dictionary<string, PlayerClip>();
    }

    public sealed class PlayerManifest
    {
        public PixelPoint Canvas;
        public PixelPoint PivotPx;
        public bool MirrorRightForLeft;
        public readonly Dictionary<string, PlayerCharacter> Characters = new Dictionary<string, PlayerCharacter>();
    }

    // ---- wands/manifest.json ----

    public sealed class WandStage
    {
        public int Stage;
        public PixelPoint Head;
        public List<PixelPoint> Mutations = new List<PixelPoint>();
        public PixelPoint Expansion;
    }

    public sealed class WandSprites
    {
        public string Folder;
        public string HandFile, InspectionFile, IconFile;
        public PixelPoint HandCell, InspectionCell, IconCell;
        public int HandColumns, InspectionColumns, InspectionRows, IconColumns;
        public List<string> HandRows = new List<string>();
        public List<WandStage> Stages = new List<WandStage>();
    }

    public sealed class WandDetached
    {
        public string Key, File;
        public PixelPoint Cell, Anchor;
        public int Frames;
    }

    public sealed class WandManifest
    {
        public PixelPoint HandCanvas, Grip;
        public readonly Dictionary<string, WandSprites> Sprites = new Dictionary<string, WandSprites>();
        public readonly Dictionary<string, WandDetached> Detached = new Dictionary<string, WandDetached>();
    }

    // ---- monsters/manifest.json ----

    public sealed class MonsterFrame
    {
        public PixelRect Rect;
        public PixelPoint Origin;
    }

    public sealed class MonsterClipDef
    {
        public string Name;
        public List<MonsterFrame> Frames = new List<MonsterFrame>();
    }

    public sealed class MonsterEntry
    {
        public string Id, Path;
        public PixelPoint Size, Grid, Origin;
        public List<MonsterClipDef> Clips = new List<MonsterClipDef>();
        public MonsterClipDef Clip(string name)
        {
            for (int i = 0; i < Clips.Count; i++) if (Clips[i].Name == name) return Clips[i];
            return null;
        }
    }

    public sealed class BossUiEntry
    {
        public string Portrait, Hud;
        public PixelPoint PortraitSize, HudSize;
    }

    public sealed class MonsterManifest
    {
        public readonly List<MonsterEntry> Entries = new List<MonsterEntry>();
        public readonly Dictionary<string, MonsterEntry> ById = new Dictionary<string, MonsterEntry>();
        public readonly Dictionary<string, BossUiEntry> BossUi = new Dictionary<string, BossUiEntry>();
    }

    // ---- effects/manifest.json, 보강 효과 묶음 ----

    public sealed class EffectEntry
    {
        public string Group, Key, Path;
        public PixelPoint Size;
        public List<PixelRect> Frames = new List<PixelRect>();
        /// <summary>프레임 안 기준점. 없으면(효과 manifest) 가운데.</summary>
        public bool HasPivot;
        public PixelPoint Pivot;
        /// <summary>보강 묶음(층)에서 각 프레임의 열쇠(예 normal_00). 없으면 null.</summary>
        public List<string> FrameKeys;
        public float Fps;
        public bool Loop;
        /// <summary>그림 해상도 배수(기본 1). Size·Frames·Pivot은 논리 px이고 시트는 이 배수만큼 크다(rect×배수로 자르고 PPU 64×배수).
        /// 정수가 아니어도 된다(예 4/3: 128px 시전 문양을 원본 자리 96px 크기로). rect×배수는 반올림한다.</summary>
        public float PixelScale = 1f;
        /// <summary>원본 항목 그대로(보강 층의 attachment·offsetByDirection 같은 붙임 정보를 표시 작업이 읽는다).</summary>
        public Newtonsoft.Json.Linq.JObject Raw;
        /// <summary>이름 붙은 프레임 구간(styles manifest의 "clips": {"rise": [0, 2]} — 처음·끝 프레임 번호, 끝 포함). 없으면 null.</summary>
        public Dictionary<string, int[]> Clips;
    }

    public sealed class EffectGroup
    {
        public string Name;
        public readonly List<EffectEntry> Entries = new List<EffectEntry>();
        public readonly Dictionary<string, EffectEntry> ByKey = new Dictionary<string, EffectEntry>();
        public void Add(EffectEntry e)
        {
            Entries.Add(e);
            ByKey[e.Key] = e;
        }
    }

    public sealed class EffectManifest
    {
        public readonly List<EffectGroup> Groups = new List<EffectGroup>();
        public readonly Dictionary<string, EffectGroup> ByName = new Dictionary<string, EffectGroup>();
        public int Count
        {
            get { int n = 0; for (int i = 0; i < Groups.Count; i++) n += Groups[i].Entries.Count; return n; }
        }
    }

    // ---- ui/manifest.json ----

    public sealed class UiEntry
    {
        public string Id, File;
        /// <summary>파일 이름에서 읽은 변형·상태(09 6.1절). 이름 꼴이 맞지 않으면 Variant = "", State = "normal".</summary>
        public string Variant, State;
        /// <summary>프레임 하나의 크기.</summary>
        public int Width, Height, Frames, FrameMs;
        /// <summary>원본 순서 [l, t, r, b]. 없으면 null.</summary>
        public int[] NineSliceLtrb;
        public bool HasHotspot;
        public PixelPoint Hotspot;
        public bool HasOrigin;
        public PixelPoint SpriteOrigin;
    }

    public sealed class UiManifest
    {
        public readonly List<UiEntry> Assets = new List<UiEntry>();
        public readonly Dictionary<string, List<UiEntry>> ById = new Dictionary<string, List<UiEntry>>();
        /// <summary>external id → ui 폴더 기준 파일 경로(예 S-0201 → ../backgrounds/title_1920x1080.png).</summary>
        public readonly Dictionary<string, string> External = new Dictionary<string, string>();
        public List<UiEntry> Of(string id)
        {
            return id != null && ById.TryGetValue(id, out var l) ? l : null;
        }
    }

    // ---- world/manifest.json ----

    public sealed class WorldTileVariant
    {
        public string File;
        public PixelRect Rect;
    }

    public sealed class WorldEntry
    {
        public string Id;
        /// <summary>타일형이면 참(variants가 있다).</summary>
        public bool IsTile;
        public readonly Dictionary<string, WorldTileVariant> Variants = new Dictionary<string, WorldTileVariant>();
        public string File;
        public PixelPoint FrameSize, Origin;
        public int FrameCount, Columns;
        public float Fps;
        /// <summary>자세 이름 → 프레임 번호들(poseFrames, 숫자 하나도 목록으로).</summary>
        public readonly Dictionary<string, int[]> PoseFrames = new Dictionary<string, int[]>();
        public string MirrorFor;
    }

    public sealed class WorldManifest
    {
        public int TilePx;
        public readonly Dictionary<string, WorldEntry> Assets = new Dictionary<string, WorldEntry>();
        public readonly Dictionary<string, string> Shared = new Dictionary<string, string>();
    }
}
