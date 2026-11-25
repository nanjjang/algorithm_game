using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
using Wizard.Solo.Data;
using Wizard.Solo.Audio;
#endif
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using Image = UnityEngine.UI.Image;
using RawImage = UnityEngine.UI.RawImage;

namespace Wizard.Solo.Screens.Story
{
    /// <summary>Plays the authored shots using the same sprite clips as the field, plaza and combat views.</summary>
    public sealed class TutorialOpening : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/TutorialOpening.unity";
        const float MasterCut = 1.65f, DoorCut = 5.35f, FieldCut = 6.1f, SlimeCut = 10.45f;
        const float DoorShut = 5.78f, Fizzle = 9.78f, SlimeWake = 11.58f;

        static readonly Vector2[] AtTheTower =
        {
            new(973f, 479f), new(824f, 510f), new(1029f, 452f), new(1055f, 552f), new(888f, 570f)
        };

        static readonly Vector2[] InTheField =
        {
            new(1037f, 749f), new(848f, 612f), new(943f, 587f), new(935f, 697f), new(818f, 727f)
        };

        [SerializeField] Texture2D fieldBackplate, foregroundPine, pathSurface, distantRuins;
        [SerializeField] float duration = 15f;
        [SerializeField] Vector2 towerFocus = new Vector2(948f, 381f);
        [SerializeField] Vector2 fieldFocus = new Vector2(975f, 470f);
        [SerializeField] Vector2 slimeFocus = new Vector2(1038f, 676f);

        static readonly Color Ink = new Color32(9, 12, 20, 255);
        static readonly Color Amber = new Color32(255, 176, 91, 255);
        readonly Actor[] _disciples = new Actor[Ids.Char.Count];
        RectTransform _mount, _stage;
        Image _master, _door, _slime, _glow, _spark, _smoke, _fade, _fog, _fieldLantern, _slimeShadow;
        RawImage _leftPine, _rightPine, _passingPine, _ruins;
        SpriteAnim _fogClip;
        SpriteAnim _masterClip, _slimeClip, _smokeClip;
        UiLabel _caption;
        GameContext _context;
        float _time, _motion;
        int _captionIndex = -1;

#if UNITY_EDITOR
        ArtLibrary _previewArt;
        GameObject _previewCanvas;

        void Start()
        {
            if (SceneManager.GetActiveScene() != gameObject.scene) return;
            _previewArt = new ArtLibrary();
            var ui = UiCanvasFactory.Create("Opening preview", UiSortOrders.Screen(ScreenLayer.Fullscreen));
            _previewCanvas = ui.parent.gameObject;
            var context = new GameContext { Art = _previewArt, Names = NameTable.LoadFromResources() };
            context.Sound = UnitySoundPlayer.Create(context);
            Build(ui, context, new RunConfig { CharId = Ids.Char.C1 }, 1f);
        }

        void Update()
        {
            if (_previewCanvas == null || _mount == null) return;
            if (_time >= duration) _time = 0f;
            Advance(Time.unscaledDeltaTime);
        }
#endif

