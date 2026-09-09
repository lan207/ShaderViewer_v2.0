# SpriteBatch preview integration

The editor now has an **Open SpriteBatch Preview** button. The launched
MonoGame window is an owned floating window: it stays associated with the WPF
main window, follows minimize/restore behavior, is removed from the taskbar,
and is closed when the editor exits. Build the generated
backend first:

```powershell
dotnet build SpriteBatchPreviewBackend\SpriteBatchPreviewBackend.csproj
```

The button launches the backend with the selected `SpriteSortMode`, blend,
sampler and depth/stencil settings plus the selected checkerboard/solid
background and the selected `iInput` texture. `Game1` calls the real MonoGame overload:

```csharp
spriteBatch.Begin(sortMode, blendState, samplerState, depthStencilState,
                  RasterizerState.CullNone, effect);
```

The WPF canvas remains the fast in-process preview. The MonoGame window also
receives a temporary MGCB-compiled effect and loads it through
`ContentManager`, so the pixel shader sees the same SpriteBatch
sampler/constant layout.

The Canvas tab has three geometry modes. `Surface` keeps the original full
canvas behavior; `Centered sprite` draws a square source rectangle around the
preview center; and `ForceField DrawData` reproduces the projectile's centered
600x600 source rectangle with `Vector2(2, 1)` scaling. The latter generates a
deterministic Perlin-like fallback texture when the Terraria `Perlin.xnb` asset
is not available. Select an extracted Perlin image in the Textures tab to use
the original asset instead.
