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

        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width (UV)", Range(0,0.2)) = 0.02
        _BorderMask ("Border Mask (L,R,T,B)", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
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

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _UVMin;
                float4 _UVMax;
                float  _RadiusTL;
                float  _RadiusTR;
                float  _RadiusBL;
                float  _RadiusBR;
                float  _Smooth;
                float4 _OutlineColor;
                float  _OutlineWidth;
                float4 _BorderMask;    // x = left, y = right, z = top, w = bottom
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // UV global -> local UV 0..1 trong rect của sprite
                float2 uvSize = max(_UVMax.xy - _UVMin.xy, float2(1e-5, 1e-5));
                float2 uvLocal = (uv - _UVMin.xy) / uvSize;
                uvLocal = saturate(uvLocal);

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * IN.color;

                float mask = 1.0;
                float s  = max(_Smooth, 1e-4);
                float ow = max(_OutlineWidth, 0.0);
                float outlineMask = 0.0;

                // ================= ROUNDED CORNERS (ALPHA + OUTLINE) =================

                // TOP-LEFT corner
                if (_RadiusTL > 0.0 && uvLocal.x < _RadiusTL && uvLocal.y > 1.0 - _RadiusTL)
                {
                    float2 c = float2(_RadiusTL, 1.0 - _RadiusTL);
                    float dist = length(uvLocal - c);
                    float d = _RadiusTL - dist;   // > 0: inside rounded shape

                    float m = saturate(d / s);
                    mask = min(mask, m);

                    // Outline chỉ vẽ khi cả cạnh trái & trên đều còn border
                    if (_BorderMask.x > 0.5 && _BorderMask.z > 0.5 && ow > 0.0)
                    {
                        float o = smoothstep(0.0, s, d) * (1.0 - smoothstep(ow, ow + s, d));
                        outlineMask = max(outlineMask, o);
                    }
                }

                // TOP-RIGHT corner
                if (_RadiusTR > 0.0 && uvLocal.x > 1.0 - _RadiusTR && uvLocal.y > 1.0 - _RadiusTR)
                {
                    float2 c = float2(1.0 - _RadiusTR, 1.0 - _RadiusTR);
                    float dist = length(uvLocal - c);
                    float d = _RadiusTR - dist;

                    float m = saturate(d / s);
                    mask = min(mask, m);

                    if (_BorderMask.y > 0.5 && _BorderMask.z > 0.5 && ow > 0.0)
                    {
                        float o = smoothstep(0.0, s, d) * (1.0 - smoothstep(ow, ow + s, d));
                        outlineMask = max(outlineMask, o);
                    }
                }

                // BOTTOM-LEFT corner
                if (_RadiusBL > 0.0 && uvLocal.x < _RadiusBL && uvLocal.y < _RadiusBL)
                {
                    float2 c = float2(_RadiusBL, _RadiusBL);
                    float dist = length(uvLocal - c);
                    float d = _RadiusBL - dist;

                    float m = saturate(d / s);
                    mask = min(mask, m);

                    if (_BorderMask.x > 0.5 && _BorderMask.w > 0.5 && ow > 0.0)
                    {
                        float o = smoothstep(0.0, s, d) * (1.0 - smoothstep(ow, ow + s, d));
                        outlineMask = max(outlineMask, o);
                    }
                }

                // BOTTOM-RIGHT corner
                if (_RadiusBR > 0.0 && uvLocal.x > 1.0 - _RadiusBR && uvLocal.y < _RadiusBR)
                {
                    float2 c = float2(1.0 - _RadiusBR, _RadiusBR);
                    float dist = length(uvLocal - c);
                    float d = _RadiusBR - dist;

                    float m = saturate(d / s);
                    mask = min(mask, m);

                    if (_BorderMask.y > 0.5 && _BorderMask.w > 0.5 && ow > 0.0)
                    {
                        float o = smoothstep(0.0, s, d) * (1.0 - smoothstep(ow, ow + s, d));
                        outlineMask = max(outlineMask, o);
                    }
                }

                // ================= STRAIGHT EDGES (OUTLINE CHIP) =================

                // Top edge (giữa TL & TR)
                if (_BorderMask.z > 0.5 && ow > 0.0)
                {
                    float minX = _RadiusTL;
                    float maxX = 1.0 - _RadiusTR;
                    if (uvLocal.x >= minX && uvLocal.x <= maxX)
                    {
                        float d = 1.0 - uvLocal.y;
                        float o = smoothstep(0.0, s, d) * (1.0 - smoothstep(ow, ow + s, d));
                        outlineMask = max(outlineMask, o);
                    }
                }

                // Bottom edge (giữa BL & BR)
                if (_BorderMask.w > 0.5 && ow > 0.0)
                {
                    float minX = _RadiusBL;
                    float maxX = 1.0 - _RadiusBR;
                    if (uvLocal.x >= minX && uvLocal.x <= maxX)
                    {
                        float d = uvLocal.y;
                        float o = smoothstep(0.0, s, d) * (1.0 - smoothstep(ow, ow + s, d));
                        outlineMask = max(outlineMask, o);
                    }
                }

                // Left edge (giữa BL & TL)
                if (_BorderMask.x > 0.5 && ow > 0.0)
                {
                    float minY = _RadiusBL;
                    float maxY = 1.0 - _RadiusTL;
                    if (uvLocal.y >= minY && uvLocal.y <= maxY)
                    {
                        float d = uvLocal.x;
                        float o = smoothstep(0.0, s, d) * (1.0 - smoothstep(ow, ow + s, d));
                        outlineMask = max(outlineMask, o);
                    }
                }

                // Right edge (giữa BR & TR)
                if (_BorderMask.y > 0.5 && ow > 0.0)
                {
                    float minY = _RadiusBR;
                    float maxY = 1.0 - _RadiusTR;
                    if (uvLocal.y >= minY && uvLocal.y <= maxY)
                    {
                        float d = 1.0 - uvLocal.x;
                        float o = smoothstep(0.0, s, d) * (1.0 - smoothstep(ow, ow + s, d));
                        outlineMask = max(outlineMask, o);
                    }
                }

                // ================= APPLY MASK + OUTLINE =================

                col.a *= mask;
                clip(col.a - 0.001);

                // Shader-based outline
                col.rgb = lerp(col.rgb, _OutlineColor.rgb, saturate(outlineMask));

                return col;
            }

            ENDHLSL
        }
    }
}
