Shader "Custom/HairSwayEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture (R channel controls sway)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Sway Settings)]
        _Speed ("Sway Speed", Range(0, 20)) = 3.0
        _Strength ("Sway Strength", Range(0, 0.1)) = 0.01
        _SwayAxis ("Sway Axis (XY)", Vector) = (1, 0, 0, 0)
        
        // 자연스러운 엇박자를 위한 노이즈 변수
        _Noise ("Random Offset", Range(0, 10)) = 0.0 
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            #include "UnityCG.cginc"
            
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            fixed4 _Color;

            float _Speed;
            float _Strength;
            float4 _SwayAxis;
            float _Noise;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 1. 마스크 값 가져오기 (0: 고정, 1: 흔들림)
                fixed maskValue = tex2D(_MaskTex, IN.texcoord).r;

                // 2. 흔들림 계산 (스윙 효과)
                // 이전 코드와의 차이점: IN.texcoord.y를 사인파 안에 넣지 않음 (혹은 아주 적게 넣음)
                // 이렇게 하면 물결치지 않고 덩어리째로 왔다갔다 합니다.
                
                // 팁: sin 안에 texcoord.y * 0.5 정도를 더해주면 머리 끝이 살짝 늦게 따라오는 관성을 표현할 수 있습니다.
                float sway = sin(_Time.y * _Speed + IN.texcoord.y * 0.5 + _Noise) * _Strength;

                // 3. UV 왜곡 적용
                // 마스크가 흰색인 부분만 X축으로 이동
                float2 axisRaw = _SwayAxis.xy;
                float axisLen = length(axisRaw);
                float2 swayAxis = axisLen > 0.0001 ? axisRaw / axisLen : float2(1.0, 0.0);

                float2 distortedUV = IN.texcoord;
                distortedUV += swayAxis * (sway * maskValue);

                // 4. 텍스처 샘플링
                fixed4 c = tex2D(_MainTex, distortedUV) * IN.color;
                
                return c;
            }
            ENDCG
        }
    }
}