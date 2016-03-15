Shader "Sprites/Indexed"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Palette ("Palette Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		[MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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
		Blend One OneMinusSrcAlpha
		//Blend SrcAlpha OneMinusSrcAlpha // switch to classic alpha blending
		
		Pass
		{
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ PIXELSNAP_ON
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
				half2 texcoord  : TEXCOORD0;
			};
			
			fixed4 _Color;

			v2f vert(appdata_t IN)
			{
				v2f OUT;
				OUT.vertex = mul(UNITY_MATRIX_MVP, IN.vertex);
				OUT.texcoord = IN.texcoord;
				OUT.color = IN.color * _Color;
				#ifdef PIXELSNAP_ON
				OUT.vertex = UnityPixelSnap (OUT.vertex);
				#endif

				return OUT;
			}

			sampler2D _MainTex;
			sampler2D _Palette;
			sampler2D _AlphaTex;
			float _AlphaSplitEnabled;

			fixed4 frag(v2f IN) : SV_Target
			{
				float color = tex2D (_MainTex, IN.texcoord).a;
				float index = fmod(color * 254.0, 127.0);
				float alpha = floor(color * 254.0 / 127.0);

				float2 paletteUV = floor(float2(fmod(index, 16.0), index / 16.0));
				paletteUV.x /= 16.0;
				paletteUV.y /= 8.0;

				fixed4 c = tex2D (_Palette, paletteUV) * IN.color;
				c.a *= alpha;
				c.rgb *= c.a;
				return c; 
			}
		ENDCG
		}
	}
}
