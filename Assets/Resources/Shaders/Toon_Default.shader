Shader "Toon/Default" {
	Properties {
		_MainTex("MainTex",2D)="white"{}
	}
	SubShader{
		Tags{
			"RenderType"="Opaque"
		}
		Pass{
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			uniform sampler2D _MainTex;
			uniform fixed4 _LightColor0;

			struct VertexInput{
				float4 vertex:POSITION;
				float3 normal:NORMAL;
				float2 uv:TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput{
				float4 vertex:SV_POSITION;
				float3 normal:NORMAL;
				float2 uv:TEXCOORD0;
			};

			VertexOutput vert(VertexInput input){
				VertexOutput output;
				UNITY_SETUP_INSTANCE_ID(input);
				output.vertex=UnityObjectToClipPos(input.vertex);
				output.normal=UnityObjectToWorldNormal(input.normal);
				output.uv=input.uv;
				return output;
			}

			float4 frag(VertexOutput output):COLOR{
				float4 col=tex2D(_MainTex,output.uv);
				float rgb=step(dot(output.normal,_WorldSpaceLightPos0.xyz),0);
				col.rgb*=UNITY_LIGHTMODEL_AMBIENT.rgb*(_LightColor0.rgb-rgb)+rgb;
				return col;
			}
			ENDCG
		}
	}
}
