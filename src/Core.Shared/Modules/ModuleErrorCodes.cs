namespace Core.Shared.Modules;

public static class ModuleErrorCodes
{
    public const int FoundNoVersion = 10;
    public const int FoundCiVersionOnly = 11;
    public const int FoundInvalidVersion = 12;
    public const int SdkVersionIncompatible = 20;

    public const int DeleteFailed = 30;
    public const int DownloadFailed = 40;

    public const int ResolveVersionFailed = 100;
    public const int RequestVersionsFailed = 101;
    public const int RequestAvailableFailed = 110;

    public const int StartupError = 131;
    public const int StartupErrorUi = 132;

    public const int UnknownResolveError = 200;
    public const int UnknownCompatibilityError = 201;

    public const int MetadataInvalid = 141;

    public const int EnqueueOperationsFailed = 230;
    public const int UpdateOptionsFailed = 231;
}
