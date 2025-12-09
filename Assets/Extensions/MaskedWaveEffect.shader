Shader "Custom/MaskedWaveEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture (R channel controls wave)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Wave Settings)]
        _Speed ("Wave Speed", Range(0, 10)) = 2.0
        _Frequency ("Wave Frequency", Range(0, 20)) = 10.0
        _Amplitude ("Wave Amplitude", Range(0, 0.1)) = 0.02
        _WaveDirection ("Wave Direction (XY)", Vector) = (0, 1, 0, 0)
        _WaveOffsetAxis ("Wave Offset Axis (XY)", Vector) = (1, 0, 0, 0)
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
            float4 _MaskTex_ST;
            fixed4 _Color;

            float _Speed;
            float _Frequency;
            float _Amplitude;
            float4 _WaveDirection;
            float4 _WaveOffsetAxis;

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
                // 1. 마스크 텍스처 샘플링 (R 채널 값만 사용)
                // 검은색(0) = 움직임 없음, 흰색(1) = 최대 움직임
                fixed maskValue = tex2D(_MaskTex, IN.texcoord).r;

                // 2. 사인파(Sine Wave) 계산
                // 시간(_Time.y)과 UV의 Y좌표(IN.texcoord.y)를 이용해 물결 생성
                float2 rawDir = _WaveDirection.xy;
                float dirLength = length(rawDir);
                float2 waveDir = dirLength > 0.0001 ? rawDir / dirLength : float2(0.0, 1.0);

                float waveCoord = dot(IN.texcoord, waveDir);
                float waveOffset = sin(_Time.y * _Speed + waveCoord * _Frequency) * _Amplitude;
                
                float2 offsetAxisRaw = _WaveOffsetAxis.xy;
                float axisLen = length(offsetAxisRaw);
                float2 offsetAxis = axisLen > 0.0001 ? offsetAxisRaw / axisLen : float2(1.0, 0.0);

                // 3. 마스크 적용
                // 마스크 값이 0이면 waveOffset이 0이 되어 움직이지 않음
                float2 distortedUV = IN.texcoord;
                distortedUV += offsetAxis * (waveOffset * maskValue);

                // 4. 왜곡된 UV로 메인 텍스처 샘플링
                fixed4 c = tex2D(_MainTex, distortedUV) * IN.color;
                
                return c;
            }
            ENDCG
        }
    }
}