using Blazor.Shared.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Services;

public class JsInteropTests
{
    private readonly ILogger<JsInterop> _loggerMock = Substitute.For<ILogger<JsInterop>>();


    [Fact]
    public async Task Calling_all_functions_should_call_js_runtime()
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
    public async Task GetCookie()
    {
        // Arrange        
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();

        var interop = new JsInterop(ctx.JSInterop.JSRuntime, _loggerMock);

        // Act
        var result = await interop.GetCookie(Guid.NewGuid().ToString());

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Fact]
    public async Task FormValid()
    {
        // Arrange        
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();

        var interop = new JsInterop(ctx.JSInterop.JSRuntime, _loggerMock);
        var reference = new ElementReference();

        // Act
        var result = await interop.FormValid(reference);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetElementByName()
    {
        // Arrange        
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();

        var interop = new JsInterop(ctx.JSInterop.JSRuntime, _loggerMock);

        // Act
        var element = await interop.GetElementByName("name");

        // Assert
        Assert.True(string.IsNullOrEmpty(element));
    }
}
