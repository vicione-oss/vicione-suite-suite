using Core.Shared.Instance.Contracts;
using Core.UiHosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sdk.Instance;
using Sdk.Modules;
using ViciOne.Ui.Localization.Extensions;

namespace Blazor.Wasm.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ModuleController(IUiHostEnvironment uiHostEnvironment, IInstanceInformationProvider instanceInformationProvider,
    ICookieAccessor cookieAccessor, ILogger<ModuleController> logger) : ControllerBase
{
    private const string IdentityCookieName = ".AspNetCore.Identity.Application";

    private readonly IUiHostEnvironment _uiHostEnvironment = uiHostEnvironment;
    private readonly IInstanceInformationProvider _instanceInformationProvider = instanceInformationProvider;
    private readonly ICookieAccessor _cookieAccessor = cookieAccessor;
    private readonly ILogger<ModuleController> _logger = logger;

    [HttpGet("localInstanceInfo")]
    public ActionResult<InstanceInformation> GetLocalInstanceInfo()
    {
        return Ok(_instanceInformationProvider.Local);
    }

    [HttpGet("metadata")]
    public ActionResult<List<ModuleMetadata>> GetMetadataList()
    {
        return Ok(_uiHostEnvironment.GetModuleMetadata().ToList());
    }

    [HttpGet("metadata/{name}")]
    public ActionResult<ModuleMetadata> GetMetadata(string name)
    {
        return Ok(_uiHostEnvironment.GetModuleMetadata().FirstOrDefault(k => k.Name == name));
    }

    // POST api/<controller>/load
    [HttpPost("load")]
    public async Task<IActionResult> Load(string[] assemblyNames)
    {
        // this is to prevent double loading the zip on client side twice - before and after login
        if (!_cookieAccessor.HasRequestCookie(HttpContext, IdentityCookieName))
        {
            return File([], System.Net.Mime.MediaTypeNames.Application.Octet, "empty.dll");
        }

        var archive = await _uiHostEnvironment.CreateModulesArchive(assemblyNames);

        _logger.LogInformation("Modules wasm archive created ({Size})",
            archive.Length.LocalizeFileSizeHumanReadable());

        return File(archive, System.Net.Mime.MediaTypeNames.Application.Octet, "modules.dll");
    }

    [HttpGet("resource/{locale}")]
    public async Task<IActionResult> Resource(string locale)
    {
        // this is to prevent double loading the zip on client side twice - before and after login
        if (!_cookieAccessor.HasRequestCookie(HttpContext, IdentityCookieName))
        {
            return File([], System.Net.Mime.MediaTypeNames.Application.Octet, "empty.dll");
        }

        var archive = await _uiHostEnvironment.CreateModulesResourceArchive(locale);

        _logger.LogInformation("Modules resource archive created ({Size})",
            archive.Length.LocalizeFileSizeHumanReadable());

        return File(archive, System.Net.Mime.MediaTypeNames.Application.Octet, "resources.dll");
    }
}
