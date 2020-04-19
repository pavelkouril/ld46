Shader "Unlit/GrassShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
		_MaskTex("Texture", 2D) = "white" {}
		_NoiseTex("Texture", 2D) = "white" {}
		_AlphaCutoff("Cutoff", float) = 0.95
		_Size("Instance Size", float) = 1.0
		_Position("Instance Size", Vector) = (0.0, 0.0, 0.0, 0.0)
		_SizeCutoff("Instance Size Cutout", float) = 0.3
    }
    SubShader
    {
		Cull Off
		LOD 100

        Pass
        {
			Tags {"LightMode" = "ForwardBase"}

			CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
			#pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"
			
            struct v2f
            {
				float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
				SHADOW_COORDS(2)
				float tmp : TEXCOORD3;
            };

			sampler2D _MainTex;
			sampler2D _MaskTex;
			sampler2D _NoiseTex;
            float4 _MainTex_ST;
			fixed _AlphaCutoff;
			fixed _Size;
			float4 _Position;
			fixed _SizeCutoff;

            v2f vert (appdata_base v)
            {
				float2 coord = ((mul(UNITY_MATRIX_M, v.vertex).xyz / (_Size * 0.5)) * 0.5 + 0.5).xz;
				float size = tex2Dlod(_MaskTex, float4(coord.xy, 0.0, 0.0)).x;
				float noise = 2.0 * tex2Dlod(_NoiseTex, float4(coord.xy + _Time * 0.1, 0.0, 0.0)).x - 1.0;

				float4 vertex = v.vertex;
				vertex.x = vertex.x + vertex.z * noise * 0.2;
				vertex.y = vertex.y + vertex.z * noise * 0.2;
				vertex.z *= size;
				if (vertex.z < 0.1)
				{
					vertex.w = 0.0;
				}

                v2f o;
				o.tmp = size;
                o.pos = UnityObjectToClipPos(vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
				TRANSFER_SHADOW(o)
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
				clip(col.a - _AlphaCutoff);
				if (i.tmp < _SizeCutoff)
				{
					discard;
				}
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
				fixed shadow = SHADOW_ATTENUATION(i);
				//return fixed4(i.tmp, 0.0, 0.0, 1.0);
				return col * max(shadow, 0.3);
            }
            ENDCG
        }

		Pass
		{
			ZWrite off
			Blend SrcAlpha OneMinusSrcAlpha
				
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
					SHADOW_COORDS(2)
					float tmp : TEXCOORD3;
			};

			sampler2D _MainTex;
			sampler2D _MaskTex;
			sampler2D _NoiseTex;
			float4 _MainTex_ST;
			fixed _AlphaCutoff;
			fixed _Size;
			float4 _Position;
			fixed _SizeCutoff;

			v2f vert(appdata_base v)
			{
				float2 coord = ((mul(UNITY_MATRIX_M, v.vertex).xyz / (_Size * 0.5)) * 0.5 + 0.5).xz;
				float size = tex2Dlod(_MaskTex, float4(coord.xy, 0.0, 0.0)).x;
				float noise = 2.0 * tex2Dlod(_NoiseTex, float4(coord.xy + _Time * 0.1, 0.0, 0.0)).x - 1.0;

				float4 vertex = v.vertex;
				vertex.x = vertex.x + vertex.z * noise * 0.2;
				vertex.y = vertex.y + vertex.z * noise * 0.2;
				vertex.z *= size;
				if (vertex.z < 0.1)
				{
					vertex.w = 0.0;
				}

				v2f o;
				o.tmp = size;
				o.pos = UnityObjectToClipPos(vertex);
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				UNITY_TRANSFER_FOG(o, o.vertex);
				TRANSFER_SHADOW(o)
					return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				// sample the texture
				fixed4 col = tex2D(_MainTex, i.uv);
				clip(_AlphaCutoff - col.a);
				if (i.tmp < _SizeCutoff)
				{
					discard;
				}
				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				fixed shadow = SHADOW_ATTENUATION(i);
				//return fixed4(shadow, shadow, shadow, col.a);
				return col * max(shadow, 0.3);
			}
			ENDCG
		}

		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			#pragma multi_compile_shadowcaster
			#pragma multi_compile_instancing // allow instanced shadow pass for most of the shaders
			#include "UnityCG.cginc"

			sampler2D _MaskTex;
			sampler2D _MainTex;
			sampler2D _NoiseTex;
			float4 _MainTex_ST;
			fixed _AlphaCutoff;
			fixed _Size;
			float4 _Position;
			fixed _SizeCutoff;

			struct v2f {
				V2F_SHADOW_CASTER;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
				float tmp : TEXCOORD3;
			};

			v2f vert(appdata_base v)
			{
				float2 coord = ((mul(UNITY_MATRIX_M, v.vertex).xyz / (_Size * 0.5)) * 0.5 + 0.5).xz;
				float size = tex2Dlod(_MaskTex, float4(coord.xy, 0.0, 0.0)).x;
				float noise = 2.0 * tex2Dlod(_NoiseTex, float4(coord.xy + _Time * 0.1, 0.0, 0.0)).x - 1.0;

				v.vertex.x = v.vertex.x + v.vertex.z * noise * 0.2;
				v.vertex.y = v.vertex.y + v.vertex.z * noise * 0.2;
				v.vertex.z *= size;

				v2f o;
				o.tmp = size;
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				clip(col.a - _AlphaCutoff);
				if (i.tmp < _SizeCutoff)
				{
					discard;
				}

				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}

    }

	/*Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo", 2D) = "white" {}

		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

		_Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
		[Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
		_MetallicGlossMap("Metallic", 2D) = "white" {}

		_BumpScale("Scale", Float) = 1.0
		_BumpMap("Normal Map", 2D) = "bump" {}

		_Parallax("Height Scale", Range(0.005, 0.08)) = 0.02
		_ParallaxMap("Height Map", 2D) = "black" {}

		_OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
		_OcclusionMap("Occlusion", 2D) = "white" {}

		_EmissionColor("Color", Color) = (0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}

		_DetailMask("Detail Mask", 2D) = "white" {}

		_DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
		_DetailNormalMapScale("Scale", Float) = 1.0
		_DetailNormalMap("Normal Map", 2D) = "bump" {}

		[Enum(UV0,0,UV1,1)] _UVSec("UV Set for secondary textures", Float) = 0


			// Blending state
			[HideInInspector] _Mode("__mode", Float) = 0.0
			[HideInInspector] _SrcBlend("__src", Float) = 1.0
			[HideInInspector] _DstBlend("__dst", Float) = 0.0
			[HideInInspector] _ZWrite("__zw", Float) = 1.0
	}

		CGINCLUDE
#define UNITY_SETUP_BRDF_INPUT MetallicSetup
			ENDCG

			SubShader
		{
			Tags { "RenderType" = "Opaque" "PerformanceChecks" = "False" }
			Cull Off
			AlphaToMask On
			LOD 300


			// ------------------------------------------------------------------
			//  Base forward pass (directional light, emission, lightmaps, ...)
			Pass
			{
				Name "FORWARD"
				Tags { "LightMode" = "ForwardBase" }

				Blend[_SrcBlend][_DstBlend]
				ZWrite[_ZWrite]
				Cull Off
				CGPROGRAM
				#pragma target 3.0
			// TEMPORARY: GLES2.0 temporarily disabled to prevent errors spam on devices without textureCubeLodEXT
			#pragma exclude_renderers gles

			// -------------------------------------

			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _EMISSION
			#pragma shader_feature _METALLICGLOSSMAP
			#pragma shader_feature ___ _DETAIL_MULX2
			#pragma shader_feature _PARALLAXMAP

			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog

			#pragma vertex vertForwardBase
			#pragma fragment fragForwardBase

			#include "UnityStandardCore.cginc"

			ENDCG
		}
			// ------------------------------------------------------------------
			//  Additive forward pass (one light per pass)
			Pass
			{
				Name "FORWARD_DELTA"
				Tags { "LightMode" = "ForwardAdd" }
				Blend[_SrcBlend] One
				Fog { Color(0,0,0,0) } // in additive pass fog should be black
				ZWrite Off
				ZTest LEqual

				CGPROGRAM
				#pragma target 3.0
			// GLES2.0 temporarily disabled to prevent errors spam on devices without textureCubeLodEXT
			#pragma exclude_renderers gles

			// -------------------------------------


			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _METALLICGLOSSMAP
			#pragma shader_feature ___ _DETAIL_MULX2
			#pragma shader_feature _PARALLAXMAP

			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_fog

			#pragma vertex vertForwardAdd
			#pragma fragment fragForwardAdd

			#include "UnityStandardCore.cginc"

			ENDCG
		}
			// ------------------------------------------------------------------
			//  Shadow rendering pass
			Pass {
				Name "ShadowCaster"
				Tags { "LightMode" = "ShadowCaster" }

				ZWrite On ZTest LEqual

				CGPROGRAM
				#pragma target 3.0
			// TEMPORARY: GLES2.0 temporarily disabled to prevent errors spam on devices without textureCubeLodEXT
			#pragma exclude_renderers gles

			// -------------------------------------


			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma multi_compile_shadowcaster

			#pragma vertex vertShadowCaster
			#pragma fragment fragShadowCaster

			#include "UnityStandardShadow.cginc"

			ENDCG
		}
			// ------------------------------------------------------------------
			//  Deferred pass
			Pass
			{
				Name "DEFERRED"
				Tags { "LightMode" = "Deferred" }
				Cull Off
				CGPROGRAM
				#pragma target 3.0
			// TEMPORARY: GLES2.0 temporarily disabled to prevent errors spam on devices without textureCubeLodEXT
			#pragma exclude_renderers nomrt gles


			// -------------------------------------

			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _EMISSION
			#pragma shader_feature _METALLICGLOSSMAP
			#pragma shader_feature ___ _DETAIL_MULX2
			#pragma shader_feature _PARALLAXMAP

			#pragma multi_compile ___ UNITY_HDR_ON
			#pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
			#pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
			#pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON

			#pragma vertex vertDeferred
			#pragma fragment fragDeferred

			#include "UnityStandardCore.cginc"

			ENDCG
		}

			// ------------------------------------------------------------------
			// Extracts information for lightmapping, GI (emission, albedo, ...)
			// This pass it not used during regular rendering.
			Pass
			{
				Name "META"
				Tags { "LightMode" = "Meta" }

				Cull Off

				CGPROGRAM
				#pragma vertex vert_meta
				#pragma fragment frag_meta

				#pragma shader_feature _EMISSION
				#pragma shader_feature _METALLICGLOSSMAP
				#pragma shader_feature ___ _DETAIL_MULX2

				#include "UnityStandardMeta.cginc"
				ENDCG
			}
		}

			SubShader
		{
			Tags { "RenderType" = "Opaque" "PerformanceChecks" = "False" }
			Cull Off
			AlphaToMask On
			LOD 150

			// ------------------------------------------------------------------
			//  Base forward pass (directional light, emission, lightmaps, ...)
			Pass
			{
				Name "FORWARD"
				Tags { "LightMode" = "ForwardBase" }

				Blend[_SrcBlend][_DstBlend]
				ZWrite[_ZWrite]

				CGPROGRAM
				#pragma target 2.0

				#pragma shader_feature _NORMALMAP
				#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
				#pragma shader_feature _EMISSION
				#pragma shader_feature _METALLICGLOSSMAP
				#pragma shader_feature ___ _DETAIL_MULX2
			// SM2.0: NOT SUPPORTED shader_feature _PARALLAXMAP

			#pragma skip_variants SHADOWS_SOFT DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE

			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog

			#pragma vertex vertForwardBase
			#pragma fragment fragForwardBase

			#include "UnityStandardCore.cginc"

			ENDCG
		}
			// ------------------------------------------------------------------
			//  Additive forward pass (one light per pass)
			Pass
			{
				Name "FORWARD_DELTA"
				Tags { "LightMode" = "ForwardAdd" }
				Blend[_SrcBlend] One
				Fog { Color(0,0,0,0) } // in additive pass fog should be black
				ZWrite Off
				ZTest LEqual

				CGPROGRAM
				#pragma target 2.0

				#pragma shader_feature _NORMALMAP
				#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
				#pragma shader_feature _METALLICGLOSSMAP
				#pragma shader_feature ___ _DETAIL_MULX2
			// SM2.0: NOT SUPPORTED shader_feature _PARALLAXMAP
			#pragma skip_variants SHADOWS_SOFT

			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_fog

			#pragma vertex vertForwardAdd
			#pragma fragment fragForwardAdd

			#include "UnityStandardCore.cginc"

			ENDCG
		}
			// ------------------------------------------------------------------
			//  Shadow rendering pass
			Pass {
				Name "ShadowCaster"
				Tags { "LightMode" = "ShadowCaster" }

				ZWrite On ZTest LEqual

				CGPROGRAM
				#pragma target 2.0

				#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
				#pragma skip_variants SHADOWS_SOFT
				#pragma multi_compile_shadowcaster

				#pragma vertex vertShadowCaster
				#pragma fragment fragShadowCaster

				#include "UnityStandardShadow.cginc"

				ENDCG
			}

			// ------------------------------------------------------------------
			// Extracts information for lightmapping, GI (emission, albedo, ...)
			// This pass it not used during regular rendering.
			Pass
			{
				Name "META"
				Tags { "LightMode" = "Meta" }

				Cull Off

				CGPROGRAM
				#pragma vertex vert_meta
				#pragma fragment frag_meta

				#pragma shader_feature _EMISSION
				#pragma shader_feature _METALLICGLOSSMAP
				#pragma shader_feature ___ _DETAIL_MULX2

				#include "UnityStandardMeta.cginc"
				ENDCG
			}
		}*/

}