        public bool Build(RectTransform parent, GameContext context, RunConfig run, float motion)
        {
            var art = context.Art;
            _masterClip = art.World("facility.master_chair");
            _slimeClip = art.MonsterClip(Ids.Enemy.SlimeMoss, "move");
            _smokeClip = art.Effect("ritual", "failure_smoke");
            if (fieldBackplate == null || _masterClip?.At(0f) == null || _slimeClip?.At(0f) == null)
                return false;

            _context = context;
            _motion = motion;
            _mount = UiKit.Node(parent, "Opening");
            UiKit.Stretch(_mount);
            UiFullBleed.Fill(UiKit.Rect(_mount, 0f, 0f, 1920f, 1080f, Ink, true).rectTransform);
            _stage = UiKit.Node(_mount, "Field", 0f, 0f, 1920f, 1080f);
            var backdrop = Plate(_stage, "Field backplate", fieldBackplate, 0f, 0f, 1920f, 1080f);
            var moonlight = new Color32(173, 193, 229, 255);
            backdrop.color = moonlight;
            Plate(_stage, "Worn trail from the tower", pathSurface, 0f, 0f, 1920f, 1080f).color = moonlight;
            _fogClip = art.World("field.weather.fog");
            _fog = Layer("Ground mist", _fogClip.At(0f), 507f, 452f, 1.8f);
            _fieldLantern = UiKit.Image(_stage, UiBackdrop.RadialSoft, 1050f, 350f, 280f, 133f, Amber, false);
            UiKit.Image(_stage, UiBackdrop.RadialSoft, 659f, 354f, 205f, 104f,
                Alpha(new Color32(247, 139, 71, 255), 0.23f), false);

            var stones = art.World("field.wall.rocks.01").At(0f);
            Layer("Stones beside the path", stones, 1357f, 805f, 1.45f).color = new Color32(123, 137, 157, 255);
            Layer("Rocks in the lower verge", stones, 778f, 836f, 1.7f).color = new Color32(98, 117, 143, 255);
            var thicket = art.World("field.wall.n.02").At(0f);
            var verge = new Color32(114, 134, 156, 255);
            Layer("Bramble at the path", thicket, 1274f, 799f, 1.63f).color = verge;
            Layer("Bramble by the lower rocks", thicket, 718f, 812f, 1.41f).color = verge;
            Layer("Bramble behind the clearing", thicket, 1373f, 651f, 1.25f).color = verge;
            _door = UiKit.Rect(_stage, 946f, 279f, 26f, 63f, new Color32(25, 24, 31, 255));
            _glow = UiKit.Image(_stage, UiBackdrop.RadialSoft, 837f, 234f, 243f, 181f, Amber, false);
            GroundShadow(_stage, 847f, 430f, 84f);
            _master = Layer("Master", _masterClip.At(0f), 847f, 432f, 1.18f);

            int selected = Ids.Char.NumberOf(run.CharId);
            foreach (int i in new[] { 2, 1, 0, 3, 4 })
            {
                int number = (selected + i - 1) % Ids.Char.Count + 1;
                _disciples[i] = new Actor(_stage, art, Ids.Char.Of(number), i == 0 ? run.WandStage : 0);
            }
            _slimeShadow = GroundShadow(_stage, 1260f, 776f, 98f);
            _slime = Layer("Moss slime", _slimeClip.At(0f), 1260f, 786f, 1.8f);
            _spark = UiKit.Image(_stage, UiBackdrop.RadialSoft, 0f, 0f, 58f, 58f, Amber, false);
            _smoke = Layer("Extinguished spark", _smokeClip?.At(0f), 0f, 0f, 0.85f);

            _ruins = Plate(_mount, "Ruins across the valley", distantRuins, -78f, -32f, 2076f, 1168f);
            _ruins.uvRect = new Rect(0f, 0.204f, 0.653f, 0.653f);
            _ruins.color = new Color32(218, 224, 255, 255);

            _leftPine = Plate(_mount, "Near pine left", foregroundPine, 0f, 0f, 356f, 890f);
            _rightPine = Plate(_mount, "Near pine right", foregroundPine, 0f, 0f, 320f, 810f);
            _leftPine.uvRect = new Rect(1f, 0f, -1f, 1f);
            _leftPine.color = new Color32(65, 77, 104, 255);
            _rightPine.color = new Color32(49, 62, 84, 255);
            _passingPine = Plate(_mount, "Pine crossing the lens", foregroundPine, 0f, 0f, 1750f, 4360f);
            _passingPine.color = new Color32(30, 39, 53, 255);

            UiFullBleed.Fill(UiKit.Image(_mount, UiBackdrop.Vignette, 0f, 0f, 1920f, 1080f,
                new Color(0.018f, 0.023f, 0.052f, 0.43f), false).rectTransform);
            UiFullBleed.Fill(UiKit.Rect(_mount, 0f, 0f, 1920f, 78f, Ink).rectTransform,
                UiBleedEdges.Horizontal | UiBleedEdges.Top);
            UiFullBleed.Fill(UiKit.Rect(_mount, 0f, 972f, 1920f, 108f, Ink).rectTransform,
                UiBleedEdges.Horizontal | UiBleedEdges.Bottom);

            _caption = UiKit.Label(context, _mount, string.Empty, UiFontId.F32, 240f, 905f, 1440f, 65f,
                UiTheme.Chalk, TextAnchor.MiddleCenter);
            UiKit.DropShadow(_caption, 2f);
            _fade = UiKit.Rect(_mount, 0f, 0f, 1920f, 1080f, Ink);
            UiFullBleed.Fill(_fade.rectTransform);
            var skip = UiKeyHint.Compose(context.Names, new[] { new UiKeyHint(UnityEngine.InputSystem.Key.Escape, "str.tutorial.opening.skip") });
            UiKit.Label(context, _mount, skip, UiFontId.F16, 1400f, 989f, 434f, 48f,
                UiTheme.SlateBlue, TextAnchor.MiddleRight);
            Render();
            return true;
        }

