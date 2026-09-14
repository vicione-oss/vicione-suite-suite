using Blazor.Shared.Extensions;
using Sdk.Instance;

namespace Blazor.Shared.Tests.Extensions;

public sealed class InstanceInformationProviderExtensionsTests
{
    public sealed class GetFormatedInstanceTitle
    {
        [Fact]
        public void Should_render_accent_markers_as_spans()
        {
            // Arrange
            var provider = CreateProvider("{ViciOne} Suite");

            // Act
            var title = provider.GetFormatedInstanceTitle();

            // Assert
            title.Value.Should().Be("<span class=\"text-accent\">ViciOne</span> Suite");
        }

        [Fact]
        public void Should_encode_markup_in_the_instance_name()
        {
            // Arrange
            var provider = CreateProvider("<script>alert(1)</script>");

            // Act
            var title = provider.GetFormatedInstanceTitle();

            // Assert
            title.Value.Should().NotContain("<script>");
            title.Value.Should().Be("&lt;script&gt;alert(1)&lt;/script&gt;");
        }

        [Fact]
        public void Should_encode_quotes_so_the_accent_span_cannot_be_broken_out_of()
        {
            // Arrange
            var provider = CreateProvider("{\" onmouseover=\"alert(1)}");

            // Act
            var title = provider.GetFormatedInstanceTitle();

            // Assert
            title.Value.Should().Be("<span class=\"text-accent\">&quot; onmouseover=&quot;alert(1)</span>");
        }

        private static IInstanceInformationProvider CreateProvider(string formattedName)
        {
            var local = Substitute.For<IInstanceInformation>();
            local.FormattedName.Returns(formattedName);

            var provider = Substitute.For<IInstanceInformationProvider>();
            provider.Local.Returns(local);

            return provider;
        }
    }
}
