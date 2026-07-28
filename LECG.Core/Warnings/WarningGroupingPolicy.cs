using System.Collections.Generic;
using System.Linq;

namespace LECG.Core.Warnings
{
    /// <summary>One warning read from the model: description, severity, failing element ids.</summary>
    public sealed record WarningItem(string Description, string Severity, IReadOnlyList<long> ElementIds);

    /// <summary>Warnings sharing a description: occurrence count and the distinct failing element ids.</summary>
    public sealed record WarningGroup(string Description, string Severity, int Count, IReadOnlyList<long> ElementIds);

    /// <summary>
    /// Groups warnings by description text. Groups are ordered by count descending,
    /// then description ascending; element ids are distinct within each group.
    /// </summary>
    public static class WarningGroupingPolicy
    {
        public static IReadOnlyList<WarningGroup> Group(IEnumerable<WarningItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            return items
                .GroupBy(i => i.Description, StringComparer.Ordinal)
                .Select(g => new WarningGroup(
                    g.Key,
                    g.First().Severity,
                    g.Count(),
                    g.SelectMany(i => i.ElementIds).Distinct().ToList()))
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.Description, StringComparer.Ordinal)
                .ToList();
        }
    }
}
