Shader "Toon/Animation" {
	Properties{
		_MainTex("MainTex",2D)="white"{}
		_SkinningTex("SkinningTex",2D)="black"{}
		_SkinningTexSize("SkinningTexSize",Float)=0
	}
	SubShader{
		Tags{
			"RenderType"="Opaque"
		}
		Pass{
			Cull Off
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
            
			uniform sampler2D _MainTex;
			uniform fixed4 _LightColor0;
			uniform sampler2D _SkinningTex;
			uniform float _SkinningTexSize;
            
			UNITY_INSTANCING_BUFFER_START(Props)
				UNITY_DEFINE_INSTANCED_PROP(float,_PixelStartIndex)
			UNITY_INSTANCING_BUFFER_END(Props)
            
			inline float4 getUV(float startIndex){
				float y=(int)(startIndex/_SkinningTexSize);
				float u=(startIndex-y*_SkinningTexSize)/_SkinningTexSize;
				float v=y/_SkinningTexSize;
				return float4(u,v,0,0);
			}
			inline float4x4 getMatrix(float startIndex){
				float4 row0=tex2Dlod(_SkinningTex,getUV(startIndex));
				float4 row1=tex2Dlod(_SkinningTex,getUV(startIndex+1));
				float4 row2=tex2Dlod(_SkinningTex,getUV(startIndex+2));
				return float4x4(row0,row1,row2,float4(0,0,0,1));
			}
			struct VertexInput{
				float4 vertex:POSITION;
				float4 normal:NORMAL;
				float2 uv:TEXCOORD0;
				float4 uv1:TEXCOORD1;
				float4 uv2:TEXCOORD2;
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
				float startPixelIndex=UNITY_ACCESS_INSTANCED_PROP(Props,_PixelStartIndex);
				float4 index=input.uv1;
				float4 weight=input.uv2;
				float4x4 matrix1=getMatrix(startPixelIndex+index.x*3);
				float4x4 matrix2=getMatrix(startPixelIndex+index.y*3);
				float4x4 matrix3=getMatrix(startPixelIndex+index.z*3);
				float4x4 matrix4=getMatrix(startPixelIndex+index.w*3);
				float4 vertex=mul(matrix1,input.vertex)*weight.x;
    			vertex=vertex+mul(matrix2,input.vertex)*weight.y;
    			vertex=vertex+mul(matrix3,input.vertex)*weight.z;
    			vertex=vertex+mul(matrix4,input.vertex)*weight.w;
				float4 normal=mul(matrix1,input.normal)*weight.x;
    			normal=normal+mul(matrix2,input.normal)*weight.y;
    			normal=normal+mul(matrix3,input.normal)*weight.z;
    			normal=normal+mul(matrix4,input.normal)*weight.w;
				output.vertex=UnityObjectToClipPos(vertex);
				output.normal=UnityObjectToWorldNormal(normal);
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
