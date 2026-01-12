namespace JiTChat.Public.Contracts
{
    public sealed class ChatMessage
    {
        public string Message { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public DateTime Time { get; set; } = DateTime.Now;
    }
}
