using Blazor.Shared.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Xunit;

namespace Blazor.Shared.Tests.Services;

public class ControlPanelServiceTests
{
    private readonly ControlPanelService _service = new(Substitute.For<ILogger<ControlPanelService>>());

    [Fact]
    public async Task BeginEditFiresOnlyIfNotDirty()
    {
        // Arrange
        var fired = 0;
        _service.OnBeginEdit += () =>
        {
            fired++;
            return Task.CompletedTask;
        };

        // Act
        await _service.BeginEdit();
        await _service.BeginEdit();

        // Assert
        Assert.True(_service.IsDirty);
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task CancelEditClearDirtyAndFires()
    {
        // Arrange
        var fired = 0;
        await _service.BeginEdit();
        _service.OnCancel += () =>
        {
            fired++;
            return Task.CompletedTask;
        };

        // Act
        await _service.CancelEdit();

        // Assert
        Assert.False(_service.IsDirty);
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task FinishEditClearDirtyOnSaveSuccess()
    {
        // Arrange
        var fired = 0;
        await _service.BeginEdit();
        _service.OnSave += () =>
        {
            fired++;
            return Task.FromResult<ISaveResult>(new SaveSuccessResult());
        };

        // Act
        var result = await _service.FinishEdit();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.False(_service.IsDirty);
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task FinishEditNotClearDirtyOnSaveFail()
    {
        // Arrange
        var fired = 0;
        await _service.BeginEdit();
        _service.OnSave += () =>
        {
            fired++;
            return Task.FromResult<ISaveResult>(new SaveErrorResult("An error has occurred."));
        };

        // Act
        var result = await _service.FinishEdit();

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.True(_service.IsDirty);
        Assert.Equal(1, fired);
    }


    [Fact]
    public async Task FinishEditReturnResultWithErrInfoOnException()
    {
        // Arrange
        var fired = 0;
        await _service.BeginEdit();
        _service.OnSave += () =>
        {
            fired++;
            throw new InvalidOperationException();
        };

        // Act
        var result = await _service.FinishEdit();

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.True(_service.IsDirty);
        Assert.Equal(1, fired);
    }
}
