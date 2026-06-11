## Identity

- Identity needs to be configured to use `Microsoft.AspNetCore.Identity.IdentitySchemaVersions.Version3`
- Identity uses the applications service provider to read its configuration options (`Microsoft.AspNetCore.Identity.IdentityOptions`) from the applications service provider

## ViciOne specialties

- VO does not use `AddDbContext` to register its DB contexts. Instead `Sdk.Backend.Persistence.DbContextResolver<TSqliteDbContext,TPostgresDbContext,TDbContextBaseInterface>` is to instantiate the context during runtime.
- `AddDbContext` usually calls `UseApplicationServiceProvider` to set the application service provider for Identity.
  - which enables Identity to read its options, if not done, Identity will use default values, which in our example would lead to version 1 of the schema being used
  - the current version of the sdk calls `UseApplicationServiceProvider` to restore the link for Identity.

### edge S & master/slave

Passkeys require a DNS name to be used when [creating the credentials](https://w3c.github.io/webauthn/#sctn-createCredential):

> NOTE: An effective domain may resolve to a host, which can be represented in various manners, such as domain, ipv4 address, ipv6 address, opaque host, or empty host.
> Only the domain format of host is allowed here. This is for simplification and also is in recognition of various issues with using direct IP address identification in concert with PKI-based security.

Without a proper DNS name, passkeys will not be working and triggering the passkey flow will be rejected by the browser itself.

## Links

- [Enable WebAuth API passkeys](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)
- [Passkeys in blazor](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/blazor?view=aspnetcore-10.0&tabs=visual-studio&pivots=existing-app)
- [Changes in ef core 9](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/breaking-changes#pending-model-changes)
