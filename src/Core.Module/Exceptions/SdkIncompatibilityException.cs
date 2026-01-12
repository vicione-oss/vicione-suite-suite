namespace Core.Module.Exceptions;

public class SdkIncompatibilityException : Exception
{
    public SdkIncompatibilityException() : base()
    {
    }

    public SdkIncompatibilityException(string message) : base(message)
    {
    }

    public SdkIncompatibilityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
