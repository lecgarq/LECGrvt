using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace LECG.RevitCopilot.UI;

internal static class MarkdownRenderer
{
    internal static FlowDocument CreateDocument(string markdown)
    {
        FlowDocument document = new()
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 28))
        };

        string normalized = (markdown ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        bool inCode = false;
        List<string> codeLines = [];

        foreach (string line in lines)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode)
                {
                    document.Blocks.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeLines)));
                    codeLines.Clear();
                }
                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                codeLines.Add(line);
                continue;
            }

            Paragraph paragraph = new() { Margin = new Thickness(0, 0, 0, 4) };
            string content = line;
            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                paragraph.TextIndent = -12;
                paragraph.Margin = new Thickness(12, 0, 0, 3);
                paragraph.Inlines.Add(new Run("• "));
                content = line[2..];
            }

            AppendInlineMarkdown(paragraph, content);
            document.Blocks.Add(paragraph);
        }

        if (codeLines.Count > 0)
        {
            document.Blocks.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeLines)));
        }

        return document;
    }

    private static Paragraph CreateCodeBlock(string code)
    {
        return new Paragraph(new Run(code))
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(228, 227, 222)),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 3, 0, 6)
        };
    }

    private static void AppendInlineMarkdown(Paragraph paragraph, string text)
    {
        int position = 0;
        while (position < text.Length)
        {
            int boldStart = text.IndexOf("**", position, StringComparison.Ordinal);
            int codeStart = text.IndexOf('`', position);
            int next = NextMarker(boldStart, codeStart);
            if (next < 0)
            {
                paragraph.Inlines.Add(new Run(text[position..]));
                return;
            }

            if (next > position) paragraph.Inlines.Add(new Run(text[position..next]));
            if (next == boldStart)
            {
                int end = text.IndexOf("**", next + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    paragraph.Inlines.Add(new Run(text[next..]));
                    return;
                }
                paragraph.Inlines.Add(new Bold(new Run(text[(next + 2)..end])));
                position = end + 2;
            }
            else
            {
                int end = text.IndexOf('`', next + 1);
                if (end < 0)
                {
                    paragraph.Inlines.Add(new Run(text[next..]));
                    return;
                }
                paragraph.Inlines.Add(new Run(text[(next + 1)..end])
                {
                    FontFamily = new FontFamily("Consolas"),
                    Background = new SolidColorBrush(Color.FromRgb(226, 232, 240))
                });
                position = end + 1;
            }
        }
    }

    private static int NextMarker(int first, int second)
    {
        if (first < 0) return second;
        if (second < 0) return first;
        return Math.Min(first, second);
    }
}
