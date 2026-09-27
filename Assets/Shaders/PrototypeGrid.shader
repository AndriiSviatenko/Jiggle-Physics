Shader "Parkour/PrototypeGrid"
{
    Properties
    {
        _Color ("Base", Color) = (0.24, 0.26, 0.32, 1)
        _LineColor ("Line", Color) = (0.42, 0.46, 0.55, 1)
        _MajorColor ("Major", Color) = (0.95, 0.45, 0.15, 1)
        _Cell ("Cell meters", Float) = 1.0
        _MajorEvery ("Major every", Float) = 10
        _LineStrength ("Line strength", Range(0,1)) = 0.75
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            float4 _Color; float4 _LineColor; float4 _MajorColor; float _Cell; float _MajorEvery; float _LineStrength;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float GridMask(float2 uv, float cell, float thickness)
            {
                float2 coord = uv / cell;
                float2 grid = abs(frac(coord - 0.5) - 0.5) / max(fwidth(coord), 1e-5);
                return 1.0 - saturate(min(grid.x, grid.y) / max(thickness, 1e-4));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 absolute = abs(normalize(input.normalWS));
                float2 uv;
                if (absolute.y >= absolute.x && absolute.y >= absolute.z) uv = input.positionWS.xz;
                else if (absolute.x >= absolute.z) uv = input.positionWS.zy;
                else uv = input.positionWS.xy;

                float minor = GridMask(uv, _Cell, 1.0);
                float major = GridMask(uv, _Cell * max(_MajorEvery, 2.0), 1.4);

                float3 albedo = _Color.rgb;
                albedo = lerp(albedo, _LineColor.rgb, minor * _LineStrength);
                albedo = lerp(albedo, _MajorColor.rgb, major);

                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normalize(input.normalWS), mainLight.direction));
                float3 lighting = mainLight.color * (ndotl * 0.85 + 0.35) + unity_AmbientSky.rgb * 1.2;
                return half4(albedo * lighting, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct ShadowAttributes { float4 positionOS : POSITION; };
            struct ShadowVaryings { float4 positionCS : SV_POSITION; };

            ShadowVaryings shadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 depthFrag(DepthVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
