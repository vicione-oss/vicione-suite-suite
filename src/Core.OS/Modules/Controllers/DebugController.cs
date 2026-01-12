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
    private readonly IModuleHost _contextProvider = contextProvider;

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
            sb.AppendFormattedLine("AssemblyLoadContext.{0}:", context.Name);
            AddAssemblies(sb, context.Assemblies.OrderBy(a => a.GetName().Name));
        }

        return Ok(sb.ToString());
    }

    private static void AddAssemblies(StringBuilder sb, IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            if (!string.IsNullOrEmpty(assembly.Location))
                sb.AppendFormattedLine("{0} location:{1}", assembly.FullName, assembly.Location);
            else
                sb.AppendFormattedLine("{0}", assembly.FullName);
        }
    }

    [HttpGet("json")]
    public ActionResult<string> GetSuiteContext()
    {
        var context = JsonSerializer.Serialize(_contextProvider.GetContext(), DefaultJsonSerializerSettings.Default);

        return Ok(context);
    }
}

#endif
