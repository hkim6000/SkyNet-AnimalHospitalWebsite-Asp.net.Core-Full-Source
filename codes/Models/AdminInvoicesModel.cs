namespace AnimalHospital.Models
{
    public class IvMail
    {
        public string MailKind { get; set; } = string.Empty;
        public string ToAddr { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime SentUtc { get; set; }
        public string Result { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }

    public class IvSum
    {
        public int RowCnt { get; set; }
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
    }
}
