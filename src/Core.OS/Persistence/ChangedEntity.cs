using Microsoft.EntityFrameworkCore;

namespace Core.OS.Persistence;

public sealed record ChangedEntity(string? Entity, string EntityTypeFullName, string? AssemblyFullName, EntityState State);
