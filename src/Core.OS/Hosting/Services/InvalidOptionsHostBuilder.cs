using Serilog;

namespace Core.OS.Hosting.Services;

internal static class InvalidOptionsHostBuilder
{
    public static WebApplication Build(string[] args, string[] failures)
    {
        // create the simplest host possible to display the errors
        var builder = WebApplication.CreateBuilder(args);
        var host = builder.Build();
        host.UseHttpsRedirection();

        foreach (var failure in failures)
        {
            Log.Error(failure);
        }

        // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-8.0#t-any-other-type-return-values
        host.MapGet("/", () => failures);

        return host;
    }
}
