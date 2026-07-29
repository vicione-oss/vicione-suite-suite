using System.IO.Abstractions;
using Blazor.Server.Backend.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Client.Contracts;
using Sdk.Client.Models;

namespace Blazor.Server.Backend.Services;

public sealed partial class StreamUploadHandler<TModule, TContext>(IWorkspaceProvider<TModule> workspace, IFileSystem fileSystem,
    StreamUploadHandlerOptions options, ILogger<StreamUploadHandler<TModule, TContext>> logger)
        : IStreamUploadHandler<TContext>
            where TModule : BackendModule
{
    public Func<IStreamUploadProgress, Task>? OnProgress { get; set; }

    public async Task<IStreamUploadResult> Execute(Stream stream, string filename, CancellationToken cancellationToken = default)
    {
        var path = workspace.Cache;

        try
        {
            if (options.PathTransform is not null)
                path = options.PathTransform(path);

            var drive = fileSystem.DriveInfo.New(path);
            if (drive.AvailableFreeSpace < stream.Length * 2)
                return new StreamUploadErrorResult("The device requires at least twice as much free disk space as the file size.");

            Directory.CreateDirectory(path);
        }
        catch (Exception ex)
        {
            PathHandlingError(logger, filename, ex.GetType().Name, ex.Message, ex.StackTrace ?? string.Empty);
            return new StreamUploadErrorResult("Failed to prepare upload path");
        }

        var progress = new StreamUploadProgress { Path = path, Filename = filename, BytesTotal = stream.Length };

        try
        {
            if (options.FilenameTransform is not null)
                filename = options.FilenameTransform(filename);

            progress.DestinationFile = fileSystem.Path.Combine(progress.Path, filename);

            await using var fileSystemStream = fileSystem.File.Create(progress.DestinationFile);
            try
            {
                var bytesRead = 0;
                var buffer = new byte[1024 * 10];

                while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    await fileSystemStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);

                    progress.BytesUploaded += bytesRead;

                    if (OnProgress is not null)
                        await OnProgress(progress).ConfigureAwait(false);
                }

                UploadSuccessful(logger, progress);

                return new StreamUploadSuccessResult(progress.DestinationFile);
            }
            catch (Exception)
            {
                fileSystemStream.Close();

                try
                {
                    fileSystem.File.Delete(progress.DestinationFile);
                }
                catch (Exception ex)
                {
                    DeleteFailed(logger, ex, progress.DestinationFile);
                }

                throw;
            }
        }
        catch (OperationCanceledException)
        {
            UploadCanceled(logger, progress);

            throw;
        }
        catch (Exception ex)
        {
            UploadFailed(logger, ex, progress);

            return new StreamUploadErrorResult(ex.Message);
        }
    }

    [LoggerMessage(1, LogLevel.Error, "An error occured during path preparation for file upload {filename}: {ex} {message} {trace}")]
    private static partial void PathHandlingError(ILogger<StreamUploadHandler<TModule, TContext>> logger, string filename, string ex, string message, string trace);

    [LoggerMessage(1, LogLevel.Information, "Upload successful ({@LastProgress})")]
    private static partial void UploadSuccessful(ILogger<StreamUploadHandler<TModule, TContext>> logger, IStreamUploadProgress lastProgress);

    [LoggerMessage(2, LogLevel.Error, "Upload failed ({@LastProgress})")]
    private static partial void UploadFailed(ILogger<StreamUploadHandler<TModule, TContext>> logger, Exception exception,
        IStreamUploadProgress lastProgress);

    [LoggerMessage(3, LogLevel.Information, "Upload canceled ({@LastProgress})")]
    private static partial void UploadCanceled(ILogger<StreamUploadHandler<TModule, TContext>> logger, IStreamUploadProgress lastProgress);

    [LoggerMessage(4, LogLevel.Error, "Delete {Filename} failed")]
    private static partial void DeleteFailed(ILogger<StreamUploadHandler<TModule, TContext>> logger, Exception exception, string filename);
}
