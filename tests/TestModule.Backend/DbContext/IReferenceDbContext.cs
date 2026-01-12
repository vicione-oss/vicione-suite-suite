using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;
using TestModule.Backend.Contracts;

namespace TestModule.Backend.DbContext;

//Mock this for unit tests:
public interface IReferenceDbContext : IModuleDbContext
{
    DbSet<Employees> Employees { get; }
    DbSet<Departments> Departments { get; }
    DbSet<DepartmentEmployees> DepartmentEmployees { get; }
    DbSet<DepartmentManager> DepartmentManagers { get; }
    DbSet<Titles> Titles { get; }
    DbSet<Salaries> Salaries { get; }
}
