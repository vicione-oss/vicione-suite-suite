using Blazor.Shared.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Client;

namespace Blazor.Shared.Tests.Services;

public sealed class JsInteropTests
{
    private readonly ILogger<JsInterop> _loggerMock = Substitute.For<ILogger<JsInterop>>();


    [Fact]
    public async Task Should_call_js_runtime_for_all_functions()
    {
        var thing = new Dictionary<string, Func<JsInterop, Task>>()
        {
            { "ViciOne.Interop.setCookie", interop => interop.SetCookie("MyCookie", "MyCookieValue", 1) },
            { "ViciOne.Interop.setElementAttribute", interop => interop.SetElementAttribute("elementId", "style", "height:100%;") },
            { "ViciOne.Interop.updateTitle", interop => interop.UpdateTitle("NewTitle") },
            { "ViciOne.Interop.includeMeta", interop => interop.IncludeMeta("elementId", "attribute", "name", "content", "key") },
            { "ViciOne.Interop.includeLink", interop => interop.IncludeLink("elementId", "rel", new Uri("http://url"), "type", "integrity", "crossOrigin", "key") },
            { "ViciOne.Interop.includeLinks", interop => interop.IncludeLinks([]) },
            { "ViciOne.Interop.includeScript", interop => interop.IncludeScript("elementId", new Uri("http://url"), "integrity", "crossOrigin", "content", "location", "key") },
            { "ViciOne.Interop.includeScripts", interop => interop.IncludeScripts([]) },
            { "ViciOne.Interop.removeElementsById", interop => interop.RemoveElementsById("prefix", "first", "last") },
            { "ViciOne.Interop.removeScriptsBySource", interop => interop.RemoveScriptsBySource(new Uri("http://url")) },
            { "ViciOne.Interop.submitForm", interop => interop.SubmitForm("path", "fields") },
            { "ViciOne.Interop.getFiles", interop => interop.GetFiles("id") },
            { "ViciOne.Interop.uploadFiles", interop => interop.UploadFiles("postUrl", "folder", "id") },
            { "ViciOne.Interop.refreshBrowser", interop => interop.RefreshBrowser(true, 1000) },
            { "ViciOne.Interop.redirectBrowser", interop => interop.RedirectBrowser(new Uri("http://url"), 1000) },
            { "ViciOne.Download.fileFromStream", interop => interop.DownloadAs("content", "name") },
        };

        // Arrange        
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();

        var interop = new JsInterop(ctx.JSInterop.JSRuntime, _loggerMock);

        foreach (var test in thing)
        {
            // Act
            await test.Value(interop);

            // Assert
            ctx.JSInterop.VerifyInvoke(test.Key);
        }
    }

    [Fact]
    public async Task Should_return_empty_cookie()
    {
        // Arrange        
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();

        var interop = new JsInterop(ctx.JSInterop.JSRuntime, _loggerMock);

        // Act
        var result = await interop.GetCookie(Guid.NewGuid().ToString(), Xunit.TestContext.Current.CancellationToken);

        // Assert
        string.IsNullOrEmpty(result).Should().BeTrue();
    }

    [Fact]
    public async Task Should_return_invalid_form()
    {
        // Arrange        
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();

        var interop = new JsInterop(ctx.JSInterop.JSRuntime, _loggerMock);
        var reference = new ElementReference();

        // Act
        var result = await interop.FormValid(reference, Xunit.TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_return_empty_element_when_not_found()
    {
        // Arrange        
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();

        var interop = new JsInterop(ctx.JSInterop.JSRuntime, _loggerMock);

        // Act
        var element = await interop.GetElementByName("name", Xunit.TestContext.Current.CancellationToken);

        // Assert
        string.IsNullOrEmpty(element).Should().BeTrue();
    }
}
