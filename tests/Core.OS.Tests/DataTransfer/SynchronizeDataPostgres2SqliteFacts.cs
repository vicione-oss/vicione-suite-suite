using Core.OS.DbContext.Extensions;
using Core.OS.Instance.Initialization;
using Core.OS.Tests.DataTransfer.Helpers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Modules;
using TestModule.Backend;
using TestModule.Backend.Contracts;
using Xunit;
using static Core.OS.Tests.DataTransfer.Helpers.SeedData;
using static Core.OS.Tests.DataTransfer.Helpers.UnitTestHelper;

namespace Core.OS.Tests.DataTransfer;

public sealed class SynchronizeDataPostgres2SqliteFacts
{
    private readonly string _pathDbSqlite = "..\\..\\..\\DataTransfer\\";

    [Fact]
    [Trait(Traits.Category, Traits.ManualDbTest)]
    public async Task FirstStep_CheckSimpleData()
    {
        // Arrange
        // Konfiguration für Quell-DB
        var localPostgresConnection = @"Host=127.0.0.1; Port=5432; Database=testmodulesource; User Id=postgres; Password=admin; Include Error Detail=true";
        await using var dbSourceContext =
            InitializeTestModuleDbContextPg(localPostgresConnection, DefaultSeedDataSimpleDataTypes());
        await using var serviceProvider = CreateServiceProvider(null, dbSourceContext);

        // Konfiguration für Ziel-DB
        var localSqliteConnection = $"{_pathDbSqlite}TestModuleDest.db";
        await using var dbDestContext = InitializeTestModuleDbContextSqlite(localSqliteConnection);
        await using var serviceProviderSlave = CreateServiceProvider(null, dbDestContext);
        var loggerMock = Substitute.For<ILogger<SyncDataActivity>>();
        SyncDataActivity sdActivity = new(serviceProviderSlave, loggerMock);

        var installedModules = new List<string> { ModuleIdResolver.ResolveId<TestBackendModule>() };
        var executeContextMock = Substitute.For<ExecuteContext<SyncDataArguments>>();

        // Act
        var argumentsCollection = await SyncDataHelpers.CreateSyncDataArgumentsPg(serviceProvider, installedModules, TestContext.Current.CancellationToken);
        foreach (var arguments in argumentsCollection)
        {
            executeContextMock.Arguments
                .Returns(arguments);

            await sdActivity.Execute(executeContextMock);
        }

        // Assert
        Assert.NotNull(argumentsCollection);
        var machineIdSource = dbSourceContext.SimpleDataTypes.First(r => r.Id == 4).MachineId;
        var machineIdDest = dbDestContext.SimpleDataTypes.First(r => r.Id == 4).MachineId;
        Assert.Equal(machineIdSource, machineIdDest);
    }

    [Fact]
    [Trait(Traits.Category, Traits.ManualDbTest)]
    public async Task SynchronizeSuccessful_EmployeeSmallDB()
    {
        // Arrange
        // Konfiguration für Quell-DB
        var localPostgresConnection = @"Host=127.0.0.1; Port=5432; Database=masteremployeesmall; User Id=postgres; Password=admin; Include Error Detail=true";
        await using var dbSourceContext = InitializeReferenceDbContextPg(localPostgresConnection);
        await using var serviceProvider = CreateServiceProviderReferenceDb(null, dbSourceContext);

        // Konfiguration für Ziel-DB
        var localSqliteConnection = $"{_pathDbSqlite}ReferenceDest.db";
        await using var dbDestContext = InitializeReferenceDbContextSqlite(localSqliteConnection);
        await using var serviceProviderSlave = CreateServiceProviderReferenceDb(null, dbDestContext);
        var loggerMock = Substitute.For<ILogger<SyncDataActivity>>();
        SyncDataActivity sdActivity = new(serviceProviderSlave, loggerMock);

        await using var dbDestConnection = dbDestContext.Database.GetDbConnection();
        await dbDestConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = dbDestConnection.CreateCommand();

        var installedModules = new List<string> { ModuleIdResolver.ResolveId<TestBackendModule>() };
        var tableList = await command.GetTablesSqlite(TestContext.Current.CancellationToken);
        var executeContextMock = Substitute.For<ExecuteContext<SyncDataArguments>>();

        // Act
        var argumentsCollection = await SyncDataHelpers.CreateSyncDataArgumentsPg(serviceProvider, installedModules, TestContext.Current.CancellationToken);
        foreach (var arguments in argumentsCollection)
        {
            var foundTable = tableList.Any(tablename => arguments.Table.Equals(tablename, StringComparison.OrdinalIgnoreCase));
            if (foundTable)
            {
                executeContextMock.Arguments
                    .Returns(arguments);

                await sdActivity.Execute(executeContextMock);
            }
        }

        // Assert
        Assert.NotNull(argumentsCollection);
        Assert.Equal(dbSourceContext.Employees.Count(), dbDestContext.Employees.Count());
        Assert.Equal(dbSourceContext.Titles.Count(), dbDestContext.Titles.Count());
        Assert.Equal(dbSourceContext.Salaries.Count(), dbDestContext.Salaries.Count());

        var gender = dbDestContext.Employees.First(e => e.emp_no == 10001).gender;
        Assert.Equal(Employees.Gender.M, gender);
    }

