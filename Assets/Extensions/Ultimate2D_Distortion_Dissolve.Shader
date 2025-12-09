Shader "Custom/Ultimate2D_Distortion_Dissolve"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _Color("Color Tint", Color) = (1,1,1,1)

        _MaskTex("Distortion Mask", 2D) = "white" {}

        // Distortion mode (9 total)
        [KeywordEnum(Wave, Noise, Heat, Wiggle, Wind, ScrollWave, DissolveDistort, FrameDistort, CurveDistort)]
        _Mode("Distortion Mode", Float) = 0

        _Strength("Distortion Strength", Range(0,2)) = 0.2
        _Speed("Speed", Range(0, 10)) = 1.0

        // Wave
        _WaveScale("Wave Scale", Range(0,5)) = 1

        // Noise
        _NoiseScale("Noise Scale", Range(0.1,10)) = 1

        // Heat
        _HeatStrength("Heat Strength", Range(0,1)) = 0.1

        // Wiggle
        _WiggleFreq("Wiggle Frequency", Range(0,20)) = 5

        // Wind
        _WindIntensity("Wind Intensity", Range(0,1)) = 0.2
        _WindFrequency("Wind Frequency", Range(0.5,10)) = 2

        // ScrollWave
        _ScrollWaveFreq("Scroll Wave Frequency", Range(0,10)) = 3
        _ScrollWaveSpeed("Scroll Wave Speed", Range(0,20)) = 5

        // Dissolve
        _DissolveTex("Dissolve Mask", 2D) = "white" {}
        _DissolveAmount("Dissolve Amount", Range(0,1)) = 0.5
        _EdgeWidth("Edge Width", Range(0,0.3)) = 0.05
        _EdgeColor("Edge Color", Color) = (1,0.5,0,1)

        // Sprite Frame Distortion
        _FrameIndex("Frame Index", Float) = 0
        _FrameDistortStrength("Frame Distort Strength", Range(0,2)) = 1

        // Curve Distortion
        _CurveValue("Curve Value", Range(0,1)) = 0
        _CurveDistortStrength("Curve Distort Strength", Range(0,2)) = 1

        // UI 보정
        _PixelsPerUnit("Pixels Per Unit", Float) = 100
        _Scale("Canvas Scale", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _MODE_WAVE _MODE_NOISE _MODE_HEAT _MODE_WIGGLE _MODE_WIND _MODE_SCROLLWAVE _MODE_DISSOLVEDISTORT _MODE_FRAMEDISTORT _MODE_CURVEDISTORT

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;

            sampler2D _DissolveTex;

            float4 _MainTex_ST;
            float4 _MaskTex_ST;
            float4 _DissolveTex_ST;

            float4 _Color;

            float _Strength;
            float _Speed;

            float _WaveScale;
            float _NoiseScale;
            float _HeatStrength;
            float _WiggleFreq;

            float _WindIntensity;
            float _WindFrequency;

            float _ScrollWaveFreq;
            float _ScrollWaveSpeed;

            float _DissolveAmount;
            float _EdgeWidth;
            float4 _EdgeColor;

            float _FrameIndex;
            float _FrameDistortStrength;

            float _CurveValue;
            float _CurveDistortStrength;

            float _PixelsPerUnit;
            float _Scale;

            // Perlin substitute
            float2 perlin(float2 p)
            {
                float noise = frac(sin(dot(p , float2(12.9898,78.233))) * 43758.5453);
                return float2(noise, noise * 0.5);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;

                float mask = tex2D(_MaskTex, i.uv).r;

                float2 offset = 0;

                //-----------------------------------------------------------
                // 각 Distortion 모드 처리
                //-----------------------------------------------------------

                #if _MODE_WAVE
                offset.y = sin(i.uv.x * _WaveScale * 10 + t * 5) * 0.01;

                #elif _MODE_NOISE
                float2 n = perlin(i.uv * _NoiseScale + t);
                offset = (n - 0.5) * 0.05;

                #elif _MODE_HEAT
                offset.x = sin(i.uv.y * 25 + t * 20) * _HeatStrength;

                #elif _MODE_WIGGLE
                offset.x = sin(t * _WiggleFreq + i.uv.y * 50) * 0.003;
                offset.y = cos(t * _WiggleFreq + i.uv.x * 50) * 0.003;

                #elif _MODE_WIND
                offset.y = sin(i.uv.x * _WindFrequency + t * 2)
                         * sin(i.uv.y * (_WindFrequency * 0.5) + t * 1.5)
                         * _WindIntensity * 0.05;

                #elif _MODE_SCROLLWAVE
                offset.y = sin(i.uv.x * _ScrollWaveFreq + t * _ScrollWaveSpeed) * 0.02;

                //-----------------------------------------------------------
                // 7) Dissolve + Distortion
                //-----------------------------------------------------------
                #elif _MODE_DISSOLVEDISTORT
                float dissolve = tex2D(_DissolveTex, i.uv).r;

                float edge = smoothstep(_DissolveAmount - _EdgeWidth, _DissolveAmount, dissolve)
                           - smoothstep(_DissolveAmount, _DissolveAmount + _EdgeWidth, dissolve);

                offset.y = edge * 0.05;

                //-----------------------------------------------------------
                // 8) Sprite Animation Frame 기반 흔들림
                //-----------------------------------------------------------
                #elif _MODE_FRAMEDISTORT
                offset.x = sin(i.uv.y * 40 + _FrameIndex * 0.5) 
                         * 0.003 * _FrameDistortStrength;

                //-----------------------------------------------------------
                // 9) Curve 기반 Distortion (애니메이션 커브)
                //-----------------------------------------------------------
                #elif _MODE_CURVEDISTORT
                offset.y = sin(i.uv.x * 15 + t * 3) * _CurveValue * _CurveDistortStrength * 0.05;

                #endif

                //-----------------------------------------------------------
                // Mask / Strength / UI 보정
                //-----------------------------------------------------------
                offset *= mask * _Strength;
                offset /= (_PixelsPerUnit * _Scale);

                float2 uv = i.uv + offset;

                fixed4 col = tex2D(_MainTex, uv) * i.color;

                //-----------------------------------------------------------
                // Dissolve 색 처리 (투명화 포함)
                //-----------------------------------------------------------
                #if _MODE_DISSOLVEDISTORT
                float d = tex2D(_DissolveTex, i.uv).r;

                if (d < _DissolveAmount)
                {
                    discard;
                }

                float e = smoothstep(_DissolveAmount - _EdgeWidth, _DissolveAmount + _EdgeWidth, d);
                col.rgb = lerp(_EdgeColor.rgb, col.rgb, e);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
