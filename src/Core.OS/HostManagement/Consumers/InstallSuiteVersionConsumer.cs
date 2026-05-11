using Core.Module;
using Core.OS.HostManagement.Extensions;
using Core.OS.Modules;
using Core.Shared.HostManagement;
using Core.Shared.HostManagement.Events;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using SystemContracts = HostManagement.Shared.Contracts.System;

namespace Core.OS.HostManagement.Consumers;

public sealed partial class InstallSuiteVersionConsumer(IPipeClient pipeClient,
    ISuiteArtifactRepository repository,
    IWorkspaceProvider<SystemBackendModule> workspaceProvider,
    ILogger<InstallSuiteVersionConsumer> logger) : IConsumer<InstallSuiteVersion>
{
    public async Task Consume(ConsumeContext<InstallSuiteVersion> context)
    {
        var correlationId = context.Message.CorrelationId;
        var packageName = context.Message.PackageName;
        var signatureName = context.Message.SignatureName;

        try
        {
            LogInstallSuiteVersion(logger, correlationId, packageName, signatureName);

            var suitePackage = await repository.DownloadAndValidate(workspaceProvider.Cache, packageName, signatureName, context.CancellationToken);

            // HM will validate the package again but might have other keys
            // therefore we have to provide both paths
            SystemContracts.SignedDebianPackage signedPackage = new()
            {
                FilePath = suitePackage.FilePath,
                SignatureFilePath = suitePackage.SignatureFilePath,
            };

            var result = await pipeClient.InstallSignedDebianPackage(signedPackage, context.CancellationToken);
            if (result is null || result.Status == OperationStatus.Error)
            {
                LogInstalledSuiteFailed(logger, correlationId, packageName, signatureName);

                var errorEvent = new InstallSuiteVersionStarted(result?.Message, result?.Status == OperationStatus.Warning)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = new ErrorInfo((int?)result?.Status ?? -1, result?.Message)
                };

                await context.Publish(errorEvent, context.CancellationToken);
                return;
            }

            LogInstalledSuiteVersion(logger, correlationId, packageName, signatureName);

            var response = new InstallSuiteVersionStarted(result.Message, result.Status == OperationStatus.Warning)
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex, correlationId, packageName, signatureName);

            var response = new InstallSuiteVersionStarted(null, false)
            {
                CorrelationId = correlationId,
                ErrorInfo = new ErrorInfo(-1, ex.Message)
            };
            await context.Publish(response, context.CancellationToken);
        }
    }

    [LoggerMessage(LogLevel.Debug, "Installing suite version package='{Package}' with signature='{Signature}' correlated by {CorrelationId}")]
    private static partial void LogInstallSuiteVersion(ILogger<InstallSuiteVersionConsumer> logger, Guid correlationId, string package, string signature);

    [LoggerMessage(LogLevel.Information, "Installed suite version package='{Package}' with signature='{Signature}' correlated by {CorrelationId}")]
    private static partial void LogInstalledSuiteVersion(ILogger<InstallSuiteVersionConsumer> logger, Guid correlationId, string package, string signature);

    [LoggerMessage(LogLevel.Information, "Failed to install suite version package='{Package}' with signature='{Signature}' correlated by {CorrelationId}")]
    private static partial void LogInstalledSuiteFailed(ILogger<InstallSuiteVersionConsumer> logger, Guid correlationId, string package, string signature);

    [LoggerMessage(LogLevel.Error, "Unexpected error on install suite version package='{Package}' with signature='{Signature}' correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<InstallSuiteVersionConsumer> logger, Exception exception, Guid correlationId, string package, string signature);
}
