using System.Text;
using Blazor.Shared.Logging;
using Blazor.Shared.Services;
using Blazor.Tests.Tools;
using Bunit;
using DevExpress.Blazor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Client.Services;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Logging;

public class LogPageTests
{
    private const string DisplayLogButtonIcon = "mdi mdi-magnify";
    private const string DownloadLogButtonIcon = "mdi mdi-download";

    private const string SuiteLogDirectory = "AppData/vicione-suite";
    private const string SuiteLogFile = $"{SuiteLogDirectory}/suite-log-1.log";

    private readonly ILayoutService _layoutServiceMock = Substitute.For<ILayoutService>();
    private readonly IBackendLogService _logServiceMock = Substitute.For<IBackendLogService>();
    private readonly IJsInterop _jsInteropMock = Substitute.For<IJsInterop>();

    [Fact]
    public void Page_init_should_load_logfiles_and_select_the_first_one()
    {
        // Arrange
        using var ctx = SetupTestContext();

        // Act
        var page = ctx.RenderComponent<LogViewComponent>();

        // Assert
        var combo = page.FindComponent<DxComboBox<string, string>>();
        Assert.Equal(SuiteLogFile, combo.Instance.Value);
    }

#pragma warning disable xUnit1004// Test methods should not be skipped
    [Fact(Skip = "howto select another item without exposing it???")]
    public void Selecting_loglevel_should_set_the_backend_loglevel()
    {
        // Arrange
        using var ctx = SetupTestContext();
        var page = ctx.RenderComponent<LogViewComponent>();

        // Act
        var combo = page.FindComponent<DxComboBox<LogLevel, LogLevel>>();

        // Assert
        _logServiceMock.Received().SetLogLevel(Arg.Any<LogLevel>());
    }
#pragma warning restore xUnit1004// Test methods should not be skipped

    [Fact]
    public void Click_on_button_should_display_selected_logfile()
    {
        // Arrange
        using var ctx = SetupTestContext();
        var page = ctx.RenderComponent<LogViewComponent>();

        // Act
        page.FindIconButton(DisplayLogButtonIcon).Click();

        // Assert
        _logServiceMock.Received().GetLog(SuiteLogFile, CancellationToken.None);
        Assert.NotNull(page.Find(".logging-text").InnerHtml);
    }

    [Fact]
    public void Click_on_button_should_download_selected_logfile()
    {
        // Arrange
        using var ctx = SetupTestContext();

        var descriptor = new ServiceDescriptor(typeof(IJsInterop), _ => _jsInteropMock, ServiceLifetime.Transient);
        ctx.Services.Replace(descriptor);

        var page = ctx.RenderComponent<LogViewComponent>();

        // Act
        page.FindIconButton(DownloadLogButtonIcon).Click();

        // Assert
        _logServiceMock.Received().GetLog(SuiteLogFile, CancellationToken.None);
        _jsInteropMock.Received().DownloadAs(Arg.Any<Stream>(), Arg.Any<string>());
    }

    private TestContext SetupTestContext()
    {
        SetupLogHttpMock();

        var ctx = new TestContext();
        ctx.SetupSuiteServicesWithBlazorDx();
        ctx.Services.AddSingleton(_layoutServiceMock);
        ctx.Services.AddSingleton(_logServiceMock);
        return ctx;
    }

    private void SetupLogHttpMock()
    {
        _logServiceMock.GetLogLevel()
            .Returns(LogLevel.Warning);

        _logServiceMock.GetLogPaths()
            .Returns(
            [
                SuiteLogFile,
                $"{SuiteLogDirectory}/suite-log-2.log",
                $"{SuiteLogDirectory}/suite-log-3.log.gz",
                $"{Sdk.Constants.SystemModuleId}/mySuperSystemLog.log"
            ]);

        _logServiceMock.GetLog(SuiteLogFile, CancellationToken.None)
            .Returns(
                GenerateStreamFromString("[Debug] Some debug message from our suite"),
                GenerateStreamFromString("[Error] Another log message"),
                GenerateStreamFromString("[Info] Just to have more streams"));

        static MemoryStream GenerateStreamFromString(string s)
            => new(Encoding.UTF8.GetBytes(s));
    }
}
