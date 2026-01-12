using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;

namespace Blazor.Wasm.Client.Infrastructure.Security;

public sealed class ArrayClaimsPrincipalFactory<TAccount>(IAccessTokenProviderAccessor accessor) :
    AccountClaimsPrincipalFactory<TAccount>(accessor) where TAccount : RemoteUserAccount
{

    // when a user belongs to multiple roles, IS4 returns a single claim with a serialized array of values
    // this class improves the original factory by deserializing the claims in the correct way
    public override async ValueTask<ClaimsPrincipal> CreateUserAsync(TAccount account,
        RemoteAuthenticationUserOptions options)
    {
        var user = await base.CreateUserAsync(account, options);

        var claimsIdentity = (ClaimsIdentity?)user.Identity;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // !! not working without account null check !!
        if (account is not null && claimsIdentity is not null)
            foreach (var (claimType, value) in account.AdditionalProperties)
            {
                if (value is JsonElement { ValueKind: JsonValueKind.Array } element)
                {
                    claimsIdentity.RemoveClaim(claimsIdentity.FindFirst(claimType));

                    var claims = element.EnumerateArray()
                        .Select(x => new Claim(claimType, x.ToString()));

                    claimsIdentity.AddClaims(claims);
                }
            }

        return user;
    }
}
