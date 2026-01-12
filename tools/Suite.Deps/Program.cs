using CommandLine;
using Suite.Deps;

await Parser.Default.ParseArguments<AssemblyMappingAnalyserOptions, ModulesAnalyserOptions, CoreAnalyserOptions>(args)
    .MapResult(
        (AssemblyMappingAnalyserOptions options) => AssemblyMappingAnalyser.Execute(options),
        (CoreAnalyserOptions options) => CoreAnalyser.Execute(options),
        (ModulesAnalyserOptions options) => ModulesAnalyser.Execute(options),
        HandleOptionErrors);

static Task HandleOptionErrors(IEnumerable<Error> errs)
{
    return Task.CompletedTask;
}
