// 솔로 스프라이트 셰이더 공용(00 문서 7·10절, D-70). 텍스처 × 정점 색 × _Color, 단색 덮기, 외곽선. 조명 없음.
// 속성 이름은 Wizard.Solo.Art.SpriteShaderProps와 같다: _FlashColor, _FlashAmount, _OutlineColor, _OutlineSize(텍셀).
// 개체별 값은 MaterialPropertyBlock으로 받고, 인스턴싱이 켜지면 인스턴스 속성으로 읽는다.
#ifndef SOLO_SPRITE_COMMON_INCLUDED
#define SOLO_SPRITE_COMMON_INCLUDED

#include "UnityCG.cginc"
#include "UnitySprites.cginc"

UNITY_INSTANCING_BUFFER_START(SoloSpriteProps)
    UNITY_DEFINE_INSTANCED_PROP(fixed4, _FlashColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
    UNITY_DEFINE_INSTANCED_PROP(fixed4, _OutlineColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _OutlineSize)
UNITY_INSTANCING_BUFFER_END(SoloSpriteProps)

float4 _MainTex_TexelSize;

// 외곽선 두께 상한(텍셀). 엘리트 금색 2px가 쓰는 범위보다 넉넉하게.
#define SOLO_OUTLINE_MAX 4

struct solo_appdata
{
    float4 vertex   : POSITION;
    float4 color    : COLOR;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct solo_v2f
{
    float4 vertex   : SV_POSITION;
    fixed4 color    : COLOR;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

solo_v2f SoloSpriteVert(solo_appdata IN)
{
    solo_v2f OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    OUT.vertex = UnityFlipSprite(IN.vertex, _Flip);
    OUT.vertex = UnityObjectToClipPos(OUT.vertex);
    OUT.texcoord = IN.texcoord;
    OUT.color = IN.color * _Color * _RendererColor;
    #ifdef PIXELSNAP_ON
    OUT.vertex = UnityPixelSnap(OUT.vertex);
    #endif
    return OUT;
}

// 곧은 알파(straight) 결과. 호출하는 패스가 혼합에 맞게 곱한다.
fixed4 SoloSpriteColor(solo_v2f IN)
{
    fixed4 flashColor = UNITY_ACCESS_INSTANCED_PROP(SoloSpriteProps, _FlashColor);
    float flashAmount = saturate(UNITY_ACCESS_INSTANCED_PROP(SoloSpriteProps, _FlashAmount));
    fixed4 outlineColor = UNITY_ACCESS_INSTANCED_PROP(SoloSpriteProps, _OutlineColor);
    float outlineSize = UNITY_ACCESS_INSTANCED_PROP(SoloSpriteProps, _OutlineSize);

    fixed4 tex = SampleSpriteTexture(IN.texcoord);
    fixed4 c = tex * IN.color;
    c.rgb = lerp(c.rgb, flashColor.rgb, flashAmount);

    if (outlineSize > 0.5)
    {
        // 둘레 8방향을 1~두께 텍셀까지 본다: 지금 칸이 비고 이웃이 차 있으면 외곽선.
        float neighbour = 0;
        [unroll]
        for (int i = 1; i <= SOLO_OUTLINE_MAX; i++)
        {
            if (i <= outlineSize)
            {
                float2 dx = float2(_MainTex_TexelSize.x * i, 0);
                float2 dy = float2(0, _MainTex_TexelSize.y * i);
                neighbour = max(neighbour, tex2D(_MainTex, IN.texcoord + dx).a);
                neighbour = max(neighbour, tex2D(_MainTex, IN.texcoord - dx).a);
                neighbour = max(neighbour, tex2D(_MainTex, IN.texcoord + dy).a);
                neighbour = max(neighbour, tex2D(_MainTex, IN.texcoord - dy).a);
                neighbour = max(neighbour, tex2D(_MainTex, IN.texcoord + dx + dy).a);
                neighbour = max(neighbour, tex2D(_MainTex, IN.texcoord - dx - dy).a);
                neighbour = max(neighbour, tex2D(_MainTex, IN.texcoord + dx - dy).a);
                neighbour = max(neighbour, tex2D(_MainTex, IN.texcoord - dx + dy).a);
            }
        }
        float edge = saturate(neighbour - tex.a) * outlineColor.a * IN.color.a;
        // 그림 아래에 외곽선을 깐다(곧은 알파 합성).
        float a = c.a + edge * (1 - c.a);
        float3 rgb = a > 0 ? (c.rgb * c.a + outlineColor.rgb * edge * (1 - c.a)) / a : c.rgb;
        c = fixed4(rgb, a);
    }
    return c;
}

#endif
