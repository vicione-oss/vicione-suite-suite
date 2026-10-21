using HostManagement.Shared.Communication.Capabilities;
using HostManagement.Shared.Communication.NamedPipe.Client;
using HostManagement.Shared.Contracts;
using CommunicationEnums = HostManagement.Shared.Communication.Enums;
using CommunicationContracts = HostManagement.Shared.Communication.Contracts;
using IPipeClient = Core.OS.HostManagement.IPipeClient;

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

            pipeClient.SendRequest(PipeRequests.GetSystemConfiguration, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(result);
        }

        public void SetupGetSystemConfigurationResult(CommunicationEnums.OperationStatus status, SystemConfiguration configuration, string message = "OK")
        {
            var result = new CommunicationContracts.GetSystemConfigurationResult
            {
                Status = status,
                Message = message,
                Configuration = configuration
            };

            pipeClient.SendRequest(PipeRequests.GetSystemConfiguration, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(result);
        }

        public void SetupSetSystemConfigurationResult(CommunicationEnums.OperationStatus status, string message = "OK")
        {
            var result = new CommunicationContracts.SetSystemConfigurationResult
            {
                Status = status,
                Message = message
            };

            pipeClient.SendRequest(PipeRequests.SetSystemConfiguration, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(result);
        }

        public void SetupResetSystemResult(CommunicationEnums.OperationStatus status, string message = "OK")
        {
            var result = new CommunicationContracts.SystemControlResult
            {
                Status = status,
                Message = message
            };

            pipeClient.SendRequest(PipeRequests.ResetSystem, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(result);
        }

        public void SetupRestartSystemResult(CommunicationEnums.OperationStatus status, string message = "OK")
        {
            var result = new CommunicationContracts.SystemControlResult
            {
                Status = status,
                Message = message
            };

            pipeClient.SendRequest(PipeRequests.RestartSystem, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(result);
        }

        public void SetupRestartServiceResult(CommunicationEnums.OperationStatus status, string message = "OK")
        {
            var result = new CommunicationContracts.ServiceControlResult
            {
                Status = status,
                Message = message
            };

            pipeClient.SendRequest(PipeRequests.RestartService, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(result);
        }

        public void SetupServiceControlResult(PipeRequest<CommunicationContracts.ServiceControlResult> request, CommunicationEnums.OperationStatus status, string message = "OK")
        {
            var result = new CommunicationContracts.ServiceControlResult
            {
                Status = status,
                Message = message
            };

            pipeClient.SendRequest(request, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(result);
        }

        public void SetupGetSupportedCapabilitiesResult(CommunicationEnums.OperationStatus status, SupportedCapabilities capabilities, string message = "OK")
        {
            var result = new CommunicationContracts.GetSupportedCapabilitiesResult
            {
                Status = status,
                Message = message,
                SupportedCapabilities = capabilities
            };

            pipeClient.SendRequest(CapabilityPipeRequests.GetSupportedCapabilities, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(result);
        }
    }
}
