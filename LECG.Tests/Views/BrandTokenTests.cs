using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace LECG.Tests.Views;

/// <summary>
/// Convention test for the LECG Graphic Standard on screen.
///
/// Every colour a view paints must come from Themes/LecgColors.xaml, so a palette change
/// reaches every window and nothing drifts back to an ad-hoc hex. Sixteen colours is the
/// ceiling (rule C6); a literal in a view is a seventeenth.
/// </summary>
public class BrandTokenTests
{
    private static readonly Regex HexLiteral = new(@"#[0-9A-Fa-f]{6,8}\b", RegexOptions.Compiled);

    [Fact]
    public void NoView_HardcodesAColour()
    {
        var offenders = Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Resources{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => File.ReadLines(f)
                .Select((line, i) => (line, i))
                .Where(x => HexLiteral.IsMatch(x.line))
                .Select(x => $"{Path.GetRelativePath(RepoRoot(), f).Replace('\\', '/')}:{x.i + 1}"))
            .ToArray();

        offenders.Should().BeEmpty(
            "views paint with brand tokens (Lecg* brushes in Themes/LecgColors.xaml), never a hex literal");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LECG.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException(
            $"Could not locate LECG.sln above {AppContext.BaseDirectory}.");
    }
}
