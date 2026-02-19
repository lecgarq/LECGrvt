using FluentAssertions;
using LECG.Core.Purge;
using Xunit;

namespace LECG.Tests.Services
{
    public class PurgeSequenceTests
    {
        [Theory]
        [InlineData(1, new[] { 1 })]
        [InlineData(3, new[] { 1, 2, 3 })]
        [InlineData(0, new int[0])]
        public void GetPasses_ReturnsCorrectSequence(int input, int[] expected)
        {
            // Act
            var result = PurgeSequence.GetPasses(input);

            // Assert
            result.Should().BeEquivalentTo(expected);
        }
    }
}
