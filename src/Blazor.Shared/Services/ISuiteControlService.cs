namespace Blazor.Shared.Services;

public interface ISuiteControlService
{
    Task RestartInstance();

    Task RestartAllInstances();

    Task RestartSystem();

    Task ShutdownSystem();
}
