using Core.Module.Exceptions;
using Core.Module.Utils;
using AwesomeAssertions;
using Xunit;
using Semver;

namespace Core.Module.Tests.Utils;

public class ModuleVersionValidatorTests
{
    public sealed class ValidateSdkCompatibility_with_Version_and_string
    {
        [Fact]
        public void Should_throw_if_module_sdk_version_is_invalid()
        {
            // Arrange
            var sdkVersion = new SemVersion(1, 2);
            var invalidModuleVersion = "abc";

            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(sdkVersion, invalidModuleVersion);

            // Assert
            act.Should().Throw<SdkIncompatibilityException>()
               .WithMessage("Invalid module SDK version abc reference.");
        }

        [Fact]
        public void Should_throw_if_major_or_minor_versions_do_not_match()
        {
            // Arrange
            var sdkVersion = new SemVersion(2, 5);
            var moduleVersion = "3.5.0";

            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(sdkVersion, moduleVersion);

            // Assert
            act.Should().Throw<SdkIncompatibilityException>()
               .WithMessage("Update ViciOne.Suite.Sdk at least to version 2.5.x.");
        }

        [Fact]
        public void Should_not_throw_if_major_and_minor_versions_match()
        {
            // Arrange
            var sdkVersion = new SemVersion(1, 2);
            var moduleVersion = "1.2.9";

            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(sdkVersion, moduleVersion);

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
               .WithMessage("Invalid SDK version bad");
        }

        [Fact]
        public void Should_throw_if_module_version_is_incompatible()
        {
            // Arrange
            var sdkVersionString = "2.0.0";
            var moduleVersionString = "3.0.0";

            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(sdkVersionString, moduleVersionString);

            // Assert
            act.Should().Throw<SdkIncompatibilityException>()
               .WithMessage("Update ViciOne.Suite.Sdk at least to version 2.0.x.");
        }

        [Fact]
        public void Should_not_throw_if_versions_are_compatible()
        {
            // Arrange
            var sdkVersionString = "1.3.0";
            var moduleVersionString = "1.3.5";

            // Act
            var act = () => ModuleVersionValidator.ValidateSdkCompatibility(sdkVersionString, moduleVersionString);

            // Assert
            act.Should().NotThrow();
        }
    }
}


