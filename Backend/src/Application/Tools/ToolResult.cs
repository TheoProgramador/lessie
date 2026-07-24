namespace Lessie.Application.Tools;

public sealed class ToolResult
{
    public bool Success { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public object? Data { get; set; }
    public string? Error { get; set; }
}
