using FluentAssertions;
using LECG.Core;
using LECG.Services.Logging;
using Xunit;

// EXPECTED RED until Wave 3:
// - Build error: DialogWhitelist type does not yet exist.
// - Build error: IDialogOverride type does not yet exist.
// Wave 3 will create both types in src/Core/ and all tests here must turn GREEN.

namespace LECG.Tests.Core;

// ---------------------------------------------------------------------------
// Test seam — avoids dependency on sealed Revit type DialogBoxShowingEventArgs
// ---------------------------------------------------------------------------

/// <summary>
/// Test double that records whether OverrideResult was called and with what value.
/// This seam lets unit tests verify whitelist hit/miss behavior without a live Revit instance.
/// </summary>
file sealed class RecordingDialogOverride : IDialogOverride
{
    public bool WasCalled { get; private set; }
    public int LastResult { get; private set; }

    public void OverrideResult(int result)
    {
        WasCalled = true;
        LastResult = result;
    }
}

// ---------------------------------------------------------------------------
// CROSS-03 — DialogWhitelist behavior tests
// ---------------------------------------------------------------------------

/// <summary>
/// Verifies DialogWhitelist.Apply hit/miss/null/case-sensitivity behavior and logging policy.
/// All tests are EXPECTED RED until Wave 3 creates DialogWhitelist + IDialogOverride.
/// </summary>
[Trait("Category", "CrossCutting")]
public class DialogWhitelistTests
{
    private readonly Logger _logger = new();

    [Fact]
    public void Apply_WhenDialogIdIsWhitelisted_CallsOverrideResultAndLogsInfo()
    {
        // Arrange
        var whitelist = new DialogWhitelist(new Dictionary<string, int>
        {
            ["TaskDialog_Overwrite"] = 1
        });
        var sink = new RecordingDialogOverride();

        // Act
        whitelist.Apply("TaskDialog_Overwrite", sink, _logger);

        // Assert — override was invoked with the whitelisted result code
        sink.WasCalled.Should().BeTrue();
        sink.LastResult.Should().Be(1);

        // Assert — an Info entry was logged referencing the DialogId
        _logger.Entries.Should().ContainSingle()
            .Which.Should().Match<LogEntry>(e =>
                e.Level == LogLevel.Info &&
                e.Message.Contains("TaskDialog_Overwrite"));
    }

    [Fact]
    public void Apply_WhenDialogIdIsNotWhitelisted_DoesNotCallOverrideAndLogsWarning()
    {
        // Arrange
        var whitelist = new DialogWhitelist(new Dictionary<string, int>
        {
            ["TaskDialog_Overwrite"] = 1
        });
        var sink = new RecordingDialogOverride();

        // Act
        whitelist.Apply("TaskDialog_Unknown", sink, _logger);

        // Assert — dialog reaches the user (no override)
        sink.WasCalled.Should().BeFalse();

        // Assert — a Warning entry was logged referencing the unhandled DialogId
        _logger.Entries.Should().ContainSingle()
            .Which.Should().Match<LogEntry>(e =>
                e.Level == LogLevel.Warning &&
                e.Message.Contains("TaskDialog_Unknown"));
    }

    [Fact]
    public void Apply_WhenDialogIdIsNull_TreatsAsMissAndLogsWarning()
    {
        // Arrange
        var whitelist = new DialogWhitelist(new Dictionary<string, int>
        {
            ["TaskDialog_Overwrite"] = 1
        });
        var sink = new RecordingDialogOverride();

        // Act
        whitelist.Apply(null, sink, _logger);

        // Assert — null id is treated as miss (reach-user)
        sink.WasCalled.Should().BeFalse();

        // Assert — Warning logged
        _logger.Entries.Should().ContainSingle()
            .Which.Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void Apply_WhenDialogIdDiffersByCase_TreatsAsMiss()
    {
        // Arrange — CONTEXT §3.2: exact string match, case-sensitive
        var whitelist = new DialogWhitelist(new Dictionary<string, int>
        {
            ["TaskDialog_Overwrite"] = 1
        });
        var sink = new RecordingDialogOverride();

        // Act — lowercase variant should NOT match
        whitelist.Apply("taskdialog_overwrite", sink, _logger);

        // Assert — no override, dialog reaches user
        sink.WasCalled.Should().BeFalse();
    }

    [Fact]
    public void Apply_WhenWhitelistIsEmpty_AlwaysTreatsAsMiss()
    {
        // Arrange
        var whitelist = new DialogWhitelist(new Dictionary<string, int>());
        var sink = new RecordingDialogOverride();

        // Act
        whitelist.Apply("TaskDialog_Overwrite", sink, _logger);

        // Assert
        sink.WasCalled.Should().BeFalse();
        _logger.Entries.Should().ContainSingle()
            .Which.Level.Should().Be(LogLevel.Warning);
    }
}
