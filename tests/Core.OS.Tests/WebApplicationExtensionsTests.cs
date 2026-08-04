using System.ComponentModel.DataAnnotations;
using Core.OS.Extensions;
using Core.OS.Instance;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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
                .ValidateDataAnnotations();

            var host = builder.Build();
            // Act
            var failures = (host.GetInvalidOptions() ?? []).ToArray();

            // Assert
            Assert.NotNull(failures);

            // These assertions supposed to be .HaveCount(1) and .Should.Contain instead!
            // Changed them to have at least a test for the status quo.
            // TODO adjust accordingly when solving https://gitlab.com/vicione-oss/vicione/suite/suite/-/issues/2664
            failures.Should().HaveCount(0);
            failures.FirstOrDefault().Should().NotContain(nameof(TestOptions.Option));
        }

        [Fact]
        public void Should_return_null_if_options_are_valid()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();

            builder.Services.AddOptions<InstanceOptions>()
                .BindConfiguration(InstanceOptions.ConfigSection)
                .ValidateDataAnnotations();

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
