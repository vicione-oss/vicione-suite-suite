using System.IO.Abstractions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using MassTransit.Logging;
using MassTransit.Monitoring;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.OS.Diagnostics.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        internal IServiceCollection AddCoreDiagnostics(IConfiguration configuration,
            InstanceOptions instanceOptions,
            IFileSystem fileSystem)
        {
            var exporterOptions = configuration.GetOtelExporterOptions();
            if (string.IsNullOrEmpty(exporterOptions.Endpoint))
                return services;

            services
                .AddOpenTelemetry()
                .ConfigureResource(b =>
                {
                    b.AddService(
                            SuiteOtelResource.GetServiceName(exporterOptions),
                            serviceNamespace: SuiteOtelResource.ServiceNamespace,
                            serviceVersion: SuiteOtelResource.GetServiceVersion(),
                            serviceInstanceId: fileSystem.ReadLocalInstanceId(instanceOptions),
                            autoGenerateServiceInstanceId: false)
                        .AddAttributes(
                        [
                            new("process.pid", Environment.ProcessId),
                            new("service.instance.type", instanceOptions.Type.ToString())
                        ]);
                })
                .WithTracing(b => ConfigureTracing(b, CoreActivitySource.SourceName, exporterOptions))
                .WithMetrics(b => ConfigureMetrics(b, exporterOptions))
                .UseOtlpExporter();

            return services;

            static void ConfigureTracing(TracerProviderBuilder builder, string serviceName, OtelExporterOptions exporterOptions)
            {
                builder
                    .AddAspNetCoreInstrumentation()
                    .AddSource(serviceName)
                    .AddSource(DiagnosticHeaders.DefaultListenerName);

                builder.AddSource(exporterOptions.AdditionalSources);
            }

            static void ConfigureMetrics(MeterProviderBuilder builder, OtelExporterOptions exporterOptions)
            {
                builder
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(InstrumentationOptions.MeterName);

                foreach (var meter in exporterOptions.AdditionalMeters)
                    builder.AddMeter(meter);
            }
        }
    }
}
