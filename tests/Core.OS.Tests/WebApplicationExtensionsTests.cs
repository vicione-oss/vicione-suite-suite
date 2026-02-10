using System.ComponentModel.DataAnnotations;
using Core.OS.Extensions;
using Core.OS.Instance;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Core.OS.Tests;

public class WebApplicationExtensionsTests
{
    public class GetInvalidOptions : WebApplicationExtensionsTests
    {
        [Fact]
        public void Should_return_invalid_options()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();

            builder.Services.AddOptions<TestOptions>()
                .BindConfiguration("TestOptions")
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var host = builder.Build();

            // Act
            var failures = (host.GetInvalidOptions() ?? []).ToArray();

            // Assert
            Assert.NotNull(failures);
            failures.Should().HaveCount(1);
            failures.First().Should().Contain(nameof(TestOptions.Option));
        }

        [Fact]
        public void Should_return_null_if_options_are_valid()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();

            builder.Services.AddOptions<InstanceOptions>()
                .BindConfiguration(InstanceOptions.ConfigSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var host = builder.Build();

            // Act
            var failures = host.GetInvalidOptions();

            // Assert
            failures.Should().BeNull();
        }

        internal class TestOptions
        {
            [Required]
            public string? Option { get; set; }
        }
    }
}
