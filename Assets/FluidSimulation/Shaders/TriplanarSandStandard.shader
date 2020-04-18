Shader "Custom/TriplanarSandStandard" {
	Properties{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0

		_TextureScale("Texture Scale",float) = 1
		_TriplanarBlendSharpness("Blend Sharpness",float) = 1
	}
		SubShader{
			Tags { "RenderType" = "Opaque" }
			LOD 200

			CGPROGRAM
			#pragma surface surf Standard vertex:vert
			#pragma target 5.0

			sampler2D _MainTex;

			struct Vertex
			{
				float3 vPosition;
				float3 vNormal;
			};

			struct Triangle
			{
				Vertex v[3];
			};

			struct appdata {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 texcoord : TEXCOORD0;
				float2 texcoord1 : TEXCOORD1;
				float2 texcoord2 : TEXCOORD2;

				uint id : SV_VertexID;
			};

			uniform StructuredBuffer<Triangle> triangles;
			uniform float4x4 model;

			half _Glossiness;
			half _Metallic;
			fixed4 _Color;

			float _TextureScale;
			float _TriplanarBlendSharpness;

			struct Input {
				float4 color;
				float3 worldNormal;
				float3 worldPos;
			};

			void vert(inout appdata v)
			{
				uint pid = v.id / 3;
				uint vid = v.id % 3;

				v.vertex = mul(model, float4(triangles[pid].v[vid].vPosition.xyz, 1));
				v.normal = normalize(triangles[pid].v[vid].vNormal.xyz);
				v.texcoord = float2(0,0);
			}

			void surf(Input IN, inout SurfaceOutputStandard o) {
				o.Metallic = _Metallic;
				o.Smoothness = _Glossiness;
				o.Alpha = 1;

				half2 yUV = IN.worldPos.xz / _TextureScale;
				half2 xUV = IN.worldPos.zy / _TextureScale;
				half2 zUV = IN.worldPos.xy / _TextureScale;
				half3 yDiff = tex2D(_MainTex, yUV);
				half3 xDiff = tex2D(_MainTex, xUV);
				half3 zDiff = tex2D(_MainTex, zUV);
				half3 blendWeights = pow(abs(IN.worldNormal), _TriplanarBlendSharpness);
				blendWeights = blendWeights / (blendWeights.x + blendWeights.y + blendWeights.z);
				o.Albedo = (xDiff * blendWeights.x + yDiff * blendWeights.y + zDiff * blendWeights.z) * _Color;
			}
			ENDCG
		}
			FallBack "Diffuse"
}
