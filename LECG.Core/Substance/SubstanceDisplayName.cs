using System.Globalization;

namespace LECG.Core.Substance;

public static class SubstanceDisplayName
{
    public static string FromSlug(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);
        var tokens = slug.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var text = CultureInfo.InvariantCulture.TextInfo;
        return string.Join(' ', tokens.Select(t => text.ToTitleCase(t.ToLowerInvariant())));
    }
}
