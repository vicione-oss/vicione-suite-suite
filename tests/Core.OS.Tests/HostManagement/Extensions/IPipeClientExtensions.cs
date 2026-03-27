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
    extension(IPipeClient pipeClient)
    {
        public void SetupGetSystemConfigurationResult(CommunicationEnums.OperationStatus status, string message = "OK")
        {
            var result = new CommunicationContracts.GetSystemConfigurationResult
            {
                Status = status,
                Message = message,
                Configuration = TestPipeClient.GetEmbeddedSystemConfiguration()
            };

            pipeClient.SendRequest(Topics.GetSystemConfiguration, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(JsonSerializer.Serialize(result, CommunicationJsonContext.Default.GetSystemConfigurationResult));
        }

        public void SetupSetSystemConfigurationResult(CommunicationEnums.OperationStatus status, string message = "OK")
        {
            var result = new CommunicationContracts.SetSystemConfigurationResult
            {
                Status = status,
                Message = message
            };

            pipeClient.SendRequest(Topics.SetSystemConfiguration, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(JsonSerializer.Serialize(result, CommunicationJsonContext.Default.SetSystemConfigurationResult));

        }

        public void SetupResetSystemResult(CommunicationEnums.OperationStatus status, string message = "OK")
        {
            var result = new CommunicationContracts.SystemControlResult
            {
                Status = status,
                Message = message
            };

            pipeClient.SendRequest(Topics.ResetSystem, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(JsonSerializer.Serialize(result, CommunicationJsonContext.Default.SystemControlResult));
        }

        public void SetupRestartSystemResult(CommunicationEnums.OperationStatus status, string message = "OK")
        {
            var result = new CommunicationContracts.SystemControlResult
            {
                Status = status,
                Message = message
            };

            pipeClient.SendRequest(Topics.RestartSystem, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(JsonSerializer.Serialize(result, CommunicationJsonContext.Default.SystemControlResult));
        }

        public void SetupRestartServiceResult(CommunicationEnums.OperationStatus status, string message = "OK")
        {
            var result = new CommunicationContracts.ServiceControlResult
            {
                Status = status,
                Message = message
            };

            pipeClient.SendRequest(Topics.RestartService, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(JsonSerializer.Serialize(result, CommunicationJsonContext.Default.ServiceControlResult));
        }
    }
}
