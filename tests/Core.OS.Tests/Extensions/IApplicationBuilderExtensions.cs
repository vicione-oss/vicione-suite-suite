using Microsoft.AspNetCore.Builder;

namespace Core.OS.Tests.Extensions;

public static class IApplicationBuilderExtensions
{
    public static IApplicationBuilder ConfigureTestSetup(this IApplicationBuilder app)
    {
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorPages();
            endpoints.MapControllers();
            endpoints.MapFallbackToFile("index.html");
        });

        return app;
    }
}
