using Core.Module.Exceptions;
using Core.Module.Utils;
using Semver;

namespace Core.Module.Tests.Utils;

public class ModuleVersionValidatorTests
{
    public sealed class ValidateSdkCompatibility_with_Version_and_string
    {
        [Theory]
        [InlineData("1.3.5", "2.0.1")]
        [InlineData("1.3.5", "1.3.6")]
        [InlineData("1.3.5", "1.5.0")]
        [InlineData("1.3.5", "abc")]
        [InlineData("1.3.5", "1.ab.15")]
        [InlineData("1.3.5", "1.0.0~rc")]
        public void Should_throw_if_module_sdk_version_is_invalid(string sdkVersion, string moduleSdkVersion)
        {
            // Arrange
            var version = SemVersion.Parse(sdkVersion);

            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(sdkVersion, moduleSdkVersion);

            // Assert
            act.Should().Throw<SdkIncompatibilityException>();
        }

        [Theory]
        [InlineData("1.3.5", "1.3.5")]
        [InlineData("1.3.5", "1.3.3")]
        [InlineData("1.3.5", "1.2.15")]
        [InlineData("1.3.5", "1.0.0")]
        [InlineData("1.3.5", "1.3.5-ci231323")]
        [InlineData("1.3.5", "1.0.1-ci234343")]
        public void Should_not_throw_if_versions_are_compatible(string sdkVersion, string moduleSdkVersion)
        {
            // Arrange
            var suiteVersion = SemVersion.Parse(sdkVersion);

            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(suiteVersion, moduleSdkVersion);

            // Assert
            act.Should().NotThrow();
        }
    }

    public sealed class ValidateSdkCompatibility_with_strings
    {
        [Fact]
        public void Should_throw_if_sdk_version_string_is_invalid()
        {
            // Arrange
            var sdkVersionString = "bad";
            var moduleVersionString = "1.0.0";

            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(sdkVersionString, moduleVersionString);

            // Assert
            act.Should().Throw<SdkIncompatibilityException>()
               .WithMessage("Invalid ViciOne.Suite.Sdk version bad");
        }

        [Fact]
        public void Should_throw_with_module_upgrade_hint_if_sdk_major_is_higher_than_module_major()
        {
            // Arrange
            var sdkVersionString = "3.0.0";
            var moduleVersionString = "2.0.0";

            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(sdkVersionString, moduleVersionString);

            // Assert
            act.Should().Throw<SdkIncompatibilityException>()
               .WithMessage("Upgrade module ViciOne.Suite.Sdk to at least 3.0.x.");
        }

        [Fact]
        public void Should_throw_with_module_downgrade_hint_if_module_major_is_higher_than_sdk_major()
        {
            // Arrange
            var sdkVersionString = "2.0.0";
            var moduleVersionString = "3.0.0";

            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(sdkVersionString, moduleVersionString);

            // Assert
            act.Should().Throw<SdkIncompatibilityException>()
               .WithMessage("Downgrade module ViciOne.Suite.Sdk to 3.0.x.");
        }

        [Fact]
        public void Should_throw_with_module_downgrade_hint_if_module_minor_is_higher_than_sdk_minor()
        {
            // Arrange
            var sdkVersionString = "2.3.0";
            var moduleVersionString = "2.5.0";

            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(sdkVersionString, moduleVersionString);

            // Assert
            act.Should().Throw<SdkIncompatibilityException>()
               .WithMessage("Downgrade module ViciOne.Suite.Sdk to 2.3.0 or lower.");
        }

        [Theory]
        [InlineData("1.3.5", "1.3.5")]
        [InlineData("1.3.5", "1.3.3")]
        [InlineData("1.3.5", "1.2.15")]
        [InlineData("1.3.5", "1.0.0")]
        [InlineData("1.3.5", "1.3.5-ci231323")]
        [InlineData("1.3.5", "1.0.1-ci234343")]
        public void Should_not_throw_if_versions_are_compatible(string sdkVersion, string moduleSdkVersion)
        {
            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(sdkVersion, moduleSdkVersion);

            // Assert
            act.Should().NotThrow();
        }
    }
}
