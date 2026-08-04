namespace Core.Module.Tests;

public class MetadataAssetPathCiRegexTests
{
    [Theory]
    [InlineData("/modules/ViciOne.Suite.ClusterManagement/12.3.1-ci1399909-linux-x64_0.18.0.json")]
    [InlineData("/modules/ViciOne.Suite.ClusterManagement/0.25.5-ci4399909-win-x64_0.25.0.json")]
    [InlineData("/modules/ViciOne.Suite.ClusterManagement/ci1392909-win-x64_0.25.0.json")]
    [InlineData("/modules/ViciOne.Suite.ClusterManagement/ci-1399909-win-x64_0.18.0.json")]
    public void Should_match_module_ci_paths(string version)
    {
        // Act + Assert
        Assert.True(MetadataAssetPathCiRegex.IsMatch(version));
    }

    [Theory]
    [InlineData("/modules/ViciOne.Suite.ClusterManagement/12.3.1-linux-x64_0.18.0.json")]
    [InlineData("/modules/ViciOne.Suite.ClusterManagement/0.25.5-win-x64_0.25.0.json")]
    public void Should_not_match_module_tagged_paths(string version)
    {
        // Act + Assert
        Assert.False(MetadataAssetPathCiRegex.IsMatch(version));
    }
}
