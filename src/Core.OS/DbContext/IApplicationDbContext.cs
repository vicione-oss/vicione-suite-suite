using Core.Shared.Instance.Contracts;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;

namespace Core.OS.DbContext;

public interface IApplicationDbContext : IModuleDbContext
{
    DbSet<InstanceInformation> InstanceInfo { get; }
    DbSet<CrossInstanceConfiguration> CrossInstanceConfiguration { get; }
    DbSet<Nonce> Nonces { get; }
    DbSet<OnboardingState> OnboardingStates { get; }
}
