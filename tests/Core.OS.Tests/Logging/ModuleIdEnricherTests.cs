using Serilog.Sinks.Journal;

namespace Core.OS.Tests.Logging;

public class ModuleIdEnricherTests
{
    public sealed class TryGetModuleIdFromStackTrace : ModuleIdEnricherTests
    {
        [Fact]
        public void Should_get_module_id()
        {
            // Arrange + Act
            var result = ModuleIdEnricher.TryGetModuleIdFromStackTrace(0, [".Tests"], out var id);

            // Assert
            result.Should().BeTrue();
            id.Should().Be("ViciOne.Suite.Core.OS");
        }
    }

    public sealed class TryGetModuleId : ModuleIdEnricherTests
    {
        [Fact]
        public void Should_get_module()
        {
            // Arrange + Act
            var result = ModuleIdEnricher.TryGetModuleId([".Tests"], "Serilog.Tests", out var id);

            // Assert
            result.Should().BeTrue();
            id.Should().Be("Serilog");
        }

        [Fact]
        public void Should_return_false_if_there_is_no_match()
        {
            // Arrange + Act
            var result = ModuleIdEnricher.TryGetModuleId([".Tests"], "Serilog.Sinks", out var id);

            // Assert
            result.Should().BeFalse();
            id.Should().Be(null);
        }

        [Fact]
        public void Should_return_false_with_no_suffix()
        {
            // Arrange + Act
            var result = ModuleIdEnricher.TryGetModuleId([], "Serilog.Sinks", out var id);

            // Assert
            result.Should().BeFalse();
            id.Should().Be(null);
        }

        [Fact]
        public void Should_return_false_when_assembly_name_is_null()
        {
            // Arrange + Act
            var result = ModuleIdEnricher.TryGetModuleId([".Tests"], null, out var id);

            // Assert
            result.Should().BeFalse();
            id.Should().Be(null);
        }
    }
}
