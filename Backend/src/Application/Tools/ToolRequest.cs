namespace Lessie.Application.Tools;

public sealed class ToolRequest
{
    public string Query { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int? Limit { get; set; }
}
