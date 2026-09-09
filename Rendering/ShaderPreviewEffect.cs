using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ShaderViewer.Rendering;

internal sealed class ShaderPreviewEffect : ShaderEffect
{
    private readonly PixelShader _shader = new();
    private MemoryStream? _shaderStream;

    public ShaderPreviewEffect()
    {
        PixelShader = _shader;

        UpdateShaderValue(InputProperty);
        UpdateShaderValue(Channel0Property);
        UpdateShaderValue(Channel1Property);
        UpdateShaderValue(Channel2Property);

        UpdateShaderValue(TimeProperty);
        UpdateShaderValue(ResolutionXProperty);
        UpdateShaderValue(ResolutionYProperty);

        UpdateShaderValue(ScreenColorProperty);
        UpdateShaderValue(SecondaryColorProperty);
        UpdateShaderValue(OpacityProperty);
        UpdateShaderValue(IntensityProperty);
        UpdateShaderValue(ProgressProperty);
        UpdateShaderValue(DirectionProperty);
        UpdateShaderValue(TargetPositionProperty);
        UpdateShaderValue(ImageOffsetProperty);
        UpdateShaderValue(ImageScaleProperty);
        UpdateShaderValue(GlobalOpacityProperty);
        UpdateShaderValue(ImageSize0Property);
        UpdateShaderValue(ImageSize1Property);
        UpdateShaderValue(ImageSize2Property);
        UpdateShaderValue(ImageSize3Property);
        UpdateShaderValue(ScreenPositionProperty);
        UpdateShaderValue(ZoomProperty);
        UpdateShaderValue(SourceRectProperty);

        // Editable parameters are compiled as static constants by
        // ShaderSourceTools. They do not occupy runtime WPF constant
        // registers; keeping these legacy dependency properties unregistered
        // prevents overlap with the ScreenShaderData c14..c18 values.
    }

    public void LoadShader(byte[] bytecode)
    {
        _shaderStream?.Dispose();
        _shaderStream = new MemoryStream(bytecode, writable: false);
        _shader.SetStreamSource(_shaderStream);
    }

    public Brush Input
    {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public static readonly DependencyProperty InputProperty =
        ShaderEffect.RegisterPixelShaderSamplerProperty(nameof(Input), typeof(ShaderPreviewEffect), 0);

    public void SetUniform(int index, double value)
    {
        switch (index)
        {
            case 0: Param0 = value; break;
            case 1: Param1 = value; break;
            case 2: Param2 = value; break;
            case 3: Param3 = value; break;
            case 4: Param4 = value; break;
            case 5: Param5 = value; break;
            case 6: Param6 = value; break;
            case 7: Param7 = value; break;
            case 8: Param8 = value; break;
            case 9: Param9 = value; break;
            case 10: Param10 = value; break;
            case 11: Param11 = value; break;
            case 12: Param12 = value; break;
            case 13: Param13 = value; break;
            case 14: Param14 = value; break;
            case 15: Param15 = value; break;
        }
    }

    public Brush Channel0
    {
        get => (Brush)GetValue(Channel0Property);
        set => SetValue(Channel0Property, value);
    }

    public static readonly DependencyProperty Channel0Property =
        ShaderEffect.RegisterPixelShaderSamplerProperty(nameof(Channel0), typeof(ShaderPreviewEffect), 1);

    public Brush Channel1
    {
        get => (Brush)GetValue(Channel1Property);
        set => SetValue(Channel1Property, value);
    }

    public static readonly DependencyProperty Channel1Property =
        ShaderEffect.RegisterPixelShaderSamplerProperty(nameof(Channel1), typeof(ShaderPreviewEffect), 2);

    public Brush Channel2
    {
        get => (Brush)GetValue(Channel2Property);
        set => SetValue(Channel2Property, value);
    }

    public static readonly DependencyProperty Channel2Property =
        ShaderEffect.RegisterPixelShaderSamplerProperty(nameof(Channel2), typeof(ShaderPreviewEffect), 3);

    public double Time
    {
        get => (double)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public static readonly DependencyProperty TimeProperty =
        DependencyProperty.Register(nameof(Time), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0)));

    public double ResolutionX
    {
        get => (double)GetValue(ResolutionXProperty);
        set => SetValue(ResolutionXProperty, value);
    }

    public static readonly DependencyProperty ResolutionXProperty =
        DependencyProperty.Register(nameof(ResolutionX), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(1.0, PixelShaderConstantCallback(1)));

    public double ResolutionY
    {
        get => (double)GetValue(ResolutionYProperty);
        set => SetValue(ResolutionYProperty, value);
    }

