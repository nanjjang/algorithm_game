using UnityEngine;
using UnityEngine.UI;

namespace Wizard.Solo.Screens.Enhance
{
    // The workbench uses the original seal and wand sprites for the main action.
    // This layer only draws the small embers that break loose from the ritual.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class EnhanceCinematic : MaskableGraphic
    {
        enum Beat { Rise, Collapse }

        Beat _beat;
        Color _ink;
        float _age, _duration, _strength;
        int _count;
        bool _drop;

        bool Playing => gameObject.activeSelf;
        public bool Rising => Playing && _beat == Beat.Rise;

        public float SealVisibility => !Playing ? 1f : Rising
            ? Mathf.Lerp(1f, 0.22f, Progress(0.15f, 0.52f) * (1f - Progress(0.65f, _duration)) * _strength)
            : Mathf.Lerp(1f, 0.08f, Progress(0f, 0.18f) * (1f - Progress(0.62f, _duration)) * _strength);

        public float SealScale => !Playing ? 1f : Rising
            ? 1f + 0.15f * Progress(0.03f, 0.24f) * (1f - Progress(0.34f, _duration)) * _strength
            : 1f - 0.58f * Progress(0f, 0.22f) * (1f - Progress(0.62f, _duration)) * _strength;

        public float CameraOffset => !Playing ? 0f : Rising
            ? 0.11f * (1f - Progress(0.05f, 0.62f)) * _strength
            : 0.06f * (1f - Progress(0f, 0.42f)) * _strength;

        public float WandLift => Rising
            ? 55f * Progress(0f, 0.26f) * (1f - Progress(0.31f, 0.92f)) * _strength
            : 0f;

        float Progress(float from, float to) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, _age));

        public void Rise(Color ink, float strength, int tier) =>
            Begin(Beat.Rise, ink, strength, 1f + 0.08f * tier, 12 + tier * 2);

        public void Collapse(Color ink, float strength, bool drop)
        {
            _drop = drop;
            Begin(Beat.Collapse, ink, strength, drop ? 1.24f : 0.84f, drop ? 18 : 10);
        }

        void Begin(Beat beat, Color ink, float strength, float duration, int count)
        {
            _beat = beat;
            _ink = ink;
            _strength = strength;
            _duration = duration;
            _count = Mathf.CeilToInt(count * strength);
            _age = 0f;
            gameObject.SetActive(true);
            SetVerticesDirty();
        }

        public void Advance(float dt)
        {
            if (!Playing || dt <= 0f) return;
            _age += dt;
            if (_age >= _duration) gameObject.SetActive(false);
            else SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            bool rise = _beat == Beat.Rise;
            Color hot = Color.Lerp(_ink, Color.white, rise ? 0.55f : 0.30f);
            for (int i = 0; i < _count; i++)
            {
                float age = _age - (i % 5) * 0.027f;
                if (age < 0f) continue;
                float t = Mathf.Clamp01(age / _duration);
                float v = 0.45f + 0.55f * Mathf.Repeat(i * 0.618034f, 1f);
                float angle = i * 2.399963f;
                float x = Mathf.Cos(angle) * (rise ? 62f + 330f * t * v : 155f * (1f - t) + 140f * t * t * v);
                float y = Mathf.Sin(angle) * (rise ? 110f : 158f);
                y += rise ? 340f * t * v : 160f * t * v - (_drop ? 460f : 320f) * t * t;
                float alpha = (1f - t) * _strength;
                int size = v > 0.79f ? 8 : 4;
                Color edge = _ink;
                edge.a = alpha * 0.74f;
                Color core = hot;
                core.a = alpha;
                Pixel(mesh, x, y, size, edge);
                if (size == 8) Pixel(mesh, x + 4f, y + 4f, 4, core);
            }
        }

        static void Pixel(VertexHelper mesh, float x, float y, int size, Color32 tint)
        {
            const float grid = 4f;
            x = Mathf.Round(x / grid) * grid;
            y = Mathf.Round(y / grid) * grid;
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector2(x, y), tint, Vector2.zero);
            mesh.AddVert(new Vector2(x, y + size), tint, Vector2.zero);
            mesh.AddVert(new Vector2(x + size, y + size), tint, Vector2.zero);
            mesh.AddVert(new Vector2(x + size, y), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
