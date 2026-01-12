using System.Text.Json;
using Core.Module;
using Core.OS.HostManagement.Extensions;
using Core.OS.Modules;
using Core.Shared.HostManagement;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using CommunicationJsonContext = HostManagement.Shared.Communication.Contracts.SourceGenerationContext;
using SharedJsonContext = HostManagement.Shared.Contracts.SourceGenerationContext;
using SystemContracts = HostManagement.Shared.Contracts.System;

namespace Core.OS.HostManagement.Consumers;

public sealed partial class InstallSuiteVersionConsumer(IPipeClient pipeClient,
    ISuiteArtifactRepository repository,
    IWorkspaceProvider<SystemBackendModule> workspaceProvider,
    ILogger<InstallSuiteVersionConsumer> logger) : IConsumer<InstallSuiteVersion>
{
    public async Task Consume(ConsumeContext<InstallSuiteVersion> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        try
        {
            LogInstallSuiteVersion(logger, context.Message.PackageName, context.Message.SignatureName);

            var suitePackage = await repository.DownloadAndValidate(workspaceProvider.Cache, context.Message.PackageName, context.Message.SignatureName, context.CancellationToken);

            // HM will validate the package again but might have other keys
            // therefore we have to provide both paths
            SystemContracts.SignedDebianPackage signedPackage = new()
            {
                FilePath = suitePackage.FilePath,
                SignatureFilePath = suitePackage.SignatureFilePath,
            };

            var result = await pipeClient.InstallSignedDebianPackage(signedPackage, context.CancellationToken);            
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
            LogInstallSuiteVersionFailed(logger, ex, context.Message.PackageName, context.Message.SignatureName);

            await context.Publish(new InstallSuiteVersionError(correlationId, new ErrorInfo(-1, ex.Message)), context.CancellationToken);
        }
    }

    [LoggerMessage(LogLevel.Information, "Installing suite version package='{Package}' with signature='{Signature}'")]
    private static partial void LogInstallSuiteVersion(ILogger<InstallSuiteVersionConsumer> logger, string package, string signature);

    [LoggerMessage(LogLevel.Error, "Failed to install suite version package='{Package}' with signature='{Signature}'")]
    private static partial void LogInstallSuiteVersionFailed(ILogger<InstallSuiteVersionConsumer> logger, Exception exception, string package, string signature);
}