    public static readonly DependencyProperty ResolutionYProperty =
        DependencyProperty.Register(nameof(ResolutionY), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(1.0, PixelShaderConstantCallback(2)));

    public Color ScreenColor
    {
        get => (Color)GetValue(ScreenColorProperty);
        set => SetValue(ScreenColorProperty, value);
    }

    public static readonly DependencyProperty ScreenColorProperty =
        DependencyProperty.Register(nameof(ScreenColor), typeof(Color), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(Colors.White, PixelShaderConstantCallback(3)));

    public Color SecondaryColor
    {
        get => (Color)GetValue(SecondaryColorProperty);
        set => SetValue(SecondaryColorProperty, value);
    }

    public static readonly DependencyProperty SecondaryColorProperty =
        DependencyProperty.Register(nameof(SecondaryColor), typeof(Color), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(Colors.White, PixelShaderConstantCallback(4)));

    public double Opacity
    {
        get => (double)GetValue(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    public static readonly DependencyProperty OpacityProperty =
        DependencyProperty.Register(nameof(Opacity), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(1.0, PixelShaderConstantCallback(5)));

    public double Intensity
    {
        get => (double)GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public static readonly DependencyProperty IntensityProperty =
        DependencyProperty.Register(nameof(Intensity), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(1.0, PixelShaderConstantCallback(6)));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(7)));

    public Point Direction
    {
        get => (Point)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public static readonly DependencyProperty DirectionProperty =
        DependencyProperty.Register(nameof(Direction), typeof(Point), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(new Point(0, 1), PixelShaderConstantCallback(8)));

    public Point TargetPosition
    {
        get => (Point)GetValue(TargetPositionProperty);
        set => SetValue(TargetPositionProperty, value);
    }

    public static readonly DependencyProperty TargetPositionProperty =
        DependencyProperty.Register(nameof(TargetPosition), typeof(Point), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(9)));

    public Point ImageOffset
    {
        get => (Point)GetValue(ImageOffsetProperty);
        set => SetValue(ImageOffsetProperty, value);
    }

    public static readonly DependencyProperty ImageOffsetProperty =
        DependencyProperty.Register(nameof(ImageOffset), typeof(Point), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(new Point(0, 0), PixelShaderConstantCallback(10)));

    public Point ImageScale
    {
        get => (Point)GetValue(ImageScaleProperty);
        set => SetValue(ImageScaleProperty, value);
    }

    public static readonly DependencyProperty ImageScaleProperty =
        DependencyProperty.Register(nameof(ImageScale), typeof(Point), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(11)));

    public double GlobalOpacity
    {
        get => (double)GetValue(GlobalOpacityProperty);
        set => SetValue(GlobalOpacityProperty, value);
    }

    public static readonly DependencyProperty GlobalOpacityProperty =
        DependencyProperty.Register(nameof(GlobalOpacity), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(1.0, PixelShaderConstantCallback(12)));

    public Point ImageSize0
    {
        get => (Point)GetValue(ImageSize0Property);
        set => SetValue(ImageSize0Property, value);
    }

