using System.Reflection;

namespace Blazor.Shared.Services;

public class CopyrightYearProvider
{
    public string Year { get; }

    public CopyrightYearProvider()
    {
        var metadata = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "Year");

        if (metadata is null || metadata.Value is null)
        {
            Year = string.Empty;
            return;
        }

        Year = metadata.Value;
    }
}
