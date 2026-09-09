// 闪电扭曲着色器 - ps_3_0
// 输入：包含线条的纹理（如白线在黑色背景上）
// 输出：扭曲后的纹理，线条呈现闪电状

sampler Main : register(s0); // 原始线条纹理
float iTime; // 时间变量（用于动画，可由外部传入）

// ---- 伪随机函数 ----
float hash(float x)
{
    return frac(sin(x * 127.1 + 311.7) * 43758.5453);
}

// ---- 1D 平滑噪声（值噪声） ----
float smoothNoise(float x)
{
    float i = floor(x);
    float f = frac(x);
    float a = hash(i);
    float b = hash(i + 1.0);
    // 平滑插值
    float t = f * f * (3.0 - 2.0 * f);
    return lerp(a, b, t);
}

// ---- 分形布朗运动 (fBm) ----
float fbm(float x)
{
    float value = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    for (int i = 0; i < 4; i++)
    {
        value += amp * smoothNoise(x * freq + iTime * 0.3);
        freq *= 2.0;
        amp *= 0.5;
    }
    return value;
}

// ---- 像素着色器主函数 ----
float4 MainPS(float2 uv : TEXCOORD0) : SV_Target
{
    float y = uv.y; // 纵向坐标，决定闪电的走向
    float offset = 0.0;

    // 1. 主扭曲：低频随机弯曲（类似闪电的主干）
    offset += (fbm(y * 8.0) - 0.5) * 0.4;

    // 2. 分支效果：高频噪声的绝对值产生尖锐突起
    float branch = abs(smoothNoise(y * 20.0 + iTime * 0.5) - 0.5) * 0.3;

    // 3. 分支方向随机性（正负交替）
    float signBranch = sign(sin(y * 50.0 + iTime * 2.0));
    offset += branch * signBranch;

    // 4. 额外的高频细节（更细小的分支）
    float detail = abs(smoothNoise(y * 40.0 - iTime * 0.7) - 0.5) * 0.15;
    offset += detail * sign(sin(y * 80.0 + iTime * 3.0));

    // 限制偏移范围，防止拉伸过度
    offset = clamp(offset, -0.6, 0.6);

    // 应用扭曲到纹理坐标
    float2 distortedUV = uv;
    distortedUV.x += offset;

    // 采样并输出
    float4 color = tex2D(Main, distortedUV);
    return color;
}
technique MainTechnique
{
    pass P0
    {
        PixelShader = compile ps_3_0 MainPS(); // 推荐使用 ps_3_0 或更高
    }
}