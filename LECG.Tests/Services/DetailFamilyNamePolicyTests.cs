using FluentAssertions;
using LECG.Core.Naming;
using Xunit;

namespace LECG.Tests.Services;

public class DetailFamilyNamePolicyTests
{
    [Fact]
    public void FromTypeName_WhenDwgOrPdfSuffixExists_RemovesSuffixAndAppendsDetail()
    {
        DetailFamilyNamePolicy.FromTypeName("plan.dwg").Should().Be("plan_Detail");
        DetailFamilyNamePolicy.FromTypeName("sheet.pdf").Should().Be("sheet_Detail");
    }

    [Fact]
    public void FromTypeName_WhenNoKnownSuffix_AppendsDetail()
    {
        var result = DetailFamilyNamePolicy.FromTypeName("GenericType");
        result.Should().Be("GenericType_Detail");
    }

    [Fact]
    public void FromTypeName_WhenNull_ThrowsArgumentNullException()
    {
        Action act = () => DetailFamilyNamePolicy.FromTypeName(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
