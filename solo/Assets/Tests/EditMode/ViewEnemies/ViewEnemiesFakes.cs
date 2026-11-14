using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.View;
using Wizard.Solo.View.Enemies;

namespace Wizard.Solo.Tests.ViewEnemies
{
    /// <summary>그림 창고 가짜: 모든 요청에 작은 흰 스프라이트를 준다(64×64, 기준점 아래 가운데). 클립 이름 목록은 테스트가 채운다.</summary>
    public sealed class FakeArt : IArtLibrary
    {
        readonly Dictionary<long, Sprite> _sprites = new Dictionary<long, Sprite>();
        readonly List<Object> _made = new List<Object>();
        public readonly Dictionary<string, List<string>> ClipNames = new Dictionary<string, List<string>>();
        public readonly List<string> MonsterRequests = new List<string>();

        public Sprite Make(int w, int h, float pivotY01 = 0.1f)
        {
            long key = ((long)w << 40) | ((long)h << 20) | (long)(pivotY01 * 1000f);
            if (_sprites.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, pivotY01), SimConst.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            _made.Add(tex);
            _made.Add(s);
            _sprites[key] = s;
            return s;
        }

        SpriteAnim Anim(int w, int h, int frames = 2, float fps = 12f, bool loop = true)
        {
            var f = new Sprite[frames];
            for (int i = 0; i < frames; i++) f[i] = Make(w, h);
            return new SpriteAnim { Frames = f, Fps = fps, Loop = loop };
        }

        public void DestroyAll()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            _sprites.Clear();
        }

