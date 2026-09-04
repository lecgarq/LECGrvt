namespace LECG.Core.Substance;

public sealed record SubstanceRowState(string Category, string Slug, string DisplayName, bool IsSelected);

public static class SubstanceSelectionPolicy
{
    public static bool? CategoryState(IEnumerable<SubstanceRowState> rows, string category)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(category);
        var inCat = rows.Where(r => Same(r.Category, category)).ToList();
        if (inCat.Count == 0) return false;
        int selected = inCat.Count(r => r.IsSelected);
        if (selected == 0) return false;
        if (selected == inCat.Count) return true;
        return null;
    }

    public static IReadOnlyList<SubstanceRowState> SetCategory(IEnumerable<SubstanceRowState> rows, string category, bool selected)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(category);
        return rows.Select(r => Same(r.Category, category) ? r with { IsSelected = selected } : r).ToList();
    }

    public static IReadOnlyList<SubstanceRowState> SetAll(IEnumerable<SubstanceRowState> rows, bool selected)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows.Select(r => r with { IsSelected = selected }).ToList();
    }

    public static bool Matches(SubstanceRowState row, string? filter)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (string.IsNullOrWhiteSpace(filter)) return true;
        string f = filter.Trim();
        return row.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase)
            || row.Category.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<(string Category, int Count)> CategoryCounts(IEnumerable<SubstanceRowState> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows.GroupBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.Count()))
            .ToList();
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
