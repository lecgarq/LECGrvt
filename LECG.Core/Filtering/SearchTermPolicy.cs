namespace LECG.Core.Filtering;

public static class SearchTermPolicy
{
    public static IReadOnlyList<string> ParseTerms(string? filterText)
    {
        return filterText?.Split(';', StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.Trim())
                   .Where(s => !string.IsNullOrEmpty(s))
                   .ToList()
               ?? new List<string>();
    }

    public static bool MatchesAny(string source, IReadOnlyList<string> terms)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(terms);
        return terms.Any(term => source.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
