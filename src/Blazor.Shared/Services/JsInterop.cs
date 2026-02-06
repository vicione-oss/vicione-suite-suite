using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Client.Modules;
using Sdk.Client.Services;
using Sdk.Modules;

namespace Blazor.Shared.Services;

public sealed class JsInterop(IJSRuntime jsRuntime, ILogger<JsInterop> logger) : IJsInterop
{
    public async Task SetCookie(string name, string value, int days)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.setCookie",
                name, value, days);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(SetCookie));
        }
    }

    public async Task<string> GetCookie(string name)
    {
        try
        {
            return await jsRuntime.InvokeAsync<string>(
                "ViciOne.Interop.getCookie",
                name);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task UpdateTitle(string title)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.updateTitle",
                title);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(UpdateTitle));
        }
    }

    public async Task IncludeMeta(string id, string attribute, string name, string content, string key)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.includeMeta",
                id, attribute, name, content, key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(IncludeMeta));
        }
    }

    public async Task IncludeLink(string id, string rel, Uri href, string type, string integrity, string crossorigin, string key)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.includeLink",
                id, rel, href.OriginalString, type, integrity, crossorigin, key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(IncludeLink));
        }
    }

    public async Task IncludeLinks(object[] links)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.includeLinks",
                (object)links);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(IncludeLinks));
        }
    }

    public async Task IncludeScript(string id, Uri src, string integrity, string crossorigin, string content, string location, string key)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.includeScript",
                id, src.OriginalString, integrity, crossorigin, content, location, key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(IncludeScript));
        }
    }

    public async Task<IJSObjectReference?> IncludeModuleScript(Uri location)
    {
        try
        {
            var reference = await jsRuntime.InvokeAsync<IJSObjectReference>("import", location.OriginalString);

            return reference;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(IncludeModuleScript));

            return null;
        }
    }

    public Task<IJSObjectReference?> IncludeModuleScript<T>(string filename)
        where T : IModule
        => IncludeModuleScript(ModuleAssetHelper.GetModuleJsUrl<T>(filename));

    public async Task IncludeScripts(object[] scripts)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.includeScripts",
                (object)scripts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(IncludeScripts));
        }
    }

    public async Task RemoveElementsById(string prefix, string first, string last)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.removeElementsById",
                prefix, first, last);
        }
        catch (Exception ex)
        {
            // happens if ModuleComponentBase wants to cleanup
            logger.LogDebug(ex, nameof(RemoveElementsById));
        }
    }

    public async Task RemoveScriptsBySource(Uri source)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.removeScriptsBySource",
                source.OriginalString);
        }
        catch (Exception ex)
        {
            // happens if ModuleComponentBase wants to cleanup
            logger.LogDebug(ex, nameof(RemoveScriptsBySource));
        }
    }

    public async Task<string> GetElementByName(string name)
    {
        try
        {
            return await jsRuntime.InvokeAsync<string>(
                "ViciOne.Interop.getElementByName",
                name);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task SubmitForm(string path, object fields)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.submitForm",
                path, fields);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(SubmitForm));
        }
    }

    public async Task<string[]> GetFiles(string id)
    {
        try
        {
            return await jsRuntime.InvokeAsync<string[]>(
                "ViciOne.Interop.getFiles",
                id);
        }
        catch
        {
            return [];
        }
    }

    public async Task UploadFiles(string posturl, string folder, string id)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.uploadFiles",
                posturl, folder, id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(UploadFiles));
        }
    }

    public async Task RefreshBrowser(bool force, int wait)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.refreshBrowser",
                force, wait);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(RefreshBrowser));
        }
    }

    public async Task RedirectBrowser(Uri url, int wait)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.redirectBrowser",
                url.AbsoluteUri, wait);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(RedirectBrowser));
        }
    }

    public async Task<bool> FormValid(ElementReference form)
    {
        try
        {
            return await jsRuntime.InvokeAsync<bool>(
                "ViciOne.Interop.formValid",
                form);
        }
        catch
        {
            return false;
        }
    }

    public async Task SetElementAttribute(string id, string attribute, string value)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "ViciOne.Interop.setElementAttribute",
                id, attribute, value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(SetElementAttribute));
        }
    }

    public async Task DownloadAs(string content, string name)
    {
        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        await DownloadAs(memoryStream, name);
    }

    public async Task DownloadAs(Stream content, string name)
    {
        try
        {
            using var streamRef = new DotNetStreamReference(content);

            await jsRuntime.InvokeVoidAsync("ViciOne.Download.fileFromStream", name, streamRef);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(SetElementAttribute));
        }
    }
}
