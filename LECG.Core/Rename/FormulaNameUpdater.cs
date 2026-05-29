using System.Text.RegularExpressions;

namespace LECG.Core.Rename;

public static class FormulaNameUpdater
{
    private const RegexOptions ReferenceRegexOptions = RegexOptions.CultureInvariant;

    public static string UpdateFormula(string formula, string oldName, string newName)
    {
        ArgumentNullException.ThrowIfNull(formula);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentNullException.ThrowIfNull(newName);

        if (formula.Length == 0)
        {
            return formula;
        }

        Regex referenceRegex = CreateReferenceRegex(oldName);
        return referenceRegex.Replace(
            formula,
            match => IsInsideQuotedText(formula, match.Index) ? match.Value : newName);
    }

    public static bool ContainsReference(string formula, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(formula);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

        if (formula.Length == 0)
        {
            return false;
        }

        Regex referenceRegex = CreateReferenceRegex(parameterName);
        foreach (Match match in referenceRegex.Matches(formula))
        {
            if (!IsInsideQuotedText(formula, match.Index))
            {
                return true;
            }
        }

        return false;
    }

    private static Regex CreateReferenceRegex(string parameterName)
    {
        string escapedName = Regex.Escape(parameterName);
        return new Regex($@"(?<![\p{{L}}\p{{Nd}}_]){escapedName}(?![\p{{L}}\p{{Nd}}_])", ReferenceRegexOptions);
    }

    private static bool IsInsideQuotedText(string formula, int index)
    {
        bool insideQuotedText = false;
        for (int i = 0; i < index; i++)
        {
            if (formula[i] != '"')
            {
                continue;
            }

            if (i + 1 < index && formula[i + 1] == '"')
            {
                i++;
                continue;
            }

            insideQuotedText = !insideQuotedText;
        }

        return insideQuotedText;
    }
}
