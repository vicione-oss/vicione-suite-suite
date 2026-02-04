using System.Text.Json;
using Core.OS.HostManagement;
using HostManagement.Shared.Communication;
using NSubstitute;
using CommunicationJsonContext = HostManagement.Shared.Communication.Contracts.SourceGenerationContext;
using CommunicationEnums = HostManagement.Shared.Communication.Enums;
using CommunicationContracts = HostManagement.Shared.Communication.Contracts;

namespace Core.OS.Tests.HostManagement.Extensions;

internal static class IPipeClientExtensions
{
    public static void SetupGetSystemConfigurationResult(this IPipeClient pipeClient, CommunicationEnums.OperationStatus status, string message = "OK")
    {
        var result = new CommunicationContracts.GetSystemConfigurationResult
        {
            Status = status,
            ResultType = nameof(CommunicationContracts.GetSystemConfigurationResult),
            Message = message,
            Configuration = TestPipeClient.GetEmbeddedSystemConfiguration()
        };

        pipeClient.SendRequest(Topics.GetSystemConfiguration, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(result, CommunicationJsonContext.Default.GetSystemConfigurationResult));
    }

    public static void SetupSetSystemConfigurationResult(this IPipeClient pipeClient, CommunicationEnums.OperationStatus status, string message = "OK")
    {
        var result = new CommunicationContracts.SetSystemConfigurationResult
        {
            Status = status,
            ResultType = nameof(CommunicationContracts.SetSystemConfigurationResult),
            Message = message
        };

        pipeClient.SendRequest(Topics.SetSystemConfiguration, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(result, CommunicationJsonContext.Default.SetSystemConfigurationResult));

    }

    public static void SetupResetSystemResult(this IPipeClient pipeClient, CommunicationEnums.OperationStatus status, string message = "OK")
    {
        var result = new CommunicationContracts.ResetSystemResult
        {
            Status = status,
            ResultType = nameof(CommunicationContracts.ResetSystemResult),
            Message = message
        };

        pipeClient.SendRequest(Topics.ResetSystem, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(result, CommunicationJsonContext.Default.ResetSystemResult));
    }

    public static void SetupRestartSystemResult(this IPipeClient pipeClient, CommunicationEnums.OperationStatus status, string message = "OK")
    {
        var result = new CommunicationContracts.RestartSystemResult
        {
            Status = status,
            ResultType = nameof(CommunicationContracts.RestartSystemResult),
            Message = message
        };

        pipeClient.SendRequest(Topics.RestartSystem, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(result, CommunicationJsonContext.Default.RestartSystemResult));
    }

    public static void SetupRestartServiceResult(this IPipeClient pipeClient, CommunicationEnums.OperationStatus status, string message = "OK")
    {
        var result = new CommunicationContracts.ServiceControlResult
        {
            Status = status,
            ResultType = nameof(CommunicationContracts.ServiceControlResult),
            Message = message
        };

        pipeClient.SendRequest(Topics.RestartService, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(result, CommunicationJsonContext.Default.ServiceControlResult));
    }
}
