Shader "Custom/RoundedSpriteCorners2D"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _RadiusTL ("Radius TL", Range(0,0.5)) = 0.2
        _RadiusTR ("Radius TR", Range(0,0.5)) = 0.2
        _RadiusBL ("Radius BL", Range(0,0.5)) = 0.2
        _RadiusBR ("Radius BR", Range(0,0.5)) = 0.2

        _Smooth ("Edge Smooth", Range(0,0.1)) = 0.02

        _UVMin ("UV Min", Vector) = (0,0,0,0)
        _UVMax ("UV Max", Vector) = (1,1,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderPipeline"="UniversalRenderPipeline"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Color;

            float4 _UVMin;
            float4 _UVMax;

            float _RadiusTL;
            float _RadiusTR;
            float _RadiusBL;
            float _RadiusBR;
            float _Smooth;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

                        half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // CHUYỂN UV GLOBAL -> UV LOCAL CỦA SPRITE (0..1 TRONG RECT CỦA MẢNH)
                float2 uvSize = max(_UVMax.xy - _UVMin.xy, float2(1e-5, 1e-5));
                float2 uvLocal = (uv - _UVMin.xy) / uvSize;
                uvLocal = saturate(uvLocal);

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * IN.color;

                float mask = 1.0;
                float s = max(_Smooth, 1e-4);

                // top-left corner (uvLocal 0..1)
                if (_RadiusTL > 0.0 && uvLocal.x < _RadiusTL && uvLocal.y > 1.0 - _RadiusTL)
                {
                    float2 c = float2(_RadiusTL, 1.0 - _RadiusTL);
                    float dist = length(uvLocal - c);
                    float m = saturate((_RadiusTL - dist) / s);
                    mask = min(mask, m);
                }

                // top-right corner
                if (_RadiusTR > 0.0 && uvLocal.x > 1.0 - _RadiusTR && uvLocal.y > 1.0 - _RadiusTR)
                {
                    float2 c = float2(1.0 - _RadiusTR, 1.0 - _RadiusTR);
                    float dist = length(uvLocal - c);
                    float m = saturate((_RadiusTR - dist) / s);
                    mask = min(mask, m);
                }

                // bottom-left corner
                if (_RadiusBL > 0.0 && uvLocal.x < _RadiusBL && uvLocal.y < _RadiusBL)
                {
                    float2 c = float2(_RadiusBL, _RadiusBL);
                    float dist = length(uvLocal - c);
                    float m = saturate((_RadiusBL - dist) / s);
                    mask = min(mask, m);
                }

                // bottom-right corner
                if (_RadiusBR > 0.0 && uvLocal.x > 1.0 - _RadiusBR && uvLocal.y < _RadiusBR)
                {
                    float2 c = float2(1.0 - _RadiusBR, _RadiusBR);
                    float dist = length(uvLocal - c);
                    float m = saturate((_RadiusBR - dist) / s);
                    mask = min(mask, m);
                }

                col.a *= mask;
                clip(col.a - 0.001);

                return col;
            }


            ENDHLSL
        }
    }
}
