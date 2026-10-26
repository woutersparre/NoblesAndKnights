Shader "SphereVoronoi/VertexColorLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient", Range(0, 1)) = 0.32
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Ambient;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            half4 color : COLOR;
        };

        Varyings VertexColorVert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.color = input.color * _BaseColor;
            return output;
        }

        half4 VertexColorFrag(Varyings input) : SV_Target
        {
            float3 normalWS = normalize(input.normalWS);
            float3 keyLight = normalize(float3(0.35, 0.55, -0.75));
            float diffuse = saturate(dot(normalWS, keyLight));
            float shade = _Ambient + (1.0 - _Ambient) * diffuse;
            return half4(input.color.rgb * shade, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex VertexColorVert
            #pragma fragment VertexColorFrag
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex VertexColorVert
            #pragma fragment VertexColorFrag
            ENDHLSL
        }
    }

    Fallback Off
}
