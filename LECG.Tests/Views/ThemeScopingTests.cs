using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace LECG.Tests.Views;

/// <summary>
/// Convention tests for WPF theme scoping.
///
/// LecgTheme.xaml carries implicit styles (Button, TextBox, ComboBox, RadioButton,
/// ProgressBar, TreeView, TreeViewItem). In a Revit add-in, Application.Current is
/// *Revit's* WPF application, so merging the theme there applies those styles to
/// Revit's own dialogs and every other loaded add-in. The theme must therefore be
/// merged per window, never application-wide.
///
/// These read source files rather than run WPF: a missing merge produces a runtime
/// XAML parse failure ("Cannot find resource named ..."), which compiling and the
/// rest of the suite cannot detect.
/// </summary>
public class ThemeScopingTests
{
    private const string ThemeReference = "LecgTheme.xaml";

    [Fact]
    public void NoSourceFile_MergesTheThemeIntoApplicationResources()
    {
        var offenders = EnumerateSource("*.cs")
            .Concat(EnumerateSource("*.xaml"))
            .Where(f => File.ReadLines(f).Any(IsApplicationResourceUse))
            .Select(RepoRelative)
            .ToArray();

        offenders.Should().BeEmpty(
            "Application.Current is Revit's WPF application — merging into its resources " +
            "restyles Revit's dialogs and every other add-in. Merge per window instead " +
            "(see docs/ai/ui-guide.md).");
    }

    [Fact]
    public void EveryLecgWindow_MergesTheTheme()
    {
        var offenders = EnumerateSource("*.xaml")
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Resources{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => (Path: f, Text: File.ReadAllText(f)))
            // Only windows own a resource scope; UserControls inherit from the window hosting them.
            .Where(x => x.Text.Contains("<base:LecgWindow", StringComparison.Ordinal))
            .Where(x => !x.Text.Contains(ThemeReference, StringComparison.Ordinal))
            .Select(x => RepoRelative(x.Path))
            .ToArray();

        offenders.Should().BeEmpty(
            "every LecgWindow must merge LecgTheme.xaml in its own Resources. Declaring " +
            "<base:LecgWindow.Resources><ResourceDictionary> REPLACES the dictionary the " +
            "LecgWindow constructor populates, so the constructor fallback does not cover " +
            "these views and StaticResource lookups fail at runtime.");
    }

    /// <summary>
    /// True for real code touching Application.Current.Resources. Comments are excluded
    /// deliberately — the rule itself is documented in comments at the sites that enforce it.
    /// </summary>
    private static bool IsApplicationResourceUse(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal)
            || trimmed.StartsWith("<!--", StringComparison.Ordinal))
        {
            return false;
        }

        return trimmed.Contains("Application.Current.Resources", StringComparison.Ordinal);
    }

    private static string[] EnumerateSource(string pattern) =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "src"), pattern, SearchOption.AllDirectories);

    private static string RepoRelative(string path) =>
        Path.GetRelativePath(RepoRoot(), path).Replace('\\', '/');

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LECG.sln")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                $"Could not locate LECG.sln above {AppContext.BaseDirectory}.");

        return dir.FullName;
    }
}
