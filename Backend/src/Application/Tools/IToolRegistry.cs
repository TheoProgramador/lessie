namespace Lessie.Application.Tools;

public interface IToolRegistry
{
    ITool? Find(string name);
    Task<ToolResult> ExecuteAsync(string name, ToolRequest request, CancellationToken cancellationToken);
}
