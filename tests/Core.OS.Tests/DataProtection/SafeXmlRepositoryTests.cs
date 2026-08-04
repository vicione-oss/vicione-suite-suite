using System.IO.Abstractions.TestingHelpers;
using System.Xml.Linq;
using Core.OS.DataProtection;
using Microsoft.Extensions.Logging;

namespace Core.OS.Tests.DataProtection;

public class SafeXmlRepository_
{
    [Fact]
    public void Acceptance()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var system = new MockFileSystem();
        system.AddDirectory(tempDirectory);

        var repo = new SafeXmlRepository(system, tempDirectory, loggerFactory);
        var element = new XElement(XName.Get("test", "test"), 1);

        repo.StoreElement(element, "test");
        repo.GetAllElements().Should().ContainSingle().Which.Value.Should().Be("1");
    }

    [Fact]
    public void Can_handle_empty_files()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var system = new MockFileSystem();
        system.AddDirectory(tempDirectory);
        system.AddEmptyFile(Path.Combine(tempDirectory, "text.xml"));

        var logger = Substitute.For<ILogger<SafeXmlRepository>>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(typeof(SafeXmlRepository).FullName!).Returns(logger);

        var repo = new SafeXmlRepository(system, tempDirectory, loggerFactory);

        var elements = repo.GetAllElements();
        elements.Should().BeEmpty();

        logger.ReceivedCalls().Count().Should().Be(2);
    }
}
