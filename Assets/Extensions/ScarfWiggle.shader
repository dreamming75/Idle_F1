Shader "UI/ScarfWiggle"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        [MainColor]   _Color   ("Tint", Color) = (1,1,1,1)

        _Amplitude ("Amplitude (px)", Float) = 6
        _Frequency ("Frequency (cycles)", Float) = 2
        _Speed     ("Speed", Float) = 2
        _AnchorDir ("Anchor Direction (0=Left,1=Right,2=Bottom,3=Top)", Float) = 0
        _AnchorWidth ("Anchor Width (0~1)", Range(0,1)) = 0.25
        _Phase     ("Phase Offset", Float) = 0

        // --- UGUI 표준 ---
        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID", Float) = 0
        _StencilOp        ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask", Float) = 255
        _ColorMask        ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.001
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
    }

    SubShader
    {
        Tags{
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
        }

        Stencil{
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UI_ScarfWiggle"
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            // ★ 여기가 핵심: TexelSize 선언 추가
            float4 _MainTex_TexelSize; // (1/width, 1/height, width, height)

            float _Amplitude;
            float _Frequency;
            float _Speed;
            float _Phase;
            float _AnchorDir;   // 0=L,1=R,2=B,3=T
            float _AnchorWidth; // 0~1

            float4 _ClipRect;
            float  _UseUIAlphaClip;
            float  _Cutoff;

            struct appdata_t {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            v2f vert (appdata_t v){
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.uv       = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color    = v.color * _Color;
                o.worldPos = v.vertex;
                return o;
            }

            // 1=자유, 0=고정 (목쪽)
            float anchorMask(float2 uv){
                if (_AnchorDir < 0.5)      return saturate( (uv.x - 0.0) / max(1e-5, _AnchorWidth) ); // Left
                else if (_AnchorDir < 1.5) return saturate( (1.0 - uv.x) / max(1e-5, _AnchorWidth) ); // Right
                else if (_AnchorDir < 2.5) return saturate( (uv.y - 0.0) / max(1e-5, _AnchorWidth) ); // Bottom
                else                       return saturate( (1.0 - uv.y) / max(1e-5, _AnchorWidth) ); // Top
            }

            fixed4 frag (v2f i) : SV_Target
            {
                bool vertical = (_AnchorDir >= 2.5);
                // 픽셀→UV 변환: _MainTex_TexelSize.x/y = 1/width,height
                float ampUV = _Amplitude * (vertical ? _MainTex_TexelSize.y : _MainTex_TexelSize.x);

                float t = _Time.y * _Speed + _Phase;
                float2 uv = i.uv;

                float strength = anchorMask(uv);

                if (vertical)
                    uv.y += sin(uv.x * (6.2831853 * _Frequency) + t) * ampUV * strength;
                else
                    uv.x += sin(uv.y * (6.2831853 * _Frequency) + t) * ampUV * strength;

                fixed4 col = tex2D(_MainTex, uv) * i.color;

                col.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                if (_UseUIAlphaClip > 0 && col.a < _Cutoff) discard;

                return col;
            }
            ENDCG
        }
    }
}
