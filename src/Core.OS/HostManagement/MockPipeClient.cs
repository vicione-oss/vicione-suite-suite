using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using System.Text.Json;
using Core.OS.Instance;
using Core.Shared.HostManagement;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.NetworkStatus;
using Microsoft.Extensions.Options;
using SourceGenerationContext = HostManagement.Shared.Communication.Contracts.SourceGenerationContext;

namespace Core.OS.HostManagement;

public sealed class MockPipeClient(IFileSystem fileSystem,
    IHostApplicationLifetime lifetime,
    IOptions<MockPipeClientOptions> options,
    IOptions<InstanceOptions> instanceOptions) : IPipeClient
{
    internal const string GetSystemConfigurationResultResource = "Core.OS.HostManagement.Resources.GetSystemConfigurationResult.json";

    private SystemConfiguration? _systemConfiguration = GetSystemConfigurationByOptions(fileSystem, options.Value);

    public PipeState State { get; private set; }

    public void Dispose() { }

    private static SystemConfiguration? GetSystemConfigurationByOptions(IFileSystem fileSystem, MockPipeClientOptions options)
    {
        switch (options.DataSource)
        {
            case MockPipeClientDataSource.SystemConfiguration:
                return options.SystemConfiguration;

            case MockPipeClientDataSource.SystemConfigurationJsonFile:

                if (!fileSystem.Path.Exists(options.SystemConfigurationJsonFile))
                    throw new FileNotFoundException(options.SystemConfigurationJsonFile);

                var resultJson = fileSystem.File.ReadAllText(options.SystemConfigurationJsonFile);
                var result = JsonSerializer.Deserialize(resultJson, SourceGenerationContext.Default.GetSystemConfigurationResult);

                return result?.Configuration;

            case MockPipeClientDataSource.SystemConfigurationEmbedded:
                return GetEmbeddedSystemConfiguration();

            default:
                throw new UnreachableException($"Invalid DataSource option '{options.DataSource}'");
        }
    }

    public Task Connect(CancellationToken cancellationToken = default)
    {
        State = PipeState.Connected;
        return Task.CompletedTask;
    }

    public async Task<string> SendRequest(string topic, string content, CancellationToken cancellationToken = default)
    {
        switch (topic)
        {
            case Topics.SetSystemConfiguration:
                return HandleSetSystemConfiguration(content);

            case Topics.GetSystemConfiguration:
                return await HandleGetSystemConfiguration();

            case Topics.Echo:
                return content;

            case Topics.InstallSignedDebianPackage:
                return JsonSerializer.Serialize(new InstallSignedDebianPackageResult { Status = OperationStatus.Success, Message = null },
                    SourceGenerationContext.Default.InstallSignedDebianPackageResult);

            case Topics.GetNetworkStatusInformation:
                return HandleGetGetNetworkStatusInformationNet(content);

            case Topics.RestartService:
                return HandleRestartService(content);

            case Topics.RestartSystem:
                return HandleRestartSystem();

            case Topics.ResetSystem:
                return JsonSerializer.Serialize(new ResetSystemResult { Status = OperationStatus.Success, Message = null },
                    SourceGenerationContext.Default.ResetSystemResult);

            case Topics.StartService:
            case Topics.StopService:
                return JsonSerializer.Serialize(new ServiceControlResult { Status = OperationStatus.Success, Message = null },
                    SourceGenerationContext.Default.ServiceControlResult);

            case Topics.UpdateSystem:
                return JsonSerializer.Serialize(new UpdateSystemResult { Status = OperationStatus.Success, Message = null },
                    SourceGenerationContext.Default.UpdateSystemResult);

            case Topics.ShutdownSystem:
                return JsonSerializer.Serialize(new ShutdownSystemResult { Status = OperationStatus.Success, Message = null },
                    SourceGenerationContext.Default.ShutdownSystemResult);

            case Topics.RenewDHCPLease:
                return JsonSerializer.Serialize(new GetDHCPLeaseInformationResult()
                {
                    Status = OperationStatus.Success,
                    DHCPLease = new()
                    {
                        Gateway = new([192, 168, 14, 1]),
                        LeaseExpires = DateTimeOffset.UtcNow.AddYears(2),
                        LeaseObtained = DateTimeOffset.UtcNow.AddHours(-1),
                        IPv4Detail = new() { IPAddress = new([192, 168, 14, 32]), Netmask = new([192, 168, 255, 255]) },
                        NetworkDNSSettings = new(nameServers: [new([8, 8, 8, 8]), new([10, 10, 10, 10])])
                    },
                    Message = null
                },
                SourceGenerationContext.Default.GetDHCPLeaseInformationResult);

            case Topics.GetDHCPLeaseInformation:
                return HandleGetDCHPLease(content);

            case Topics.GetNTPFallbackInformation:
                return JsonSerializer.Serialize(new GetNTPFallbackInformationResult
                {
                    Status = OperationStatus.Success,
                    FallbackNTPServers = ["time.google.com", "time.windows.com"],
                    Message = null
                },
                    SourceGenerationContext.Default.GetNTPFallbackInformationResult);

            case Topics.GetOriginalPhysicalAddress:
                return JsonSerializer.Serialize(new GetOriginalPhysicalAddressResult()
                {
                    Status = OperationStatus.Success,
                    OriginalPhysicalAddress = "00:02:01:10:53:25",
                    Message = null
                },
                SourceGenerationContext.Default.GetOriginalPhysicalAddressResult);

            default:
                return "Ok";
        }
    }

    private string HandleSetSystemConfiguration(string content)
    {
        try
        {
            var systemConfiguration = JsonSerializer.Deserialize(content, SourceGenerationContext.Default.SystemConfiguration);

            _systemConfiguration = systemConfiguration ?? throw new JsonException($"{nameof(JsonSerializer.Deserialize)} returned null");

            return JsonSerializer.Serialize(
                new SetSystemConfigurationResult
                {
                    Status = OperationStatus.Success,
                    Message = null
                },
                SourceGenerationContext.Default.SetSystemConfigurationResult);
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new SetSystemConfigurationResult { Status = OperationStatus.Error, Message = exception.Message },
                SourceGenerationContext.Default.SetSystemConfigurationResult);
        }
    }

    private string HandleRestartService(string content)
    {
        // fake restart on our deployments without host management
        // do it the same way as host management would do it
        // !before we get an answer, the service is already down
        if (content == instanceOptions.Value.ServiceName)
        {
            lifetime.StopApplication();
        }

        return JsonSerializer.Serialize(new ServiceControlResult { Status = OperationStatus.Success, Message = null },
                SourceGenerationContext.Default.ServiceControlResult);
    }

    private string HandleRestartSystem()
    {
        lifetime.StopApplication();

        return JsonSerializer.Serialize(new RestartSystemResult { Status = OperationStatus.Success, Message = null },
                    SourceGenerationContext.Default.RestartSystemResult);
    }

    private Task<string> HandleGetSystemConfiguration()
        => Task.FromResult(JsonSerializer.Serialize(
            new GetSystemConfigurationResult
            {
                Status = OperationStatus.Success,
                Message = null,
                Configuration = _systemConfiguration ?? new SystemConfiguration()
            },
            SourceGenerationContext.Default.GetSystemConfigurationResult));

    private static string HandleGetDCHPLease(string networkInterfaceName)
    {
        // fake the error sent by HM when interface name is empty
        if (string.IsNullOrWhiteSpace(networkInterfaceName))
        {
            return JsonSerializer.Serialize(new Response
            {
                Message = "Network interface name is empty"
            }, SourceGenerationContext.Default.Response);
        }

        return JsonSerializer.Serialize(new GetDHCPLeaseInformationResult
        {
            Status = OperationStatus.Success,
            DHCPLease = new()
            {
                Gateway = new([192, 168, 14, 1]),
                LeaseExpires = DateTimeOffset.UtcNow.AddYears(2),
                LeaseObtained = DateTimeOffset.UtcNow.AddHours(-1),
                IPv4Detail = new() { IPAddress = new([192, 168, 14, 32]), Netmask = new([192, 168, 255, 255]) },
                NetworkDNSSettings = new(nameServers: [new([8, 8, 8, 8]), new([10, 10, 10, 10])])
            },
            Message = null
        }, SourceGenerationContext.Default.GetDHCPLeaseInformationResult);
    }

    private string HandleGetGetNetworkStatusInformationNet(string content)
    {
        NetworkStatusInformation networkStatusInformation;

        if (_systemConfiguration is not null)
        {
            networkStatusInformation = new NetworkStatusInformation
            {
                IsDefaultRouteConfigured = true,
                IsDefaultGatewayAvailable = true,
                IsInternetAvailable = true,
                IsDNSFunctional = true,
                InterfaceState = _systemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
                    .Where(i => i.CommonInformation.Name == content)
                    .Select(i => "Online")
                    .FirstOrDefault(NetworkStatusInformation.Empty.InterfaceState)
            };
        }
        else
        {
            networkStatusInformation = NetworkStatusInformation.Empty;
        }

        return JsonSerializer.Serialize(new GetNetworkStatusInformationResult { NetworkStatusInformation = networkStatusInformation, Status = OperationStatus.Success, Message = null },
            SourceGenerationContext.Default.GetNetworkStatusInformationResult);
    }

    public static SystemConfiguration GetEmbeddedSystemConfiguration()
    {
        var result = GetEmbeddedSystemConfigurationResult();
        return result.Configuration ?? throw new InvalidOperationException("Embedded 'GetSystemConfigurationResult.json' does not contain configuration");
    }

    private static GetSystemConfigurationResult GetEmbeddedSystemConfigurationResult()
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(GetSystemConfigurationResultResource)
            ?? throw new InvalidOperationException("Can't find embedded resource 'GetSystemConfigurationResult.json'");

        var result = JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.GetSystemConfigurationResult);
        return result
            ?? throw new InvalidOperationException("Failed to deserialize embedded resource 'GetSystemConfigurationResult.json'");
    }
}
