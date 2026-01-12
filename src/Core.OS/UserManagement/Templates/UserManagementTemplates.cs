using System.IO.Abstractions;

namespace Core.OS.UserManagement.Templates;

public class UserManagementTemplates(IFileSystem fileSystem)
{
    private static string BasePath
        => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserManagement", "Templates");

    public IFileInfo VerifyAddressEmailTemplate => GetFileInfo("VerifyAddressEmailTemplate");
    public IFileInfo ResetPasswordEmailTemplate => GetFileInfo("ResetPasswordEmailTemplate");

    private IFileInfo GetFileInfo(string templateName)
        => fileSystem.FileInfo.New(Path.Combine(BasePath, $"{templateName}.liquid.html"));
}