    [Fact]
    [Trait(Traits.Category, Traits.ManualDbTest)]
    public async Task SQLInjection_Insert_ExecuteSqlCommand()
    {
        // Ein gefundenes Single-Quote in einer TEXT-Spalte wird mit weiterem Single-Quote maskiert.
        // Dieses Single-Quote interpretiert die sqlite-DB als Escape-Zeichen.
        // Arrange
        // Konfiguration für Quell-DB
        var localPostgresConnection = @"Host=127.0.0.1; Port=5432; Database=EmployeeSqlInjectionExecute; User Id=postgres; Password=admin; Include Error Detail=true";
        await using var dbSourceContext = InitializeReferenceDbContextPg(localPostgresConnection, TitleCase.Execute);
        await using var serviceProvider = CreateServiceProviderReferenceDb(null, dbSourceContext);

        // Konfiguration für Ziel-DB
        var localSqliteConnection = $"{_pathDbSqlite}ReferenceDest.db";
        await using var dbDestContext = InitializeReferenceDbContextSqlite(localSqliteConnection);
        await using var serviceProviderSlave = CreateServiceProviderReferenceDb(null, dbDestContext);
        var loggerMock = Substitute.For<ILogger<SyncDataActivity>>();
        SyncDataActivity sdActivity = new(serviceProviderSlave, loggerMock);

        var installedModules = new List<string> { ModuleIdResolver.ResolveId<TestBackendModule>() };
        await using var dbDestConnection = dbDestContext.Database.GetDbConnection();
        await dbDestConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = dbDestConnection.CreateCommand();
        var tableList = await command.GetTablesSqlite(TestContext.Current.CancellationToken);
        var executeContextMock = Substitute.For<ExecuteContext<SyncDataArguments>>();

        // Act
        var argumentsCollection = await SyncDataHelpers.CreateSyncDataArgumentsPg(serviceProvider, installedModules, TestContext.Current.CancellationToken);
        foreach (var arguments in argumentsCollection)
        {
            var foundTable = tableList.Any(tablename => arguments.Table.Equals(tablename, StringComparison.OrdinalIgnoreCase));
            if (foundTable)
            {
                executeContextMock.Arguments
                    .Returns(arguments);

                await sdActivity.Execute(executeContextMock);
            }
        }

        // Assert
        Assert.NotNull(argumentsCollection);

        // Tabelle "DepartmentManager" ist mit Werten befüllt, SQL-Injection wurde nicht ausgeführt.
        Assert.Equal(dbSourceContext.DepartmentManagers.Count(), dbDestContext.DepartmentManagers.Count());

        /*
     *   emp_no, title                                                             , weitere Spalten
        (10009, "'x','1966-03-28','1966-02-27'); DELETE FROM DepartmentManager; --",'1990-02-18','1995-02-18'),
     */
        Assert.Equal(dbSourceContext.Titles.Count(), dbDestContext.Titles.Count());
        Assert.True(dbDestContext.Titles.Any(t => t.emp_no == 10009 && t.title == TitleWithExecuteStatement));
    }

