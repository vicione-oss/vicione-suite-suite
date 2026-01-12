using Core.OS.UserManagement.Entities;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;

namespace Core.OS.DbContext;

public interface IUserDbContext : IModuleDbContext
{
    DbSet<UserTicket> Tickets { get; }
}
