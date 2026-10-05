using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Client.Models;

namespace Blazor.Server.Backend.Services;

public sealed partial class StreamUploadHandler<TModule, TContext>
    where TModule : BackendModule
{
    [LoggerMessage(LogLevel.Debug, "Transforming upload path '{Path}'")]
    private static partial void TransformingUploadPath(ILogger<StreamUploadHandler<TModule, TContext>> logger, string path);

    [LoggerMessage(LogLevel.Debug, "Transformed upload path to '{Path}'")]
    private static partial void TransformedUploadPath(ILogger<StreamUploadHandler<TModule, TContext>> logger, string path);

    [LoggerMessage(LogLevel.Error, "An error occured during path transformation for file upload '{Filename}'")]
    private static partial void PathTransformationError(ILogger<StreamUploadHandler<TModule, TContext>> logger, Exception ex, string filename);

    [LoggerMessage(LogLevel.Debug, "Try to create directory '{Path}'")]
    private static partial void PreparingPath(ILogger<StreamUploadHandler<TModule, TContext>> logger, string path);

    [LoggerMessage(LogLevel.Debug, "Create directory '{Path}'")]
    private static partial void PreparedPath(ILogger<StreamUploadHandler<TModule, TContext>> logger, string path);

    [LoggerMessage(LogLevel.Error, "An error occured during path preparation for file upload '{Filename}'")]
    private static partial void PathPreparationError(ILogger<StreamUploadHandler<TModule, TContext>> logger, Exception ex, string filename);

    [LoggerMessage(LogLevel.Error, "Failed to evaluate disk space for file upload '{Filename}'")]
    private static partial void DiskSpaceValidationError(ILogger<StreamUploadHandler<TModule, TContext>> logger, Exception ex, string filename);

    [LoggerMessage(LogLevel.Debug, "Transforming filename '{Filename}'")]
    private static partial void TransformingFilename(ILogger<StreamUploadHandler<TModule, TContext>> logger, string filename);

    [LoggerMessage(LogLevel.Debug, "Transformed filename to '{Filename}'")]
    private static partial void TransformedFilename(ILogger<StreamUploadHandler<TModule, TContext>> logger, string filename);

    [LoggerMessage(LogLevel.Error, "An error occured during filename transformation for file upload '{Filename}'")]
    private static partial void FilenameTransformationError(ILogger<StreamUploadHandler<TModule, TContext>> logger, Exception ex, string filename);

    [LoggerMessage(LogLevel.Warning, "Rejected file upload '{Filename}' because its destination '{DestinationFile}' is not directly inside the upload directory")]
    private static partial void InvalidDestination(ILogger<StreamUploadHandler<TModule, TContext>> logger, string filename, string destinationFile);

    [LoggerMessage(LogLevel.Debug, "Uploading to destination '{DestinationFile}'")]
    private static partial void UploadingToDestination(ILogger<StreamUploadHandler<TModule, TContext>> logger, string destinationFile);

    [LoggerMessage(LogLevel.Information, "Upload successful ({@LastProgress})")]
    private static partial void UploadSuccessful(ILogger<StreamUploadHandler<TModule, TContext>> logger, IStreamUploadProgress lastProgress);

    [LoggerMessage(LogLevel.Error, "Upload failed ({@LastProgress})")]
    private static partial void UploadFailed(ILogger<StreamUploadHandler<TModule, TContext>> logger, Exception exception,
        IStreamUploadProgress lastProgress);

    [LoggerMessage(LogLevel.Information, "Upload canceled ({@LastProgress})")]
    private static partial void UploadCanceled(ILogger<StreamUploadHandler<TModule, TContext>> logger, IStreamUploadProgress lastProgress);

    [LoggerMessage(LogLevel.Error, "Delete '{Filename}' failed")]
    private static partial void DeleteFailed(ILogger<StreamUploadHandler<TModule, TContext>> logger, Exception exception, string filename);
}
