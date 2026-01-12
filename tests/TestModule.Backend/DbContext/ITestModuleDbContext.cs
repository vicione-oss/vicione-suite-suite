using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;
using TestModule.Backend.Contracts;

namespace TestModule.Backend.DbContext;

public interface ITestModuleDbContext : IModuleDbContext
{
    DbSet<SimpleDataTypes> SimpleDataTypes { get; }
    DbSet<SimpleEmployees> Employees { get; }
}