        public SpriteAnim CharacterClip(string charId, string clip, Facing3 facing) => Anim(64, 64);
        public PlayerClip CharacterClipInfo(string charId, string clip, Facing3 facing) => null;
        public Sprite CharacterPortrait(string charId, int expression) => Make(64, 64);
        public Sprite CharacterSelection(string charId) => Make(64, 64);
        public Sprite CharacterSilhouette(string charId) => Make(32, 32);
        public Sprite WandHand(string charId, int stage, Facing3 facing, bool aim) => Make(32, 64);
        public Sprite WandInspection(string charId, int stage) => Make(64, 64);
        public Sprite WandIcon(string charId, int stage) => Make(64, 64);
        public WandStage WandStageInfo(string charId, int stage) => null;
        public PixelPoint WandGrip => new PixelPoint(16, 48);
        public SpriteAnim WandDetachedPart(string charId) => null;
        public SpriteAnim MonsterClip(string monsterId, string clip) { MonsterRequests.Add(monsterId + ":" + clip); return Anim(64, 64, 2, 8f); }
        public IReadOnlyList<string> MonsterClipNames(string monsterId) =>
            monsterId != null && ClipNames.TryGetValue(monsterId, out var l) ? l : (IReadOnlyList<string>)new List<string>();
        public Sprite BossPortrait(string bossId) => Make(96, 96);
        public Sprite BossHudIcon(string bossId) => Make(32, 32);
        public SpriteAnim Effect(string group, string key) => Anim(16, 16, 2, 12f, false);
        public EffectEntry EffectInfo(string group, string key) => null;
        public EffectEntry LayerInfo(string key) => null;
        public UiSprite Ui(string id, string variant = null, string state = null) => new UiSprite { Sprite = Make(32, 32), Width = 32, Height = 32 };
        public UiSprite UiSized(string id, string variant, string state, int sizePx) => Ui(id, variant, state);
        public UiEntry UiInfo(string id, string variant = null, string state = null, int sizePx = 0) => null;
        public SpriteAnim UiAnim(string id, string variant = null, string state = null, int sizePx = 0) => Anim(32, 32);
        public Sprite Icon(string contentId, int sizePx = 32) => Make(sizePx, sizePx);
        public SpriteAnim World(string assetId, string todId = null) => Anim(64, 96, 4, 10f);
        public WorldEntry WorldInfo(string assetId) => null;
        public string WorldMapJson(string mapId) => null;
        public Sprite Background(string key) => Make(64, 64);
        public Sprite[] CommonFrames(string relPath) => System.Array.Empty<Sprite>();
        public Sprite CommonSprite(string relPath) => null;
        public Font Font(UiFontId size) => null;
        public Sprite Placeholder(int widthPx, int heightPx) => Make(widthPx, heightPx);
        public Sprite WhitePixel => Make(1, 1, 0.5f);
        public Sprite Circle(int radiusPx, bool ringOnly = false, int ringWidthPx = 2) => Make(radiusPx * 2, radiusPx * 2, 0.5f);
        public Material Material(SpriteMaterialKind kind) => null;
        public void Release(string effectGroup) { }
    }

    /// <summary>풀 가짜: 진짜 GameObject를 만들고 빌린 수를 센다. 돌려받으면 SpritePool(F5)처럼 PropertyBlock·부모를 비운다.</summary>
    public sealed class FakePool : ISpritePool
    {
        readonly Transform _root, _storage;
        readonly Stack<SpriteRenderer> _freeSprites = new Stack<SpriteRenderer>();
        readonly Stack<MeshRenderer> _freeMeshes = new Stack<MeshRenderer>();
        public readonly HashSet<SpriteRenderer> RentedSprites = new HashSet<SpriteRenderer>();
        public readonly HashSet<MeshRenderer> RentedMeshes = new HashSet<MeshRenderer>();
        readonly List<GameObject> _all = new List<GameObject>();

        public FakePool(Transform root)
        {
            _root = root;
            var s = new GameObject("FakePoolStorage");
            s.SetActive(false);
            _storage = s.transform;
            _all.Add(s);
        }

        public SpriteRenderer Rent(Sprite sprite, int sortingOrder, SpriteMaterialKind material = SpriteMaterialKind.Sprite)
        {
            var sr = _freeSprites.Count > 0 ? _freeSprites.Pop() : null;
            if (sr == null) { var go = new GameObject("S"); _all.Add(go); sr = go.AddComponent<SpriteRenderer>(); }
            sr.transform.SetParent(_root, false);
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.gameObject.SetActive(true);
            RentedSprites.Add(sr);
            return sr;
        }

        public void Return(SpriteRenderer renderer)
        {
            if (renderer == null || !RentedSprites.Remove(renderer)) return;
            renderer.SetPropertyBlock(null);
            renderer.color = Color.white;
            renderer.flipX = false;
            renderer.sprite = null;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.enabled = true;
            renderer.transform.SetParent(_storage, false);
            renderer.transform.localScale = Vector3.one;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.gameObject.SetActive(false);
            _freeSprites.Push(renderer);
        }

        public MeshRenderer RentMesh(int sortingOrder, SpriteMaterialKind material = SpriteMaterialKind.Shape)
        {
            var mr = _freeMeshes.Count > 0 ? _freeMeshes.Pop() : null;
            if (mr == null) { var go = new GameObject("M"); _all.Add(go); go.AddComponent<MeshFilter>(); mr = go.AddComponent<MeshRenderer>(); }
            mr.transform.SetParent(_root, false);
            mr.sortingOrder = sortingOrder;
            mr.gameObject.SetActive(true);
            RentedMeshes.Add(mr);
            return mr;
        }

        public void Return(MeshRenderer renderer)
        {
            if (renderer == null || !RentedMeshes.Remove(renderer)) return;
            var mf = renderer.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = null;
            renderer.transform.SetParent(_storage, false);
            renderer.gameObject.SetActive(false);
            _freeMeshes.Push(renderer);
        }

        public int CountSpritesWithOrder(int order)
        {
            int n = 0;
            foreach (var s in RentedSprites) if (s.sortingOrder == order) n++;
            return n;
        }

        public void DestroyAll()
        {
            foreach (var go in _all) if (go != null) Object.DestroyImmediate(go);
            _all.Clear();
        }
    }

    /// <summary>층 하나를 세계에 붙여 돌리는 받침.</summary>
    public sealed class ViewRig
    {
        public readonly FakeArt Art = new FakeArt();
        public readonly GameObject RootGo = new GameObject("G15TestRoot");
        public readonly FakePool Pool;
        public readonly ViewContext Vc;
        public readonly SimWorld World;

        public ViewRig()
        {
            Pool = new FakePool(RootGo.transform);
            World = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), 1);
            Vc = new ViewContext
            {
                Game = new GameContext { Art = Art, Content = TestContent.Db },
                World = World,
                WorldRoot = RootGo.transform,
                Pool = Pool,
                Options = new ViewOptions()
            };
        }

        public void Destroy()
        {
            Pool.DestroyAll();
            if (RootGo != null) Object.DestroyImmediate(RootGo);
            Art.DestroyAll();
        }

        public EnemyViewTuning Tuning => EnemyViewTuning.Load(TestContent.Db);
    }
}
