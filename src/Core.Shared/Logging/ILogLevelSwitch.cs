using Microsoft.Extensions.Logging;

namespace Core.Shared.Logging;

public interface ILogLevelSwitch
{
    LogLevel LogLevel { get; set; }
}
