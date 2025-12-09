
Shader "Custom/Spritedissolve"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		[MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
		[PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
		_Maintex("MainTex", 2D) = "white" {}
		_ColorPower("ColorPower", Float) = 0
		_DissolvePower("DissolvePower", Float) = 0
		_Xspeed("Xspeed", Float) = 0
		_Yspeed("Yspeed", Float) = 0
		_FXTex("FXTex", 2D) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
	}

	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

		Cull Off
		Lighting Off
		ZWrite Off
		Blend One OneMinusSrcAlpha


		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}
		

		Pass
		{
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile _ PIXELSNAP_ON
			#pragma multi_compile _ ETC1_EXTERNAL_ALPHA
			#include "UnityCG.cginc"
			#include "UnityShaderVariables.cginc"


			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID

			};

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				fixed4 color    : COLOR;
				float2 texcoord  : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO

			};

			uniform fixed4 _Color;
			uniform float _EnableExternalAlpha;
			uniform sampler2D _MainTex;
			uniform sampler2D _AlphaTex;
			uniform float _ColorPower;
			uniform sampler2D _FXTex;
			uniform float _Xspeed;
			uniform float _Yspeed;
			uniform float4 _FXTex_ST;
			uniform float _DissolvePower;
			uniform sampler2D _Maintex;
			uniform float4 _Maintex_ST;

			v2f vert( appdata_t IN  )
			{
				v2f OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);


				IN.vertex.xyz +=  float3(0,0,0) ;
				OUT.vertex = UnityObjectToClipPos(IN.vertex);
				OUT.texcoord = IN.texcoord;
				OUT.color = IN.color * _Color;
				#ifdef PIXELSNAP_ON
				OUT.vertex = UnityPixelSnap (OUT.vertex);
				#endif

				return OUT;
			}

			fixed4 SampleSpriteTexture (float2 uv)
			{
				fixed4 color = tex2D (_MainTex, uv);

#if ETC1_EXTERNAL_ALPHA
				// get the color from an external texture (usecase: Alpha support for ETC1 on android)
				fixed4 alpha = tex2D (_AlphaTex, uv);
				color.a = lerp (color.a, alpha.r, _EnableExternalAlpha);
#endif //ETC1_EXTERNAL_ALPHA

				return color;
			}

			fixed4 frag(v2f IN  ) : SV_Target
			{
				float2 appendResult43 = (float2(_Xspeed , _Yspeed));
				float2 uv_FXTex = IN.texcoord.xy * _FXTex_ST.xy + _FXTex_ST.zw;
				float2 panner19 = ( 1.0 * _Time.y * appendResult43 + uv_FXTex);
				float2 panner55 = ( 1.0 * _Time.y * ( appendResult43 * 0.5 ) + uv_FXTex);
				float2 uv_Maintex = IN.texcoord.xy * _Maintex_ST.xy + _Maintex_ST.zw;
				float4 tex2DNode2 = tex2D( _Maintex, uv_Maintex );

				fixed4 c = ( ( ( _ColorPower * IN.color ) * tex2D( _FXTex, ( panner19 + ( tex2D( _FXTex, panner55 ).r * _DissolvePower ) ) ).r ) * tex2DNode2.r * tex2DNode2.a );
				c.rgb *= c.a;
				return c;
			}
		ENDCG
		}
	}
	Fallback "diffuse"
}
