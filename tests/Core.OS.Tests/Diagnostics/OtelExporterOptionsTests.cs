using Core.OS.Diagnostics.Extensions;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Diagnostics;

public class OtelExporterOptionsTests
{
    public sealed class GetOtelExporterOptions : OtelExporterOptionsTests
    {
        [Fact]
        public void Should_bind_standard_otlp_configuration_keys()
        {
            // Arrange
            var config = new TestConfig()
                .SetSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317")
                .SetSetting("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
                .SetSetting("OTEL_SERVICE_NAME", "custom-service")
                .SetSetting("OTEL_ADDITIONAL_SOURCES:0", "Custom.Source")
                .SetSetting("OTEL_ADDITIONAL_METERS:0", "Custom.Meter")
                .BuildConfiguration();

            // Act
            var options = config.GetOtelExporterOptions();

            // Assert
            options.Endpoint.Should().Be("http://localhost:4317");
            options.Protocol.Should().Be("http/protobuf");
            options.ServiceName.Should().Be("custom-service");
            options.AdditionalSources.Should().Equal("Custom.Source");
            options.AdditionalMeters.Should().Equal("Custom.Meter");
        }

        [Fact]
        public void Should_return_empty_options_when_nothing_is_configured()
        {
            // Arrange
            var config = new TestConfig(false).BuildConfiguration();

            // Act
            var options = config.GetOtelExporterOptions();

            // Assert
            options.Endpoint.Should().BeNull();
            options.Protocol.Should().BeNull();
            options.ServiceName.Should().BeNull();
            options.AdditionalSources.Should().BeEmpty();
            options.AdditionalMeters.Should().BeEmpty();
        }
    }
}
