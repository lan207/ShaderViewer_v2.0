# Terraria Filters.Scene preview

The viewer keeps Terraria's two shader systems separate:

- `GameShaders.Misc["ForceField"]` applies the `PixelShader.xnb` `ForceField`
  pass to one `DrawData` sprite.
- `Filters.Scene["Vortex"]` owns a `ScreenShaderData` using the
  `ScreenShader.xnb` `FilterTower` pass. It runs later over a captured screen
  render target and does not change the ForceField sprite's geometry.

Select **Filters.Scene (screen RenderTarget pass)** in the Canvas tab (or open
an entry point whose name starts with `Filter`) to use the screen-filter path.
The native backend renders the input scene into a `RenderTarget2D`, then draws
that captured texture through the selected shader pass. Texture slot mapping
matches `ScreenShaderData.Apply`:

| Viewer texture | Graphics slot | Terraria meaning |
| --- | --- | --- |
| `iInput` | `s0` | Captured scene render target |
| `iChannel0` | `s1` | `UseImage(..., 0)` |
| `iChannel1` | `s2` | `UseImage(..., 1)` |
| `iChannel2` | `s3` | `UseImage(..., 2)` |

The implementation supplies `uColor`, `uSecondaryColor`, combined
`uOpacity * uGlobalOpacity`, `uTime`, `uScreenResolution`, `uScreenPosition`,
`uTargetPosition`, `uImageOffset`, `uIntensity`, `uProgress`, `float2
uDirection`, `uZoom`, and independent `uImageSize0..3` values.

Opening the decompiled `FilterTower` pass automatically selects the screen
render-target mode and applies the Terraria Vortex initializer values:

```text
uColor = (0, 0.7, 0.7)
uOpacity = 0.5
uIntensity = 1
uProgress = 0
uDirection = (0, 1)
uZoom = (1, 1)
```

The original Terraria 1.4.4 `ScreenShader.xnb` is an XNA/D3DX9 Effect
container and cannot be loaded directly by the MonoGame D3D11 backend. The
built-in `Terraria.FilterTower.fx` was reconstructed from that XNB's D3D9
pixel bytecode; WPF recompiles it as ps_2_0 and the native preview recompiles
it as MGFX with a compatible sprite vertex shader.

The **Filters.Scene** tab exposes the matching multi-filter manager:

- Register the current shader and its current `ScreenShaderData`/texture values.
- Choose `VeryLow` through `VeryHigh` priority, `Active`, or `Hidden` per filter.
- Set the 0–16 filter limit and the minimum priority threshold.
- Save/load the complete registry as a `.filters.json` file.
- Use **Edit** to load a snapshot into the editors and **Capture current** to
  write the edited shader, parameters, textures, and ScreenShaderData back.

When the floating preview is opened in `Filters.Scene` geometry, every
registered shader is compiled independently. The native manager applies
Terraria's one-opacity-unit-per-second activation/deactivation fade, keeps the
highest-priority filters allowed by the limit, skips hidden filters, and
ping-pongs visible passes between two `RenderTarget2D` instances before the
last pass is drawn to the back buffer.
