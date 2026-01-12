namespace Core.OS.Modules;

public interface IWorkspaceManagement
{
    string GetHomeDirectory(string moduleId);

    string GetCacheDirectory(string moduleId);

    void WriteResetHomeDirectoryFlag(string moduleId);

    void WriteResetCacheDirectoryFlag(string moduleId);
}
