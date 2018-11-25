Shader "Unlit/DrawGrassLines"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		Cull Off

		Pass
		{
			CGPROGRAM
			#pragma target 5.0
			#pragma vertex VS_Main
			#pragma geometry GS_Main
			#pragma fragment FS_Main

			#include "UnityCG.cginc"

			float4x4 mvp;
			float2 worldPositionOffset;
			float2 worldDimensions;
			float grassLength;
			StructuredBuffer<float2> grassVerts;
			Texture2D<float4> displacementTex;

			struct VS_Input
			{
				uint id : SV_VertexID;
			};

			struct GS_Input
			{
				float4 pos : POSITION;
			};

			struct FS_Input
			{
				float4 pos : POSITION;
				float4 col : COLOR;
			};

			SamplerState samplerdisplacementTex;

			float random(float2 p)
			{
				return frac(cos(dot(p, float2(23.14069263277926, 2.665144142690225)))*123456.);
			}

			GS_Input VS_Main(VS_Input v)
			{
				GS_Input o = (GS_Input)0;
				float2 grassVertPos = grassVerts[v.id];
				o.pos = float4(grassVertPos.x, 0, grassVertPos.y, 1);
				return o;
			}

			[maxvertexcount(2)]
			void GS_Main(point GS_Input p[1], inout LineStream<FS_Input> lineStream)
			{
				float2 uv = p[0].pos.xz - worldPositionOffset; // [0,128]
				uv /= worldDimensions; // [-0.5, 0.5]
				uv += 0.5; // [0, 1]
				uv = 1 - uv;

				float4 d = displacementTex.SampleLevel(samplerdisplacementTex, uv, 0);

				FS_Input pIn;

				float r = random(uv);
				pIn.pos = mul(mvp, p[0].pos);
				pIn.col = 0.1 * r + fixed4(0.2 + 0.1 * r, 0.5, 0.05, 1.0);
				lineStream.Append(pIn);

				float3 p2 = float3(0, 3, 0) + d.xyz;
				p2 = grassLength * normalize(p2);
				p2 = p[0].pos.xyz + p2;

				pIn.pos = mul(mvp, float4(p2, 1));
				pIn.col = 0.1 * r + lerp(fixed4(0.2 + 0.1 * r, 0.5, 0.05, 1.0), fixed4(0.3 + 0.1 * r, 0.75, 0.1, 1.0), length(d.xyz) / 5);
				lineStream.Append(pIn);
			}

			fixed4 FS_Main(FS_Input i) : SV_Target
			{
				// sample the texture
				fixed4 col = i.col;
				return col;
			}
			ENDCG
		}
	}
}
