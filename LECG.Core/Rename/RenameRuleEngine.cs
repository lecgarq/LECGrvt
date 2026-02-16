using System.Globalization;
using System.Text.RegularExpressions;

namespace LECG.Core.Rename;

public enum CoreCaseMode
{
    Lower,
    Upper,
    Title,
    Capitalize
}

public enum CoreNumberingMode
{
    Suffix,
    Prefix
}

public sealed record ReplaceRuleOptions(
    bool IsActive,
    string FindText,
    string ReplaceText,
    bool MatchCase,
    bool UseRegex);

public sealed record RemoveRuleOptions(
    bool IsActive,
    int FirstN,
    int LastN,
    int FromPos,
    int Count);

public sealed record AddRuleOptions(
    bool IsActive,
    string Prefix,
    string Suffix,
    string Insert,
    int AtPos);

public sealed record CaseRuleOptions(
    bool IsActive,
    CoreCaseMode Mode);

public sealed record NumberingRuleOptions(
    bool IsActive,
    CoreNumberingMode Mode,
    int StartAt,
    int Increment,
    string Separator,
    int Padding);

public static class RenameRuleEngine
{
    public static string ApplyReplace(string text, ReplaceRuleOptions options, int index = 0)
    {
        if (!options.IsActive || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(options.FindText)) return text;

        if (options.UseRegex)
        {
            var regexOptions = options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            try
            {
                return Regex.Replace(text, options.FindText, options.ReplaceText ?? string.Empty, regexOptions);
            }
            catch
            {
                return text;
            }
        }

        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return text.Replace(options.FindText, options.ReplaceText ?? string.Empty, comparison);
    }

    public static string ApplyRemove(string text, RemoveRuleOptions options, int index = 0)
    {
        if (!options.IsActive || string.IsNullOrEmpty(text)) return text;
        var result = text;

        if (options.FirstN > 0)
        {
            if (options.FirstN >= result.Length) return string.Empty;
            result = result.Substring(options.FirstN);
        }

        if (options.LastN > 0 && result.Length > 0)
        {
            if (options.LastN >= result.Length) return string.Empty;
            result = result.Substring(0, result.Length - options.LastN);
        }

        if (options.Count > 0 && result.Length > 0)
        {
            var start = options.FromPos;
            var len = options.Count;
            if (start >= 0 && start < result.Length)
            {
                if (start + len > result.Length) len = result.Length - start;
                result = result.Remove(start, len);
            }
        }

        return result;
    }

    public static string ApplyAdd(string text, AddRuleOptions options, int index = 0)
    {
        if (!options.IsActive) return text;
        var result = text ?? string.Empty;

        if (!string.IsNullOrEmpty(options.Insert))
        {
            var pos = options.AtPos;
            if (pos < 0) pos = 0;
            if (pos > result.Length) pos = result.Length;
            result = result.Insert(pos, options.Insert);
        }

        if (!string.IsNullOrEmpty(options.Prefix))
        {
            result = options.Prefix + result;
        }

        if (!string.IsNullOrEmpty(options.Suffix))
        {
            result = result + options.Suffix;
        }

        return result;
    }

    public static string ApplyCase(string text, CaseRuleOptions options, int index = 0)
    {
        if (!options.IsActive || string.IsNullOrEmpty(text)) return text;

        return options.Mode switch
        {
            CoreCaseMode.Lower => text.ToLowerInvariant(),
            CoreCaseMode.Upper => text.ToUpperInvariant(),
            CoreCaseMode.Title => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower()),
            CoreCaseMode.Capitalize when text.Length > 0 => char.ToUpper(text[0]) + text.Substring(1),
            _ => text
        };
    }

    public static string ApplyNumbering(string text, NumberingRuleOptions options, int index = 0)
    {
        if (!options.IsActive) return text;

        var val = options.StartAt + (index * options.Increment);
        var numStr = val.ToString().PadLeft(options.Padding, '0');

        return options.Mode == CoreNumberingMode.Prefix
            ? numStr + options.Separator + text
            : text + options.Separator + numStr;
    }
}
