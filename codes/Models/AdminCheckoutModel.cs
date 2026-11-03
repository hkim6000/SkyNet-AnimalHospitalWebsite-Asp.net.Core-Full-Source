namespace AnimalHospital.Models
{
    public class CkVisit
    {
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime CheckinAt { get; set; }
        public DateTime? ReadyAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string PetAlert { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string OwnerAlert { get; set; } = string.Empty;
    }

    public class CkDesk
    {
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public DateTime? ReadyAt { get; set; }
        public string PetName { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal Estimate { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public decimal InvTotal { get; set; }
        public decimal InvPaid { get; set; }
    }

    public class CkDone
    {
        public string VisitId { get; set; } = string.Empty;
        public string InvoiceId { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string PetName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
    }

    public class CkMail
    {
        public string MailKind { get; set; } = string.Empty;
        public string ToAddr { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime SentUtc { get; set; }
        public string Result { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }

    public class CkOrder
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public bool Taxable { get; set; }
    }
}
