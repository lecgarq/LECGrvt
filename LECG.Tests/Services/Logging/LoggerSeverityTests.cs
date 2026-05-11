using FluentAssertions;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using Xunit;

// EXPECTED RED until Wave 1:
// - Logger_ScopeParameterTests: build error — ILogger.LogWarning/LogError/Log/LogSuccess do not
//   yet accept a `scope` parameter. Wave 1 adds this parameter to the ILogger contract.
// - RevitCommandProgressReporter_SeverityTests: build error — RevitCommandProgressReporter ctor
//   currently takes Action<string> + Action<double,string>; Wave 1 migrates it to accept ILogger.
// - LegacyProgressReporter_SeverityTests: build error — LegacyProgressReporter ctor currently
//   takes Action callbacks; Wave 1 migrates it to accept ILogger.
// - SimpleProgressReporter_SeverityTests: build error — SimpleProgressReporter ctor currently
//   takes Action<ProgressReport>; Wave 1 migrates it to accept ILogger.

namespace LECG.Tests.Services.Logging;

// ---------------------------------------------------------------------------
// CROSS-01 — Scope parameter compile guard
// ---------------------------------------------------------------------------

/// <summary>
/// Verifies the future ILogger scope parameter is present on every log method.
/// Expected RED until Wave 1 extends the ILogger contract with scope: parameter.
/// </summary>
[Trait("Category", "CrossCutting")]
public class Logger_ScopeParameterTests
{
    private readonly ILogger _logger = new Logger();

    [Fact]
    public void Log_AcceptsOptionalScopeParameter()
    {
        // This call MUST compile once Wave 1 adds `scope` to ILogger.Log.
        // Build error until then — that is the intended RED state.
        _logger.Log("message", scope: "Test");
    }

    [Fact]
    public void LogSuccess_AcceptsOptionalScopeParameter()
    {
        _logger.LogSuccess("message", scope: "Test");
    }

    [Fact]
    public void LogWarning_AcceptsOptionalScopeParameter()
    {
        _logger.LogWarning("message", scope: "Test");
    }

    [Fact]
    public void LogError_AcceptsOptionalScopeParameter()
    {
        _logger.LogError("message", scope: "Test");
    }
}

// ---------------------------------------------------------------------------
// CROSS-02 — Severity preservation per IProgressReporter implementation
// ---------------------------------------------------------------------------

/// <summary>
/// Verifies RevitCommandProgressReporter forwards LogWarning/LogError with correct severity.
/// Expected RED until Wave 1: constructor signature change + severity-forward impl.
/// </summary>
[Trait("Category", "CrossCutting")]
public class RevitCommandProgressReporter_SeverityTests
{
    [Fact]
    public void LogWarning_ProducesWarningLevelEntry()
    {
        // Arrange — Wave 1 ctor: RevitCommandProgressReporter(ILogger, Action<double,string>)
        var logger = new Logger();
        var reporter = new RevitCommandProgressReporter(logger, (_, _) => { });

        // Act
        reporter.LogWarning("something went wrong");

        // Assert
        logger.Entries.Should().ContainSingle()
            .Which.Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void LogError_ProducesErrorLevelEntry()
    {
        var logger = new Logger();
        var reporter = new RevitCommandProgressReporter(logger, (_, _) => { });

        reporter.LogError("fatal failure");

        logger.Entries.Should().ContainSingle()
            .Which.Level.Should().Be(LogLevel.Error);
    }
}

/// <summary>
/// Verifies LegacyProgressReporter forwards LogWarning/LogError with correct severity.
/// Expected RED until Wave 1: constructor signature change + severity-forward impl.
/// </summary>
[Trait("Category", "CrossCutting")]
public class LegacyProgressReporter_SeverityTests
{
    [Fact]
    public void LogWarning_ProducesWarningLevelEntry()
    {
        // Arrange — Wave 1 ctor: LegacyProgressReporter(ILogger, Action<double,string>?)
        var logger = new Logger();
        var reporter = new LegacyProgressReporter(logger);

        // Act
        reporter.LogWarning("legacy warning");

        // Assert
        logger.Entries.Should().ContainSingle()
            .Which.Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void LogError_ProducesErrorLevelEntry()
    {
        var logger = new Logger();
        var reporter = new LegacyProgressReporter(logger);

        reporter.LogError("legacy error");

        logger.Entries.Should().ContainSingle()
            .Which.Level.Should().Be(LogLevel.Error);
    }
}

/// <summary>
/// Verifies SimpleProgressReporter forwards LogWarning/LogError with correct severity.
/// Expected RED until Wave 1: constructor signature change + severity-forward impl.
/// </summary>
[Trait("Category", "CrossCutting")]
public class SimpleProgressReporter_SeverityTests
{
    [Fact]
    public void LogWarning_ProducesWarningLevelEntry()
    {
        // Arrange — Wave 1 ctor: SimpleProgressReporter(ILogger, Action<ProgressReport>?)
        var logger = new Logger();
        var reporter = new SimpleProgressReporter(logger);

        // Act
        reporter.LogWarning("simple warning");

        // Assert
        logger.Entries.Should().ContainSingle()
            .Which.Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void LogError_ProducesErrorLevelEntry()
    {
        var logger = new Logger();
        var reporter = new SimpleProgressReporter(logger);

        reporter.LogError("simple error");

        logger.Entries.Should().ContainSingle()
            .Which.Level.Should().Be(LogLevel.Error);
    }
}
