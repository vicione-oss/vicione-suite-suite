using CommandLine;

namespace Suite.Deps;

[Verb("mapping", HelpText = "analyze the assembly version mappings")]
public sealed class AssemblyMappingAnalyserOptions
{
    [Option('m', "manifest")]
    public IEnumerable<string> Manifest { get; set; } = [];

    [Option('s', "suite", Required = true, HelpText = "path to the suite installation folder")]
    public string? SuitePath { get; set; }

    [Option('o', "output", HelpText = "path output file as json")]
    public string? OutputPath { get; set; }
}
