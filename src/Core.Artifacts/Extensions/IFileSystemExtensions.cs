using System.IO.Abstractions;

namespace Core.Artifacts.Extensions;

public static class IFileSystemExtensions
{
    public static string EnsureContainedPath(this IFileSystem fileSystem, string rootPath, string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            throw new InvalidOperationException("Archive contains an entry with an empty name.");

        // Reject rooted/absolute paths (e.g. "/etc/passwd" or "C:\\Windows\\...").
        if (fileSystem.Path.IsPathRooted(entryName))
            throw new InvalidOperationException($"Archive entry '{entryName}' uses a rooted path and is rejected.");

        var normalizedRoot = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.GetFullPath(rootPath));
        var rootWithSeparator = normalizedRoot + fileSystem.Path.DirectorySeparatorChar;

        var resolved = fileSystem.Path.GetFullPath(fileSystem.Path.Combine(normalizedRoot, entryName));

        if (!resolved.StartsWith(rootWithSeparator, StringComparison.Ordinal)
            && !string.Equals(resolved, normalizedRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Archive entry '{entryName}' escapes the target directory and is rejected (possible zip-slip).");
        }

        return resolved;
    }
}
