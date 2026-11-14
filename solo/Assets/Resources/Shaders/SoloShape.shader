// 솔로 도형: 텍스처 없이 정점 색 × _Color(알파 혼합). 코드로 만든 메시(예고 구역의 원·부채·띠·고리)용.
// 정점 색(Mesh.colors / colors32)은 코드의 색 상수와 같은 sRGB 값으로 넣는다. 엔진은 메시의 정점 색을 색 공간에 맞춰 바꿔 주지 않으므로
// Linear 색 공간에서는 여기서 선형으로 바꾼다(안 바꾸면 위험색 #FF3B30이 #FF8478로 뜬다). _Color는 Color 속성이라 엔진이 바꿔 준다.
// ArtLibrary.Material(SpriteMaterialKind.Shape). 조명 없음(LightMode 없는 패스 = SRPDefaultUnlit).
Shader "Solo/Shape"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            UNITY_INSTANCING_BUFFER_START(SoloShapeProps)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_INSTANCING_BUFFER_END(SoloShapeProps)

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                fixed4 vc = IN.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                vc.rgb = GammaToLinearSpace(vc.rgb);
                #endif
                OUT.color = vc * UNITY_ACCESS_INSTANCED_PROP(SoloShapeProps, _Color);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                return IN.color;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
