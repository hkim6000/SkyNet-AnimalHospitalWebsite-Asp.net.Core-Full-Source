namespace AnimalHospital.Models
{
    public class BalInv
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientNo { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? BilledUtc { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string PetName { get; set; } = string.Empty;
        public DateTime? VisitAt { get; set; }
    }

    public class BalLast
    {
        public string ClientId { get; set; } = string.Empty;
        public DateTime? StmtUtc { get; set; }
        public string StmtResult { get; set; } = string.Empty;
        public DateTime? PayUtc { get; set; }
    }

    public class BalLine
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal Amount { get; set; }
    }

    public class BalPay
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string PayNo { get; set; } = string.Empty;
        public DateTime PaidUtc { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class BalMail
    {
        public DateTime SentUtc { get; set; }
        public string ToAddr { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }

    public class BalClient
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientNo { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
    }
}
