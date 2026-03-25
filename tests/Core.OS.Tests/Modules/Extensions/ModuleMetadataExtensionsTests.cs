using Core.OS.Modules.Extensions;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Sdk.Modules;
using Xunit;

namespace Core.OS.Tests.Modules.Extensions;

public class ModuleMetadataExtensionsTests
{
    public sealed class GetConfigurationOptions
    {
        [Fact]
        public void Should_return_empty_when_options_are_null()
        {
            // Arrange
            var metadata = new ModuleMetadata
            {
                Name = "ModuleA",
                Version = "1.0.0",
                MinSuiteSdkVersion = "1.0.0",
                Options = null,
            };

            // Act
            var result = metadata.GetConfigurationOptions();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void Should_return_all_options_if_required_only_is_false()
        {
            // Arrange
            var metadata = new ModuleMetadata
            {
                Name = "ModuleA",
                Version = "1.0.0",
                MinSuiteSdkVersion = "1.0.0",
                Options =
                [
                    new() { Key = "Key1", Value = "Value1", IsRequired = true },
                    new() { Key = "Key2", Value = "Value2", IsRequired = false }
                ]
            };

            // Act
            var result = metadata.GetConfigurationOptions();

            // Assert
            result.Should().HaveCount(2);
            result.Should().ContainKey("ModuleA:Key1").And.ContainValue("Value1");
            result.Should().ContainKey("ModuleA:Key2").And.ContainValue("Value2");
        }

        [Fact]
        public void Should_return_only_required_options_if_required_only_is_true()
        {
            // Arrange
            var metadata = new ModuleMetadata
            {
                Name = "ModuleB",
                Version = "1.0.0",
                MinSuiteSdkVersion = "1.0.0",
                Options =
                [
                    new() { Key = "Key1", Value = "Value1", IsRequired = true },
                    new() { Key = "Key2", Value = "Value2", IsRequired = false }
                ]
            };

            // Act
            var result = metadata.GetConfigurationOptions(requiredOnly: true);

            // Assert
            result.Should().HaveCount(1);
            result.Should().ContainKey("ModuleB:Key1");
        }
    }

    public sealed class GetMissingConfigurationKeys
    {
        [Fact]
        public void Should_return_missing_keys_if_env_is_null_or_empty_and_no_default()
        {
            // Arrange
            var metadata = new ModuleMetadata
            {
                Name = "ModX",
                Version = "1.0.0",
                MinSuiteSdkVersion = "1.0.0",
                Options =
                [
                    new() { Key = "Db", IsRequired = true, DefaultValue = null, OptionType = ModuleOptionType.Text },
                    new() { Key = "Api", IsRequired = false, DefaultValue = null, OptionType = ModuleOptionType.Text }
                ]
            };

            var config = Substitute.For<IConfiguration>();
            config["ModX:Db"].Returns((string?)null);
            config["ModX:Api"].Returns("");

            // Act
            var result = metadata.GetMissingConfigurationKeys(config);

            // Assert
            result.Should().Contain("Db").And.Contain("Api");
        }

        [Fact]
        public void Should_not_include_keys_with_values_or_defaults()
        {
            // Arrange
            var metadata = new ModuleMetadata
            {
                Name = "ModY",
                Version = "1.0.0",
                MinSuiteSdkVersion = "1.0.0",
                Options =
                [
                    new() { Key = "Token", DefaultValue = "abc", OptionType = ModuleOptionType.Text },
                    new() { Key = "Url", OptionType = ModuleOptionType.Text }
                ]
            };

            var config = Substitute.For<IConfiguration>();
            config["ModY:Token"].Returns(""); // ignored because of default
            config["ModY:Url"].Returns("https://example.com");

            // Act
            var result = metadata.GetMissingConfigurationKeys(config);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void Should_filter_by_required_flag_when_enabled()
        {
            // Arrange
            var metadata = new ModuleMetadata
            {
                Name = "ModZ",
                Version = "1.0.0",
                MinSuiteSdkVersion = "1.0.0",
                Options =
                [
                    new() { Key = "Port", IsRequired = true, OptionType = ModuleOptionType.Text },
                    new() { Key = "Host", IsRequired = false, OptionType = ModuleOptionType.Text }
                ]
            };

            var config = Substitute.For<IConfiguration>();
            config["ModZ:Port"].Returns((string?)null);
            config["ModZ:Host"].Returns((string?)null);

            // Act
            var result = metadata.GetMissingConfigurationKeys(config, requiredOnly: true);

            // Assert
            result.Should().ContainSingle().Which.Should().Be("Port");
        }

        [Fact]
        public void Should_throw_if_get_declaration_option_from_configuration_fails()
        {
            // Arrange
            var metadata = new ModuleMetadata
            {
                Name = "ModZ",
                Version = "1.0.0",
                MinSuiteSdkVersion = "1.0.0",
                Options =
                [
                    new() { Key = "Port", IsRequired = true, OptionType = ModuleOptionType.Number },
                    new() { Key = "Host", IsRequired = false, OptionType = ModuleOptionType.Text }
                ]
            };

            var config = Substitute.For<IConfiguration>();
            config["ModZ:Port"].Returns((string?)null);
            config["ModZ:Host"].Returns((string?)null);

            // Act
            var action = () => metadata.GetMissingConfigurationKeys(config, requiredOnly: true);

            // Assert
            action.Should().Throw<InvalidOperationException>();
        }
    }

    public sealed class ValidateAndUseDefaultValues
    {
        [Fact]
        public void Should_skip_if_value_is_present()
        {
            // Arrange
            var metadata = new ModuleMetadata
            {
                Name = "MyMod",
                Version = "1.0.0",
                MinSuiteSdkVersion = "1.0.0",
                Options =
                [
                    new() { Key = "Url", DefaultValue = "https://default", IsRequired = true }
                ]
            };

            var config = Substitute.For<IConfiguration>();
            config["MyMod:Url"].Returns("https://env");

            // Act
            var result = metadata.ValidateAndUseDefaultValues(config);

            // Assert
            result.Should().BeEmpty();
            metadata.Options[0].Value.Should().BeNull(); // not overridden
        }

        [Fact]
        public void Should_assign_value_if_default_exists_but_no_env_value()
        {
            // Arrange
            var metadata = new ModuleMetadata
            {
                Name = "MyMod",
                Version = "1.0.0",
                MinSuiteSdkVersion = "1.0.0",
                Options =
                [
                    new() { Key = "Path", DefaultValue = "/default" }
                ]
            };

            var config = Substitute.For<IConfiguration>();
            config["MyMod:Path"].Returns((string?)null);

            // Act
            var result = metadata.ValidateAndUseDefaultValues(config);

            // Assert
            result.Should().BeEmpty();
            metadata.Options[0].Value.Should().BeNull(); // remains unchanged
        }

        [Fact]
        public void Should_return_missing_required_keys_without_default()
        {
            // Arrange
            var metadata = new ModuleMetadata
            {
                Name = "X",
                Version = "1.0.0",
                MinSuiteSdkVersion = "1.0.0",
                Options =
                [
                    new() { Key = "Secret", IsRequired = true }
                ]
            };

            var config = Substitute.For<IConfiguration>();
            config["X:Secret"].Returns("");

            // Act
            var result = metadata.ValidateAndUseDefaultValues(config);

            // Assert
            result.Should().ContainSingle().Which.Should().Be("Secret");
        }
    }
}
