using Blazor.Shared.Components.Carousel;
using Bunit;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Sdk.Client.Services;

namespace Blazor.Shared.Tests.Components;

public sealed class CarouselTests : IAsyncDisposable
{
    private static readonly TestItem First = new("first");
    private static readonly TestItem Second = new("second");
    private static readonly TestItem Third = new("third");

    private readonly BunitContext _ctx = new();
    private readonly List<TestItem> _slidesEnded = [];

    private IJsInterop _jsInterop = default!;
    private TaskCompletionSource<IJSVoidResult> _slideEnd = new();

    public CarouselTests()
        => _ctx.SetupBlazorSharedSettings(setup => _jsInterop = setup.JsInterop);

    public ValueTask DisposeAsync()
        => _ctx.DisposeAsync();

    [Fact]
    public void Should_render_only_active_item()
    {
        // Act
        var carousel = RenderCarousel(activeIndex: 1);

        // Assert
        carousel.FindAll(".carousel__item").Should().HaveCount(3);
        carousel.FindAll("[data-item]").Should().ContainSingle().Which.GetAttribute("data-item").Should().Be(Second.Name);
    }

    [Fact]
    public void Should_offset_track_to_active_item()
    {
        // Act
        var carousel = RenderCarousel(activeIndex: 2);

        // Assert
        carousel.Find(".carousel__track").GetAttribute("style").Should().Be("margin-left: -200%");
    }

    [Fact]
    public void Should_not_offset_track_without_active_item()
    {
        // Act
        var carousel = RenderCarousel(activeIndex: -1);

        // Assert
        carousel.Find(".carousel__track").GetAttribute("style").Should().Be("margin-left: 0%");
        carousel.FindAll("[data-item]").Should().BeEmpty();
    }

    [Fact]
    public void Should_not_slide_without_slide_from_item()
    {
        // Act
        var carousel = RenderCarousel(activeIndex: 1);

        // Assert
        carousel.Find(".carousel__track").ClassList.Should().NotContain("sliding");
        _jsInterop.DidNotReceive().IncludeModuleScript<SharedClientModule>(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_render_slide_from_item_while_sliding()
    {
        // Arrange
        SetupCarouselModule();

        // Act
        var carousel = RenderCarousel(activeIndex: 1, slideFrom: First);

        // Assert
        carousel.Find(".carousel__track").ClassList.Should().Contain("sliding");
        GetRenderedItems(carousel).Should().Equal(First.Name, Second.Name);
    }

    [Fact]
    public void Should_raise_slide_ended_when_slide_ends()
    {
        // Arrange
        SetupCarouselModule();

        var carousel = RenderCarousel(activeIndex: 1, slideFrom: First);

        // Act
        _slideEnd.SetResult(default!);

        // Assert
        carousel.WaitForAssertion(() => _slidesEnded.Should().Equal(First));
    }

    [Fact]
    public void Should_raise_slide_ended_when_carousel_script_is_unavailable()
    {
        // Arrange
        _jsInterop.IncludeModuleScript<SharedClientModule>("carousel.js", Arg.Any<CancellationToken>()).Returns((IJSObjectReference?)null);

        // Act
        var carousel = RenderCarousel(activeIndex: 1, slideFrom: First);

        // Assert
        carousel.WaitForAssertion(() => _slidesEnded.Should().Equal(First));
    }

    [Fact]
    public void Should_raise_slide_ended_when_waiting_for_its_end_fails()
    {
        // Arrange
        SetupCarouselModule();

        var carousel = RenderCarousel(activeIndex: 1, slideFrom: First);

        // Act
        _slideEnd.SetException(new JSException("getAnimations is not a function"));

        // Assert
        carousel.WaitForAssertion(() => _slidesEnded.Should().Equal(First));
    }

    [Fact]
    public void Should_not_raise_slide_ended_when_slide_from_item_is_cleared_before_slide_ends()
    {
        // Arrange
        SetupCarouselModule();

        var carousel = RenderCarousel(activeIndex: 1, slideFrom: First);
        carousel.Render(builder => builder.Add(c => c.SlideFrom, null));

        // Act
        _slideEnd.SetResult(default!);

        // Assert
        carousel.Find(".carousel__track").ClassList.Should().NotContain("sliding");
        _slidesEnded.Should().BeEmpty();
    }

    [Fact]
    public void Should_wait_for_slide_end_again_when_sliding_from_same_item_as_before()
    {
        // Arrange
        SetupCarouselModule();

        var carousel = RenderCarousel(activeIndex: 1, slideFrom: First);
        _slideEnd.SetResult(default!);
        carousel.WaitForAssertion(() => _slidesEnded.Should().ContainSingle());

        carousel.Render(builder => builder.Add(c => c.SlideFrom, null));
        _slideEnd = new();
        carousel.Render(builder => builder.Add(c => c.SlideFrom, First));

        // Act
        _slideEnd.SetResult(default!);

        // Assert
        carousel.WaitForAssertion(() => _slidesEnded.Should().Equal(First, First));
    }

    [Fact]
    public async Task Should_not_raise_slide_ended_when_slide_ends_after_disposal()
    {
        // Arrange
        SetupCarouselModule();

        RenderCarousel(activeIndex: 1, slideFrom: First);
        // DisposeComponentsAsync skips the disposal while the renderer is busy (bUnit 2.11.3 clears its root components outside
        // the dispatcher), so the context is disposed instead.
        await _ctx.DisposeAsync();

        // Act
        _slideEnd.SetResult(default!);

        // Assert
        _slidesEnded.Should().BeEmpty();
    }

    private void SetupCarouselModule()
    {
        var carouselModule = Substitute.For<IJSObjectReference>();
#pragma warning disable CA2012 // NSubstitute only records the call here; the returned ValueTask is never consumed.
        carouselModule.InvokeAsync<IJSVoidResult>("waitForTransitionEnd", Arg.Any<CancellationToken>(), Arg.Any<object?[]?>())
            .Returns(_ => new ValueTask<IJSVoidResult>(_slideEnd.Task));
#pragma warning restore CA2012
        _jsInterop.IncludeModuleScript<SharedClientModule>("carousel.js", Arg.Any<CancellationToken>()).Returns(carouselModule);
    }

    private IRenderedComponent<Carousel<TestItem>> RenderCarousel(int activeIndex, TestItem? slideFrom = null)
        => _ctx.Render<Carousel<TestItem>>(builder => builder
            .Add(c => c.Items, [First, Second, Third])
            .Add(c => c.ActiveIndex, activeIndex)
            .Add(c => c.SlideFrom, slideFrom)
            .Add(c => c.ItemTemplate, item => $"""<div data-item="{item.Name}"></div>""")
            .Add(c => c.OnSlideEnded, _slidesEnded.Add));

    private static IReadOnlyList<string?> GetRenderedItems(IRenderedComponent<Carousel<TestItem>> carousel)
        => [.. carousel.FindAll("[data-item]").Select(item => item.GetAttribute("data-item"))];

    /// <summary>
    /// A class rather than a record, because the carousel identifies items by reference.
    /// </summary>
    private sealed class TestItem(string name)
    {
        public string Name => name;
    }
}
