using Lessie.Application.Tools;

namespace Lessie.Infrastructure.Tools;

internal sealed class ToolRegistry(IEnumerable<ITool> tools) : IToolRegistry
{
    private readonly Dictionary<string, ITool> toolsByName = tools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);

    public ITool? Find(string name)
    {
        return toolsByName.GetValueOrDefault(name);
    }

    public async Task<ToolResult> ExecuteAsync(string name, ToolRequest request, CancellationToken cancellationToken)
    {
        var tool = Find(name);
        if (tool is null)
        {
            return new ToolResult
            {
                Success = false,
                ToolName = name,
                Error = "Ferramenta nao encontrada."
            };
        }

        return await tool.ExecuteAsync(request, cancellationToken);
    }
}
