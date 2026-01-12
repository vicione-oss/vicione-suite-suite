using CommandLine;

namespace Suite.Deps;

[Verb("core", HelpText = "find assemblies provided by suite")]
public sealed class CoreAnalyserOptions
{
    [Option('v', "version", Required = false, HelpText = "add the version after the filename")]
    public bool Version { get; set; } = false;

    [Option('s', "suite", Required = true, HelpText = "path to the suite installation folder")]
    public string? SuitePath { get; set; }

    [Option('e', "environment", Required = false, HelpText = "Develop, Production, Standalone...")]
    public string? Environment { get; set; }

    [Option('h', "header", Required = false, HelpText = "Default: true. outputs #SDK:ViciOne.Suite.Sdk:vX.X.X\n#RUNTIMES:linux-x64,..\n#LANGUAGES:en,de,..")]
    public bool Header { get; set; } = true;

    [Option('r', "runtimes", Required = false, HelpText = "list of supported runtimes", Default = new[] { "linux-x64", "linux-arm64", "win-x64", "win-x86" })]
    public IEnumerable<string> Runtimes { get; set; } = [];

    [Option('l', "languages", Required = false, HelpText = "list of supported languages", Default = new[] { "en", "de" })]
    public IEnumerable<string> Languages { get; set; } = [];
}
