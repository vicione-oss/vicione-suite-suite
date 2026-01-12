using System.Text.Json;
using Core.Shared.HostManagement;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Messaging;
using CommunicationJsonContext = HostManagement.Shared.Communication.Contracts.SourceGenerationContext;

namespace Core.OS.HostManagement.Consumers;

public sealed class InstallSuiteVersionConsumer(IPipeClient pipeClient) : IConsumer<InstallSuiteVersion>
{
    public async Task Consume(ConsumeContext<InstallSuiteVersion> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        try
        {
            var resultJson = await pipeClient.SendRequest(Topics.InstallViciOneSuiteVersion, context.Message.Version, context.CancellationToken);
            var result = JsonSerializer.Deserialize(resultJson, CommunicationJsonContext.Default.InstallViciOneSuiteVersionResult);

            if (result is not null && result.Status != OperationStatus.Error)
            {
                await context.Publish(new InstallSuiteVersionStarted(correlationId, result.Message, result.Status == OperationStatus.Warning),
                    context.CancellationToken);
            }
            else
            {
                await context.Publish(new InstallSuiteVersionError(correlationId, new ErrorInfo((int?)result?.Status ?? -1, result?.Message)),
                    context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            await context.Publish(new InstallSuiteVersionError(correlationId, new ErrorInfo(-1, ex.Message)), context.CancellationToken);
        }
    }
}
