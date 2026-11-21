using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Settings
{
    /// <summary>
    /// 설정의 작은 전장 그림 424×240(D-71). 대체 도형으로 지금 값을 바로 보여 준다:
    /// 원형 예고(차오르는 채움 × telegraph_fill, 테두리, 색각 보조 줄무늬, 흰 번쩍 = 연출 단계에 따라 구역 전체 / 굵은 테두리),
    /// 적 탄(색각 보조면 주황 줄무늬), 내 탄(× my_bullet_opacity). 위험색은 이 그림의 '적 예고·적 탄' 도형에만 쓴다.
    /// 색은 DataSolo/G15.json colors(적 표시와 같은 값). 흰 번쩍은 1.5초에 한 번 0.1초(광과민 한도 14.1절 안).
    /// </summary>
    public sealed class SettingsPreviewView
    {
        public const float W = 424f, H = 240f;
        const float ZoneRadius = 72f, ShotRadius = 10f, BulletRadius = 8f;
        const float CycleSeconds = 1.5f, FlashStart = 1.3f, FlashSeconds = 0.1f;
        const float FlashAlphaFull = 0.35f;

        public RectTransform Root { get; private set; }
        UImage _fill, _ring, _flashZone, _flashBorder, _shot, _shotStripe, _bullet, _bullet2;
        RectTransform _stripes, _fillMask;
        Color _danger = new Color(1f, 0.231f, 0.188f), _shotBody = new Color(1f, 0.18f, 0.533f), _stripeColor = Color.black,
            _shotStripeColor = new Color(1f, 0.624f, 0.11f);
        float _stripeAlpha = 0.45f;
        SettingsPreview _v;
        float _t;

        public SettingsPreviewView(GameContext ctx, RectTransform parent, float x, float y)
        {
            ReadColors(ctx != null ? ctx.Content : null);
            Root = UiKit.Node(parent, "SettingsPreview", x, y, W, H);
            UiKit.Rect(Root, 0f, 0f, W, H, UiTheme.Ink, false, "Field");
            var art = ctx != null ? ctx.Art : null;
            Sprite disc = art != null ? art.Circle((int)ZoneRadius) : null;
            Sprite ring = art != null ? art.Circle((int)ZoneRadius, true, 3) : null;
            Sprite thick = art != null ? art.Circle((int)ZoneRadius, true, 8) : null;
            Sprite shot = art != null ? art.Circle((int)ShotRadius) : null;
            Sprite bullet = art != null ? art.Circle((int)BulletRadius) : null;

            float cx = 120f, cy = H * 0.5f, d = ZoneRadius * 2f;
            // 차오르는 채움: 마스크 원 안에서 크기가 커진다. 줄무늬는 채움 안에만.
            _fillMask = UiKit.Node(Root, "ZoneMask", cx - ZoneRadius, cy - ZoneRadius, d, d);
            var maskImg = _fillMask.gameObject.AddComponent<UImage>();
            maskImg.sprite = disc;
            maskImg.color = Color.white;
            maskImg.raycastTarget = false;
            var mask = _fillMask.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = false;
            _fill = UiKit.Image(_fillMask, disc, 0f, 0f, d, d, _danger, false);
            _fill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _stripes = UiKit.Node(_fill.rectTransform, "Stripes", 0f, 0f, d, d);
            const float StripeW = 4f, Pitch = 16f;
            for (float sx = -d; sx < d * 2f; sx += Pitch)
            {
                var s = UiKit.Rect(_stripes, sx, -d * 0.5f, StripeW, d * 2f, _stripeColor);
                s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            _ring = UiKit.Image(Root, ring, cx - ZoneRadius, cy - ZoneRadius, d, d, _danger, false);
            _flashZone = UiKit.Image(Root, disc, cx - ZoneRadius, cy - ZoneRadius, d, d, new Color(1f, 1f, 1f, FlashAlphaFull), false);
            _flashBorder = UiKit.Image(Root, thick, cx - ZoneRadius, cy - ZoneRadius, d, d, Color.white, false);

            // 적 탄(오른쪽 위) — 색각 보조면 주황 줄 하나.
            _shot = UiKit.Image(Root, shot, 270f - ShotRadius, 64f - ShotRadius, ShotRadius * 2f, ShotRadius * 2f, _shotBody, false);
            _shotStripe = UiKit.Rect(_shot.rectTransform, ShotRadius - 1f, 0f, 2f, ShotRadius * 2f, _shotStripeColor);
            _shotStripe.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // 내 탄(오른쪽 아래, 두 개) — 플레이어 쪽 색(위험색 아님).
            _bullet = UiKit.Image(Root, bullet, 320f, 160f, BulletRadius * 2f, BulletRadius * 2f, UiTheme.ArcaneTeal, false);
            _bullet2 = UiKit.Image(Root, bullet, 360f, 140f, BulletRadius * 2f, BulletRadius * 2f, UiTheme.ArcaneTeal, false);
        }

        void ReadColors(IContentDb db)
        {
            var j = db != null ? db.SoloExtra("G15") : null;
            var c = j != null ? j["colors"] as JObject : null;
            _danger = Hex(c, "danger_zone_red", _danger);
            _shotBody = Hex(c, "danger_bullet_body", _shotBody);
            _stripeColor = Hex(c, "colorblind_stripe", _stripeColor);
            _shotStripeColor = Hex(c, "colorblind_shot_stripe", _shotStripeColor);
            var tg = j != null ? j["telegraph"] as JObject : null;
            if (tg != null && tg["colorblind_stripe_alpha"] != null && (tg["colorblind_stripe_alpha"].Type == JTokenType.Float
                || tg["colorblind_stripe_alpha"].Type == JTokenType.Integer))
                _stripeAlpha = (float)tg["colorblind_stripe_alpha"];
        }

        static Color Hex(JObject o, string key, Color fallback)
        {
            var t = o != null ? o[key] : null;
            if (t == null || t.Type != JTokenType.String) return fallback;
            return ColorUtility.TryParseHtmlString((string)t, out var c) ? c : fallback;
        }

        public void SetVisible(bool on) { if (Root != null) Root.gameObject.SetActive(on); }

        public void Apply(SettingsPreview v)
        {
            _v = v;
            Draw();
        }

        public void Tick(float dt)
        {
            if (Root == null || !Root.gameObject.activeSelf) return;
            _t += dt;
            if (_t >= CycleSeconds) _t -= CycleSeconds;
            Draw();
        }

        void Draw()
        {
            if (_fill == null) return;
            // 채움은 한 주기 동안 가운데에서 차오른다(채움 진하기 = 알파).
            float grow = Mathf.Clamp01(_t / FlashStart);
            _fill.rectTransform.localScale = new Vector3(grow, grow, 1f);
            _fill.color = new Color(_danger.r, _danger.g, _danger.b, _v.TelegraphFillAlpha);
            _stripes.gameObject.SetActive(_v.Stripes);
            var imgs = _stripes.GetComponentsInChildren<UImage>(true);
            for (int i = 0; i < imgs.Length; i++) imgs[i].color = new Color(_stripeColor.r, _stripeColor.g, _stripeColor.b, _stripeAlpha);

            bool flashing = _t >= FlashStart && _t < FlashStart + FlashSeconds;
            _flashZone.gameObject.SetActive(flashing && _v.FlashWholeZone);
            _flashBorder.gameObject.SetActive(flashing && _v.FlashThickBorder);

            _shotStripe.gameObject.SetActive(_v.Stripes);
            var bc = UiTheme.WithAlpha(UiTheme.ArcaneTeal, _v.MyBulletAlpha);
            _bullet.color = bc;
            _bullet2.color = bc;
        }
    }
}
