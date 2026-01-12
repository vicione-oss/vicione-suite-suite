using Blazor.Wasm.Client.Pages.Authentication;
using Bunit;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Pages;

public class AuthenticationPageTests
{
    [Fact(Skip = "To be impplemented")]
    public void Component_should_be_rendered()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServices();

        // Act
        var sut = ctx.RenderComponent<Login>();

        // Assert
        Assert.NotNull(sut);
    }
}
