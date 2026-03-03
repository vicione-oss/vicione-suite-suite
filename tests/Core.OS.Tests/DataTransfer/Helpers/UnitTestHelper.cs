using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Persistence;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;
using TestModule.Backend.Contracts;
using TestModule.Backend.DbContext;

namespace Core.OS.Tests.DataTransfer.Helpers;

internal class UnitTestHelper
{
    public static TestModuleDbContextPostgres InitializeTestModuleDbContextPg(string connection, List<SimpleDataTypes> seedData)
    {
        var optionsBuilderReference = new DbContextOptionsBuilder<TestModuleDbContextPostgres>().UseNpgsql(connection);
        var dbContext = new TestModuleDbContextPostgres(optionsBuilderReference.Options, seedData);

        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    public static TestModuleDbContextSqlite InitializeTestModuleDbContextSqlite(string connection,
        List<SimpleDataTypes>? seedData = default)
    {
        var optionsBuilderDest = new DbContextOptionsBuilder<TestModuleDbContextSqlite>();
        var connectionStringBuilderDest = new SqliteConnectionStringBuilder { DataSource = connection };
        using var connectionDest = new SqliteConnection(connectionStringBuilderDest.ToString());
        optionsBuilderDest.UseSqlite(connectionDest);
        var dbContext = new TestModuleDbContextSqlite(optionsBuilderDest.Options, seedData);

        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    public static ReferenceDbContextPostgres InitializeReferenceDbContextPg(string connection, TitleCase titleCase = TitleCase.Default)
    {
        var optionsBuilderReference = new DbContextOptionsBuilder<ReferenceDbContextPostgres>()
            .UseNpgsql(connection);
        var dbContext = new ReferenceDbContextPostgres(optionsBuilderReference.Options);

        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        SeedData.SeedMasterDbData(dbContext, titleCase);
        return dbContext;
    }

    public static ReferenceDbContextSqlite InitializeReferenceDbContextSqlite(string connection)
    {
        var optionsBuilderDest = new DbContextOptionsBuilder<ReferenceDbContextSqlite>();
        var connectionStringBuilderDest = new SqliteConnectionStringBuilder { DataSource = connection };
        using var connectionDest = new SqliteConnection(connectionStringBuilderDest.ToString());
        optionsBuilderDest.UseSqlite(connectionDest);
        var dbContext = new ReferenceDbContextSqlite(optionsBuilderDest.Options);

        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    public static ReferenceDbContextSqlite InitializeAndSeedingReferenceDbContextSqlite(string connection, TitleCase titleCase = TitleCase.Default)
    {
        var dbContext = InitializeReferenceDbContextSqlite(connection);
        SeedData.SeedMasterDbData(dbContext, titleCase);
        return dbContext;
    }

    public static ServiceProvider CreateServiceProvider(Dictionary<string, string?>? settings = null, ITestModuleDbContext? dbContext = null)
    {
        var config = new TestConfig().AddCustomSettings(settings).BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.AddSingleton(new ModuleContextTypeInformation(ModuleIdResolver.ResolveId<TestBackendModule>(), typeof(TestBackendModule), typeof(TestBackendModule).FullName!));
        services.AddSingleton(new ModuleContextTypeInformation(ModuleIdResolver.ResolveId<TestBackendModule>(), typeof(ITestModuleDbContext), typeof(ITestModuleDbContext).FullName!));
        services.AddSingleton(dbContext!);
        return services.BuildServiceProvider();
    }

    public static ServiceProvider CreateServiceProviderReferenceDb(Dictionary<string, string?>? settings = null, IReferenceDbContext? dbContext = null)
    {
        var config = new TestConfig().AddCustomSettings(settings).BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.AddSingleton(new ModuleContextTypeInformation(ModuleIdResolver.ResolveId<TestBackendModule>(), typeof(TestBackendModule), typeof(TestBackendModule).FullName!));
        services.AddSingleton(new ModuleContextTypeInformation(ModuleIdResolver.ResolveId<TestBackendModule>(), typeof(IReferenceDbContext), typeof(IReferenceDbContext).FullName!));
        services.AddSingleton(dbContext!);
        return services.BuildServiceProvider();
    }
}
