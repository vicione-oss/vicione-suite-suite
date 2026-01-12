using System.IO.Abstractions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Core.OS.DataProtection;

internal sealed class SafeXmlRepository(IFileSystem fileSystem, string path, ILoggerFactory loggerFactory) : IXmlRepository
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SafeXmlRepository>();

    public IDirectoryInfo Directory { get; private set; } = fileSystem.DirectoryInfo.New(path);

    public IReadOnlyCollection<XElement> GetAllElements() => GetAllElementsCore().ToList().AsReadOnly();

    private IEnumerable<XElement> GetAllElementsCore()
    {
        Directory.Create();

        foreach (var fileSystemInfo in EnumerateFileSystemInfos())
        {
            var element = ReadElementFromFile(fileSystemInfo.FullName);

            if (element != null)
                yield return element;
        }
    }

    private IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos() => Directory.EnumerateFileSystemInfos("*.xml", SearchOption.TopDirectoryOnly);

    private XElement? ReadElementFromFile(string fullPath)
    {
        _logger.ReadingDataFromFile(fullPath);

        using (var fileStream = fileSystem.File.OpenRead(fullPath))
        {
            try
            {
                return XElement.Load(fileStream);
            }
            catch (XmlException ex)
            {
                _logger.FailedReadingDataFromFile(fullPath, ex);
                return null;
            }
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        Directory.Create();

        var saveFile = Path.Combine(Directory.FullName, friendlyName + ".xml");

        try
        {
            using var fileStream = fileSystem.File.OpenWrite(saveFile);
            element.Save(fileStream);
        }
        catch (Exception ex)
        {
            _logger.FailedSavingDataFromFile(saveFile, ex);
        }
    }
}
