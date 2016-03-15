Shader "Sprites/IndexedBilinear"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Palette ("Palette Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_Width ("Sprite Texture Width", Float) = 256.0
		_Height ("Sprite Texture Height", Float) = 256.0
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
			sampler2D _AlphaTex;
			float _AlphaSplitEnabled;
			sampler2D _Palette;
			float _Width;
			float _Height;
			
			fixed4 SamplePalette(float2 uv)
			{
				float color = tex2D(_MainTex, uv).a; 
				float index = fmod(color * 254.0, 127.0); // from 255.0 to 254.0.it fucks as on the device
				float alpha = floor(color * 254.0 / 127.0);

				float2 paletteUV = floor(float2(fmod(index, 16.0), index / 16.0));
				paletteUV.x /= 16.0;
				paletteUV.y /= 8.0;

				return fixed4(tex2D(_Palette, paletteUV).rgb, alpha);
			}

			fixed4 SampleBilinear(float2 uv)
			{
				half2 px = half2(1.0 / _Width, 1.0 / _Height);
			    half2 weight = frac(half2(uv.x * _Width, uv.y * _Height));
			 
			    fixed4 bottom = lerp(SamplePalette(uv),
			                         SamplePalette(uv + half2(px.x, .0)),
			                         weight.x);
			 
			    fixed4 top = lerp(SamplePalette(uv + half2(.0, px.y)), 
			                      SamplePalette(uv + half2(px.x, px.y)), 
			                      weight.x);
			 
			    return lerp(bottom, top, weight.y);
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				fixed4 c = SampleBilinear(IN.texcoord.xy) * IN.color;
				c.rgb *= c.a;
				return c; 
			}
		ENDCG
		}
	}
}
