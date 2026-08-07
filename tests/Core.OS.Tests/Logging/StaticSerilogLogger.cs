namespace Core.OS.Tests.Logging;

/// <summary>
/// Tests that replace the static Serilog logger must not run in parallel - the last writer wins
/// and would redirect or silence the log events another test is asserting on.
/// </summary>
[CollectionDefinition(Name)]
public sealed class StaticSerilogLogger
{
    public const string Name = "StaticSerilogLogger";
}
