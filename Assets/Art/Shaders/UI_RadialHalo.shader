Shader "ReMind/UI/Radial Halo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; };
            float4 _Color, _ClipRect;
            v2f vert(appdata v)
            {
                v2f o; o.local = v.vertex; o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color; o.uv = v.uv; return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float falloff = saturate((exp(-6 * dot(p,p)) - exp(-6.0)) / (1 - exp(-6.0)));
                float4 c = i.color; c.a *= falloff;
                // Dither low-alpha gradients before an 8-bit canvas target quantizes them.
                float noise = frac(52.9829189 * frac(dot(i.vertex.xy, float2(0.06711056, 0.00583715)))) - 0.5;
                c.a = falloff > 0 ? saturate(c.a + noise / 255.0) : 0;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
