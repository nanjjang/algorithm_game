using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.View;

namespace Wizard.Solo.App
{
    /// <summary>
    /// 월드 그리기 풀(ISpritePool). SpriteRenderer와 MeshRenderer(+ MeshFilter)를 따로 모아 둔다.
    /// 빌려줄 때 머티리얼 = ctx.Art.Material(kind)(그림 창고가 없으면 새로 만든 렌더러의 기본 머티리얼 그대로).
    /// 돌려받으면 PropertyBlock·색·flip·크기·회전·위치를 비우고 비활성으로 풀 상자 아래에 둔다.
    /// 머티리얼을 직접 만들거나 셰이더를 찾지 않는다(00 문서 7절 — 셰이더의 주인은 F2).
    /// </summary>
    public sealed class SpritePool : ISpritePool
    {
        readonly IArtLibrary _art;
        readonly Transform _root;
        readonly Transform _storage;
        readonly Stack<SpriteRenderer> _sprites = new Stack<SpriteRenderer>();
        readonly Stack<MeshRenderer> _meshes = new Stack<MeshRenderer>();
        readonly HashSet<SpriteRenderer> _rentedSprites = new HashSet<SpriteRenderer>();
        readonly HashSet<MeshRenderer> _rentedMeshes = new HashSet<MeshRenderer>();
        readonly Dictionary<SpriteRenderer, Material> _spriteDefaults = new Dictionary<SpriteRenderer, Material>();

        /// <param name="art">그림 창고(null이면 기본 머티리얼).</param>
        /// <param name="root">빌린 렌더러의 부모(WorldRoot).</param>
        public SpritePool(IArtLibrary art, Transform root)
        {
            _art = art;
            _root = root;
            var storage = new GameObject("SpritePool");
            storage.SetActive(false);
            _storage = storage.transform;
            if (root != null) _storage.SetParent(root, false);
        }

        public int RentedSpriteCount => _rentedSprites.Count;
        public int RentedMeshCount => _rentedMeshes.Count;
        public int FreeSpriteCount => _sprites.Count;
        public int FreeMeshCount => _meshes.Count;

        Material MaterialFor(SpriteMaterialKind kind)
        {
            if (_art == null) return null;
            return _art.Material(kind);
        }

        public SpriteRenderer Rent(Sprite sprite, int sortingOrder, SpriteMaterialKind material = SpriteMaterialKind.Sprite)
        {
            SpriteRenderer sr = null;
            while (_sprites.Count > 0 && sr == null) sr = _sprites.Pop(); // 밖에서 Destroy 된 것은 건너뛴다
            if (sr == null)
            {
                var go = new GameObject("Sprite");
                sr = go.AddComponent<SpriteRenderer>();
                _spriteDefaults[sr] = sr.sharedMaterial;
            }
            var t = sr.transform;
            t.SetParent(_root, false);
            var mat = MaterialFor(material);
            if (mat != null) sr.sharedMaterial = mat;
            else if (_spriteDefaults.TryGetValue(sr, out var def) && def != null) sr.sharedMaterial = def;
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.gameObject.SetActive(true);
            _rentedSprites.Add(sr);
            return sr;
        }

        public void Return(SpriteRenderer renderer)
        {
            if (renderer == null || !_rentedSprites.Remove(renderer)) return;
            renderer.SetPropertyBlock(null);
            renderer.color = Color.white;
            renderer.flipX = false;
            renderer.flipY = false;
            renderer.sprite = null;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.sortingOrder = 0;
            renderer.enabled = true;
            ResetTransform(renderer.transform);
            renderer.gameObject.SetActive(false);
            _sprites.Push(renderer);
        }

        public MeshRenderer RentMesh(int sortingOrder, SpriteMaterialKind material = SpriteMaterialKind.Shape)
        {
            MeshRenderer mr = null;
            while (_meshes.Count > 0 && mr == null) mr = _meshes.Pop();
            if (mr == null)
            {
                var go = new GameObject("Mesh");
                go.AddComponent<MeshFilter>();
                mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            mr.transform.SetParent(_root, false);
            var mat = MaterialFor(material);
            if (mat != null) mr.sharedMaterial = mat;
            mr.sortingOrder = sortingOrder;
            mr.gameObject.SetActive(true);
            _rentedMeshes.Add(mr);
            return mr;
        }

        public void Return(MeshRenderer renderer)
        {
            if (renderer == null || !_rentedMeshes.Remove(renderer)) return;
            var mf = renderer.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = null;
            renderer.SetPropertyBlock(null);
            renderer.sortingOrder = 0;
            renderer.enabled = true;
            ResetTransform(renderer.transform);
            renderer.gameObject.SetActive(false);
            _meshes.Push(renderer);
        }

        void ResetTransform(Transform t)
        {
            t.SetParent(_storage, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        /// <summary>빌려 간 것까지 전부 부순다(호스트를 걷을 때).</summary>
        public void DestroyAll()
        {
            foreach (var sr in _rentedSprites) if (sr != null) UnityObjects.Destroy(sr.gameObject);
            foreach (var mr in _rentedMeshes) if (mr != null) UnityObjects.Destroy(mr.gameObject);
            _rentedSprites.Clear();
            _rentedMeshes.Clear();
            _sprites.Clear();
            _meshes.Clear();
            _spriteDefaults.Clear();
            if (_storage != null) UnityObjects.Destroy(_storage.gameObject);
        }
    }
}
