namespace Blazor.DevAssets.Tests;

public class PathHelperTests
{
    public sealed class NormalizePath : PathHelperTests
    {
        [Fact]
        public void Should_produce_relative_parent_path_matching_expected()
        {
            // Arrange
            var path = "/_content/ViciOne.Suite.Module.Client/svg/toolbox.svg";

            // Act
            var normalized = PathHelper.NormalizePath(path);
            var parent = Directory.GetParent(normalized);
            var relative = Path.Combine(parent!.Name, Path.GetFileName(normalized));

            // Assert
            var expected = Path.Combine("svg", "toolbox.svg");
            Assert.Equal(expected, relative);
        }

#if WIN64
        [Fact]
        public void Should_normalize_forward_and_backward_slash_paths_equally()
        {
            // Arrange
            var input1 = @"js/location.js";
            var input2 = @"js\location.js";

            // Act
            var normalized1 = PathHelper.NormalizePath(input1);
            var normalized2 = PathHelper.NormalizePath(input2);

            // Assert
            Assert.Equal(normalized1, normalized2);
        }
#endif
    }
}
