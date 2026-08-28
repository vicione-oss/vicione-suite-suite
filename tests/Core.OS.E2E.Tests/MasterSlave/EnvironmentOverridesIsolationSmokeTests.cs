using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;
using Xunit;

namespace Core.OS.E2E.Tests.MasterSlave;

/// <summary>
/// Master/slave smoke tests for the environment-variable overrides. Two things exist only in this
/// topology: the overrides belong to the instance whose UI edited them and must not travel to
/// another node the way replicated global state does, and a slave may store them at all even though
/// it cannot write global state.
/// <para>
/// This needs one override file per instance, which the shared instance startup gets for free: it
/// switches the overrides on with <c>VICIONE_SUITE_ENV_OVERRIDES</c>, and the file follows each
/// instance's <c>Instance__HomeDirectory</c> (see docs/e2e-testing.md).
/// </para>
/// <para>
/// The variable names are per-test and removed again, for the same reason as in the standalone
/// tests: the file is applied at the next start of that instance.
/// </para>
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2EMasterSlave)]
public sealed class EnvironmentOverridesIsolationSmokeTests(PlaywrightFixture fixture) : MasterSlaveE2ETest(fixture)
{
    private readonly string _variableName = $"E2E_OVERRIDE_{Guid.NewGuid():N}".ToUpperInvariant();

    [Fact]
    public async Task Override_belongs_to_the_instance_whose_ui_stored_it()
    {
        // Arrange
        var master = await OpenPanelOn(MasterUrl);
        var slave1 = await OpenPanelOn(Slave1Url);

        // Act: the same variable name with a different value on two instances. A shared file would
        // leave both showing whichever value was written last — something a presence-only assertion
        // would not notice. That a slave can store at all is part of the assertion: it may write its
        // own override file even though it cannot write global state.
        await master.Store(_variableName, "master-value");
        await slave1.Store(_variableName, "slave1-value");

        // Assert: each instance kept its own value. The master is read back only after the slave's
        // save completed, so this cannot pass by looking too early.
        await master.Reload();
        await master.Panel.ExpectOverride(_variableName, "master-value");

        await slave1.Reload();
        await slave1.Panel.ExpectOverride(_variableName, "slave1-value");

        // Assert: the instance nobody configured stays empty — the override is not replicated. Both
        // saves have completed by now, so there was a window in which replication would have shown.
        var slave2 = await OpenPanelOn(Slave2Url);
        await slave2.Panel.ExpectNoOverride(_variableName);

        // Cleanup
        await master.Remove(_variableName);
        await slave1.Remove(_variableName);
    }

    [Fact]
    public async Task Restart_requirement_is_announced_on_every_instance()
    {
        // Arrange: a master UI that stays open while a slave is being configured.
        var masterPage = await NewPage(MasterUrl);
        await new LoginPage(masterPage).SignIn(TestUsers.UserName, TestUsers.Password);

        var slave1 = await OpenPanelOn(Slave1Url);

        // Act
        await slave1.Store(_variableName, "slave1-value");

        // Assert: the restart requirement is a global event, so it reaches the master's UI too, even
        // though only the slave needs the restart. This pins the behaviour documented in
        // docs/suite-startup.md; should it ever become instance-scoped, this test changes with it.
        await new MessageBanner(masterPage).ExpectSuiteRestartRequired();

        // Cleanup
        await slave1.Remove(_variableName);
    }

    private async Task<EnvironmentOverridesUi> OpenPanelOn(Uri instanceUrl)
        => await EnvironmentOverridesUi.Open(await NewPage(instanceUrl), TestUsers.UserName, TestUsers.Password);
}