    [Fact]
    [Trait(Traits.Category, Traits.ManualDbTest)]
    public async Task SQLInjection_Insert_CancelInsert()
    {
        // Bei einigen Datensätzen der Tabelle "Titles" wurden in der Spalte "Title" SQL-Injection-Werte eingetragen.
        // Arrange
        // Konfiguration für Quell-DB
        var localPostgresConnection = @"Host=127.0.0.1; Port=5432; Database=EmployeeSqlInjectionCancelInsert; User Id=postgres; Password=admin; Include Error Detail=true";
        await using var dbSourceContext = InitializeReferenceDbContextPg(localPostgresConnection, TitleCase.CancelInsert);
        await using var serviceProvider = CreateServiceProviderReferenceDb(null, dbSourceContext);

        var installedModules = new List<string> { ModuleIdResolver.ResolveId<TestBackendModule>() };

        // Konfiguration für Ziel-DB
        var localSqliteConnection = $"{_pathDbSqlite}ReferenceDest.db";
        await using var dbDestContext = InitializeReferenceDbContextSqlite(localSqliteConnection);
        await using var serviceProviderSlave = CreateServiceProviderReferenceDb(null, dbDestContext);
        var loggerMock = Substitute.For<ILogger<SyncDataActivity>>();
        SyncDataActivity sdActivity = new(serviceProviderSlave, loggerMock);

        await using var dbDestConnection = dbDestContext.Database.GetDbConnection();
        await dbDestConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = dbDestConnection.CreateCommand();

        var tableList = await command.GetTablesSqlite(TestContext.Current.CancellationToken);
        var executeContextMock = Substitute.For<ExecuteContext<SyncDataArguments>>();

        // Act
        var argumentsCollection = await SyncDataHelpers.CreateSyncDataArgumentsPg(serviceProvider, installedModules, TestContext.Current.CancellationToken);
        foreach (var arguments in argumentsCollection)
        {
            var foundTable = tableList.Any(tablename => arguments.Table.Equals(tablename, StringComparison.OrdinalIgnoreCase));
            if (foundTable)
            {
                executeContextMock.Arguments
                    .Returns(arguments);

                await sdActivity.Execute(executeContextMock);
            }
        }

        // Assert
        Assert.NotNull(argumentsCollection);

        // Es wurden alle Datensätze in die Zieltabelle "Titles" eingetragen.
        Assert.Equal(dbSourceContext.Titles.Count(), dbDestContext.Titles.Count());

        /*
     *   emp_no, title                  , weitere Spalten
        (10008, "Assistant Engineer''"  ,'1998-03-11','2000-07-31'),
        (10009, 'Assistant Engineer""'  ,'1985-02-18','1990-02-18'),
        (10009, "Staff')"               ,'1990-02-18','1995-02-18'),
        (10009, "Senior Engineer');"    ,'1995-02-18','9999-01-01'),
        (10010, "Engineer\'"            ,'1996-11-24','9999-01-01'),
        (10011, 'Staff`)'               ,'1998-02-11','9999-01-01'),
     */
        Assert.True(dbDestContext.Titles.Any(t => t.emp_no == 10008 && t.title == TitleWithSingleQuote));
        Assert.True(dbDestContext.Titles.Any(t => t.emp_no == 10009 && t.title == TitleWithDoubleQuote));
        Assert.True(dbDestContext.Titles.Any(t => t.emp_no == 10009 && t.title == TitleWithSingleQuoteAndRoundBracket));
        Assert.True(dbDestContext.Titles.Any(t => t.emp_no == 10009 && t.title == TitleWithSingleQuoteRoundBrackeAndSemicolon));
        Assert.True(dbDestContext.Titles.Any(t => t.emp_no == 10010 && t.title == TitleWithBackslashSingleQuote));
        Assert.True(dbDestContext.Titles.Any(t => t.emp_no == 10011 && t.title == TitleWithBackStickAndRoundBracket));
    }
}
