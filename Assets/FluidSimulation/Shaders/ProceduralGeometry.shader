Shader "PavelKouril/Marching Cubes/Procedural Geometry"
{
	SubShader
	{
		Cull Back
		
		Pass
		{
			CGPROGRAM
			#pragma target 5.0
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct Vertex
			{
				float4 vPosition;
				float4 vNormal;
			};

			struct Triangle
			{
				Vertex v[3];
			};

			uniform StructuredBuffer<Triangle> triangles;
			uniform float4x4 model;

			uniform float _Clip;
			uniform sampler2D _RefractionTex;
			uniform samplerCUBE _ReflectionTex;

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 view : TEXCOORD0;
				float3 viewDir : TEXCOORD1;
				float3 normal : NORMAL;
			};

			v2f vert(uint id : SV_VertexID)
			{
				uint pid = id / 3;
				uint vid = id % 3;

				v2f o;
				o.view = UnityObjectToClipPos(mul(model, triangles[pid].v[vid].vPosition));
				o.viewDir = mul(model, triangles[pid].v[vid].vPosition).xyz - _WorldSpaceCameraPos;
				o.vertex = mul(UNITY_MATRIX_VP, mul(model, triangles[pid].v[vid].vPosition));
				o.normal = mul(unity_ObjectToWorld, triangles[pid].v[vid].vNormal.xyz);
				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				if (_Clip > 0.1)
				{
					discard;
				}

				float2 projCoord = (i.view.xy / i.view.w) * 0.5f + 0.5f;
				projCoord.y = 1.0f - projCoord.y;

				float3 refrDir = refract(normalize(i.viewDir), normalize(i.normal), 1.0 / 1.33);
				float2 refrDirMag = normalize(float2(refrDir.x * refrDir.y, refrDir.y * refrDir.z)) * 2.0 - 1.0;

				float3 refractionTex = tex2D(_RefractionTex, projCoord + refrDirMag * 0.01 + float2(i.normal.x, i.normal.z) * 0.02).xyz;

				float3 reflDir = reflect(normalize(i.viewDir), normalize(i.normal));
				float3 reflectionTex = texCUBElod(_ReflectionTex, float4(reflDir, 0)).xyz;

				float fresnel = pow(1.0 - max(dot(normalize(-i.viewDir), normalize(i.normal)), 0.0), 2.0);

				float d = max(dot(normalize(_WorldSpaceLightPos0.xyz), i.normal), 0);
				return float4(lerp(refractionTex, reflectionTex, fresnel), 1);
			}
			ENDCG
		}
	}
}
