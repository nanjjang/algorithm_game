// 솔로 스프라이트 가산 변형. 32px 이하 그림에만 쓴다(10 요약서 3.2절. 지름 2칸 이상 가산 금지는 쓰는 쪽 규칙).
// ArtLibrary.Material(SpriteMaterialKind.SpriteAdditive). 조명 없음(LightMode 없는 패스 = SRPDefaultUnlit).
Shader "Solo/SpriteAdditive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineSize ("Outline Size (texels)", Range(0, 4)) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        // 곱한 색을 더한다(알파는 남기지 않는다).
        Blend One One, Zero One

        Pass
        {
            CGPROGRAM
            #pragma vertex SoloSpriteVert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "SoloSpriteCommon.cginc"

            fixed4 frag(solo_v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                fixed4 c = SoloSpriteColor(IN);
                return fixed4(c.rgb * c.a, c.a);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
