Shader "REmind/Notes/Box Note"
{
    Properties
    {
        [NoScaleOffset] _TopMap("Top (Top / Bottom)", 2D) = "white" {}
        [NoScaleOffset] _FrontMap("Front (Front / Back)", 2D) = "white" {}
        [NoScaleOffset] _SideMap("Side (Left / Right)", 2D) = "white" {}
        _TopColor("Top Color", Color) = (0.09, 0.14, 0.3, 0.35)
        _SideTopColor("Side Top Color", Color) = (0.16, 0.23, 0.5, 0.75)
        _SideBottomColor("Side Bottom Color", Color) = (0.025, 0.04, 0.12, 0.85)
        [HDR] _EdgeColor("Edge Color", Color) = (0.65, 0.75, 1, 1)
        _EdgeIntensity("Edge Intensity", Range(0, 10)) = 4
        _BorderWidth("Border Width (World Units)", Range(0.001, 0.2)) = 0.035
        _GlowWidth("Inner Glow Width (World Units)", Range(0.001, 0.5)) = 0.14
        _SideEdgeStrength("Side Edge Strength", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Back
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TopMap);
            SAMPLER(sampler_TopMap);
            TEXTURE2D(_FrontMap);
            SAMPLER(sampler_FrontMap);
            TEXTURE2D(_SideMap);
            SAMPLER(sampler_SideMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _SideTopColor;
                half4 _SideBottomColor;
                half4 _EdgeColor;
                float _EdgeIntensity;
                float _BorderWidth;
                float _GlowWidth;
                float _SideEdgeStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 faceCoordinates : TEXCOORD0;
                half height : TEXCOORD1;
                half topFace : TEXCOORD2;
                float2 faceUV : TEXCOORD3;
                half faceKind : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);

                float3 axisX = unity_ObjectToWorld._m00_m10_m20;
                float3 axisY = unity_ObjectToWorld._m01_m11_m21;
                float3 axisZ = unity_ObjectToWorld._m02_m12_m22;
                float3 normal = abs(input.normalOS);
                float2 facePosition = normal.y > 0.5 ? input.positionOS.xz :
                    (normal.x > 0.5 ? input.positionOS.zy : input.positionOS.xy);
                float3 faceU = normal.x > 0.5 ? axisZ : axisX;
                float3 faceV = normal.y > 0.5 ? axisZ : axisY;
                // Perpendicular distances also compensate shear from a rotated
                // child under a nonuniformly scaled parent.
                float faceArea = length(cross(faceU, faceV));
                float2 faceSize = faceArea /
                    max(float2(length(faceV), length(faceU)), 0.000001);

                // Keep signed face coordinates until the fragment stage. Taking abs
                // at the cube's vertices would interpolate zero across the whole face.
                output.faceCoordinates = float4(facePosition * faceSize, faceSize * 0.5);
                output.height = input.positionOS.y + 0.5;
                output.topFace = step(0.5, normal.y);
                output.faceKind = normal.y > 0.5 ? 0.0 : (normal.z > 0.5 ? 1.0 : 2.0);
                float2 faceUV = normal.y > 0.5
                    ? float2(input.normalOS.y * input.positionOS.x, input.positionOS.z)
                    : (normal.x > 0.5
                        ? float2(input.normalOS.x * input.positionOS.z, input.positionOS.y)
                        : float2(-input.normalOS.z * input.positionOS.x, input.positionOS.y));
                output.faceUV = faceUV + 0.5;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 distanceToEdge = input.faceCoordinates.zw -
                    abs(input.faceCoordinates.xy);
                float distance = min(distanceToEdge.x, distanceToEdge.y);
                float antialias = max(fwidth(distance), 0.00001);
                float border = 1.0 - smoothstep(_BorderWidth - antialias,
                    _BorderWidth + antialias, distance);
                float glow = 1.0 - smoothstep(_BorderWidth,
                    _BorderWidth + _GlowWidth, distance);
                half4 fill = lerp(_SideBottomColor, _SideTopColor, input.height);
                fill = lerp(fill, _TopColor, input.topFace);
                if (input.faceKind < 0.5)
                    fill *= SAMPLE_TEXTURE2D(_TopMap, sampler_TopMap, saturate(input.faceUV));
                else if (input.faceKind < 1.5)
                    fill *= SAMPLE_TEXTURE2D(_FrontMap, sampler_FrontMap, saturate(input.faceUV));
                else
                    fill *= SAMPLE_TEXTURE2D(_SideMap, sampler_SideMap, saturate(input.faceUV));
                half strength = lerp(_SideEdgeStrength, 1.0, input.topFace);
                half3 color = fill.rgb + _EdgeColor.rgb * glow * glow * strength * 0.35;
                color = lerp(color, _EdgeColor.rgb * _EdgeIntensity * strength, border);
                return half4(color, lerp(fill.a, 1.0, border));
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
