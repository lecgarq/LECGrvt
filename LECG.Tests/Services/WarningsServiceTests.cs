using System;
using System.Linq;
using FluentAssertions;
using LECG.Core.Warnings;
using LECG.Services;
using LECG.Services.Logging;
using Xunit;

namespace LECG.Tests.Services;

public class WarningsServiceTests
{
    private readonly Logger _logger = new();
    private readonly WarningsService _service;

    public WarningsServiceTests()
    {
        _service = new WarningsService(_logger);
    }

    private static WarningItem Map(string source)
    {
        if (source == "bad") throw new InvalidOperationException("disposed message");
        return new WarningItem(source, "Warning", [1L]);
    }

    [Fact]
    public void ReadAll_MapsEveryMessage()
    {
        var items = _service.ReadAll(["a", "b"], Map);

        items.Select(i => i.Description).Should().Equal("a", "b");
    }

    [Fact]
    public void ReadAll_SkipsThrowingMessage_AndContinues()
    {
        var items = _service.ReadAll(["a", "bad", "c"], Map);

        items.Select(i => i.Description).Should().Equal("a", "c");
    }

    [Fact]
    public void ReadAll_LogsWarningForSkippedMessage()
    {
        _service.ReadAll(["bad"], Map);

        _logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("disposed message"));
    }
}
