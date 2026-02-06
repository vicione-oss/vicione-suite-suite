using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sdk.Extensions;
using Sdk.Messaging;

namespace Core.OS.Modules.Controllers;

#if DEBUG

[ApiController]
[Route("api/[controller]")]
public sealed class DebugController(
    IModuleHost contextProvider) : ControllerBase
{
    [HttpGet("dump")]
    public ActionResult<string> GetInfos()
    {
        var sb = new StringBuilder();

        sb.AppendLine("AssemblyLoadContext.Default:");
        AddAssemblies(sb, AssemblyLoadContext.Default.Assemblies.OrderBy(a => a.GetName().Name));

        foreach (var context in AssemblyLoadContext.All)
        {
            if (context.Name == nameof(AssemblyLoadContext.Default))
                continue;

            sb.AppendLine();
            sb.Append(CultureInfo.CurrentCulture, $"AssemblyLoadContext.{context.Name}:").AppendLine();
            AddAssemblies(sb, context.Assemblies.OrderBy(a => a.GetName().Name));
        }

        return Ok(sb.ToString());
    }

    private static void AddAssemblies(StringBuilder sb, IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            if (!string.IsNullOrEmpty(assembly.Location))
                sb.Append(CultureInfo.CurrentCulture, $"{assembly.FullName} version: {assembly.GetName().Version} location:{assembly.Location}").AppendLine();
            else
                sb.Append(CultureInfo.CurrentCulture, $"{assembly.FullName} version: {assembly.GetName().Version}").AppendLine();
        }
    }

    [HttpGet("json")]
    [Produces("application/json")]
    public ActionResult<string> GetSuiteContext()
    {
        var context = JsonSerializer.Serialize(contextProvider.GetContext(), DefaultJsonSerializerSettings.Default);

        return Ok(context);
    }
}

#endif
