using System.Buffers;
using System.Globalization;
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
    /// <summary>
    /// Size of the copy buffer in bytes. Matches the default used by <see cref="Stream.CopyTo(Stream)"/>,
    /// which stays just below the large object heap threshold.
    /// </summary>
    private const int BufferSize = 81920;

    /// <summary>
    /// Amount of bytes that must be written before <see cref="OnProgress"/> is invoked again.
    /// Reporting every chunk would flood the callback on large uploads.
    /// </summary>
    private const long ProgressReportThreshold = 1024 * 1024;

    public Func<IStreamUploadProgress, Task>? OnProgress { get; set; }

    private bool TryPathTransformation(StreamUploadHandlerOptions options, string fileName, out string path)
    {
        path = workspace.Cache;
        if (options.PathTransform is null)
            return true;

        try
        {
            TransformingUploadPath(logger, path);
            path = options.PathTransform(path);
            TransformedUploadPath(logger, path);

            return true;
        }
        catch (Exception ex)
        {
            PathTransformationError(logger, ex, fileName);
            return false;
        }
    }

    private void PreparePath(string path, string fileName)
    {
        try
        {
            if (fileSystem.Directory.Exists(path))
                return;

            PreparingPath(logger, path);
            fileSystem.Directory.CreateDirectory(path);
            PreparedPath(logger, path);
        }
        catch (Exception ex)
        {
            PathPreparationError(logger, ex, fileName);
            throw;
        }
    }

    private void ValidateDiskSpace(Stream stream, string path, string fileName)
    {
        try
        {
            var drive = fileSystem.DriveInfo.New(path);
            if (drive.AvailableFreeSpace < stream.Length * 2)
                throw new IOException(Localization.StreamUploadHandler.InsufficientDiskSpace);
        }
        catch (Exception ex)
        {
            DiskSpaceValidationError(logger, ex, fileName);
            throw;
        }
    }

    private bool TryFileTransformation(StreamUploadHandlerOptions options, string fileName, out string transformed)
    {
        transformed = fileName;
        if (options.FilenameTransform is null)
            return true;

        try
        {
            TransformingFilename(logger, fileName);
            transformed = options.FilenameTransform(fileName);
            TransformedFilename(logger, transformed);

            return true;
        }
        catch (Exception ex)
        {
            FilenameTransformationError(logger, ex, fileName);
            return false;
        }
    }

    private async Task<IStreamUploadResult> ExecuteUpload(Stream stream, StreamUploadProgress progress, string fileName, CancellationToken cancellationToken = default)
    {
        progress.DestinationFile = fileSystem.Path.Combine(progress.Path, fileName);

        UploadingToDestination(logger, progress.DestinationFile);

        await using var fileSystemStream = fileSystem.File.Create(progress.DestinationFile);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            var bytesRead = 0;
            var bytesSinceLastReport = 0L;

            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false)) != 0)
            {
                await fileSystemStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);

                progress.BytesUploaded += bytesRead;
                bytesSinceLastReport += bytesRead;

                if (bytesSinceLastReport < ProgressReportThreshold)
                    continue;

                bytesSinceLastReport = 0;

                await ReportProgress(progress).ConfigureAwait(false);
            }

            await ReportProgress(progress).ConfigureAwait(false);

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
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private Task ReportProgress(IStreamUploadProgress progress)
        => OnProgress is null ? Task.CompletedTask : OnProgress(progress);

    public async Task<IStreamUploadResult> Execute(Stream stream, string filename, CancellationToken cancellationToken = default)
    {
        if (!TryPathTransformation(options, filename, out var path))
        {
            return new StreamUploadErrorResult(string.Format(CultureInfo.CurrentCulture, Localization.StreamUploadHandler.PathTransformationFailed, filename));
        }

        if (!TryFileTransformation(options, filename, out var transformedFileName))
        {
            return new StreamUploadErrorResult(string.Format(CultureInfo.CurrentCulture, Localization.StreamUploadHandler.FilenameTransformationFailed, filename));
        }

        var progress = new StreamUploadProgress { Path = path, Filename = filename, BytesTotal = stream.Length };

        try
        {
            PreparePath(path, transformedFileName);

            ValidateDiskSpace(stream, path, transformedFileName);

            return await ExecuteUpload(stream, progress, transformedFileName, cancellationToken);
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
}
