using System.Globalization;

namespace LECG.Core.Naming;

public static class LinePatternNamingPolicy
{
    private static readonly Dictionary<string, string> LegacyAliases = new(StringComparer.Ordinal)
    {
        { "Dash", "DASH" },
        { "Dash|Dash", "CENTER" },
        { "Dot", "DOT" },
        { "Dot|Dot", "DOT-CENTER" },
        { "Dash|Dot", "GRID" },
    };

    /// <summary>
    /// Computes a semantic family name from the segment type sequence.
    /// Returns null for invalid patterns (odd count, missing Space alternation, unknown types).
    /// </summary>
    public static string? CreateFamilyName(IReadOnlyList<string> segmentTypes)
    {
        ArgumentNullException.ThrowIfNull(segmentTypes);

        if (segmentTypes.Count == 0 || segmentTypes.Count % 2 != 0)
        {
            return null;
        }

        // Extract non-Space elements and validate alternation
        var elements = new List<string>();
        for (int i = 0; i < segmentTypes.Count; i++)
        {
            string normalized = NormalizeType(segmentTypes[i]);
            if (i % 2 == 0)
            {
                // Even positions must be Dash or Dot
                if (normalized != "Dash" && normalized != "Dot")
                {
                    return null;
                }

                elements.Add(normalized);
            }
            else
            {
                // Odd positions must be Space
                if (normalized != "Space")
                {
                    return null;
                }
            }
        }

        if (elements.Count == 0)
        {
            return null;
        }

        // Check for legacy alias
        string elementKey = string.Join("|", elements);
        if (LegacyAliases.TryGetValue(elementKey, out string? legacy))
        {
            return legacy;
        }

        // Build dynamic name from groups of consecutive same-type elements
        var parts = new List<string>();
        int groupStart = 0;
        while (groupStart < elements.Count)
        {
            string type = elements[groupStart];
            int count = 1;
            while (groupStart + count < elements.Count && elements[groupStart + count] == type)
            {
                count++;
            }

            parts.Add(FormatGroup(type, count));
            groupStart += count;
        }

        return string.Join("-", parts);
    }

    public static bool IsSemanticFamily(string? familyName) => familyName != null;

    public static string CreateTypeSignature(IReadOnlyList<string> segmentTypes)
    {
        ArgumentNullException.ThrowIfNull(segmentTypes);

        return string.Join("|", segmentTypes.Select(NormalizeType));
    }

    public static bool IsWithinTolerance(
        IReadOnlyList<double> leftLengthsMm,
        IReadOnlyList<double> rightLengthsMm,
        double toleranceMm)
    {
        ArgumentNullException.ThrowIfNull(leftLengthsMm);
        ArgumentNullException.ThrowIfNull(rightLengthsMm);

        if (leftLengthsMm.Count != rightLengthsMm.Count)
        {
            return false;
        }

        for (int i = 0; i < leftLengthsMm.Count; i++)
        {
            if (Math.Abs(leftLengthsMm[i] - rightLengthsMm[i]) > toleranceMm)
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<double> AverageLengthsMm(IEnumerable<IReadOnlyList<double>> groupedLengthsMm)
    {
        ArgumentNullException.ThrowIfNull(groupedLengthsMm);

        List<IReadOnlyList<double>> groups = groupedLengthsMm.ToList();
        if (groups.Count == 0)
        {
            return Array.Empty<double>();
        }

        int segmentCount = groups[0].Count;
        double[] averages = new double[segmentCount];
        foreach (IReadOnlyList<double> lengths in groups)
        {
            if (lengths.Count != segmentCount)
            {
                throw new ArgumentException("All length sets must have the same segment count.", nameof(groupedLengthsMm));
            }

            for (int i = 0; i < segmentCount; i++)
            {
                averages[i] += lengths[i];
            }
        }

        for (int i = 0; i < averages.Length; i++)
        {
            averages[i] /= groups.Count;
        }

        return averages;
    }

    public static string CreateSemanticName(string familyName, IReadOnlyList<double> averagedLengthsMm)
    {
        ArgumentNullException.ThrowIfNull(familyName);
        ArgumentNullException.ThrowIfNull(averagedLengthsMm);

        string values = string.Join("-", averagedLengthsMm.Select(FormatMillimeters));
        return $"LECG-LP-{familyName} ({values})";
    }

    public static string FormatMillimeters(double valueMm)
    {
        double rounded = Math.Round(valueMm, 2, MidpointRounding.AwayFromZero);
        return $"{rounded.ToString("0.##", CultureInfo.InvariantCulture)}mm";
    }

    private static string FormatGroup(string type, int count)
    {
        if (type == "Dash")
        {
            return count switch
            {
                1 => "DASH",
                2 => "DOUBLE-DASH",
                3 => "TRIPLE-DASH",
                _ => $"{count}-DASH",
            };
        }

        // Dot
        return count switch
        {
            1 => "DOT",
            2 => "2DOTS",
            3 => "3DOTS",
            _ => $"{count}DOTS",
        };
    }

    private static string NormalizeType(string segmentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segmentType);
        return char.ToUpperInvariant(segmentType[0]) + segmentType[1..].ToLowerInvariant();
    }
}