    public static readonly DependencyProperty ImageSize0Property =
        DependencyProperty.Register(nameof(ImageSize0), typeof(Point), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(13)));

    public Point ImageSize1
    {
        get => (Point)GetValue(ImageSize1Property);
        set => SetValue(ImageSize1Property, value);
    }

    public static readonly DependencyProperty ImageSize1Property =
        DependencyProperty.Register(nameof(ImageSize1), typeof(Point), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(14)));

    public Point ImageSize2
    {
        get => (Point)GetValue(ImageSize2Property);
        set => SetValue(ImageSize2Property, value);
    }

    public static readonly DependencyProperty ImageSize2Property =
        DependencyProperty.Register(nameof(ImageSize2), typeof(Point), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(15)));

    public Point ImageSize3
    {
        get => (Point)GetValue(ImageSize3Property);
        set => SetValue(ImageSize3Property, value);
    }

    public static readonly DependencyProperty ImageSize3Property =
        DependencyProperty.Register(nameof(ImageSize3), typeof(Point), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(16)));

    public Point ScreenPosition
    {
        get => (Point)GetValue(ScreenPositionProperty);
        set => SetValue(ScreenPositionProperty, value);
    }

    public static readonly DependencyProperty ScreenPositionProperty =
        DependencyProperty.Register(nameof(ScreenPosition), typeof(Point), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(new Point(0, 0), PixelShaderConstantCallback(17)));

    public Point Zoom
    {
        get => (Point)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(nameof(Zoom), typeof(Point), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(18)));

    /// <summary>
    /// SpriteBatch/DrawData source rectangle in source-texture pixels.  The
    /// ForceField effect only consumes X, but exposing all four components as a
    /// Color keeps the WPF ShaderEffect constant layout at register c30.
    /// </summary>
    public Color SourceRect
    {
        get => (Color)GetValue(SourceRectProperty);
        set => SetValue(SourceRectProperty, value);
    }

    public static readonly DependencyProperty SourceRectProperty =
        DependencyProperty.Register(nameof(SourceRect), typeof(Color), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(Colors.Transparent, PixelShaderConstantCallback(30)));

    public double Param0
    {
        get => (double)GetValue(Param0Property);
        set => SetValue(Param0Property, value);
    }

    public static readonly DependencyProperty Param0Property =
        DependencyProperty.Register(nameof(Param0), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(14)));

    public double Param1
    {
        get => (double)GetValue(Param1Property);
        set => SetValue(Param1Property, value);
    }

    public static readonly DependencyProperty Param1Property =
        DependencyProperty.Register(nameof(Param1), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(15)));

    public double Param2
    {
        get => (double)GetValue(Param2Property);
        set => SetValue(Param2Property, value);
    }

    public static readonly DependencyProperty Param2Property =
        DependencyProperty.Register(nameof(Param2), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(16)));

    public double Param3
    {
        get => (double)GetValue(Param3Property);
        set => SetValue(Param3Property, value);
    }

    public static readonly DependencyProperty Param3Property =
        DependencyProperty.Register(nameof(Param3), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(17)));

    public double Param4
    {
        get => (double)GetValue(Param4Property);
        set => SetValue(Param4Property, value);
    }

    public static readonly DependencyProperty Param4Property =
        DependencyProperty.Register(nameof(Param4), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(18)));

    public double Param5
    {
        get => (double)GetValue(Param5Property);
        set => SetValue(Param5Property, value);
    }

    public static readonly DependencyProperty Param5Property =
        DependencyProperty.Register(nameof(Param5), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(19)));

    public double Param6
    {
        get => (double)GetValue(Param6Property);
        set => SetValue(Param6Property, value);
    }

    public static readonly DependencyProperty Param6Property =
        DependencyProperty.Register(nameof(Param6), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(20)));

    public double Param7
    {
        get => (double)GetValue(Param7Property);
        set => SetValue(Param7Property, value);
    }

    public static readonly DependencyProperty Param7Property =
        DependencyProperty.Register(nameof(Param7), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(21)));

    public double Param8
    {
        get => (double)GetValue(Param8Property);
        set => SetValue(Param8Property, value);
    }

    public static readonly DependencyProperty Param8Property =
        DependencyProperty.Register(nameof(Param8), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(22)));

    public double Param9
    {
        get => (double)GetValue(Param9Property);
        set => SetValue(Param9Property, value);
    }

    public static readonly DependencyProperty Param9Property =
        DependencyProperty.Register(nameof(Param9), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(23)));

    public double Param10
    {
        get => (double)GetValue(Param10Property);
        set => SetValue(Param10Property, value);
    }

    public static readonly DependencyProperty Param10Property =
        DependencyProperty.Register(nameof(Param10), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(24)));

    public double Param11
    {
        get => (double)GetValue(Param11Property);
        set => SetValue(Param11Property, value);
    }

    public static readonly DependencyProperty Param11Property =
        DependencyProperty.Register(nameof(Param11), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(25)));

    public double Param12
    {
        get => (double)GetValue(Param12Property);
        set => SetValue(Param12Property, value);
    }

    public static readonly DependencyProperty Param12Property =
        DependencyProperty.Register(nameof(Param12), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(26)));

    public double Param13
    {
        get => (double)GetValue(Param13Property);
        set => SetValue(Param13Property, value);
    }

    public static readonly DependencyProperty Param13Property =
        DependencyProperty.Register(nameof(Param13), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(27)));

    public double Param14
    {
        get => (double)GetValue(Param14Property);
        set => SetValue(Param14Property, value);
    }

    public static readonly DependencyProperty Param14Property =
        DependencyProperty.Register(nameof(Param14), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(28)));

    public double Param15
    {
        get => (double)GetValue(Param15Property);
        set => SetValue(Param15Property, value);
    }

    public static readonly DependencyProperty Param15Property =
        DependencyProperty.Register(nameof(Param15), typeof(double), typeof(ShaderPreviewEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(29)));
}