        public bool Advance(float dt)
        {
            float previous = _time;
            _time += Mathf.Min(dt, 0.10f);
            Cue(previous, 0.28f, "tutorial.opening.wind", 0.28f, 0.67f);
            Cue(previous, FieldCut - 0.17f, "tutorial.opening.wind", 0.36f, 0.82f);
            Cue(previous, DoorShut, "player.death_thud", 0.59f, 0.81f);
            Cue(previous, DoorShut, "style.art.copy.end", 0.68f, 0.75f);
            Cue(previous, Fizzle, "sfx_t16", 0.51f, 1.16f);
            Cue(previous, SlimeWake, "enemy.slime_moss.trail", 0.44f, 0.79f);
            Render();
            return _time < duration;
        }

        void Cue(float previous, float at, string id, float volume, float pitch)
        {
            if (previous < at && _time >= at) _context.Sound?.Play(id, volume: volume, pitch: pitch);
        }

        void Render()
        {
            float t = _time;
            FrameCamera(t);
            float closing = Curve(DoorShut - 0.13f, DoorShut, t);
            _door.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 26f * closing);
            _glow.color = Alpha(Amber, 0.30f * (1f - Curve(DoorShut - 0.06f, DoorShut + 0.15f, t)));
            _fieldLantern.color = Alpha(Amber, 0.15f + 0.022f * Mathf.Sin(t * 7f) * _motion);
            _fog.color = new Color(0.51f, 0.62f, 0.78f, 0.24f);
            _fog.rectTransform.anchoredPosition = new Vector2(325f + 52f * t * _motion, -432f - 13f * t * _motion);
            _master.sprite = Frame(_masterClip, t < MasterCut ? (int)(t * 5f) % 4 :
                t < DoorCut ? 6 + (int)(t * 4f) % 2 : 0);
            _master.color = Color.Lerp(new Color32(249, 235, 212, 255), new Color32(132, 148, 181, 255), closing);

            for (int i = 0; i < _disciples.Length; i++)
            {
                float start = FieldCut + i * 0.23f;
                float stop = 8.55f + i * 0.34f;
                float depart = Curve(start, stop, t);
                Vector2 feet = Vector2.Lerp(AtTheTower[i], InTheField[i], depart);
                feet.x += Mathf.Sin(depart * Mathf.PI) * (i % 2 == 0 ? 38f : -31f);
                bool player = i == 0;
                bool walking = depart > 0f && depart < 1f;
                bool aiming = player && t >= 9.36f && t < 10.15f;
                Actor.Pose pose = t < start ? Actor.Pose.Watch :
                    aiming ? Actor.Pose.Cast : walking ? Actor.Pose.Walk :
                    player || i == 3 ? Actor.Pose.Right : Actor.Pose.Rest;
                float poseTime = aiming ? t - 9.36f : walking ? t - start : t + i * 0.23f;
                Color grade = t < DoorCut || player ? new Color32(246, 236, 228, 255) : new Color32(171, 183, 202, 255);
                _disciples[i].Draw(feet, poseTime, pose, player ? 1.28f : 1.16f, grade);
            }

            float rise = Curve(SlimeWake - 0.15f, SlimeWake + 0.5f, t);
            float lurch = Curve(SlimeWake + 0.23f, SlimeWake + 0.83f, t);
            _slime.enabled = t >= FieldCut;
            _slimeShadow.enabled = _slime.enabled;
            _slime.color = Color.Lerp(new Color32(86, 104, 114, 255), new Color32(188, 223, 183, 255), rise);
            _slime.sprite = _slimeClip.At(Mathf.Max(0f, t - SlimeWake));
            _slime.rectTransform.localScale = new Vector3(Mathf.Lerp(1.19f, 1f, rise), Mathf.Lerp(0.5f, 1f, rise), 1f);
            _slime.rectTransform.anchoredPosition = new Vector2(1271f - 57f * lurch, -780f + 19f * lurch);
            _slimeShadow.rectTransform.anchoredPosition = _slime.rectTransform.anchoredPosition + new Vector2(-48f, 8f);
            _slimeShadow.color = new Color(0.015f, 0.025f, 0.04f, 0.38f);

