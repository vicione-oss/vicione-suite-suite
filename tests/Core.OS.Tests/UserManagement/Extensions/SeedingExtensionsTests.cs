using Core.OS.DbContext;
using Core.OS.Modules;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.UserManagement.Extensions;

public class SeedingExtensionsTests : TestWithDbContextSqlite<UserDbContextSqlite>
{
    private ServiceProvider CreateServiceProvider(Dictionary<string, string?>? settings = null)
    {
        var config = new TestConfig().AddCustomSettings(settings).BuildConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUserManagement();
        services.AddSingleton(config);
        services.AddSingleton(Substitute.For<IModuleHost>());
        services.AddSingleton<UserDbContext>(_ => TestDbContext);
        services.AddIdentityCore<SuiteUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<UserDbContext>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SeedUsersAndRoles_should_seed_data()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();

        // Act
        await serviceProvider.SeedUsersAndRoles();

        // Assert
        var context = serviceProvider.GetRequiredService<UserDbContext>();
        Assert.True(context.Users.Any());
        Assert.True(context.UserRoles.Any());
    }
}
