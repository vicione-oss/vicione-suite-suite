using System.IO.Abstractions;
using Core.OS.Instance.Contracts;
using Core.Shared.Instance.Models;
using Core.Shared.Instance.Services;
using Microsoft.Extensions.Options;
using Sdk.Backend.Modules;

namespace Core.OS.Instance.Services;

public sealed partial class StreamUploadHandler<TModule, TContext>(IWorkspaceProvider<TModule> workspace, IFileSystem fileSystem,
    IOptions<StreamUploadHandlerOptions<TContext>> options, ILogger<StreamUploadHandler<TModule, TContext>> logger)
        : IStreamUploadHandler
            where TModule : BackendModule
{
    public Func<IStreamUploadProgress, Task>? OnProgress { get; set; }

    public async Task<IStreamUploadResult> Execute(Stream stream, string filename, CancellationToken cancellationToken = default)
    {
        var path = workspace.Cache;
        if (options.Value.PathTransform is not null)
            path = options.Value.PathTransform(path);

        var drive = fileSystem.DriveInfo.New(path);
        if (drive.AvailableFreeSpace < stream.Length * 2)
            return new StreamUploadErrorResult("The device requires at least twice as much free disk space as the file size.");

        var progress = new StreamUploadProgress { Path = path, Filename = filename, BytesTotal = stream.Length };

        try
        {
            if (options.Value.FilenameTransform is not null)
                filename = options.Value.FilenameTransform(filename);

            progress.DestinationFile = fileSystem.Path.Combine(path, filename);

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

    [LoggerMessage(1, LogLevel.Information, "Upload successful ({@LastProgress})")]
    private static partial void UploadSuccessful(ILogger<StreamUploadHandler<TModule, TContext>> logger, IStreamUploadProgress LastProgress);

    [LoggerMessage(2, LogLevel.Error, "Upload failed ({@LastProgress})")]
    private static partial void UploadFailed(ILogger<StreamUploadHandler<TModule, TContext>> logger, Exception exception,
        IStreamUploadProgress LastProgress);

    [LoggerMessage(3, LogLevel.Information, "Upload canceled ({@LastProgress})")]
    private static partial void UploadCanceled(ILogger<StreamUploadHandler<TModule, TContext>> logger, IStreamUploadProgress LastProgress);

    [LoggerMessage(4, LogLevel.Error, "Delete {Filename} failed")]
    private static partial void DeleteFailed(ILogger<StreamUploadHandler<TModule, TContext>> logger, Exception exception, string Filename);
}
