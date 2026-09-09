// ScreenShaderData-style post-process example.
// The viewer supplies the uColor/uOpacity/uProgress/etc. values from the
// ScreenShaderData tab and maps uImage0 to the first texture slot.
sampler uImage0 : register(s0);
float3 uColor;
float3 uSecondaryColor;
float uOpacity;
float uIntensity;
float uProgress;
float uDirection;
float2 uTargetPosition;
float2 uImageOffset;
float2 uImageScale;

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float2 centered = uv - uTargetPosition;
    float2 sampleUv = centered * uImageScale + uTargetPosition + uImageOffset;
    float4 scene = tex2D(uImage0, sampleUv);
    float radial = saturate(1.0 - length(centered) * 1.4142);
    float sweep = saturate(uProgress + centered.x * uDirection);
    float3 tint = lerp(uSecondaryColor, uColor, sweep);
    float amount = saturate(uIntensity * uOpacity * radial);
    scene.rgb = lerp(scene.rgb, scene.rgb * tint, amount);
    scene.a *= saturate(uOpacity);
    return scene;
}
