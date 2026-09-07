using Microsoft.Extensions.Options;

namespace Core.OS.Configuration;

/// <summary>
/// Marks an options type as deliberately unvalidated, with the reason it is exempt.
/// </summary>
/// <remarks>
/// An options type can only carry a source-generated validator if something in its graph is annotated:
/// for a type without a single validation attribute the generator reports <c>SYSLIB1203</c> and emits no
/// <c>Validate</c> method at all, so an <c>[OptionsValidator]</c> partial class would not compile. Those
/// types go through <c>AddUnvalidatedSuiteOptions</c>, which registers this validator so that the exemption
/// is a deliberate, greppable statement in the registration rather than the silent default it used to be.
/// The set of exempt types is pinned by <c>SuiteOptionsValidationTests</c>, so adding one is a review decision.
/// </remarks>
/// <param name="justification">Why this options type has nothing to validate.</param>
internal sealed class UnvalidatedSuiteOptions<TOptions>(string justification) : IValidateOptions<TOptions>
    where TOptions : class
{
    public string Justification { get; } = justification;

    public ValidateOptionsResult Validate(string? name, TOptions options) => ValidateOptionsResult.Skip;
}
