#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0
	#define PS_SHADERMODEL ps_4_0
#endif

// Ця матриця - ключ до успіху, SpriteBatch її передає, а ми приймаємо
float4x4 MatrixTransform;

sampler TextureSampler : register(s0);

// Параметри
float2 TextureSize;
float BlurStrength;

struct VSInput
{
	float4 Position : POSITION0;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
};

VSOutput MainVS(VSInput input)
{
	VSOutput output;
	output.Position = mul(input.Position, MatrixTransform);
	output.Color = input.Color;
	output.TexCoord = input.TexCoord;
	return output;
}

float4 MainPS(VSOutput input) : COLOR0
{
	float4 sum = 0;
	float totalWeight = 0;
	
	// Щоб не передавати TextureSize з C# постійно, можна для тесту лишити хардкод,
	// але краще спробувати передати правильно.
	// Якщо знову буде чорне - розкоментуйте хардкод нижче:
	float2 texelSize = 1.0f / TextureSize; 
	// float2 texelSize = 1.0f / float2(960.0, 540.0); 

    // Stride розширює блюр, роблячи його сильнішим візуально
    float stride = 3.0f;

	[unroll]
	for (int x = -3; x <= 3; x++)
	{
		[unroll]
		for (int y = -3; y <= 3; y++)
		{
			float distSq = x*x + y*y;
			float weight = 1.0f / (1.0f + distSq / (BlurStrength + 0.1f));
            
            float2 offset = float2(x, y) * texelSize * stride;
			sum += tex2D(TextureSampler, input.TexCoord + offset) * weight;
			totalWeight += weight;
		}
	}
	
	return (sum / totalWeight) * input.Color;
}

technique GaussianBlur
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};