#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0
	#define PS_SHADERMODEL ps_4_0
#endif

// Ця матриця критично важлива. Без неї - чорний екран.
float4x4 MatrixTransform;

texture ScreenTexture; // MonoGame автоматично підкладе сюди текстуру з SpriteBatch

sampler TextureSampler = sampler_state
{
    Texture = <ScreenTexture>;
    
    // ПРИМУСОВЕ УВІМКНЕННЯ ЛІНІЙНОЇ ФІЛЬТРАЦІЇ
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    
    // Щоб краї не "загорталися"
    AddressU = Clamp;
    AddressV = Clamp;
};

float2 ScreenSize;
float BlurStrength;

struct VSInput {
	float4 Position : POSITION0;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
};
struct VSOutput {
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
};

VSOutput MainVS(VSInput input) {
	VSOutput output;
	// Множення позиції на матрицю. Якщо матриця нульова -> Position = 0 -> Нічого не малюється.
	output.Position = mul(input.Position, MatrixTransform);
	output.Color = input.Color;
	output.TexCoord = input.TexCoord;
	return output;
}

float4 GaussianBlur(VSOutput input, float2 dir)
{
    // Запобіжник: якщо ScreenSize забули передати (він 0), ставимо дефолт
    float2 safeScreenSize = ScreenSize;
    if (safeScreenSize.x < 1.0) safeScreenSize = float2(1920.0, 1080.0);

    float3 offsets[3];
    float3 weights[3];
    
    // Ваги
    weights[0] = float3(0.227027, 0.227027, 0.227027);
    weights[1] = float3(0.316216, 0.316216, 0.316216);
    weights[2] = float3(0.070270, 0.070270, 0.070270);
    
    // Офсети для Linear Sampling
    offsets[0] = float3(0.0, 0.0, 0.0);
    offsets[1] = float3(1.3846153846, 1.3846153846, 1.3846153846);
    offsets[2] = float3(3.2307692308, 3.2307692308, 3.2307692308);

    float4 color = tex2D(TextureSampler, input.TexCoord) * float4(weights[0], 1.0);
    
    float2 step = dir / safeScreenSize * BlurStrength;

    for (int i = 1; i < 3; i++) {
        color += tex2D(TextureSampler, input.TexCoord + (step * offsets[i].x)) * float4(weights[i], 1.0);
        color += tex2D(TextureSampler, input.TexCoord - (step * offsets[i].x)) * float4(weights[i], 1.0);
    }

    return color * input.Color;
}

float4 HorizontalPS(VSOutput input) : COLOR0 {
    return GaussianBlur(input, float2(1.0, 0.0));
}

float4 VerticalPS(VSOutput input) : COLOR0 {
    return GaussianBlur(input, float2(0.0, 1.0));
}

technique Blur
{
	pass Horizontal 
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL HorizontalPS();
	}
	pass Vertical
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL VerticalPS();
	}
};