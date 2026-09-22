using Core.OS.Security;

namespace Core.OS.Tests.Security;

/// <summary>
/// The baseline is no longer one constant: the configured OpenID provider joins
/// <c>form-action</c>, because the browser refuses the challenge redirect otherwise (ADR-006).
/// What these pin is that it is the only directive the provider reaches.
/// </summary>
public class ContentSecurityPolicyTests
{
    private const string FormAction = "form-action";

    [Fact]
    public void Should_keep_form_submissions_on_the_suite_when_no_provider_is_configured()
        => Directive(ContentSecurityPolicy.GetBaseline(null), FormAction).Should().Be("'self'");

    [Fact]
    public void Should_let_a_form_submission_reach_the_configured_provider()
        => Directive(ContentSecurityPolicy.GetBaseline("https://idp.example.com"), FormAction)
            .Should().Be("'self' https://idp.example.com");

    /// <summary>
    /// A relaxation that leaked into another directive would be invisible in the directive test
    /// above and is exactly the kind of mistake a security header does not survive.
    /// </summary>
    [Fact]
    public void Should_leave_every_other_directive_untouched_by_the_provider()
        => DirectivesExceptFormAction(ContentSecurityPolicy.GetBaseline("https://idp.example.com"))
            .Should().Equal(DirectivesExceptFormAction(ContentSecurityPolicy.GetBaseline(null)));

    private static IEnumerable<string> DirectivesExceptFormAction(string policy)
        => policy.Split("; ").Where(directive => !directive.StartsWith(FormAction, StringComparison.Ordinal));

    private static string Directive(string policy, string name)
        => policy.Split("; ").Single(directive => directive.StartsWith($"{name} ", StringComparison.Ordinal))
            [(name.Length + 1)..];
}
