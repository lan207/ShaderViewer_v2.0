# MonoGame SpriteBatch preview backend

This project was generated with the `mgwindowsdx` template from
`MonoGame.Templates.CSharp@3.8.5.1`:

```powershell
dotnet new install MonoGame.Templates.CSharp@3.8.5.1
dotnet new mgwindowsdx -n SpriteBatchPreviewBackend -o SpriteBatchPreviewBackend
```

`Game1` contains the real `SpriteBatch.Begin(sortMode, blendState,
samplerState, depthStencilState, rasterizerState, effect)` path. It also draws
a checkerboard beneath the sprite, or clears to an alpha-bearing solid color.
Replace the placeholder 1x1 texture with the compiled sprite texture/effect
when wiring this project to the editor process.
