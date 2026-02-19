using FluentAssertions;
using LECG.Core.Naming;
using Xunit;

namespace LECG.Tests.Utils
{
    public class FamilySelectionPolicyTests
    {
        [Theory]
        [InlineData(true, false)] // Work-plane based -> not safe
        [InlineData(false, true)] // Not work-plane based -> safe
        public void IsSafeToConvert_ReturnsExpectedResult(bool input, bool expected)
        {
            // Act
            var result = FamilySelectionPolicy.IsSafeToConvert(input);

            // Assert
            result.Should().Be(expected);
        }
    }
}
