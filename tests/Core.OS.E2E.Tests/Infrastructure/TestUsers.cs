using Core.Tests.Tools;

namespace Core.OS.E2E.Tests.Infrastructure;

/// <summary>
/// Credentials of a user seeded into the instance under test. They are resolved from the
/// environment so that no credentials live in source: the instance under test and the test
/// run must agree on these values. CI seeds the accounts by starting the standalone instance
/// with <c>UserManagement__SeedTestUsers=true</c> and exposes the matching login through
/// <see cref="UserNameEnvVar"/> / <see cref="PasswordEnvVar"/> (see docs/e2e-testing.md).
/// </summary>
internal static class TestUsers
{
    public const string UserNameEnvVar = "SUITE_TEST_USERNAME";
    public const string PasswordEnvVar = "SUITE_TEST_PASSWORD";

    /// <summary>User name of the seeded account the tests log in with.</summary>
    public static string UserName => IntegrationServiceSettings.GetRequiredValue(UserNameEnvVar);

    /// <summary>Password of the seeded account the tests log in with.</summary>
    public static string Password => IntegrationServiceSettings.GetRequiredValue(PasswordEnvVar);
}
