Shader "Unlit/WaterShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct v2f
            {
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
				float4 view : TEXCOORD1;
				float3 viewDir : TEXCOORD2;
				float3 normal : NORMAL;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

			uniform sampler2D _RefractionTex;
			uniform samplerCUBE _ReflectionTex;

            v2f vert (appdata_base v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o,o.vertex);
				o.view = UnityObjectToClipPos(v.vertex);
				o.viewDir = mul(unity_ObjectToWorld, v.vertex).xyz - _WorldSpaceCameraPos;
				o.normal = mul(unity_ObjectToWorld, v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
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
