namespace AnimalHospital.Models
{
    public class XgMessage
    {
        public string MsgId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string MsgText { get; set; } = string.Empty;
        public DateTime ReceivedUtc { get; set; }
        public bool IsRead { get; set; }
        public bool IsDone { get; set; }
        public string ReplyNote { get; set; } = string.Empty;
        public string HandledBy { get; set; } = string.Empty;
        public string HandledName { get; set; } = string.Empty;
    }

    public class XgClient
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public int Pets { get; set; }
    }
}
