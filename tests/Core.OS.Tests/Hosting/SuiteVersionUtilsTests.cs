using Core.OS.Hosting;
using AwesomeAssertions;
using Xunit;

namespace Core.OS.Tests.Hosting;

public sealed class SuiteVersionUtilsTests
{
    public sealed class IsPatchUpdate
    {
        [Theory]
        [InlineData("0.10.1", "0.14.1")]
        [InlineData("0.10.1", "0.11.1")]
        [InlineData("0.10.1", "1.10.1")]
        [InlineData("0.10.1", "0.10.1-ci1632286")]
        [InlineData("0.33.0-ci1632286", "0.33.0-ci1622286")]
        public void Should_detect_no_patch_versions(string oldVersion, string newVersion)
        {
            SuiteVersionUtils.IsPatchUpdate(oldVersion, newVersion).Should().BeFalse();
        }

        [Theory]
        [InlineData("0.10.1", "0.10.2")]
        [InlineData("0.10.1", "0.10.13")]
        [InlineData("0.10.1", "0.10.2-ci1632286")]
        [InlineData("0.33.0-ci1632286", "0.33.0")]
        public void Should_detect_patch_versions(string oldVersion, string newVersion)
        {
            SuiteVersionUtils.IsPatchUpdate(oldVersion, newVersion).Should().BeTrue();
        }
    }
}
