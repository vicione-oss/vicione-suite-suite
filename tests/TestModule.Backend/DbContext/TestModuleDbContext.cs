using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sdk.Backend.Persistence;
using TestModule.Backend.Contracts;
using TestModule.Backend.Helpers;

namespace TestModule.Backend.DbContext;

public class TestModuleDbContext(DbContextOptions options, List<SimpleDataTypes>? seedDataSimpleDataTypes = default) :
    ModuleDbContext(options), ITestModuleDbContext
{
    public const string Id = "TestModule";
    private readonly List<SimpleDataTypes>? _seedDataSimpleDataTypes = (seedDataSimpleDataTypes == default) ?
            SeedData.DefaultSeedDataSimpleDataTypes() :
            seedDataSimpleDataTypes;
    private readonly List<SimpleEmployees>? _seedDataSimpleEmployees = SeedData.DefaultDataSimpleEmployees();

    public override string DefaultSchemaName => "datatypes";
    public DbSet<SimpleDataTypes> SimpleDataTypes => Set<SimpleDataTypes>();
    public DbSet<SimpleEmployees> Employees => Set<SimpleEmployees>();

    protected override void OnModuleModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SimpleEmployees>()
            .HasKey(e => e.emp_no);

        modelBuilder.Entity<SimpleEmployees>()
            .Property(e => e.Salutaion)
            .HasConversion<string>();

        modelBuilder.Entity<SimpleDataTypes>()
            .Property(e => e.Built).HasColumnType("DATE");
        modelBuilder.Entity<SimpleDataTypes>()
            .Property(e => e.Created).HasColumnType("DATE");

        // Keine Lösung, wie sich ein Datetime-Value als Date-Value in der DB lässt.
        // TestModuleSource.db, Werte mittels EF-API abgespeichert
        // sqlite > select * from SimpleDataTypes;
        // 4 | 666B4E88 - F076 - 4B72 - AEFE - B18637C5C78C | Presse XP1 | Presse für Stoßfänger -XP1 | 4 | 2001 - 04 - 01 00:00:00 | 2023 - 01 - 24 08:25:55.4904443
        // 5 | A85D794C - 9F7D - 465F - 88CE - BB8113501045 | Presse XP2 | Presse für Stoßfänger -XP2 | 3 | 2010 - 06 - 10 00:00:00 | 2023 - 01 - 24 08:25:55.4904679
        // 6 | 457229A9 - AA49 - 439A - A898 - 76D479853E9F | Presse XP5 | Presse für Stoßfänger -XP5 | 1 | 2011 - 11 - 30 00:00:00 | 2023 - 01 - 24 08:25:55.4904682
        // *****
        // TestModuleDest.db, Werte mittels Raw-SQL (INSERT) abgespeichert
        // sqlite > select * from SimpleDataTypes;
        // 4 | 666B4E88 - F076 - 4B72 - AEFE - B18637C5C78C | Presse XP1 | Presse für Stoßfänger -XP1 | 4 | 2001 - 04 - 01 | 2023 - 01 - 24
        // 5 | A85D794C - 9F7D - 465F - 88CE - BB8113501045 | Presse XP2 | Presse für Stoßfänger -XP2 | 3 | 2010 - 06 - 10 | 2023 - 01 - 24
        // 6 | 457229A9 - AA49 - 439A - A898 - 76D479853E9F | Presse XP5 | Presse für Stoßfänger -XP5 | 1 | 2011 - 11 - 30 | 2023 - 01 - 24
        // modelBuilder.Entity<SimpleDataTypes>()
        //    .HasCheckConstraint("simpledatatypes_isdate_built", "Built IS date(Built,'+0 days')")
        //    .HasCheckConstraint("simpledatatypes_isdate_created", "Created IS date(Created,'+0 days')");

        modelBuilder.Entity<SimpleDataTypes>()
            .HasData(_seedDataSimpleDataTypes!);

        modelBuilder.Entity<SimpleEmployees>()
            .HasData(_seedDataSimpleEmployees!);
    }
}

public class DateTimeToIso8601StringConverter : ValueConverter<DateTimeOffset, string>
{
    private static readonly Expression<Func<string, DateTimeOffset>> _deserialize = x => DateTime.Parse(x, new CultureInfo("de-DE")).ToUniversalTime();

    static readonly Expression<Func<DateTimeOffset, string>> _serialize = x => x.ToString("o", CultureInfo.InvariantCulture);

    public DateTimeToIso8601StringConverter() : base(_serialize, _deserialize)
    {
    }
}

public class TestModuleDbContextSqlite(DbContextOptions<TestModuleDbContextSqlite> options,
     List<SimpleDataTypes>? seedDataSimpleDataTypes = default) : TestModuleDbContext(options, seedDataSimpleDataTypes), ISqliteDbContext
{
    public const string DbName = "TestModule.db";
}

public class TestModuleDbContextPostgres(DbContextOptions<TestModuleDbContextPostgres> options,
    List<SimpleDataTypes>? seedDataSimpleDataTypes = default) : TestModuleDbContext(options, seedDataSimpleDataTypes), IPostgresDbContext
{
}

public class TestModuleDbContextPostgresFactory : IDesignTimeDbContextFactory<TestModuleDbContextPostgres>
{
    public TestModuleDbContextPostgres CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestModuleDbContextPostgres>();
        optionsBuilder.UseNpgsql();
        return new TestModuleDbContextPostgres(optionsBuilder.Options);
    }
}

public class TestModuleDbContextSqliteFactory : IDesignTimeDbContextFactory<TestModuleDbContextSqlite>
{
    public TestModuleDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestModuleDbContextSqlite>();
        optionsBuilder.UseSqlite();
        return new TestModuleDbContextSqlite(optionsBuilder.Options);
    }
}
