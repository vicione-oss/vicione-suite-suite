namespace Core.OS.Instance;

public sealed class StreamUploadHandlerOptions<TContext>
{
    public Func<string, string>? PathTransform { get; set; }
    public Func<string, string>? FilenameTransform { get; set; }
}
