using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sdk.Backend.Persistence;
using TestModule.Backend.Contracts;

namespace TestModule.Backend.DbContext;

// To add migrations see Readme.md section Module-Migration
public class ReferenceDbContext : ModuleDbContext, IReferenceDbContext
{
    public const string Id = "Reference";

    public override string DefaultSchemaName => "reference";
    public DbSet<Employees> Employees => Set<Employees>();
    public DbSet<Departments> Departments => Set<Departments>();
    public DbSet<DepartmentManager> DepartmentManagers => Set<DepartmentManager>();
    public DbSet<DepartmentEmployees> DepartmentEmployees => Set<DepartmentEmployees>();
    public DbSet<Titles> Titles => Set<Titles>();
    public DbSet<Salaries> Salaries => Set<Salaries>();

    internal ReferenceDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModuleModelCreating(ModelBuilder modelBuilder)
    {
#pragma warning disable CS0618 // Typ oder Element ist veraltet
        // Employees
        modelBuilder.Entity<Employees>()
            .Property(e => e.first_name).HasColumnType("TEXT");
        modelBuilder.Entity<Employees>()
            .Property(e => e.last_name).HasColumnType("TEXT");
        modelBuilder.Entity<Employees>()
            .Property(e => e.birth_date).HasColumnType("DATE");
        modelBuilder.Entity<Employees>()
            .Property(e => e.hire_date).HasColumnType("DATE");
        modelBuilder.Entity<Employees>()
            .HasCheckConstraint("employees_length_first_name", "Length(first_name) < 15 ")
            .HasCheckConstraint("employees_length_last_name", "Length(last_name) < 17 ");

        // Departments
        modelBuilder.Entity<Departments>()
            .Property(d => d.dept_no).HasColumnType("TEXT");
        modelBuilder.Entity<Departments>()
            .Property(d => d.dept_name).HasColumnType("TEXT");
        modelBuilder.Entity<Departments>()
            .HasIndex(d => d.dept_name).IsUnique();
        modelBuilder.Entity<Departments>()
            .HasCheckConstraint("departments_length_dept_no", "Length(dept_no) < 5 ")
            .HasCheckConstraint("departments_length_dept_name", "Length(dept_name) < 41 ");

        // DepartmentManager
        modelBuilder.Entity<DepartmentManager>()
            .Property(dm => dm.dept_no).HasColumnType("TEXT");
        modelBuilder.Entity<DepartmentManager>()
            .Property(dm => dm.from_date).HasColumnType("DATE");
        modelBuilder.Entity<DepartmentManager>()
            .Property(dm => dm.to_date).HasColumnType("DATE");
        modelBuilder.Entity<DepartmentManager>()
            .HasCheckConstraint("deptmanager_length_dept_no", "Length(dept_no) < 5 ");

        modelBuilder.Entity<DepartmentManager>().HasKey(dm => new { dm.emp_no, dm.dept_no });
        modelBuilder.Entity<DepartmentManager>()
            .HasOne(dm => dm.employee)
            .WithMany(e => e.dept_manager)
            .HasForeignKey(dm => dm.emp_no);
        modelBuilder.Entity<DepartmentManager>()
            .HasOne(dm => dm.department)
            .WithMany(e => e.dept_manager)
            .HasForeignKey(dm => dm.dept_no);

        // DepartmentEmployees
        modelBuilder.Entity<DepartmentEmployees>()
            .Property(de => de.dept_no).HasColumnType("TEXT");
        modelBuilder.Entity<DepartmentEmployees>()
            .Property(de => de.from_date).HasColumnType("DATE");
        modelBuilder.Entity<DepartmentEmployees>()
            .Property(de => de.to_date).HasColumnType("DATE");
        modelBuilder.Entity<DepartmentEmployees>()
            .HasCheckConstraint("deptempl_length_dept_no", "Length(dept_no) < 5 ");

        modelBuilder.Entity<DepartmentEmployees>().HasKey(de => new { de.emp_no, de.dept_no });
        modelBuilder.Entity<DepartmentEmployees>()
            .HasOne(de => de.employee)
            .WithMany(e => e.dept_empl)
            .HasForeignKey(de => de.emp_no);
        modelBuilder.Entity<DepartmentEmployees>()
            .HasOne(de => de.department)
            .WithMany(e => e.dept_empl)
            .HasForeignKey(de => de.dept_no);

        // Titles
        modelBuilder.Entity<Titles>()
            .Property(t => t.title).HasColumnType("TEXT");
        modelBuilder.Entity<Titles>()
            .Property(t => t.from_date).HasColumnType("DATE");
        modelBuilder.Entity<Titles>()
            .Property(t => t.to_date).HasColumnType("DATE");
        modelBuilder.Entity<Titles>()
            .HasCheckConstraint("titles_length_title", "Length(title) < 257 ");

        modelBuilder.Entity<Titles>().HasKey(t => new { t.emp_no, t.title, t.from_date });
        modelBuilder.Entity<Titles>()
            .HasOne(t => t.employee)
            .WithMany(e => e.titles)
            .HasForeignKey(t => t.emp_no);

        // Salaries
        modelBuilder.Entity<Salaries>()
            .Property(t => t.from_date).HasColumnType("DATE");
        modelBuilder.Entity<Salaries>()
            .Property(t => t.to_date).HasColumnType("DATE");

        modelBuilder.Entity<Salaries>().HasKey(s => new { s.emp_no, s.from_date });
        modelBuilder.Entity<Salaries>()
            .HasOne(s => s.employee)
            .WithMany(e => e.salaries)
            .HasForeignKey(s => s.emp_no);
#pragma warning restore CS0618 // Typ oder Element ist veraltet

    }
}

public class ReferenceDbContextSqlite(DbContextOptions<ReferenceDbContextSqlite> options) : ReferenceDbContext(options), ISqliteDbContext
{
    public const string DbName = "Reference.db";
}

public class ReferenceDbContextSqliteFactory : IDesignTimeDbContextFactory<ReferenceDbContextSqlite>
{
    public ReferenceDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReferenceDbContextSqlite>();
        optionsBuilder.UseSqlite();
        return new ReferenceDbContextSqlite(optionsBuilder.Options);
    }
}

public class ReferenceDbContextPostgres(DbContextOptions<ReferenceDbContextPostgres> options) : ReferenceDbContext(options), IPostgresDbContext
{
}

public class ReferenceDbContextPostgresFactory : IDesignTimeDbContextFactory<ReferenceDbContextPostgres>
{
    public ReferenceDbContextPostgres CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReferenceDbContextPostgres>();
        optionsBuilder.UseNpgsql();
        return new ReferenceDbContextPostgres(optionsBuilder.Options);
    }
}
