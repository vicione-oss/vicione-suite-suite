using AwesomeAssertions;
using Serilog.Sinks.Journal;
using Xunit;

namespace Core.OS.Tests.Logging;

public class ModuleIdEnricher_TryGetModuleIdFromStackTrace
{
    [Fact]
    public void Can_get_ModuleId()
    {
        ModuleIdEnricher.TryGetModuleIdFromStackTrace(0, [".Tests"], out var id).Should().BeTrue();

        id.Should().Be("ViciOne.Suite.Core.OS");
    }
}

public class ModuleIdEnricher_TryGetModule
{
    [Fact]
    public void Can_get_module()
    {
        ModuleIdEnricher.TryGetModuleId([".Tests"], "Serilog.Tests", out var id).Should().BeTrue();

        id.Should().Be("Serilog");
    }

    [Fact]
    public void Returns_false_if_there_is_no_match()
    {
        ModuleIdEnricher.TryGetModuleId([".Tests"], "Serilog.Sinks", out var id).Should().BeFalse();

        id.Should().Be(null);
    }

    [Fact]
    public void Returns_false_with_no_suffix()
    {
        ModuleIdEnricher.TryGetModuleId([], "Serilog.Sinks", out var id).Should().BeFalse();

        id.Should().Be(null);
    }

    [Fact]
    public void Returns_false_when_assembly_name_is_null()
    {
        ModuleIdEnricher.TryGetModuleId([".Tests"], null, out var id).Should().BeFalse();

        id.Should().Be(null);
    }
}
