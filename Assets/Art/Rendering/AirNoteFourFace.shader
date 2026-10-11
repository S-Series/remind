Shader "REmind/Notes/Air Note Four Face"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _ViewRotation("View Rotation XYZ", Vector) = (-90, 180, 180, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _ViewRotation;
            CBUFFER_END

            float3 RotateX(float3 position, float angle)
            {
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float3(
                    position.x,
                    cosine * position.y - sine * position.z,
                    sine * position.y + cosine * position.z);
            }

            float3 RotateY(float3 position, float angle)
            {
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float3(
                    cosine * position.x + sine * position.z,
                    position.y,
                    -sine * position.x + cosine * position.z);
            }

            float3 RotateZ(float3 position, float angle)
            {
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float3(
                    cosine * position.x - sine * position.y,
                    sine * position.x + cosine * position.y,
                    position.z);
            }

            float3 ApplyViewRotation(float3 position)
            {
                float3 angles = radians(_ViewRotation.xyz);

                // Quaternion.Euler와 동일한 Z -> X -> Y 적용 순서입니다.
                position = RotateZ(position, angles.z);
                position = RotateX(position, angles.x);
                return RotateY(position, angles.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 objectScale = float3(
                    length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21),
                    length(unity_ObjectToWorld._m02_m12_m22));
                float3 safeScale = max(objectScale, 0.000001);
                float3 scaledPosition = input.positionOS.xyz * objectScale;
                float3 rotatedPosition = ApplyViewRotation(scaledPosition);

                // ObjectToWorld가 Scale을 다시 적용하므로 먼저 나눠서
                // Transform의 R * S가 아닌 Material의 R * S 순서를 보존합니다.
                output.positionHCS = TransformObjectToHClip(
                    rotatedPosition / safeScale);
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    input.uv) * input.color;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
