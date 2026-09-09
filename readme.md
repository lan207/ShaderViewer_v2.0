# Shader Viewer v2.0 User Guide
## English Guide

### 1. Overview

Shader Viewer edits and previews HLSL/FX pixel shaders. It specifically supports the two rendering paths commonly used by Terraria/tModLoader:

- `GameShaders.Misc`: applies a shader to one SpriteBatch/DrawData sprite, such as the spherical `ForceField` shield.
- `Filters.Scene`: captures the screen and applies one or more `ScreenShaderData` filters to render targets.

Two preview paths are available:

- WPF canvas for quick, ordinary pixel-shader previews.
- MonoGame SpriteBatch floating window for real SpriteBatch states, texture slots, ForceField geometry, and screen render-target filters.

### 2. Requirements and startup

Windows and a .NET SDK are required. Build both projects before the first run:

```powershell
dotnet build ShaderViewer.csproj
dotnet build SpriteBatchPreviewBackend\SpriteBatchPreviewBackend.csproj
```

Start the viewer with:

```powershell
dotnet run --project ShaderViewer.csproj
```

You can also double-click `Run.bat` in the project directory.

### 3. Ordinary shader preview

1. Open an `.fx`, `.hlsl`, or `.txt` shader.
2. Assign `iInput` and optional `iChannel0..2` images on the `Textures` tab.
3. Edit detected parameters or add custom values on the `Params` tab.
4. Select a drawing construction on the `Canvas` tab.
5. Compile/apply the source to view the WPF result.
6. Click `Open SpriteBatch Preview` when real SpriteBatch behavior is required.

Texture-slot mapping:

| Viewer name | Graphics slot | Meaning |
| --- | --- | --- |
| `iInput` | `s0` | Main sprite texture, or captured scene in screen-filter mode |
| `iChannel0` | `s1` | First auxiliary texture |
| `iChannel1` | `s2` | Second auxiliary texture |
| `iChannel2` | `s3` | Third auxiliary texture |

### 4. Spherical ForceField preview

Use XnbFxDecompiler to split Terraria's `PixelShader.xnb`, then open:

```text
XnbFxDecompiler_v2.0\pseudo-fx-split\PixelShader.ForceField0.fx
```

The correct file identifies `ForceField` as Pixel Shader 22, not Shader 53.

1. Assign an extracted Terraria Perlin PNG to `iInput`.
2. Select `ForceField DrawData (2:1 sprite)` on the Canvas tab.
3. Recommended SpriteBatch state:
   - `SpriteSortMode = Immediate`
   - `SamplerState = PointWrap`
   - `DepthStencilState = Default`
   - `BlendState = AlphaBlend`, or `Opaque` for direct reference-image comparison
4. Start with `uColor = float3(1.5, 1.5, 1.5)` and `uTime = 1.0`.

The ForceField construction reproduces the centered 600×600 source rectangle, 2:1 destination scaling, and wrapped UV coordinates used by `ForceField.PreDraw`.

The shader's main texture is the Perlin noise itself. Extract the texture XNB to PNG first. Terraria's original Perlin texture is fully opaque; a low-alpha noise image can make the shield almost invisible.

### 5. ScreenShaderData

The `ScreenShaderData` tab exposes Terraria-style color, secondary color, opacity, intensity, progress, direction, target position, image offset/scale, global opacity, screen position, and zoom. The viewer also supplies time, screen resolution, and independent image sizes for slots 0–3.

Save reusable values as `.screen.json` files with `Save as custom scheme`, reload them with `Load scheme file`, and use the Canvas tab to apply, update, or delete a saved scheme.

### 6. Terraria Vortex filter

1. Click `Load Terraria Vortex Filter`.
2. The Canvas construction automatically switches to `Filters.Scene (screen RenderTarget pass)`.
3. Assign any required auxiliary textures to `iChannel0..2`.
4. Open the SpriteBatch preview to run the filter over a captured scene target.

### 7. Filters.Scene manager

1. Open and configure a screen shader.
2. Click `Register current shader` to snapshot its source, entry point, custom parameters, ScreenShaderData, and texture paths.
3. Select a priority from `VeryLow` through `VeryHigh`.
4. Use `Active` for Terraria-style one-unit-per-second fade-in/out, or `Hidden` to keep the filter registered without drawing it.
5. Configure the 0–16 filter limit and minimum priority threshold.
6. Use `Edit` to load a snapshot and `Capture current` to write the edited state back.
7. Use `Deactivate all` to fade all filters out.
8. Persist the registry with `Save filter stack`; reload `.filters.json` files with `Load filter stack`.
9. Select the `Filters.Scene` Canvas construction and open the SpriteBatch preview.

The native backend compiles/loads every Effect independently, ping-pongs visible passes between two `RenderTarget2D` objects, and renders the final pass directly to the back buffer.

### 8. Troubleshooting

If ForceField shows only the background or appears transparent:

- Use the newly generated Shader 22 `PixelShader.ForceField0.fx`.
- Load an opaque Perlin PNG into `iInput`.
- Select `PointWrap` and the ForceField drawing construction.
- Verify that color, opacity, and source image alpha are nonzero.

The WPF canvas is intentionally a fast approximation. Use the MonoGame SpriteBatch window as the authoritative preview for ForceField, sampler-sensitive effects, and multi-pass `Filters.Scene` rendering.

Terraria's original Effect XNB uses an XNA/D3D9 container and cannot be loaded directly by the D3D11 MonoGame backend. Decompile it to FX first; the viewer recompiles the reconstruction to an MGFX-compatible `level_9_3` target.

Author: **codex gpt5.6**