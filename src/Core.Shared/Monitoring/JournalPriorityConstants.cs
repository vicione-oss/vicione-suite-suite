namespace Core.Shared.Monitoring;

public static class JournalPriorityConstants
{
    public const string FieldName = "PRIORITY";

    public const string Emergency = $"{FieldName}=0";
    public const string Alert = $"{FieldName}=1";
    public const string Critical = $"{FieldName}=2";
    public const string Error = $"{FieldName}=3";
    public const string Warning = $"{FieldName}=4";
    public const string Notice = $"{FieldName}=5";
    public const string Information = $"{FieldName}=6";
    public const string Debug = $"{FieldName}=7";
}
