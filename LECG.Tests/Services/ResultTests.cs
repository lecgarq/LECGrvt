using FluentAssertions;

namespace LECG.Tests.Services;

public class ResultTests
{
    [Fact]
    public void Success_SetsValueWithoutError()
    {
        var result = LECG.Core.Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_SetsErrorWithoutValue()
    {
        var result = LECG.Core.Result<int>.Failure("boom");

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(0);
        result.Error.Should().Be("boom");
    }
}
