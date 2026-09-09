namespace ShaderViewer.ViewModels;

/// <summary>One source-level compiler or runtime-safety diagnostic.</summary>
public sealed record ShaderDiagnosticItem(string Severity, string Message, string SourceFile, int? Line, int? Column)
{
    public string Location => Line is null
        ? $"{SourceFile} (source location unavailable)"
        : Column is null
            ? $"{SourceFile}, line {Line}, column unavailable"
            : $"{SourceFile}, line {Line}, column {Column}";

    public string DisplayText => string.Concat(Severity.ToUpperInvariant(), ": ", Location, " - ", Message);
}
