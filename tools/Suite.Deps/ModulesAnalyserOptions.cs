using CommandLine;

namespace Suite.Deps;

[Verb("modules", HelpText = "find redundant suite assembly dependencies")]
public sealed class ModulesAnalyserOptions
{
    [Option('m', "manifest")]
    public IEnumerable<string> Manifest { get; set; } = [];

    [Option('r', "relative", Required = false, HelpText = "set to write paths relative (default is absolute)")]
    public bool RelativePaths { get; set; } = false;

    [Option('s', "suite", Required = true, HelpText = "path to the suite installation folder")]
    public string? SuitePath { get; set; }
}
