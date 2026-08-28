using Core.Tests.Tools;

namespace Core.OS.E2E.Tests.Infrastructure;

/// <summary>
/// Credentials of the users seeded into the instance under test. They are resolved from the
/// environment so that no credentials live in source: the instance under test and the test
/// run must agree on these values. CI seeds the accounts by starting the standalone instance
/// with <c>UserManagement__SeedTestUsers=true</c> and exposes the matching logins through
/// <see cref="UserNameEnvVar"/> / <see cref="PasswordEnvVar"/> and
/// <see cref="NonAdminUserNameEnvVar"/> / <see cref="NonAdminPasswordEnvVar"/>
/// (see docs/e2e-testing.md).
/// </summary>
internal static class TestUsers
{
    public const string UserNameEnvVar = "SUITE_TEST_USERNAME";
    public const string PasswordEnvVar = "SUITE_TEST_PASSWORD";

    public const string NonAdminUserNameEnvVar = "SUITE_TEST_NONADMIN_USERNAME";
    public const string NonAdminPasswordEnvVar = "SUITE_TEST_NONADMIN_PASSWORD";

    /// <summary>User name of the seeded account the tests log in with. It has full access.</summary>
    public static string UserName => IntegrationServiceSettings.GetRequiredValue(UserNameEnvVar);

    /// <summary>Password of the seeded account the tests log in with.</summary>
    public static string Password => IntegrationServiceSettings.GetRequiredValue(PasswordEnvVar);

    /// <summary>
    /// User name of a seeded account <em>without</em> full access, for the tests that assert an
    /// admin-only panel stays hidden. Signing in as this user must not grant the module claims an
    /// admin-only control panel requires.
    /// </summary>
    public static string NonAdminUserName => IntegrationServiceSettings.GetRequiredValue(NonAdminUserNameEnvVar);

    /// <summary>Password of the seeded account without full access.</summary>
    public static string NonAdminPassword => IntegrationServiceSettings.GetRequiredValue(NonAdminPasswordEnvVar);
}
