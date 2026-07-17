using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;
using Microsoft.Playwright;
using Xunit;

namespace Core.OS.E2E.Tests.MasterSlave;

/// <summary>
/// Master/slave smoke test: a user that only the master seeds must become usable on the slaves.
/// Slaves run with <c>SeedTestUsers=false</c> and never seed (seeding is gated to non-slaves),
/// so they can only obtain the account via replication — a successful login on each slave
/// therefore proves the user replicated.
///
/// The CI job gates each slave's readiness on <c>/hc</c> (synchronization complete) before this
/// runs; the bounded retry below is belt-and-suspenders for any remaining replication lag.
/// See docs/e2e-testing.md.
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2EMasterSlave)]
public sealed class UserReplicationSmokeTests(PlaywrightFixture fixture) : MasterSlaveE2ETest(fixture)
{
    private const int MaxLoginAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task Master_seeded_user_is_replicated_to_each_slave()
    {
        // Act + Assert: the master-seeded account must be able to authenticate on both slaves.
        await AssertSeededUserCanLogIn(Slave1Url);
        await AssertSeededUserCanLogIn(Slave2Url);
    }

    private async Task AssertSeededUserCanLogIn(Uri slaveUrl)
    {
        for (var attempt = 1; ; attempt++)
        {
            var login = await FreshLoginPageFor(slaveUrl);

            try
            {
                await login.Goto();
                await login.Login(TestUsers.UserName, TestUsers.Password);
                await login.ExpectAuthenticated();
                return;
            }
            catch (Exception e) when (e is PlaywrightException or TimeoutException && attempt < MaxLoginAttempts)
            {
                await Task.Delay(RetryDelay);
            }
        }
    }

    private async Task<LoginPage> FreshLoginPageFor(Uri slaveUrl) => new(await NewPage(slaveUrl));
}
