namespace Core.Shared;

public static class Constants
{
    public const string SystemModuleId = "System";
    public const string AuthenticationSchema = "Identity.Application";
    public const string SetByEnvironmentMarker = "<set_by_environment>";
    public const string BackupFileExtension = ".zip";
    public const string DeviceImageFileExtension = ".swu";
    public const string DeviceImageFileName = "device_image" + DeviceImageFileExtension;
    public const long DeviceImageMaximumAllowedSize = 314572800; // 300 MB
    public const int MinimumPasswordLength = 12;

    public static readonly Guid MasterInstanceGuid = Guid.Parse("6151CF7C-0FBB-44DF-9CAC-D719C62315C9");
}