            float light = Curve(9.39f, 9.63f, t) * (1f - Curve(Fizzle - 0.02f, Fizzle + 0.2f, t));
            Vector2 wandTip = _disciples[0].WandTip;
            _spark.rectTransform.anchoredPosition = wandTip + new Vector2(-29f, 29f);
            _spark.color = Alpha(Amber, 0.77f * light);
            float smokeAge = t - Fizzle;
            _smoke.enabled = smokeAge >= 0f && smokeAge < 0.7f && _smokeClip?.At(smokeAge) != null;
            if (_smoke.enabled)
            {
                _smoke.sprite = _smokeClip.At(smokeAge);
                Place(_smoke, _smoke.sprite, wandTip.x + smokeAge * 16f, -wandTip.y - smokeAge * 23f, 0.78f);
                _smoke.color = Alpha(new Color32(184, 194, 218, 255), 1f - Curve(0.18f, 0.7f, smokeAge));
            }

            int caption = t >= 2.16f && t < DoorCut ? 1 : t >= 12.2f && t < 14.52f ? 2 : 0;
            if (caption != _captionIndex)
            {
                _captionIndex = caption;
                _caption.SetText(caption == 0 ? string.Empty :
                    _context.Names?.Get("str.tutorial.opening.line." + caption) ?? string.Empty);
            }
            _fade.color = Alpha(Ink, Mathf.Max(1f - Curve(0f, 0.65f, t), Curve(duration - 0.58f, duration, t)));
        }

        void FrameCamera(float t)
        {
            Vector2 focus, drift;
            float zoom, zoomChange, beat, foreground;
            if (t < MasterCut)
            {
                focus = new Vector2(1004f, 310f);
                drift = new Vector2(-26f, 14f);
                zoom = 1.74f; zoomChange = 0.07f;
                beat = Curve(0f, MasterCut, t);
                foreground = 0f;
            }
            else if (t < DoorCut)
            {
                focus = towerFocus;
                drift = new Vector2(-32f, 8f);
                zoom = 2.37f; zoomChange = 0.19f;
                beat = Curve(MasterCut, DoorCut, t);
                foreground = 34f;
            }
            else if (t < FieldCut)
            {
                focus = new Vector2(959f, 257f);
                drift = Vector2.zero;
                zoom = 5.22f; zoomChange = 0f;
                beat = 0f;
                foreground = -220f;
            }
            else if (t < SlimeCut)
            {
                focus = fieldFocus;
                drift = new Vector2(20f, 82f);
                zoom = 1.92f; zoomChange = 0.32f;
                beat = Curve(FieldCut, SlimeCut - 0.4f, t);
                foreground = -45f;
            }
            else
            {
                focus = slimeFocus;
                drift = new Vector2(-51f, -44f);
                zoom = 2.74f; zoomChange = -1.22f;
                beat = Curve(12.15f, duration - 0.45f, t);
                foreground = 20f - 160f * beat * _motion;
            }

            focus += drift * (beat * _motion);
            zoom += zoomChange * beat * _motion;
            _stage.localScale = new Vector3(zoom, zoom, 1f);
            _stage.anchoredPosition = new Vector2(960f - focus.x * zoom, -540f + focus.y * zoom);

            _ruins.enabled = distantRuins != null && t < MasterCut;
            _ruins.rectTransform.anchoredPosition = new Vector2(-92f + 54f * beat * _motion, 26f - 14f * beat * _motion);
            _leftPine.rectTransform.anchoredPosition = new Vector2(-180f + foreground - beat * 46f * _motion, -176f);
            _rightPine.rectTransform.anchoredPosition = new Vector2(1738f - foreground + beat * 42f * _motion, -128f);
            _passingPine.enabled = foregroundPine != null && _motion >= 0.99f && t >= FieldCut - 0.29f && t <= FieldCut + 0.38f;
            if (_passingPine.enabled)
            {
                float pass = Mathf.InverseLerp(FieldCut - 0.29f, FieldCut + 0.38f, t);
                _passingPine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-1810f, 2040f, pass), 3060f);
            }
        }

        static RawImage Plate(RectTransform parent, string name, Texture2D texture, float x, float y, float w, float h)
        {
            var image = UiKit.Node(parent, name, x, y, w, h).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.enabled = texture != null;
            image.raycastTarget = false;
            return image;
        }

        void OnDestroy()
        {
            if (_mount != null) Destroy(_mount.gameObject);
#if UNITY_EDITOR
            if (_previewCanvas != null)
            {
                Destroy(_previewCanvas);
                UnitySoundPlayer.Release();
            }
            _previewArt?.Dispose();
#endif
        }

        public void Hide()
        {
            if (_mount != null) _mount.gameObject.SetActive(false);
        }

        Image Layer(string name, Sprite sprite, float x, float y, float scale)
        {
            var image = UiKit.Image(_stage, sprite, 0f, 0f, 0f, 0f, preserveAspect: false);
            image.name = name;
            Place(image, sprite, x, y, scale);
            return image;
        }

        static void Place(Image image, Sprite sprite, float x, float y, float scale)
        {
            if (sprite == null) return;
            var rect = image.rectTransform;
            rect.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            float logical = scale * 64f / sprite.pixelsPerUnit;
            rect.sizeDelta = sprite.rect.size * logical;
            rect.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
        }

        static Image GroundShadow(RectTransform stage, float x, float y, float width)
        {
            var shadow = UiKit.Image(stage, UiBackdrop.RadialSoft, x - width / 2f, y - 9f, width, 20f,
                new Color(0.015f, 0.02f, 0.02f, 0.58f), false);
            shadow.name = "Contact shadow";
            return shadow;
        }

        static Sprite Frame(SpriteAnim clip, int index) => clip?.Frames != null && clip.Frames.Length > 0
            ? clip.Frames[Mathf.Clamp(index, 0, clip.Frames.Length - 1)] : null;

        static float Curve(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        sealed class Actor
        {
            public enum Pose { Watch, Walk, Rest, Right, Cast }

            readonly Clip[] _clips;
            readonly PixelPoint _wandHead, _grip;
            readonly Image _body, _hand, _shadow;
            public Vector2 WandTip { get; private set; }

            public Actor(RectTransform stage, IArtLibrary art, string id, int wandStage)
            {
                _clips = new[]
                {
                    new Clip(art, id, wandStage, "idle", Facing3.Up),
                    new Clip(art, id, wandStage, "walk", Facing3.Down),
                    new Clip(art, id, wandStage, "idle", Facing3.Down),
                    new Clip(art, id, wandStage, "idle", Facing3.Right),
                    new Clip(art, id, wandStage, "fire", Facing3.Right, true)
                };
                _grip = art.WandGrip;
                _wandHead = art.WandStageInfo(id, wandStage)?.Head ?? new PixelPoint(16, 8);
                _shadow = GroundShadow(stage, 0f, 0f, 52f);
                _hand = UiKit.Image(stage, null, 0f, 0f, 0f, 0f, Color.white, false);
                _hand.name = id + " wand";
                _body = UiKit.Image(stage, null, 0f, 0f, 0f, 0f, Color.white, false);
                _body.name = id;
            }

            public void Draw(Vector2 feet, float seconds, Pose pose, float scale, Color color)
            {
                _shadow.rectTransform.anchoredPosition = new Vector2(Mathf.Round(feet.x - 26f), -Mathf.Round(feet.y - 9f));
                var clip = _clips[(int)pose];
                var sprite = clip.Animation?.At(seconds);
                _body.sprite = sprite;
                _body.color = color;
                _body.enabled = sprite != null;
                Place(_body, sprite, feet.x, feet.y, scale);
                _hand.enabled = clip.Wand != null && clip.Info != null && clip.Info.Frames.Count > 0 && sprite != null;
                if (!_hand.enabled) return;

                int frameIndex = System.Array.IndexOf(clip.Animation.Frames, sprite);
                var frame = clip.Info.Frames[Mathf.Clamp(frameIndex, 0, clip.Info.Frames.Count - 1)];
                Vector2 hand = feet + new Vector2(frame.Hand.X - frame.Feet.X, frame.Hand.Y - frame.Feet.Y) * scale;
                WandTip = new Vector2(hand.x + (_wandHead.X - _grip.X) * scale,
                    -hand.y - (_wandHead.Y - _grip.Y) * scale);
                _hand.sprite = clip.Wand;
                _hand.color = color;
                int bodyIndex = _body.transform.GetSiblingIndex();
                int handIndex = _hand.transform.GetSiblingIndex();
                if ((pose == Pose.Watch) != (handIndex < bodyIndex)) _hand.transform.SetSiblingIndex(bodyIndex);
                Place(_hand, clip.Wand, hand.x, hand.y, scale);
            }

            readonly struct Clip
            {
                public readonly SpriteAnim Animation;
                public readonly PlayerClip Info;
                public readonly Sprite Wand;

                public Clip(IArtLibrary art, string id, int stage, string key, Facing3 direction, bool aim = false)
                {
                    Animation = art.CharacterClip(id, key, direction);
                    Info = art.CharacterClipInfo(id, key, direction);
                    Wand = art.WandHand(id, stage, direction, aim);
                }
            }
        }
    }
}
