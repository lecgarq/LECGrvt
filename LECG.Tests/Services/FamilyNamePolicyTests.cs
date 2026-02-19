using FluentAssertions;
using LECG.Core.Naming;
using System.Collections.Generic;
using Xunit;

namespace LECG.Tests.Services
{
    public class FamilyNamePolicyTests
    {
        [Fact]
        public void ResolveName_WhenNoCollision_ReturnsBaseName()
        {
            // Arrange
            var existingNames = new List<string> { "Family1", "Family2" };

            // Act
            var result = FamilyNamePolicy.ResolveName(existingNames, "Source", "Custom");

            // Assert
            result.Should().Be("Custom");
        }

        [Fact]
        public void ResolveName_WithCollision_AppendsCounter()
        {
            // Arrange
            var existingNames = new List<string> { "Custom", "Custom_1" };

            // Act
            var result = FamilyNamePolicy.ResolveName(existingNames, "Source", "Custom");

            // Assert
            result.Should().Be("Custom_2");
        }

        [Fact]
        public void ResolveName_WithDefaultNameCollision_AppendsCounter()
        {
            // Arrange
            var existingNames = new List<string> { "Source_Converted" };

            // Act
            var result = FamilyNamePolicy.ResolveName(existingNames, "Source", null!);

            // Assert
            result.Should().Be("Source_Converted_1");
        }
    }
}
