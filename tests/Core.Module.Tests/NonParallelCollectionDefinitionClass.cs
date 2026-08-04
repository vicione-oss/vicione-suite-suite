namespace Core.Module.Tests;

[CollectionDefinition(NonParallelCollection, DisableParallelization = true)]
public static class NonParallelCollectionDefinitionClass
{
    public const string NonParallelCollection = "Non-Parallel Collection";
}
