using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace LECG.Tests.Views;

/// <summary>
/// Convention tests for XAML resource use across every view and control.
///
/// These catch the failure class that costs the most time in this repo: markup that compiles
/// cleanly and then throws when the window is constructed. Neither the compiler nor a normal
/// unit test sees it, because XAML compilation does not resolve resources or check that a
/// Style fits the element it is applied to.
///
/// Paid for on 2026-08-18: a ToggleButton in SearchReplaceView carried
/// SecondaryButtonStyle, whose TargetType is Button. ToggleButton derives from ButtonBase,
/// not Button, so WPF threw "'Button' TargetType does not match type of element
/// 'ToggleButton'" the first time the dialog was opened in Revit — after a clean build, a
/// green suite, and a deploy.
///
/// Like <see cref="ThemeScopingTests"/>, these read source rather than run WPF.
/// </summary>
public class XamlResourceTests
{
    private static readonly Regex KeyDefinition = new(@"x:Key=""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex ResourceUse = new(@"\{(?:Dynamic|Static)Resource ([A-Za-z0-9_]+)\}", RegexOptions.Compiled);
    private static readonly Regex StyledElement = new(@"<([A-Za-z:]+)\b((?:[^<>]|\n)*?)/?>", RegexOptions.Compiled);
    private static readonly Regex StyleAttribute = new(@"Style=""\{(?:Dynamic|Static)Resource ([A-Za-z0-9_]+)\}""", RegexOptions.Compiled);
    private static readonly Regex TargetTypeAttribute = new(@"TargetType=""(?:\{x:Type )?([A-Za-z:]+)\}?""", RegexOptions.Compiled);

    [Fact]
    [Trait("Category", "Unit")]
    public void EveryResourceKey_IsDefined()
    {
        // Both kinds are checked, for different reasons.
        //
        // A missing StaticResource throws "Cannot find resource named '...'" at parse time
        // and the window never opens — loud, at least.
        //
        // A missing DynamicResource is worse, because it is silent: WPF resolves it to null
        // and carries on. Paid for on 2026-08-18 — OffsetElevationsView's "Add" button bound
        // its background to LecgSuccess, which was never defined, so a primary action button
        // rendered white-on-white with no error anywhere. It had been shipping like that.
        HashSet<string> defined = DefinedResourceKeys();

        var offenders = new List<string>();
        foreach (string file in MarkupFiles())
        {
            string text = File.ReadAllText(file);
            HashSet<string> local = KeyDefinition.Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

            offenders.AddRange(ResourceUse.Matches(text)
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .Where(key => !defined.Contains(key) && !local.Contains(key))
                .Select(key => $"{RepoRelative(file)} -> {key}"));
        }

        offenders.Should().BeEmpty(
            "an undefined resource key either throws at window construction (StaticResource) " +
            "or silently renders nothing (DynamicResource) — neither is visible to the " +
            "compiler or to any test that does not open the window");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EveryStyle_MatchesTheElementItIsAppliedTo()
    {
        // WPF requires the element to BE the style's TargetType, not merely derive from it.
        // ToggleButton + a TargetType="Button" style is the canonical trap, because both are
        // buttons in every sense except the one WPF checks.
        Dictionary<string, string> targetTypes = StyleTargetTypes();

        var offenders = new List<string>();
        foreach (string file in MarkupFiles())
        {
            string text = File.ReadAllText(file);
            foreach (Match element in StyledElement.Matches(text))
            {
                Match style = StyleAttribute.Match(element.Groups[2].Value);
                if (!style.Success) continue;
                if (!targetTypes.TryGetValue(style.Groups[1].Value, out string? targetType)) continue;

                string elementName = LocalName(element.Groups[1].Value);
                if (elementName == LocalName(targetType)) continue;

                offenders.Add($"{RepoRelative(file)}: <{elementName}> uses {style.Groups[1].Value} (TargetType={targetType})");
            }
        }

        offenders.Should().BeEmpty(
            "WPF throws \"TargetType does not match type of element\" at window construction; " +
            "a derived type is not accepted");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NoStyleThatDropsContent_IsAppliedToAnElementWithChildContent()
    {
        // A ControlTemplate with no ContentPresenter cannot render whatever it is given —
        // it renders what its own markup hardcodes and silently discards the rest.
        //
        // Paid for on 2026-08-19: three collapsible section headers used LinkButtonStyle,
        // whose template is a TextBlock bound to Content. Their accent bar, label, chevron
        // and summary all vanished, so OPERATIONS had no header to click and the rename
        // rules were unreachable. No exception, no warning, nothing in the log.
        HashSet<string> dropping = StylesWithoutAContentHost();

        var offenders = new List<string>();
        foreach (string file in MarkupFiles())
        {
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (System.Xml.XmlException) { continue; }

            foreach (XElement element in doc.Descendants())
            {
                string style = (string?)element.Attribute("Style") ?? string.Empty;
                Match m = StyleAttribute.Match($"Style=\"{style}\"");
                if (!m.Success || !dropping.Contains(m.Groups[1].Value)) continue;

                // Children whose tag carries a dot are property-element syntax
                // (<Button.Template>), not content.
                XElement? content = element.Elements()
                    .FirstOrDefault(c => !c.Name.LocalName.Contains('.'));
                if (content == null) continue;

                offenders.Add($"{RepoRelative(file)}: <{element.Name.LocalName}> uses " +
                              $"{m.Groups[1].Value} but has child content <{content.Name.LocalName}>");
            }
        }

        offenders.Should().BeEmpty(
            "a style whose ControlTemplate has no ContentPresenter renders nothing for " +
            "element content — the control disappears with no error at all");
    }

    /// <summary>
    /// Keyed styles whose ControlTemplate has nowhere to put the control's Content.
    /// </summary>
    private static HashSet<string> StylesWithoutAContentHost()
    {
        var dropping = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in ResourceFiles())
        {
            string text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"<Style\s+x:Key=""([^""]+)""(.*?)</Style>", RegexOptions.Singleline))
            {
                string body = m.Groups[2].Value;
                if (!body.Contains("<ControlTemplate")) continue;
                if (body.Contains("<ContentPresenter") || body.Contains("<ItemsPresenter") || body.Contains("<ContentControl")) continue;
                dropping.Add(m.Groups[1].Value);
            }
        }
        return dropping;
    }

    private static HashSet<string> DefinedResourceKeys() =>
        ResourceFiles()
            .SelectMany(f => KeyDefinition.Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> StyleTargetTypes()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in ResourceFiles())
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"x:Key=""([^""]+)""([^>]*)"))
            {
                Match tt = TargetTypeAttribute.Match(m.Groups[2].Value);
                if (tt.Success) map[m.Groups[1].Value] = tt.Groups[1].Value;
            }
        }
        return map;
    }

    private static string LocalName(string name) => name.Split(':').Last();

    private static IEnumerable<string> ResourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "Resources"), "*.xaml", SearchOption.AllDirectories);

    private static IEnumerable<string> MarkupFiles() =>
        new[] { "Views", "Controls" }
            .Select(d => Path.Combine(RepoRoot(), "src", d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.xaml", SearchOption.AllDirectories));

    private static string RepoRelative(string path) => Path.GetRelativePath(RepoRoot(), path).Replace('\\', '/');

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "LECG.sln"))) dir = dir.Parent;
        if (dir == null) throw new InvalidOperationException("LECG.sln not found above " + AppContext.BaseDirectory);
        return dir.FullName;
    }
}
