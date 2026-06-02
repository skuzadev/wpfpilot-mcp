using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Core.Primitives;

namespace WpfPilot.Mcp.Core.Abstractions;

public interface ISelectorResolver
{
    Result<IElement> Resolve(ElementSelector selector);
    Result<bool> Validate(ElementSelector selector);
    Result<(bool IsUnique, int MatchCount)> ValidateUniqueness(ElementCriteria criteria);
    Result<IReadOnlyList<SelectorCandidate>> Rank(IElement element);
    Result<ElementSelector> Build(IElement element);
    string? LastStrategy { get; }
}

public sealed class SelectorCandidate
{
    public ElementSelector Selector { get; init; } = new();
    public string Strategy { get; init; } = string.Empty;
    public int Stability { get; init; }
    public int MatchCount { get; init; }
}
