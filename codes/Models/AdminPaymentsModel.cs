namespace AnimalHospital.Models
{
    public class PyRow
    {
        public string PayId { get; set; } = string.Empty;
        public string PayNo { get; set; } = string.Empty;
        public DateTime PaidUtc { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string InvoiceId { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public bool IsVoid { get; set; }
        public string VoidReason { get; set; } = string.Empty;
    }

    public class PySum
    {
        public string Method { get; set; } = string.Empty;
        public int N { get; set; }
        public decimal Total { get; set; }
        public int Voided { get; set; }
    }

    public class PyCash
    {
        public DateTime PaidUtc { get; set; }
        public decimal Amount { get; set; }
    }

    public class PyStaff
    {
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }

    public class PyInv
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
