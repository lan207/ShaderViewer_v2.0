using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShaderViewer.Rendering;

// The WPF preview backend does not ship with the XNA assemblies.  These enums
// intentionally mirror the values accepted by SpriteBatch.Begin so a shader
// can be authored and reviewed with the same draw-state vocabulary.  A future
// MonoGame/Direct3D backend can pass these values through without changing the
// UI or the saved project format.
public enum SpriteSortMode
{
    Deferred,
    Immediate,
    Texture,
    BackToFront,
    FrontToBack
}

public enum SpriteBlendState
{
    AlphaBlend,
    Additive,
    Opaque,
    NonPremultiplied
}

public enum SpriteSamplerState
{
    PointClamp,
    LinearClamp,
    PointWrap,
    LinearWrap,
    AnisotropicClamp,
    AnisotropicWrap
}

public enum SpriteDepthStencilState
{
    None,
    Default,
    DepthRead
}

public sealed class SpriteBatchSettings : INotifyPropertyChanged
{
    private SpriteSortMode _sortMode = SpriteSortMode.Deferred;
    private SpriteBlendState _blendState = SpriteBlendState.AlphaBlend;
    private SpriteSamplerState _samplerState = SpriteSamplerState.LinearClamp;
    private SpriteDepthStencilState _depthStencilState = SpriteDepthStencilState.None;

    public SpriteSortMode SortMode
    {
        get => _sortMode;
        set => Set(ref _sortMode, value);
    }

    public SpriteBlendState BlendState
    {
        get => _blendState;
        set => Set(ref _blendState, value);
    }

    public SpriteSamplerState SamplerState
    {
        get => _samplerState;
        set => Set(ref _samplerState, value);
    }

    public SpriteDepthStencilState DepthStencilState
    {
        get => _depthStencilState;
        set => Set(ref _depthStencilState, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
