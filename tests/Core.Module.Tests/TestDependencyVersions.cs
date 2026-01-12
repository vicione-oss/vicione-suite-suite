namespace Core.Module.Tests;

/// <summary>
/// use to configure different versions on test dependency context
/// </summary>
public class TestDependencyVersions
{
    public string ClusterManagement { get; set; } = "0.15.0";
    public string CodeAnalysis { get; internal set; } = "1.0.0";
    public string DevExpressBlazor { get; set; } = "23.2.5";
    public string DataCollectionWizard { get; set; } = "0.5.1";
    public string DotNetVersion { get; set; } = "8.0.1";
    public string MassTransit { get; set; } = "8.2.2";
    public string PingModule { get; set; } = "0.8.0";
    public string SQLitePCLRaw { get; set; } = "2.1.6";
    public string SQLiteMicrosoft { get; set; } = "8.0.4";
    public string SuiteSdk { get; set; } = "0.11.0";
    public string SassCompiler { get; set; } = "1.72.0";
    public string MsDependencyInjection { get; internal set; } = "8.0.0";
    public string MsDependencyInjectionAbstractions { get; internal set; } = "8.0.1";
    public string UiSharedDx { get; internal set; } = "0.1.0.62553";
    public string ThirdPartyPackage { get; internal set; } = "1.4.0";
}
