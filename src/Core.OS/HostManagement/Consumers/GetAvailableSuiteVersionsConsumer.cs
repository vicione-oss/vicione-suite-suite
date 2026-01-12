using System.Text.Json;
using Core.Shared.HostManagement;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Messaging;
using SourceGenerationContext = HostManagement.Shared.Communication.Contracts.SourceGenerationContext;

namespace Core.OS.HostManagement.Consumers;

public sealed class GetAvailableSuiteVersionsConsumer(IPipeClient pipeClient) : RequestConsumer<GetAvailableSuiteVersions, GetAvailableSuiteVersionsResponse>
{
    protected override async Task<GetAvailableSuiteVersionsResponse> Respond(ConsumeContext<GetAvailableSuiteVersions> context)
    {
        var suiteVersionsJson = await pipeClient.SendRequest(Topics.GetViciOneSuiteVersions, "", context.CancellationToken);
        var suiteVersionsResult = JsonSerializer.Deserialize(suiteVersionsJson, SourceGenerationContext.Default.GetViciOneSuiteVersionsResult);
        if (suiteVersionsResult?.Status == OperationStatus.Success)
            return new GetAvailableSuiteVersionsResponse(suiteVersionsResult.SuiteVersions);

        return new GetAvailableSuiteVersionsResponse(suiteVersionsResult?.SuiteVersions ?? [])
        {
            RequestError = new ErrorInfo(0, suiteVersionsResult?.Message ?? "Could not fetch available software versions from HostManagement")
        };
    }

    protected override Task<GetAvailableSuiteVersionsResponse> HandleException(ConsumeContext<GetAvailableSuiteVersions> context,
        Exception e)
        => Task.FromResult(new GetAvailableSuiteVersionsResponse([]) { RequestError = new ErrorInfo(0, e.Message) });
}
