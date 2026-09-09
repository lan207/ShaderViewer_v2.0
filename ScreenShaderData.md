# ScreenShaderData support

The editor exposes the common values used by Terraria/tModLoader's
`ScreenShaderData`:

| Viewer value | ScreenShaderData API | Shader parameter |
| --- | --- | --- |
| Color | `UseColor` | `uColor` / `_uColor` |
| Secondary color | `UseSecondaryColor` | `uSecondaryColor` / `_uSecondaryColor` |
| Opacity | `UseOpacity` | `uOpacity` / `_uOpacity` |
| Intensity | `UseIntensity` | `uIntensity` / `_uIntensity` |
| Progress | `UseProgress` | `uProgress` / `_uProgress` |
| Direction (`float2`) | `UseDirection` | `uDirection` / `_uDirection` |
| Target position | `UseTargetPosition` | `uTargetPosition` / `_uTargetPosition` |
| Image offset | `UseImageOffset` | `uImageOffset` / `_uImageOffset` |
| Image scale | `UseImageScale` | `uImageScale` / `_uImageScale` |
| Global opacity | `UseGlobalOpacity` | `uGlobalOpacity` / `_uGlobalOpacity` |
| Screen position | supplied by `Apply` | `uScreenPosition` |
| Zoom (`float2`) | supplied by `Apply` | `uZoom` |

`uColor` and `uSecondaryColor` are entered as `float3(r, g, b)` values (the
legacy `#AARRGGBB` form is still accepted for compatibility). `uSecondColor`
is accepted as an alias for `uSecondaryColor`.

`uTime` aliases the existing `iTime`; `uScreenResolution` aliases
`iResolution`. `uImageSize0` is populated from the captured scene/selected
input, while `uImageSize1..3` come independently from `iChannel0..2` and are
multiplied by `UseImageScale`. As in Terraria, the shader's `uOpacity` receives
`UseOpacity * UseGlobalOpacity`.

The WPF path reserves constant registers `c3`–`c13` for these values and moves
the 16 editable scalar parameters to `c14`–`c29`. This keeps existing shader
parameters working while allowing ScreenShaderData-style globals in the same
source file.

Current register note: the WPF runtime now reserves additional registers for
independent `uImageSize1..3`, `uScreenPosition`, and `uZoom`. Editable source
parameters are compiled as constants and do not occupy those runtime slots.
