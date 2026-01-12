using Core.OS.DbContext;
using Core.Shared.Instance.Contracts;
using Microsoft.EntityFrameworkCore;
using Sdk.Instance;

namespace Core.OS.Tests.Extensions;

internal static class ApplicationDbContextExtensions
{
    public static IEnumerable<IInstanceInformation> SeedInstanceInfos(this IApplicationDbContext dbContext, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            dbContext.InstanceInfo.Add(new InstanceInformation()
            {
                Id = Guid.NewGuid(),
                Name = $"Instance {i}",
                Description = $"Instance {i} description",
                FormattedName = $"Instance {i} {{FormattedName}}",
                Type = i == 1 ? InstanceType.Master : InstanceType.Slave,
                InstalledModules =
                [
                    "Module1",
                    "Module2"
                ],
                Version = "ci-12345"
            });

        }

        dbContext.Instance.SaveChanges();

        return dbContext.InstanceInfo.AsNoTracking().ToList();
    }
}
