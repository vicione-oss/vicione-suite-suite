using Core.OS.Security;

namespace Core.OS.Tests.Security;

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
