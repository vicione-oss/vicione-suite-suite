using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;
using Sdk.Connections.Contracts;

namespace Core.OS.DbContext;

public interface IConnectionDbContext : IModuleDbContext
{
    DbSet<Connection> Connections { get; }
    DbSet<Tag> Tags { get; }
}
