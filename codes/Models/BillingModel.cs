namespace AnimalHospital.Models
{
    public class BlInvoice
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public string DiscountNote { get; set; } = string.Empty;
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? BilledUtc { get; set; }
        public DateTime? ClosedUtc { get; set; }
        public string VoidReason { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string PetSize { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string VisitStatus { get; set; } = string.Empty;
        public DateTime? CheckinAt { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string VisitStaffId { get; set; } = string.Empty;
    }

    public class BlLine
    {
        public string IlineId { get; set; } = string.Empty;
        public string InvoiceId { get; set; } = string.Empty;
        public string LineId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public bool Taxable { get; set; }
        public decimal Amount { get; set; }
        public int LineOrder { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal OrderQty { get; set; }
    }

    public class BlPay
    {
        public string PayId { get; set; } = string.Empty;
        public string PayNo { get; set; } = string.Empty;
        public string InvoiceId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        public DateTime PaidUtc { get; set; }
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public bool IsVoid { get; set; }
        public string VoidReason { get; set; } = string.Empty;
    }

    public class BlItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal PriceM { get; set; }
        public decimal PriceL { get; set; }
        public bool Taxable { get; set; }
        public string Species { get; set; } = string.Empty;
    }

    public class LtVisit
    {
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime CheckinAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? RecheckDate { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }

    public class LtMed
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Dose { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
    }

    public class LtVax
    {
        public string VaxName { get; set; } = string.Empty;
        public DateTime GivenDate { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class LtResult
    {
        public string LineId { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public decimal? ValueNum { get; set; }
        public string ValueText { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal? LowValue { get; set; }
        public decimal? HighValue { get; set; }
        public string Flag { get; set; } = string.Empty;
    }

    public class LtFile
    {
        public string StoredName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }
}
