using FluentAssertions;
using LECG.Core.Filtering;
using Xunit;

namespace LECG.Tests.Services;

public class SearchTermPolicyTests
{
    [Fact]
    public void ParseTerms_WhenNull_ReturnsEmpty()
    {
        var result = SearchTermPolicy.ParseTerms(null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseTerms_WhenSemicolonDelimited_ParsesTrimmedTerms()
    {
        var result = SearchTermPolicy.ParseTerms("  view ; filter;  ");
        result.Should().Equal("view", "filter");
    }

    [Fact]
    public void MatchesAny_WhenAnyTermMatches_ReturnsTrue()
    {
        var terms = new[] { "abc", "view" };
        var result = SearchTermPolicy.MatchesAny("Main View", terms);
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesAny_WhenNoTermMatches_ReturnsFalse()
    {
        var terms = new[] { "abc", "def" };
        var result = SearchTermPolicy.MatchesAny("Main View", terms);
        result.Should().BeFalse();
    }
}
