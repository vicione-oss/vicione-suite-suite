namespace Blazor.Shared.Services;

public interface ISuiteControlService
{
    Task RestartSuite();

    Task RestartSystem();

    Task ShutdownSystem();
}
